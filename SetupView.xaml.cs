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

            try
            {
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(selectedDevice.Address);
                if (device == null)
                {
                    TxtStatus.Text = "Status: Failed to connect.";
                    BtnConnect.IsEnabled = true;
                    return;
                }

                // 1. Find Wahoo Control Point
                GattCharacteristic? controlPoint = null;
                
                // Try Wahoo Service First
                var wahooServices = await device.GetGattServicesForUuidAsync(WAHOO_SERVICE_UUID, BluetoothCacheMode.Uncached);
                if (wahooServices.Status == GattCommunicationStatus.Success && wahooServices.Services.Count > 0)
                {
                    var cpResult = await wahooServices.Services[0].GetCharacteristicsForUuidAsync(WAHOO_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                    if (cpResult.Status == GattCommunicationStatus.Success && cpResult.Characteristics.Count > 0)
                    {
                        controlPoint = cpResult.Characteristics[0];
                    }
                }

                // Fallback: Try Power Service for CP (Legacy/Odd behavior)
                GattDeviceService? powerService = null;
                var powerServices = await device.GetGattServicesForUuidAsync(POWER_SERVICE_UUID, BluetoothCacheMode.Uncached);
                if (powerServices.Status == GattCommunicationStatus.Success && powerServices.Services.Count > 0)
                {
                    powerService = powerServices.Services[0];
                    if (controlPoint == null)
                    {
                        var cpResult = await powerService.GetCharacteristicsForUuidAsync(WAHOO_CONTROL_POINT_UUID, BluetoothCacheMode.Uncached);
                        if (cpResult.Status == GattCommunicationStatus.Success && cpResult.Characteristics.Count > 0)
                        {
                            controlPoint = cpResult.Characteristics[0];
                        }
                    }
                }

                if (controlPoint == null)
                {
                    TxtStatus.Text = "Status: Control Point not found.";
                    BtnConnect.IsEnabled = true;
                    return;
                }

                // 2. Get Power Measurement
                GattCharacteristic? powerChar = null;
                if (powerService != null) // Already found above
                {
                    var pmResult = await powerService.GetCharacteristicsForUuidAsync(POWER_MEASUREMENT_UUID, BluetoothCacheMode.Uncached);
                    if (pmResult.Status == GattCommunicationStatus.Success && pmResult.Characteristics.Count > 0)
                    {
                        powerChar = pmResult.Characteristics[0];
                    }
                }
                else 
                {
                   // Try to find Power Service again if we didn't find it in fallback step
                   // (Unlikely if we are here, but safe to check if fallback wasn't triggered because CP was found in Wahoo service)
                   var servicesResult = await device.GetGattServicesForUuidAsync(POWER_SERVICE_UUID, BluetoothCacheMode.Uncached);
                   if (servicesResult.Status == GattCommunicationStatus.Success && servicesResult.Services.Count > 0)
                   {
                       var service = servicesResult.Services[0];
                       var pmResult = await service.GetCharacteristicsForUuidAsync(POWER_MEASUREMENT_UUID, BluetoothCacheMode.Uncached);
                       if (pmResult.Status == GattCommunicationStatus.Success && pmResult.Characteristics.Count > 0)
                       {
                           powerChar = pmResult.Characteristics[0];
                       }
                   }
                }

                // 3. Get Speed Service
                GattCharacteristic? speedChar = null;
                var speedServicesResult = await device.GetGattServicesForUuidAsync(SPEED_SERVICE_UUID, BluetoothCacheMode.Uncached);
                if (speedServicesResult.Status == GattCommunicationStatus.Success && speedServicesResult.Services.Count > 0)
                {
                    var speedService = speedServicesResult.Services[0];
                    var cscResult = await speedService.GetCharacteristicsForUuidAsync(CSC_MEASUREMENT_UUID, BluetoothCacheMode.Uncached);
                    if (cscResult.Status == GattCommunicationStatus.Success && cscResult.Characteristics.Count > 0)
                    {
                        speedChar = cscResult.Characteristics[0];
                    }
                }

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