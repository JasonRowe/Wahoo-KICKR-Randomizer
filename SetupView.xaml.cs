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
        private static readonly Guid SPEED_SERVICE_UUID = new Guid("00001816-0000-1000-8000-00805f9b34fb");
        
        private static readonly Guid WAHOO_SERVICE_UUID = new Guid("a026e001-0a7d-4ab3-97fa-f1500f9feb8b");
        private static readonly Guid WAHOO_CONTROL_POINT_UUID = new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b");
        
        private static readonly Guid POWER_MEASUREMENT_UUID = new Guid("00002A63-0000-1000-8000-00805f9b34fb");
        private static readonly Guid CSC_MEASUREMENT_UUID = new Guid("00002A5B-0000-1000-8000-00805f9b34fb");

        private BluetoothLEAdvertisementWatcher? _watcher;
        public ObservableCollection<DeviceDisplay> FoundDevices { get; set; } = new ObservableCollection<DeviceDisplay>();

        public event Action<BluetoothLEDevice, GattCharacteristic, GattCharacteristic?, GattCharacteristic?>? ConnectionSuccessful;

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

                // Helper local function to find a characteristic
                async Task<GattCharacteristic?> FindCharacteristic(BluetoothLEDevice d, Guid serviceUuid, Guid charUuid)
                {
                    try 
                    {
                        var serviceResult = await d.GetGattServicesForUuidAsync(serviceUuid, BluetoothCacheMode.Uncached);
                        if (serviceResult.Status == GattCommunicationStatus.Success && serviceResult.Services.Count > 0)
                        {
                            var service = serviceResult.Services[0];
                            var charResult = await service.GetCharacteristicsForUuidAsync(charUuid, BluetoothCacheMode.Uncached);
                            if (charResult.Status == GattCommunicationStatus.Success && charResult.Characteristics.Count > 0)
                            {
                                return charResult.Characteristics[0];
                            }
                        }
                    }
                    catch { /* Ignore specific lookup errors */ }
                    return null;
                }

                // 1. Find Wahoo Control Point
                GattCharacteristic? controlPoint = await FindCharacteristic(device, WAHOO_SERVICE_UUID, WAHOO_CONTROL_POINT_UUID);
                
                if (controlPoint == null)
                {
                    Logger.Log("Control Point not found in Wahoo Service. Checking Power Service...");
                    controlPoint = await FindCharacteristic(device, POWER_SERVICE_UUID, WAHOO_CONTROL_POINT_UUID);
                }

                if (controlPoint == null)
                {
                    // Fallback: Get ALL services and search manually (most robust method)
                    Logger.Log("Control Point not found by UUID. Scanning ALL services...");
                    var allServices = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                    if (allServices.Status == GattCommunicationStatus.Success)
                    {
                        foreach (var service in allServices.Services)
                        {
                            Logger.Log($"Scanning Service: {service.Uuid}");
                            if (service.Uuid == WAHOO_SERVICE_UUID || service.Uuid == POWER_SERVICE_UUID)
                            {
                                var cpResult = await service.GetCharacteristicsForUuidAsync(WAHOO_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                                if (cpResult.Status == GattCommunicationStatus.Success && cpResult.Characteristics.Count > 0)
                                {
                                    controlPoint = cpResult.Characteristics[0];
                                    Logger.Log("Found Control Point via ALL scan.");
                                    break;
                                }
                            }
                        }
                    }
                }

                if (controlPoint == null)
                {
                    TxtStatus.Text = "Status: Control Point not found.";
                    BtnConnect.IsEnabled = true;
                    Logger.Log("CRITICAL: Control Point NOT found in any service.");
                    return;
                }

                // 2. Get Power Measurement (Soft Fail)
                GattCharacteristic? powerChar = await FindCharacteristic(device, POWER_SERVICE_UUID, POWER_MEASUREMENT_UUID);
                if (powerChar == null) Logger.Log("Power Measurement not found.");
                else Logger.Log("Found Power Measurement.");

                // 3. Get Speed Service (Soft Fail)
                GattCharacteristic? speedChar = await FindCharacteristic(device, SPEED_SERVICE_UUID, CSC_MEASUREMENT_UUID);
                if (speedChar == null) Logger.Log("Speed Measurement not found.");
                else Logger.Log("Found Speed Measurement.");

                ConnectionSuccessful?.Invoke(device, controlPoint, powerChar, speedChar);
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