using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BikeFitnessApp.Services;

namespace BikeFitnessApp
{
    public partial class SetupView : UserControl
    {
        public ObservableCollection<DeviceDisplay> FoundDevices { get; set; } = new ObservableCollection<DeviceDisplay>();

        // Changed event signature - no longer passing raw Bluetooth objects
        public event Action? ConnectionSuccessful;

        private IBluetoothService _bluetoothService;

        public SetupView()
        {
            InitializeComponent();
            ListDevices.ItemsSource = FoundDevices;
            _bluetoothService = App.BluetoothService;
            
            Loaded += SetupView_Loaded;
            Unloaded += SetupView_Unloaded;
        }

        private void SetupView_Loaded(object sender, RoutedEventArgs e)
        {
            _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;
            _bluetoothService.StatusChanged += OnStatusChanged;
            
            StartScanning();
        }

        private void SetupView_Unloaded(object sender, RoutedEventArgs e)
        {
            _bluetoothService.DeviceDiscovered -= OnDeviceDiscovered;
            _bluetoothService.StatusChanged -= OnStatusChanged;
            _bluetoothService.StopScanning();
        }

        private void OnStatusChanged(string status)
        {
            Dispatcher.Invoke(() => TxtStatus.Text = $"Status: {status}");
        }

        private void OnDeviceDiscovered(DeviceDisplay device)
        {
            Dispatcher.Invoke(() =>
            {
                if (!FoundDevices.Any(d => d.Address == device.Address))
                {
                    FoundDevices.Add(device);
                    BtnConnect.IsEnabled = true;

                    if (FoundDevices.Count == 1)
                    {
                        ListDevices.SelectedIndex = 0;
                    }
                }
            });
        }

        private void StartScanning()
        {
            FoundDevices.Clear();
            _bluetoothService.StartScanning();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = ListDevices.SelectedItem as DeviceDisplay;
            if (selectedDevice == null) return;

            BtnConnect.IsEnabled = false;

            try
            {
                await _bluetoothService.ConnectAsync(selectedDevice.Address);
                
                if (_bluetoothService.IsConnected)
                {
                    ConnectionSuccessful?.Invoke();
                }
                else
                {
                    BtnConnect.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
                BtnConnect.IsEnabled = true;
            }
        }

        private void BtnRescan_Click(object sender, RoutedEventArgs e)
        {
            StartScanning();
        }
    }
}
