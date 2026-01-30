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
        private bool _isConnecting;

        public ObservableCollection<DeviceDisplay> Devices { get; } = new ObservableCollection<DeviceDisplay>();

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    OnPropertyChanged(nameof(CanScan));
                }
            }
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                if (SetProperty(ref _isConnecting, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                }
            }
        }

        public bool CanScan => !IsScanning && !IsConnecting;
        public bool CanConnect => !IsConnecting && (SelectedDevice != null || Devices.Count == 1);

        private DeviceDisplay? _selectedDevice;
        public DeviceDisplay? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RelayCommand ScanCommand { get; }
        public RelayCommand ConnectCommand { get; }

        public event System.Action? ConnectionSuccessful;

        public SetupViewModel(IBluetoothService bluetoothService)
        {
            _bluetoothService = bluetoothService;
            
            _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
            _bluetoothService.StatusChanged += OnStatusChanged;

            ScanCommand = new RelayCommand(StartScan, () => CanScan);
            ConnectCommand = new RelayCommand(Connect, () => CanConnect);

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
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async void Connect()
        {
            // If only one device exists and none selected, select it automatically
            if (SelectedDevice == null && Devices.Count == 1)
            {
                SelectedDevice = Devices[0];
            }

            if (SelectedDevice == null) return;
            
            IsConnecting = true;
            IsScanning = false;
            _bluetoothService.StopScanning();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            try
            {
                await _bluetoothService.ConnectAsync(SelectedDevice.Address);
                if (_bluetoothService.IsConnected)
                {
                    ConnectionSuccessful?.Invoke();
                }
            }
            finally
            {
                IsConnecting = false;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
                
                // Trigger re-eval of Connect button immediately
                OnPropertyChanged(nameof(CanConnect));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
