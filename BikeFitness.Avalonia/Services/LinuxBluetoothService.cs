using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BikeFitness.Shared;
using BikeFitness.Shared.Models;
using BikeFitness.Shared.Services;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Tmds.DBus;

namespace BikeFitness.Avalonia.Services
{
    public class LinuxBluetoothService : IBluetoothService
    {

        private static readonly string WAHOO_CONTROL_POINT_UUID = "a026e005-0a7d-4ab3-97fa-f1500f9feb8b";
        private static readonly string POWER_MEASUREMENT_UUID = "00002A63-0000-1000-8000-00805f9b34fb";

        // Internal State
        private Adapter? _adapter;
        private Device? _device;
        private IGattCharacteristic1? _controlPoint;
        private IGattCharacteristic1? _powerChar;
        private IDisposable? _powerWatcher;
        private KickrLogic _logic = new KickrLogic();
        
        // Command Loop State
        private bool _isLoopRunning;
        private double? _pendingResistance;

        // Speed/Distance State
        private uint _prevWheelRevs = 0;
        private ushort _prevWheelTime = 0;
        private uint _startWheelRevs = 0;
        private bool _firstWheelData = true;
        private double _lastDistMeters = 0;
        
        // Events
        public event Action<DeviceDisplay>? DeviceDiscovered;
        public event Action<string>? StatusChanged;
        public event Action<int>? PowerReceived;
        public event Action<double, double>? SpeedValuesUpdated;
        public event Action? ConnectionLost;

        public bool IsScanning { get; private set; }
        public bool IsConnected => _device != null; // Simplified for now
        public string CurrentStatus { get; private set; } = "Ready";

        public LinuxBluetoothService()
        {
        }

        private void UpdateStatus(string status)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(status);
            Logger.Log($"[Linux BT Service] {status}");
        }

        public async void StartScanning()
        {
            try
            {
                if (_adapter == null)
                {
                    var adapters = await BlueZManager.GetAdaptersAsync();
                    _adapter = adapters.FirstOrDefault();
                }

                if (_adapter == null)
                {
                    UpdateStatus("No Bluetooth adapters found.");
                    return;
                }

                _adapter.DeviceFound += Adapter_DeviceFound;
                await _adapter.StartDiscoveryAsync();
                IsScanning = true;
                UpdateStatus("Scanning for trainers...");
            }
            catch (Exception ex)
            {
                Logger.Log($"Scanning Error: {ex.Message}");
                UpdateStatus($"Bluetooth Error: {ex.Message}");
            }
        }

        private async Task Adapter_DeviceFound(Adapter sender, DeviceFoundEventArgs args)
        {
            var device = args.Device;
            string? name = await device.GetAliasAsync();
            if (string.IsNullOrEmpty(name)) return;

            if (name.ToUpper().Contains("KICKR") || name.ToUpper().Contains("WAHOO"))
            {
                string addressStr = await device.GetAddressAsync();
                ulong address = ParseBluetoothAddress(addressStr);
                
                DeviceDiscovered?.Invoke(new DeviceDisplay 
                { 
                    Name = name, 
                    Address = address 
                });
            }
        }

        private ulong ParseBluetoothAddress(string address)
        {
            try
            {
                return ulong.Parse(address.Replace(":", ""), System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                return 0;
            }
        }

        private string FormatBluetoothAddress(ulong address)
        {
            string hex = address.ToString("X12");
            return $"{hex[0..2]}:{hex[2..4]}:{hex[4..6]}:{hex[6..8]}:{hex[8..10]}:{hex[10..12]}";
        }

        private IDisposable? _deviceWatcher;
        
        public async Task ConnectAsync(ulong address)
        {
            StopScanning();
            UpdateStatus($"Connecting to {address}...");

            // Reset Calculation State
            _firstWheelData = true;
            _prevWheelRevs = 0;
            _prevWheelTime = 0;
            _startWheelRevs = 0;
            _lastDistMeters = 0;

            try
            {
                string addressStr = FormatBluetoothAddress(address);
                if (_adapter == null)
                {
                    var adapters = await BlueZManager.GetAdaptersAsync();
                    _adapter = adapters.FirstOrDefault();
                }

                if (_adapter == null)
                {
                    UpdateStatus("No Bluetooth adapter found.");
                    return;
                }

                _device = await _adapter.GetDeviceAsync(addressStr);
                if (_device == null)
                {
                    UpdateStatus("Device not found by adapter.");
                    return;
                }

                await _device.ConnectAsync();
                
                // Watch for connection loss
                _deviceWatcher = await _device.WatchPropertiesAsync(props => {
                    foreach (var change in props.Changed)
                    {
                        if (change.Key == "Connected" && change.Value is bool connected && !connected)
                        {
                            UpdateStatus("Device Disconnected");
                            _isLoopRunning = false;
                            ConnectionLost?.Invoke();
                        }
                    }
                });

                UpdateStatus("Connected. Discovering services...");

                // Wait for services to be resolved
                int retry = 0;
                while (!(await _device.GetServicesResolvedAsync()) && retry < 20)
                {
                    await Task.Delay(500);
                    retry++;
                }

                var services = await _device.GetServicesAsync();
                
                // 2. Find Control Point
                _controlPoint = null;
                _powerChar = null;

                foreach (var service in services)
                {
                    string serviceUuid = await service.GetUUIDAsync();
                    var characteristics = await service.GetCharacteristicsAsync();

                    foreach (var ch in characteristics)
                    {
                        string charUuid = await ch.GetUUIDAsync();
                        
                        if (charUuid.Equals(WAHOO_CONTROL_POINT_UUID, StringComparison.OrdinalIgnoreCase))
                        {
                            _controlPoint = ch;
                            Logger.Log($"Found Control Point in Service {serviceUuid}");
                        }
                        
                        if (charUuid.Equals(POWER_MEASUREMENT_UUID, StringComparison.OrdinalIgnoreCase))
                        {
                            _powerChar = ch;
                            Logger.Log($"Found Power Measurement in Service {serviceUuid}");
                        }
                    }
                }

                if (_controlPoint == null)
                {
                    UpdateStatus("Control Point NOT found.");
                }

                if (_powerChar != null)
                {
                    await SubscribeToPowerAsync();
                }

                UpdateStatus("Connected and Ready");
                
                // Start Command Loop
                _isLoopRunning = true;
                _ = CommandLoop();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Connection Error: {ex.Message}");
                Logger.Log($"Connection Exception: {ex}");
            }
        }

        private async Task SubscribeToPowerAsync()
        {
            if (_powerChar == null) return;
            try
            {
                _powerWatcher = await _powerChar.WatchPropertiesAsync(props => {
                    foreach (var change in props.Changed)
                    {
                        if (change.Key == "Value" && change.Value is byte[] data)
                        {
                            Power_ValueChanged(data);
                        }
                    }
                });
                await _powerChar.StartNotifyAsync();
                Logger.Log("Subscribed to Power notifications.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to subscribe to power: {ex.Message}");
            }
        }

        private void Power_ValueChanged(byte[] data)
        {
            // 1. Parse Power
            int watts = _logic.ParsePower(data);
            PowerReceived?.Invoke(watts);

            // 2. Parse Speed/Distance (Wheel Data)
            var (hasWheel, wheelRevs, wheelTime) = _logic.ParseWheelDataFromPower(data);
            if (hasWheel)
            {
                double kph = 0;
                double distMeters = _lastDistMeters;

                if (_firstWheelData)
                {
                    _startWheelRevs = wheelRevs;
                    _prevWheelRevs = wheelRevs;
                    _prevWheelTime = wheelTime;
                    _firstWheelData = false;
                }
                else
                {
                    kph = _logic.CalculateSpeed(_prevWheelRevs, _prevWheelTime, wheelRevs, wheelTime, AppSettings.WheelCircumference);
                    
                    long totalRevs = (long)wheelRevs - _startWheelRevs;
                    if (totalRevs < 0) totalRevs += (long)uint.MaxValue + 1;
                    
                    double candidateDist = _logic.CalculateDistance((uint)totalRevs, AppSettings.WheelCircumference);
                    
                    if (candidateDist - _lastDistMeters <= 1000.0)
                    {
                        distMeters = candidateDist;
                        _lastDistMeters = candidateDist;
                    }
                    
                    _prevWheelRevs = wheelRevs;
                    _prevWheelTime = wheelTime;
                }

                SpeedValuesUpdated?.Invoke(kph, distMeters);
            }
        }

        public async Task DisconnectAsync()
        {
            _isLoopRunning = false;
            
            if (_powerWatcher != null)
            {
                _powerWatcher.Dispose();
                _powerWatcher = null;
            }

            if (_deviceWatcher != null)
            {
                _deviceWatcher.Dispose();
                _deviceWatcher = null;
            }

            if (_powerChar != null)
            {
                try { 
                    await _powerChar.StopNotifyAsync();
                } catch {}
                _powerChar = null;
            }

            if (_device != null)
            {
                try { await _device.DisconnectAsync(); } catch {}
                _device = null;
            }
            
            _controlPoint = null;
            UpdateStatus("Disconnected");
        }

        public void QueueResistance(double resistance)
        {
            _pendingResistance = resistance;
        }

        public void QueueGrade(double gradePercent)
        {
            double resistance = _logic.CalculateResistanceFromGrade(gradePercent);
            QueueResistance(resistance);
        }

        public async Task<bool> SendInitCommand()
        {
            if (_controlPoint == null) return false;
            byte[] initCmd = new byte[] { 0x00 };
            return await WriteWithRetry(initCmd);
        }

        private async Task CommandLoop()
        {
            while (_isLoopRunning && IsConnected)
            {
                if (_pendingResistance.HasValue && _controlPoint != null)
                {
                    double target = _pendingResistance.Value;
                    byte[] cmd = _logic.CreateWahooResistanceCommand(target);
                    
                    bool success = await WriteWithRetry(cmd);
                    if (success)
                    {
                        _pendingResistance = null;
                        Logger.Log($"Sent Resistance: {(target * 100):F0}%");
                    }
                    else
                    {
                        Logger.Log("Retrying resistance command...");
                        await Task.Delay(2000); 
                        continue; 
                    }
                }
                await Task.Delay(200);
            }
        }

        private async Task<bool> WriteWithRetry(byte[] data)
        {
            if (_controlPoint == null) return false;
            try
            {
                await _controlPoint.WriteValueAsync(data, new Dictionary<string, object>());
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Write Error: {ex.Message}");
                return false;
            }
        }

        public void StopScanning()
        {
            if (_adapter != null && IsScanning)
            {
                _ = _adapter.StopDiscoveryAsync();
                _adapter.DeviceFound -= Adapter_DeviceFound;
                IsScanning = false;
            }
            UpdateStatus("Scanning stopped.");
        }
    }
}
