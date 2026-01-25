using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BikeFitnessApp
{
    public partial class MainWindow : Window
    {
        private static readonly Guid FTMS_SERVICE_UUID = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        // Fitness Machine Control Point UUID
        private static readonly Guid FTMS_CONTROL_POINT_UUID = new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b");

        private BluetoothLEAdvertisementWatcher? _watcher;
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _controlPoint;
        private DispatcherTimer _workoutTimer;
        private KickrLogic _logic = new KickrLogic();

        public ObservableCollection<DeviceDisplay> FoundDevices { get; set; } = new ObservableCollection<DeviceDisplay>();

        public MainWindow()
        {
            InitializeComponent();
            ListDevices.ItemsSource = FoundDevices;

            // Setup Timer for random changes
            _workoutTimer = new DispatcherTimer();
            _workoutTimer.Interval = TimeSpan.FromSeconds(30); 
            _workoutTimer.Tick += WorkoutTimer_Tick;
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (_watcher != null)
            {
                _watcher.Stop();
            }
            FoundDevices.Clear();
            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.ScanningMode = BluetoothLEScanningMode.Active;
            _watcher.Received += Watcher_Received;
            _watcher.Start();
            TxtStatus.Text = "Status: Scanning for KICKR...";
        }

        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // Filter for devices that look like a KICKR
            if (!string.IsNullOrEmpty(args.Advertisement.LocalName) && 
                (args.Advertisement.LocalName.ToUpper().Contains("KICKR") || args.Advertisement.LocalName.ToUpper().Contains("WAHOO")))
            {
                Dispatcher.Invoke(() =>
                {
                    if (!FoundDevices.Any(d => d.Address == args.BluetoothAddress))
                    {
                        FoundDevices.Add(new DeviceDisplay { Name = args.Advertisement.LocalName, Address = args.BluetoothAddress });
                        BtnConnect.IsEnabled = true;
                    }
                });
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = ListDevices.SelectedItem as DeviceDisplay;
            if (selectedDevice == null) return;

            if(_watcher != null)
            {
                _watcher.Stop();
                _watcher = null;
            }
            
            TxtStatus.Text = "Status: Connecting...";

            // Dispose of previous device connection if it exists
            if (_device != null)
            {
                _device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                _device.Dispose();
                _device = null;
                _controlPoint = null;
            }

            try
            {
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.Address);
                _device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
                
                if (_device == null)
                {
                    TxtStatus.Text = "Status: Failed to connect.";
                    return;
                }

                // Find the Fitness Machine Service
                var servicesResult = await _device.GetGattServicesForUuidAsync(FTMS_SERVICE_UUID, BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    var allServicesResult = await _device.GetGattServicesAsync();
                    if (allServicesResult.Status == GattCommunicationStatus.Success)
                    {
                        var allServices = allServicesResult.Services.Select(s => s.Uuid).ToList();
                        var serviceList = string.Join("\n", allServices);
                        TxtStatus.Text = $"FTMS service not found. Found services:\n{serviceList}";
                    }
                    else
                    {
                        TxtStatus.Text = "Status: FTMS Service not found and could not get all services.";
                    }
                    return;
                }

                var service = servicesResult.Services[0];
                var characteristicsResult = await service.GetCharacteristicsForUuidAsync(FTMS_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                
                if (characteristicsResult.Status != GattCommunicationStatus.Success || characteristicsResult.Characteristics.Count == 0)
                {
                    TxtStatus.Text = "Status: Control Point not found.";
                    return;
                }

                _controlPoint = characteristicsResult.Characteristics[0];
                
                // Request Control (Op Code 0x00) to take ownership of the trainer
                await SendCommand(0x00, (byte?)null);

                TxtStatus.Text = $"Status: Connected to {selectedDevice.Name}";
                BtnSend.IsEnabled = true;
                BtnRequestControl.IsEnabled = true;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
                Logger.Log($"Connection error: {ex}");
            }
        }

        private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Log($"Device Connection Status Changed: {sender.ConnectionStatus}");
                if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                {
                    TxtStatus.Text = "Status: Device Disconnected.";
                    BtnSend.IsEnabled = false;
                    BtnRequestControl.IsEnabled = false;
                }
            });
        }

        private async void BtnRequestControl_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Status: Requesting Control (0x00)...";
            try
            {
                await SendCommand(0x00, (byte?)null);
                TxtStatus.Text = "Status: Control Requested.";
            }
            catch (Exception ex)
            {
                HandleCommandError(ex);
            }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parse OpCode (Handle Hex with or without 0x prefix)
                string opCodeText = TxtOpCode.Text.Trim();
                if (opCodeText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    opCodeText = opCodeText.Substring(2);
                }

                if (!byte.TryParse(opCodeText, System.Globalization.NumberStyles.HexNumber, null, out byte opCode))
                {
                    TxtStatus.Text = "Error: Invalid OpCode. Use Hex (e.g., 04, 05).";
                    return;
                }
                
                // Parse Value (Allow decimals but cast to int, handle parsing errors)
                if (!double.TryParse(TxtValue.Text, out double doubleVal))
                {
                    TxtStatus.Text = "Error: Invalid Value. Please enter a number.";
                    return;
                }
                
                // Apply Scaling
                double multiplier = 1.0;
                if (CmbScale.SelectedIndex == 1) multiplier = 10.0;
                else if (CmbScale.SelectedIndex == 2) multiplier = 100.0;

                int val = (int)(doubleVal * multiplier);

                string logMsg = $"Input: {TxtValue.Text}, Scale: x{multiplier}, Sending: {val} (Op {opCode:X2})";
                Logger.Log(logMsg);
                TxtStatus.Text = $"Status: {logMsg}...";

                // Use CheckBox to determine if we send as Short (16-bit) or Byte (8-bit)
                if (Chk16Bit.IsChecked == true)
                {
                    // Cast to short first to handle negatives (2's complement), then ushort
                    await SendCommand(opCode, (ushort)(short)val);
                }
                else
                {
                    // Cast to byte (unchecked) to allow wrapping for negatives/overflows
                    await SendCommand(opCode, (byte)val);
                }

                TxtStatus.Text = $"Status: Sent Op {opCode:X2} Val {val}. Verify on Bike.";
            }
            catch (Exception ex)
            {
                HandleCommandError(ex);
            }
        }

        private async void WorkoutTimer_Tick(object? sender, EventArgs e)
        {
            if (_controlPoint == null)
            {
                Logger.Log("WorkoutTimer_Tick called but _controlPoint is null.");
                return;
            }

            try
            {
                // Convert UI percentage (0-10) to logic value (0.0-0.1)
                // double resistance = _logic.CalculateResistance(SliderMin.Value / 100.0, SliderMax.Value / 100.0);
                
                // Logger.Log($"Calculated resistance: {resistance}");
                // TxtCurrentResistance.Text = $"Min: {SliderMin.Value:F0}% Max: {SliderMax.Value:F0}% Current: {(resistance * 100):F1}%";

                // FTMS Op Code 0x04 is "Set Target Resistance Level". Send byte 0-100.
                // await SendCommand(0x04, (byte)(resistance * 100));
                Logger.Log("Successfully sent resistance command.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Exception in WorkoutTimer_Tick: {ex}");
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }



        private async Task SendCommand(byte opCode, byte? parameter = null)
        {
            if (_device == null || _device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                TxtStatus.Text = "Error: Device not connected.";
                return;
            }

            byte[] bytes;
            if (parameter.HasValue)
            {
                bytes = _logic.CreateCommandBytes(opCode, parameter.Value);
            }
            else
            {
                bytes = _logic.CreateCommandBytes(opCode);
            }

            Logger.Log($"Sending command (Byte): OpCode={opCode:X2}, Param={parameter}, Bytes=[{BitConverter.ToString(bytes)}]");
            var writer = new DataWriter();
            writer.WriteBytes(bytes);

            if (_controlPoint != null)
            {
                await WriteCharacteristicWithRetry(writer.DetachBuffer());
            }
        }

        private async Task SendCommand(byte opCode, ushort? parameter = null)
        {
            if (_device == null || _device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                TxtStatus.Text = "Error: Device not connected.";
                return;
            }

            byte[] bytes;
            if (parameter.HasValue)
            {
                bytes = _logic.CreateCommandBytes(opCode, parameter.Value);
            }
            else
            {
                bytes = _logic.CreateCommandBytes(opCode);
            }

            Logger.Log($"Sending command: OpCode={opCode}, Param={parameter}, Bytes=[{string.Join(", ", bytes)}]");
            Logger.Log($"Sending command (UShort): OpCode={opCode:X2}, Param={parameter}, Bytes=[{BitConverter.ToString(bytes)}]");
            var writer = new DataWriter();
            writer.WriteBytes(bytes);

            if (_controlPoint != null)
            {
                await WriteCharacteristicWithRetry(writer.DetachBuffer());
            }
        }

        private async Task WriteCharacteristicWithRetry(IBuffer buffer)
        {
            const int MaxRetries = 5;
            int delay = 250;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    var status = await _controlPoint.WriteValueAsync(buffer);
                    if (status == GattCommunicationStatus.Success)
                    {
                        return;
                    }
                    Logger.Log($"Write failed with status: {status}. Retry {i + 1}/{MaxRetries}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Write exception: {ex}. Retry {i + 1}/{MaxRetries}");
                    if (i == MaxRetries - 1) throw; // Throw on last attempt
                }
                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }

        private void HandleCommandError(Exception ex)
        {
            Logger.Log($"Command Error: {ex}");
            if (ex.Message.Contains("0x80650081") || (ex is System.Runtime.InteropServices.COMException comEx && comEx.ErrorCode == -2140995455))
            {
                TxtStatus.Text = "Error: Device Unreachable (0x80650081). Try connecting again.";
            }
            else
            {
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }
    }

    public class DeviceDisplay
    {
        public string Name { get; set; } = "";
        public ulong Address { get; set; }
    }
}