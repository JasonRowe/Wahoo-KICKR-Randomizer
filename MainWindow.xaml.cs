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
            }
            
            TxtStatus.Text = "Status: Connecting...";

            try
            {
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.Address);
                
                if (_device == null)
                {
                    TxtStatus.Text = "Status: Failed to connect.";
                    return;
                }

                // Find the Fitness Machine Service
                var servicesResult = await _device.GetGattServicesForUuidAsync(FTMS_SERVICE_UUID);
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
                var characteristicsResult = await service.GetCharacteristicsForUuidAsync(FTMS_CONTROL_POINT_UUID);
                
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

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Start button clicked.");
            _workoutTimer.Start();
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            await SendCommand(0x04, (byte)0); // Set initial resistance to 0
            WorkoutTimer_Tick(this, EventArgs.Empty); // Trigger immediately
            TxtStatus.Text = "Status: Workout Started";
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
                int resistance = _logic.CalculateResistance(SliderMin.Value, SliderMax.Value);
                Logger.Log($"Calculated resistance: {resistance}");
                TxtCurrentResistance.Text = $"Min: {(int)SliderMin.Value}% Max: {(int)SliderMax.Value}% Current: {resistance}%";

                // FTMS Op Code 0x04 is "Set Target Resistance Level"
                await SendCommand(0x04, (byte)resistance);
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
            byte[] bytes;
            if (parameter.HasValue)
            {
                bytes = _logic.CreateCommandBytes(opCode, parameter.Value);
            }
            else
            {
                bytes = _logic.CreateCommandBytes(opCode);
            }

            var writer = new DataWriter();
            writer.WriteBytes(bytes);

            if (_controlPoint != null)
            {
                await _controlPoint.WriteValueAsync(writer.DetachBuffer());
            }
        }

        private async Task SendCommand(byte opCode, ushort? parameter = null)
        {
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
            var writer = new DataWriter();
            writer.WriteBytes(bytes);

            if (_controlPoint != null)
            {
                await _controlPoint.WriteValueAsync(writer.DetachBuffer());
            }
        }
    }

    public class DeviceDisplay
    {
        public string Name { get; set; } = "";
        public ulong Address { get; set; }
    }
}