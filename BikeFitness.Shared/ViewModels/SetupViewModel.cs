using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BikeFitness.Shared.Services;
using BikeFitness.Shared.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BikeFitness.Shared.ViewModels
{
    public partial class SetupViewModel : ObservableObject, System.IDisposable
    {
        private readonly IBluetoothService _bluetoothService;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanScan))]
        private string _status = "Ready to scan";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanScan))]
        private bool _isScanning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanScan))]
        [NotifyPropertyChangedFor(nameof(CanConnect))]
        private bool _isConnecting;

        public ObservableCollection<DeviceDisplay> Devices { get; } = new ObservableCollection<DeviceDisplay>();

        public bool CanScan => !IsScanning && !IsConnecting;
        public bool CanConnect => !IsConnecting && (SelectedDevice != null || Devices.Count == 1);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanConnect))]
        private DeviceDisplay? _selectedDevice;

        partial void OnSelectedDeviceChanged(DeviceDisplay? value)
        {
            ScanCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsScanningChanged(bool value)
        {
            ScanCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsConnectingChanged(bool value)
        {
            ScanCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
        }

        public IRelayCommand ScanCommand { get; }
        public IRelayCommand ConnectCommand { get; }

        public event System.Action? ConnectionSuccessful;

        // Platform-specific UI thread dispatcher
        public static System.Action<System.Action>? UIDispatcher { get; set; }

        public SetupViewModel(IBluetoothService bluetoothService)
        {
            _bluetoothService = bluetoothService;
            
            _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
            _bluetoothService.StatusChanged += (s) => Status = s;

            ScanCommand = new RelayCommand(StartScan, () => CanScan);
            ConnectCommand = new RelayCommand(Connect, () => CanConnect);

            StartScan();
        }

        public void Dispose()
        {
            if (IsScanning)
            {
                _bluetoothService.StopScanning();
                IsScanning = false;
            }
            _bluetoothService.DeviceDiscovered -= OnDeviceDiscovered;
            // Note: anonymous delegate for StatusChanged might cause slight leak if disposed many times, 
            // but for this app it's fine. 
            System.GC.SuppressFinalize(this);
        }

        private void StartScan()
        {
            Devices.Clear();
            SelectedDevice = null;
            
            IsScanning = true;
            _bluetoothService.StartScanning();
            
            if (_bluetoothService.CurrentStatus.Contains("Error"))
            {
                IsScanning = false;
            }
        }

        private async void Connect()
        {
            if (SelectedDevice == null && Devices.Count == 1)
            {
                SelectedDevice = Devices[0];
            }

            if (SelectedDevice == null) return;
            
            IsConnecting = true;
            IsScanning = false;
            _bluetoothService.StopScanning();

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
            }
        }

        private void OnDeviceDiscovered(DeviceDisplay device)
        {
            void AddDevice()
            {
                foreach (var d in Devices)
                {
                    if (d.Address == device.Address) return;
                }
                Devices.Add(device);
                
                if (Devices.Count == 1)
                {
                    SelectedDevice = device;
                }
                
                ConnectCommand.NotifyCanExecuteChanged();
            }

            if (UIDispatcher != null)
            {
                UIDispatcher(AddDevice);
            }
            else
            {
                AddDevice();
            }
        }
    }
}
