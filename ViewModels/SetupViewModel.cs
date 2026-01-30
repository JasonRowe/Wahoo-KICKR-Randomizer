using BikeFitnessApp.MVVM;
using BikeFitnessApp.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BikeFitnessApp.ViewModels
{
    public class SetupViewModel : ObservableObject
    {
        private readonly IBluetoothService _bluetoothService;
        private string _status = "Ready to scan";
        private bool _isScanning;

        public ObservableCollection<DeviceDisplay> Devices { get; } = new ObservableCollection<DeviceDisplay>();

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        private DeviceDisplay? _selectedDevice;
        public DeviceDisplay? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        public RelayCommand ScanCommand { get; }
        public RelayCommand ConnectCommand { get; }

        public event System.Action? ConnectionSuccessful;

        public SetupViewModel(IBluetoothService bluetoothService)
        {
            _bluetoothService = bluetoothService;
            
            _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
            _bluetoothService.StatusChanged += OnStatusChanged;

            ScanCommand = new RelayCommand(StartScan);
            ConnectCommand = new RelayCommand(_ => Connect());

            StartScan();
        }

        public void Cleanup()
        {
            if (IsScanning)
            {
                _bluetoothService.StopScanning();
                IsScanning = false;
            }
            _bluetoothService.DeviceDiscovered -= OnDeviceDiscovered;
            _bluetoothService.StatusChanged -= OnStatusChanged;
        }

        private void StartScan()
        {
            Devices.Clear();
            SelectedDevice = null;
            IsScanning = true;
            _bluetoothService.StartScanning();
        }

        private async void Connect()
        {
            // If only one device exists and none selected, select it automatically
            if (SelectedDevice == null && Devices.Count == 1)
            {
                SelectedDevice = Devices[0];
            }

            if (SelectedDevice == null) return;
            
            IsScanning = false;
            await _bluetoothService.ConnectAsync(SelectedDevice.Address);
            if (_bluetoothService.IsConnected)
            {
                ConnectionSuccessful?.Invoke();
            }
        }

        private void OnDeviceDiscovered(DeviceDisplay device)
        {
            void AddDevice()
            {
                // Avoid duplicates
                foreach (var d in Devices)
                {
                    if (d.Address == device.Address) return;
                }
                Devices.Add(device);
                
                // Auto-select if it's the first one
                if (Devices.Count == 1)
                {
                    SelectedDevice = device;
                }
            }

            // Run on UI thread if available, otherwise run directly (for tests)
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(AddDevice);
            }
            else
            {
                AddDevice();
            }
        }

        private void OnStatusChanged(string status)
        {
            Status = status;
        }
    }
}
