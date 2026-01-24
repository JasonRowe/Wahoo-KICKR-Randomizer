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
        // Standard Fitness Machine Service UUID
        private static readonly Guid FTMS_SERVICE_UUID = new Guid("00001826-0000-1000-8000-00805f9b34fb");
        // Standard Indoor Bike Service UUID
        private static readonly Guid INDOOR_BIKE_SERVICE_UUID = new Guid("00001826-0000-1000-8000-00805f9b34fb");
        // Fitness Machine Control Point UUID
        private static readonly Guid FTMS_CONTROL_POINT_UUID = new Guid("00002AD9-0000-1000-8000-00805f9b34fb");

        private BluetoothLEAdvertisementWatcher _watcher;
        private BluetoothLEDevice _device;
        private GattCharacteristic _controlPoint;
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

            _watcher.Stop();
            TxtStatus.Text = "Status: Connecting...";

            try
            {
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.Address);

                if (_device == null)
                {
                    TxtStatus.Text = "Status: Failed to connect.";
                    return;
                }

                var service = await GetFitnessMachineService(_device);

                if (service == null)
                {
                    TxtStatus.Text = "Status: FTMS service not found.";
                    return;
                }

                var characteristicsResult = await service.GetCharacteristicsForUuidAsync(FTMS_CONTROL_POINT_UUID);

                if (characteristicsResult.Status != GattCommunicationStatus.Success || characteristicsResult.Characteristics.Count == 0)
                {
                    TxtStatus.Text = "Status: Control Point not found.";
                    return;
                }

                _controlPoint = characteristicsResult.Characteristics[0];

                // Request Control (Op Code 0x00) to take ownership of the trainer
                await SendCommand(new byte[] { 0x00 });

                TxtStatus.Text = $"Status: Connected to {selectedDevice.Name}";
                BtnStart.IsEnabled = true;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
            }
        }

        private async Task<GattDeviceService> GetFitnessMachineService(BluetoothLEDevice device)
        {
            var servicesResult = await device.GetGattServicesForUuidAsync(FTMS_SERVICE_UUID);
            if (servicesResult.Status == GattCommunicationStatus.Success && servicesResult.Services.Count > 0)
            {
                return servicesResult.Services[0];
            }

            var indoorBikeServicesResult = await device.GetGattServicesForUuidAsync(INDOOR_BIKE_SERVICE_UUID);
            if (indoorBikeServicesResult.Status == GattCommunicationStatus.Success && indoorBikeServicesResult.Services.Count > 0)
            {
                return indoorBikeServicesResult.Services[0];
            }

            return null;
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _workoutTimer.Start();
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            await SendCommand(new byte[] { 0x04, 0 }); // Set initial resistance to 0
            WorkoutTimer_Tick(null, null); // Trigger immediately
            TxtStatus.Text = "Status: Workout Started";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _workoutTimer.Stop();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Status: Workout Stopped";
        }

        private async void WorkoutTimer_Tick(object sender, EventArgs e)
        {
            if (_controlPoint == null) return;

            int resistance = _logic.CalculateResistance(SliderMin.Value, SliderMax.Value);
            TxtCurrentResistance.Text = $"Current Resistance: {resistance}%";

            await SendCommand(new byte[] { 0x30, (byte)resistance });
        }



        private async Task SendCommand(byte[] command)
        {
            var writer = new DataWriter();
            writer.WriteBytes(command);
            await _controlPoint.WriteValueAsync(writer.DetachBuffer());
        }
    }

    public class DeviceDisplay
    {
        public string Name { get; set; }
        public ulong Address { get; set; }
    }
}
