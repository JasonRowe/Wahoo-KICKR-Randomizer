using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BikeFitnessApp
{
    public partial class SetupView : UserControl
    {
        private static readonly Guid FTMS_SERVICE_UUID = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        private static readonly Guid FTMS_CONTROL_POINT_UUID = new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b");

        private BluetoothLEAdvertisementWatcher? _watcher;
        public ObservableCollection<DeviceDisplay> FoundDevices { get; set; } = new ObservableCollection<DeviceDisplay>();

        public event Action<BluetoothLEDevice, GattCharacteristic>? ConnectionSuccessful;

        public SetupView()
        {
            InitializeComponent();
            ListDevices.ItemsSource = FoundDevices;
            Loaded += SetupView_Loaded;
        }

        private void SetupView_Loaded(object sender, RoutedEventArgs e)
        {
            StartScanning();
        }

        private void StartScanning()
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
            BtnConnect.IsEnabled = false;

            try
            {
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.Address);
                if (device == null)
                {
                    TxtStatus.Text = "Status: Failed to connect.";
                    BtnConnect.IsEnabled = true;
                    return;
                }

                var servicesResult = await device.GetGattServicesForUuidAsync(FTMS_SERVICE_UUID, BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    TxtStatus.Text = "Status: FTMS Service not found.";
                    BtnConnect.IsEnabled = true;
                    return;
                }

                var service = servicesResult.Services[0];
                var characteristicsResult = await service.GetCharacteristicsForUuidAsync(FTMS_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                
                if (characteristicsResult.Status != GattCommunicationStatus.Success || characteristicsResult.Characteristics.Count == 0)
                {
                    TxtStatus.Text = "Status: Control Point not found.";
                    BtnConnect.IsEnabled = true;
                    return;
                }

                var controlPoint = characteristicsResult.Characteristics[0];
                ConnectionSuccessful?.Invoke(device, controlPoint);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
                Logger.Log($"Connection error: {ex}");
                BtnConnect.IsEnabled = true;
            }
        }

        private void BtnRescan_Click(object sender, RoutedEventArgs e)
        {
            StartScanning();
        }
    }
}