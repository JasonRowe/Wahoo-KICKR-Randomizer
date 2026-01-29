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
        private static readonly Guid POWER_SERVICE_UUID = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        // Removed Speed Service UUID

        // Updated WAHOO_SERVICE based on device dump (ee01) - Matched Console App
        private static readonly Guid WAHOO_SERVICE_UUID = new Guid("a026ee01-0a7d-4ab3-97fa-f1500f9feb8b");
        private static readonly Guid WAHOO_CONTROL_POINT_UUID = new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b");
        
        private static readonly Guid POWER_MEASUREMENT_UUID = new Guid("00002A63-0000-1000-8000-00805f9b34fb");
        // Removed CSC Measurement UUID

        private BluetoothLEAdvertisementWatcher? _watcher;
        public ObservableCollection<DeviceDisplay> FoundDevices { get; set; } = new ObservableCollection<DeviceDisplay>();

        public event Action<BluetoothLEDevice, GattCharacteristic, GattCharacteristic?>? ConnectionSuccessful;

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
            TxtStatus.Text = "Status: Scanning for trainers...";
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

                        // Auto-select if it's the only device
                        if (FoundDevices.Count == 1)
                        {
                            ListDevices.SelectedIndex = 0;
                        }
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
            Logger.Log($"Connecting to device: {selectedDevice.Name} ({selectedDevice.Address})");

            try
            {
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.Address);
                if (device == null)
                {
                    TxtStatus.Text = "Status: Failed to connect.";
                    BtnConnect.IsEnabled = true;
                    Logger.Log("Failed to connect: Device object is null.");
                    return;
                }

                // 1. Get All Services (Robust method from Console)
                var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    TxtStatus.Text = $"Status: Failed to get services ({servicesResult.Status})";
                    BtnConnect.IsEnabled = true;
                    Logger.Log($"Failed to get services: {servicesResult.Status}");
                    return;
                }
                var allServices = servicesResult.Services;
                Logger.Log($"Found {allServices.Count} services.");

                // 2. Find Control Point
                GattCharacteristic? controlPoint = null;
                var cpCandidates = new[] { WAHOO_SERVICE_UUID, POWER_SERVICE_UUID };
                
                foreach (var uuid in cpCandidates)
                {
                    var service = allServices.FirstOrDefault(s => s.Uuid == uuid);
                    if (service != null)
                    {
                        var charsResult = await service.GetCharacteristicsForUuidAsync(WAHOO_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                        if (charsResult.Status == GattCommunicationStatus.Success && charsResult.Characteristics.Count > 0)
                        {
                            controlPoint = charsResult.Characteristics[0];
                            Logger.Log($"Found Control Point in Service {service.Uuid}");
                            break;
                        }
                    }
                }

                if (controlPoint == null)
                {
                    TxtStatus.Text = "Status: Control Point not found.";
                    BtnConnect.IsEnabled = true;
                    Logger.Log("CRITICAL: Control Point NOT found.");
                    return;
                }

                // 3. Find Power Measurement
                GattCharacteristic? powerChar = null;
                var pwrService = allServices.FirstOrDefault(s => s.Uuid == POWER_SERVICE_UUID);
                if (pwrService != null)
                {
                    var chars = await pwrService.GetCharacteristicsForUuidAsync(POWER_MEASUREMENT_UUID, BluetoothCacheMode.Uncached);
                    if (chars.Status == GattCommunicationStatus.Success && chars.Characteristics.Count > 0)
                    {
                        powerChar = chars.Characteristics[0];
                        Logger.Log("Found Power Measurement.");
                    }
                    else
                    {
                        Logger.Log("Power Measurement char not found in Power Service.");
                    }
                }
                else
                {
                    Logger.Log("Power Service not found.");
                }

                ConnectionSuccessful?.Invoke(device, controlPoint, powerChar);
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