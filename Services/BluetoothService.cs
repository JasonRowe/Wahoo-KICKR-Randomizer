using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BikeFitnessApp;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BikeFitnessApp.Services
{
    public class BluetoothService : IBluetoothService
    {
        // UUIDs
        private static readonly Guid POWER_SERVICE_UUID = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        private static readonly Guid WAHOO_SERVICE_UUID = new Guid("a026ee01-0a7d-4ab3-97fa-f1500f9feb8b");
        private static readonly Guid WAHOO_CONTROL_POINT_UUID = new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b");
        private static readonly Guid POWER_MEASUREMENT_UUID = new Guid("00002A63-0000-1000-8000-00805f9b34fb");

        // Internal State
        private BluetoothLEAdvertisementWatcher? _watcher;
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _controlPoint;
        private GattCharacteristic? _powerChar;
        private KickrLogic _logic = new KickrLogic();
        
        // Command Loop State
        private bool _isLoopRunning;
        private double? _pendingResistance;

        // Speed/Distance State
        private uint _prevWheelRevs = 0;
        private ushort _prevWheelTime = 0;
        private uint _startWheelRevs = 0;
        private bool _firstWheelData = true;
        
        // Events
        public event Action<DeviceDisplay>? DeviceDiscovered;
        public event Action<string>? StatusChanged;
        public event Action<int>? PowerReceived;
        public event Action<double, double>? SpeedValuesUpdated;
        public event Action? ConnectionLost;

        public bool IsScanning => _watcher != null && _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;
        public bool IsConnected => _device != null && _device.ConnectionStatus == BluetoothConnectionStatus.Connected;
        public string CurrentStatus { get; private set; } = "Ready";

        public BluetoothService()
        {
        }

        private void UpdateStatus(string status)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(status);
            Logger.Log($"[BT Service] {status}");
        }

        public void StartScanning()
        {
            if (_watcher != null) StopScanning();

            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Passive
            };
            _watcher.Received += Watcher_Received;
            _watcher.Start();
            UpdateStatus("Scanning for trainers...");
        }

        public void StopScanning()
        {
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.Received -= Watcher_Received;
                _watcher = null;
            }
            UpdateStatus("Scanning stopped.");
        }

        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Advertisement.LocalName) &&
                (args.Advertisement.LocalName.ToUpper().Contains("KICKR") || args.Advertisement.LocalName.ToUpper().Contains("WAHOO")))
            {
                DeviceDiscovered?.Invoke(new DeviceDisplay 
                { 
                    Name = args.Advertisement.LocalName, 
                    Address = args.BluetoothAddress 
                });
            }
        }

        public async Task ConnectAsync(ulong address)
        {
            StopScanning();
            UpdateStatus($"Connecting to {address}...");

            // Reset Calculation State
            _firstWheelData = true;
            _prevWheelRevs = 0;
            _prevWheelTime = 0;
            _startWheelRevs = 0;

            try
            {
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
                if (_device == null)
                {
                    UpdateStatus("Failed to connect: Device object is null.");
                    return;
                }

                _device.ConnectionStatusChanged += Device_ConnectionStatusChanged;

                // 1. Get Services
                var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    UpdateStatus($"Failed to get services ({servicesResult.Status})");
                    return;
                }

                var allServices = servicesResult.Services;
                
                // 2. Find Control Point
                _controlPoint = null;
                var cpCandidates = new[] { WAHOO_SERVICE_UUID, POWER_SERVICE_UUID };
                
                foreach (var uuid in cpCandidates)
                {
                    var service = allServices.FirstOrDefault(s => s.Uuid == uuid);
                    if (service != null)
                    {
                        var charsResult = await service.GetCharacteristicsForUuidAsync(WAHOO_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                        if (charsResult.Status == GattCommunicationStatus.Success && charsResult.Characteristics.Count > 0)
                        {
                            _controlPoint = charsResult.Characteristics[0];
                            Logger.Log($"Found Control Point in Service {service.Uuid}");
                            break;
                        }
                    }
                }

                if (_controlPoint == null)
                {
                    UpdateStatus("CRITICAL: Control Point NOT found.");
                    return;
                }

                // 3. Find Power Measurement
                _powerChar = null;
                var pwrService = allServices.FirstOrDefault(s => s.Uuid == POWER_SERVICE_UUID);
                if (pwrService != null)
                {
                    var chars = await pwrService.GetCharacteristicsForUuidAsync(POWER_MEASUREMENT_UUID, BluetoothCacheMode.Uncached);
                    if (chars.Status == GattCommunicationStatus.Success && chars.Characteristics.Count > 0)
                    {
                        _powerChar = chars.Characteristics[0];
                        await SubscribeToPowerAsync();
                    }
                }

                UpdateStatus("Connected");
                
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
                var status = await _powerChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Success)
                {
                    _powerChar.ValueChanged += Power_ValueChanged;
                    Logger.Log("Subscribed to Power notifications.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to subscribe to power: {ex.Message}");
            }
        }

        private void Power_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            byte[] data = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(data);
            
            // 1. Parse Power
            int watts = _logic.ParsePower(data);
            PowerReceived?.Invoke(watts);

            // 2. Parse Speed/Distance (Wheel Data)
            var (hasWheel, wheelRevs, wheelTime) = _logic.ParseWheelDataFromPower(data);
            if (hasWheel)
            {
                double kph = 0;
                double distMeters = 0;

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
                    
                    // We calculate distance based on accumulated revolutions since session start
                    // This handles potential wrapping of wheelRevs (UInt32) better if we just do current - start
                    // But if it wraps, current < start. CalculateDistance handles totalRevs.
                    // Let's assume standard logic:
                    
                    long totalRevs = (long)wheelRevs - _startWheelRevs;
                    if (totalRevs < 0) totalRevs += uint.MaxValue; // Handle wrap once? rarely happens for uint32 (4 billion revs)
                    
                    distMeters = _logic.CalculateDistance((uint)totalRevs, AppSettings.WheelCircumference);
                    
                    _prevWheelRevs = wheelRevs;
                    _prevWheelTime = wheelTime;
                }

                SpeedValuesUpdated?.Invoke(kph, distMeters);
            }
        }

        private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                UpdateStatus("Device Disconnected");
                _isLoopRunning = false;
                ConnectionLost?.Invoke();
            }
        }

        public async Task DisconnectAsync()
        {
            _isLoopRunning = false;
            
            if (_powerChar != null)
            {
                try { 
                    await _powerChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                    _powerChar.ValueChanged -= Power_ValueChanged;
                } catch {}
                _powerChar = null;
            }

            if (_device != null)
            {
                _device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                _device.Dispose();
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
            // "Fake" Sim Mode: Convert Grade -> Resistance
            double resistance = _logic.CalculateResistanceFromGrade(gradePercent);
            QueueResistance(resistance);
        }

        public async Task<bool> SendInitCommand()
        {
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
                var writer = new DataWriter();
                writer.WriteBytes(data);
                var result = await _controlPoint.WriteValueAsync(writer.DetachBuffer());
                return result == GattCommunicationStatus.Success;
            }
            catch (Exception ex)
            {
                Logger.Log($"Write Error: {ex.Message}");
                return false;
            }
        }
    }
}
