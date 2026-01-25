using System;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BikeFitnessApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowSetup();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            PowerManagement.AllowSleep();
            base.OnClosing(e);
        }

        private void ShowSetup()
        {
            var setupView = new SetupView();
            setupView.ConnectionSuccessful += (device, controlPoint) =>
            {
                ShowWorkout(device, controlPoint);
            };
            MainContainer.Children.Clear();
            MainContainer.Children.Add(setupView);
        }

        private void ShowWorkout(BluetoothLEDevice device, GattCharacteristic controlPoint)
        {
            var workoutView = new WorkoutView(device, controlPoint);
            workoutView.Disconnected += () =>
            {
                // Optionally go back to setup or show error
                Dispatcher.Invoke(() => ShowSetup());
            };
            MainContainer.Children.Clear();
            MainContainer.Children.Add(workoutView);
        }
    }
}