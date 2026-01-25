using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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
                BtnStart.IsEnabled = true;
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
                    BtnStart.IsEnabled = false;
                    BtnStop.IsEnabled = false;
                    _workoutTimer.Stop();
                }
            });
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Start button clicked.");
            try
            {
                _workoutTimer.Start();
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
                
                // Trigger immediately
                WorkoutTimer_Tick(this, EventArgs.Empty); 
                
                TxtStatus.Text = "Status: Workout Started";
            }
            catch (Exception ex)
            {
                Logger.Log($"Error starting workout: {ex}");
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Stop button clicked.");
            _workoutTimer.Stop();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Status: Workout Stopped";
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
                double min = SliderMin.Value / 100.0;
                double max = SliderMax.Value / 100.0;
                double resistance = _logic.CalculateResistance(min, max);
                
                Logger.Log($"Calculated resistance: {resistance}");
                TxtCurrentResistance.Text = $"{(resistance * 100):F0}%";

                // Color coding: Green (Low) -> Yellow -> Red (High) relative to the range
                double range = max - min;
                double ratio = range > 0 ? (resistance - min) / range : 0;
                ratio = Math.Clamp(ratio, 0, 1);

                byte r = 0;
                byte g = 0;
                if (ratio < 0.5)
                {
                    r = (byte)(ratio * 2 * 255);
                    g = 255;
                }
                else
                {
                    r = 255;
                    g = (byte)((1 - ratio) * 2 * 255);
                }
                TxtCurrentResistance.Foreground = new SolidColorBrush(Color.FromRgb(r, g, 0));

                // Create Wahoo Command (OpCode 0x42)
                byte[] commandBytes = _logic.CreateWahooResistanceCommand(resistance);
                var writer = new DataWriter();
                writer.WriteBytes(commandBytes);
                await WriteCharacteristicWithRetry(writer.DetachBuffer());

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