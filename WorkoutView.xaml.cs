using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BikeFitnessApp
{
    public partial class WorkoutView : UserControl
    {
        private BluetoothLEDevice _device;
        private GattCharacteristic _controlPoint;
        private DispatcherTimer _workoutTimer;
        private KickrLogic _logic = new KickrLogic();

        public event Action? Disconnected;

        public WorkoutView(BluetoothLEDevice device, GattCharacteristic controlPoint)
        {
            InitializeComponent();
            _device = device;
            _controlPoint = controlPoint;
            _device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
            this.Unloaded += WorkoutView_Unloaded;

            // Setup Timer for random changes
            _workoutTimer = new DispatcherTimer();
            _workoutTimer.Interval = TimeSpan.FromSeconds(30); 
            _workoutTimer.Tick += WorkoutTimer_Tick;

            // Initial command to take ownership
            _ = SendCommand(0x00, (byte?)null);
        }

        private void WorkoutView_Unloaded(object sender, RoutedEventArgs e)
        {
            _workoutTimer.Stop();
            PowerManagement.AllowSleep();
        }

        private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            Dispatcher.Invoke(() =>
            {
                Logger.Log($"Device Connection Status Changed: {sender.ConnectionStatus}");
                if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                {
                    TxtLog.Text = "Status: Device Disconnected.";
                    TxtStatus.Content = "DISCONNECTED";
                    TxtStatus.Background = Brushes.Red;
                    BtnStart.IsEnabled = false;
                    BtnStop.IsEnabled = false;
                    _workoutTimer.Stop();
                    PowerManagement.AllowSleep();
                    Disconnected?.Invoke();
                }
            });
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Start button clicked.");
            try
            {
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
            TxtStatus.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // #4CAF50
        }

        private async void WorkoutTimer_Tick(object? sender, EventArgs e)
        {
            if (_controlPoint == null)
            {
                Logger.Log("WorkoutTimer_Tick called but _controlPoint is null.");
                return;
            }

            try
            {
                double min = SliderMin.Value / 100.0;
                double max = SliderMax.Value / 100.0;
                double resistance = _logic.CalculateResistance(min, max);
                
                Logger.Log($"Calculated resistance: {resistance}");

                // Create Wahoo Command (OpCode 0x42)
                byte[] commandBytes = _logic.CreateWahooResistanceCommand(resistance);
                var writer = new DataWriter();
                writer.WriteBytes(commandBytes);
                await WriteCharacteristicWithRetry(writer.DetachBuffer());

                TxtCurrentResistance.Text = $"{(resistance * 100):F0}%";
                ResistanceGauge.Value = resistance * 100;

                // Color coding: Green (Low) -> Yellow -> Red (High) relative to the range
                double range = max - min;
                double ratio = range > 0 ? (resistance - min) / range : 0;
                ratio = Math.Clamp(ratio, 0, 1);

                byte r = 0;
                byte g = 0;
                if (ratio < 0.5)
                {
                    r = (byte)(ratio * 2 * 255);
                    g = 255;
                }
                else
                {
                    r = 255;
                    g = (byte)((1 - ratio) * 2 * 255);
                }
                TxtCurrentResistance.Foreground = new SolidColorBrush(Color.FromRgb(r, g, 0));

                Logger.Log("Successfully sent resistance command.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Exception in WorkoutTimer_Tick: {ex}");
                TxtLog.Text = $"Error: {ex.Message}";
            }
        }

        private async Task SendCommand(byte opCode, byte? parameter = null)
        {
            if (_device == null || _device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                return;
            }

            byte[] bytes = parameter.HasValue 
                ? _logic.CreateCommandBytes(opCode, parameter.Value) 
                : _logic.CreateCommandBytes(opCode);

            var writer = new DataWriter();
            writer.WriteBytes(bytes);

            if (_controlPoint != null)
            {
                await WriteCharacteristicWithRetry(writer.DetachBuffer());
            }
        }

        private async Task WriteCharacteristicWithRetry(IBuffer buffer)
        {
            const int MaxRetries = 5;
            int delay = 250;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    var status = await _controlPoint.WriteValueAsync(buffer);
                    if (status == GattCommunicationStatus.Success)
                    {
                        return;
                    }
                    Logger.Log($"Write failed with status: {status}. Retry {i + 1}/{MaxRetries}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Write exception: {ex}. Retry {i + 1}/{MaxRetries}");
                    if (i == MaxRetries - 1) throw; 
                }
                await Task.Delay(delay);
                delay *= 2; 
            }
        }
    }
}