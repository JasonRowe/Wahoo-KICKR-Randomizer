using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BikeFitnessApp.Services;

namespace BikeFitnessApp
{
    public partial class WorkoutView : UserControl
    {
        private IBluetoothService _bluetoothService;
        private DispatcherTimer _workoutTimer;
        private KickrLogic _logic = new KickrLogic();
        private int _stepIndex = 0;
        private int _intervalSeconds = 30;

        public event Action? Disconnected;

        public WorkoutView()
        {
            InitializeComponent();
            _bluetoothService = App.BluetoothService;

            // Subscribe to Service Events
            _bluetoothService.ConnectionLost += OnConnectionLost;
            _bluetoothService.PowerReceived += OnPowerReceived;

            this.Unloaded += WorkoutView_Unloaded;

            // Setup Timer for random changes
            _workoutTimer = new DispatcherTimer();
            UpdateInterval();
            _workoutTimer.Tick += WorkoutTimer_Tick;

            // Initialize Logging Menu State
            MenuEnableLogging.IsChecked = Logger.IsEnabled;

            // Init Trainer
            _ = InitializeTrainer();
        }

        private async Task InitializeTrainer()
        {
            Logger.Log("Initializing Trainer (0x00)...");
            bool success = await _bluetoothService.SendInitCommand();
            if (success)
            {
                Logger.Log("Trainer Initialized.");
            }
            else
            {
                Logger.Log("Trainer Init failed (or not connected).");
            }
        }

        private void OnPowerReceived(int watts)
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtPower != null) TxtPower.Text = watts.ToString();
            });
        }

        private void OnConnectionLost()
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.Text = "Status: Device Disconnected.";
                TxtStatus.Content = "DISCONNECTED";
                TxtStatus.Background = Brushes.Red;
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = false;
                _workoutTimer.Stop();
                PowerManagement.AllowSleep();
                Disconnected?.Invoke();
            });
        }

        private void UpdateInterval()
        {
            _workoutTimer.Interval = TimeSpan.FromSeconds(_intervalSeconds);
            if (TxtInterval != null)
            {
                TxtInterval.Text = $"Interval: {_intervalSeconds}s";
            }
        }

        private void BtnIncreaseInterval_Click(object sender, RoutedEventArgs e)
        {
            _intervalSeconds += 10;
            UpdateInterval();
        }

        private void BtnDecreaseInterval_Click(object sender, RoutedEventArgs e)
        {
            if (_intervalSeconds > 10)
            {
                _intervalSeconds -= 10;
                UpdateInterval();
            }
        }

        private void MenuEnableLogging_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                Logger.IsEnabled = menuItem.IsChecked;
            }
        }

        private void WorkoutView_Unloaded(object sender, RoutedEventArgs e)
        {
            _bluetoothService.ConnectionLost -= OnConnectionLost;
            _bluetoothService.PowerReceived -= OnPowerReceived;
            _workoutTimer.Stop();
            PowerManagement.AllowSleep();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Start button clicked.");
            try
            {
                _stepIndex = 0;
                _workoutTimer.Start();
                PowerManagement.PreventSleep();
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
                
                // Trigger immediately
                WorkoutTimer_Tick(this, EventArgs.Empty); 
                
                TxtLog.Text = "Status: Workout Started";
                TxtStatus.Content = "WORKOUT ACTIVE";
                TxtStatus.Background = Brushes.Orange;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error starting workout: {ex}");
                TxtLog.Text = $"Error: {ex.Message}";
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Stop button clicked.");
            _workoutTimer.Stop();
            PowerManagement.AllowSleep();
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtLog.Text = "Status: Workout Stopped";
            TxtStatus.Content = "CONNECTED";
            TxtStatus.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); 
        }

        private void WorkoutTimer_Tick(object? sender, EventArgs e)
        {
            if (!_bluetoothService.IsConnected) return;

            try
            {
                double min = SliderMin.Value / 100.0;
                double max = SliderMax.Value / 100.0;

                WorkoutMode mode = WorkoutMode.Random;
                if (ComboWorkoutMode.SelectedIndex == 1) mode = WorkoutMode.Hilly;
                if (ComboWorkoutMode.SelectedIndex == 2) mode = WorkoutMode.Mountain;

                double resistance = _logic.CalculateResistance(mode, min, max, _stepIndex);
                _stepIndex++;
                
                // Update UI immediately
                TxtCurrentResistance.Text = $"{(resistance * 100):F0}%";
                ResistanceGauge.Value = resistance * 100;
                
                // Update Color
                double range = max - min;
                double ratio = range > 0 ? (resistance - min) / range : 0;
                ratio = Math.Clamp(ratio, 0, 1);
                byte r = 0;
                byte g = 0;
                if (ratio < 0.5) { r = (byte)(ratio * 2 * 255); g = 255; }
                else { r = 255; g = (byte)((1 - ratio) * 2 * 255); }
                TxtCurrentResistance.Foreground = new SolidColorBrush(Color.FromRgb(r, g, 0));

                // Queue the resistance command via Service
                _bluetoothService.QueueResistance(resistance);
                Logger.Log($"Queued resistance: {resistance:F2}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Exception in WorkoutTimer_Tick: {ex}");
                TxtLog.Text = $"Error: {ex.Message}";
            }
        }
    }
}
