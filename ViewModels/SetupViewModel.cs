using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using BikeFitnessApp.MVVM;
using BikeFitnessApp.Services;

namespace BikeFitnessApp.ViewModels
{
    public class SetupViewModel : ObservableObject
    {
        private readonly IBluetoothService _bluetoothService;
        private string _status = "Ready";
        private DeviceDisplay? _selectedDevice;
        private bool _isConnecting;

        public ObservableCollection<DeviceDisplay> FoundDevices { get; } = new ObservableCollection<DeviceDisplay>();

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public DeviceDisplay? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
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

        public bool CanConnect => !IsConnecting && SelectedDevice != null;

        public ICommand ScanCommand { get; }
        public ICommand ConnectCommand { get; }

        public event Action? ConnectionSuccessful;

        public SetupViewModel(IBluetoothService bluetoothService)
        {
            _bluetoothService = bluetoothService;
            _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
            _bluetoothService.StatusChanged += OnStatusChanged;

            ScanCommand = new RelayCommand(_ => StartScanning());
            ConnectCommand = new RelayCommand(_ => Connect(), _ => CanConnect);
            
            StartScanning();
        }

        public void Cleanup()
        {
            _bluetoothService.DeviceDiscovered -= OnDeviceDiscovered;
            _bluetoothService.StatusChanged -= OnStatusChanged;
            _bluetoothService.StopScanning();
        }

        private void StartScanning()
        {
            FoundDevices.Clear();
            _bluetoothService.StartScanning();
        }

        private void OnStatusChanged(string status)
        {
            Status = status;
        }

        private void OnDeviceDiscovered(DeviceDisplay device)
        {
            // Note: In WPF, updating ObservableCollection from background thread requires Dispatcher
            // or using a thread-safe collection. For now, I'll assume we might need to jump to UI thread
            // but I'll try to keep VM UI-agnostic. 
            // Actually, I'll use App.Current.Dispatcher if needed, but let's see if Service already invokes on UI thread.
            // SetupView used Dispatcher.Invoke. So I should probably do it here or in Service.
            // Better to do it in VM or use a helper.
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!FoundDevices.Any(d => d.Address == device.Address))
                {
                    FoundDevices.Add(device);
                    if (FoundDevices.Count == 1)
                    {
                        SelectedDevice = device;
                    }
                }
            });
        }

        private async void Connect()
        {
            if (SelectedDevice == null) return;

            IsConnecting = true;
            try
            {
                await _bluetoothService.ConnectAsync(SelectedDevice.Address);
                if (_bluetoothService.IsConnected)
                {
                    ConnectionSuccessful?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }
    }
}
