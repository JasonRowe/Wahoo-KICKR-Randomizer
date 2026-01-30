using System;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BikeFitnessApp.MVVM;
using BikeFitnessApp.Services;

namespace BikeFitnessApp.ViewModels
{
    public class WorkoutViewModel : ObservableObject
    {
        private readonly IBluetoothService _bluetoothService;
        private readonly KickrLogic _logic = new KickrLogic();
        private readonly DispatcherTimer _workoutTimer;

        private int _stepIndex = 0;
        private int _intervalSeconds = 30;
        private int _power;
        private double _resistancePercent;
        private bool _isWorkoutActive;
        private string _status = "CONNECTED";
        private WorkoutMode _selectedMode = WorkoutMode.Random;
        private double _minResistance = 0;
        private double _maxResistance = 7;
        private Brush _resistanceBrush = Brushes.White;
        private string _log = "Ready to ride.";

        public int Power
        {
            get => _power;
            set => SetProperty(ref _power, value);
        }

        public double ResistancePercent
        {
            get => _resistancePercent;
            set
            {
                if (SetProperty(ref _resistancePercent, value))
                {
                    UpdateResistanceBrush();
                }
            }
        }

        public Brush ResistanceBrush
        {
            get => _resistanceBrush;
            set => SetProperty(ref _resistanceBrush, value);
        }

        public string Log
        {
            get => _log;
            set => SetProperty(ref _log, value);
        }

        public int IntervalSeconds
        {
            get => _intervalSeconds;
            set
            {
                if (SetProperty(ref _intervalSeconds, value))
                {
                    _workoutTimer.Interval = TimeSpan.FromSeconds(_intervalSeconds);
                    OnPropertyChanged(nameof(IntervalText));
                }
            }
        }

        public string IntervalText => $"Interval: {IntervalSeconds}s";

        public bool IsWorkoutActive
        {
            get => _isWorkoutActive;
            set
            {
                if (SetProperty(ref _isWorkoutActive, value))
                {
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanStop));
                }
            }
        }

        public bool CanStart => !IsWorkoutActive;
        public bool CanStop => IsWorkoutActive;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsLoggingEnabled
        {
            get => Logger.IsEnabled;
            set
            {
                if (Logger.IsEnabled != value)
                {
                    Logger.IsEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public WorkoutMode SelectedMode
        {
            get => _selectedMode;
            set => SetProperty(ref _selectedMode, value);
        }

        public int SelectedModeIndex
        {
            get => (int)SelectedMode;
            set => SelectedMode = (WorkoutMode)value;
        }

        public double MinResistance
        {
            get => _minResistance;
            set => SetProperty(ref _minResistance, value);
        }

        public double MaxResistance
        {
            get => _maxResistance;
            set => SetProperty(ref _maxResistance, value);
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand IncreaseIntervalCommand { get; }
        public ICommand DecreaseIntervalCommand { get; }

        public event Action? Disconnected;

        public WorkoutViewModel(IBluetoothService bluetoothService)
        {
            _bluetoothService = bluetoothService;
            _bluetoothService.ConnectionLost += OnConnectionLost;
            _bluetoothService.PowerReceived += OnPowerReceived;

            _workoutTimer = new DispatcherTimer();
            _workoutTimer.Interval = TimeSpan.FromSeconds(_intervalSeconds);
            _workoutTimer.Tick += WorkoutTimer_Tick;

            StartCommand = new RelayCommand(_ => StartWorkout(), _ => CanStart);
            StopCommand = new RelayCommand(_ => StopWorkout(), _ => CanStop);
            IncreaseIntervalCommand = new RelayCommand(_ => IntervalSeconds += 10);
            DecreaseIntervalCommand = new RelayCommand(_ => { if (IntervalSeconds > 10) IntervalSeconds -= 10; });

            _ = InitializeTrainer();
        }

        public void Cleanup()
        {
            _bluetoothService.ConnectionLost -= OnConnectionLost;
            _bluetoothService.PowerReceived -= OnPowerReceived;
            _workoutTimer.Stop();
            PowerManagement.AllowSleep();
        }

        private async System.Threading.Tasks.Task InitializeTrainer()
        {
            await _bluetoothService.SendInitCommand();
        }

        private void OnPowerReceived(int watts)
        {
            Power = watts;
        }

        private void OnConnectionLost()
        {
            IsWorkoutActive = false;
            _workoutTimer.Stop();
            PowerManagement.AllowSleep();
            Status = "DISCONNECTED";
            Log = "Status: Device Disconnected.";
            Disconnected?.Invoke();
        }

        private void StartWorkout()
        {
            _stepIndex = 0;
            IsWorkoutActive = true;
            _workoutTimer.Start();
            PowerManagement.PreventSleep();
            Status = "WORKOUT ACTIVE";
            Log = "Status: Workout Started";
            
            // Trigger first step immediately
            WorkoutTimer_Tick(this, EventArgs.Empty);
        }

        private void StopWorkout()
        {
            IsWorkoutActive = false;
            _workoutTimer.Stop();
            PowerManagement.AllowSleep();
            Status = "CONNECTED";
            Log = "Status: Workout Stopped";
        }

        private void WorkoutTimer_Tick(object? sender, EventArgs e)
        {
            if (!_bluetoothService.IsConnected) return;

            double min = MinResistance / 100.0;
            double max = MaxResistance / 100.0;

            double resistance = _logic.CalculateResistance(SelectedMode, min, max, _stepIndex);
            _stepIndex++;

            ResistancePercent = resistance * 100;
            _bluetoothService.QueueResistance(resistance);
        }

        private void UpdateResistanceBrush()
        {
            double min = MinResistance / 100.0;
            double max = MaxResistance / 100.0;
            double res = ResistancePercent / 100.0;

            double range = max - min;
            double ratio = range > 0 ? (res - min) / range : 0;
            ratio = Math.Clamp(ratio, 0, 1);
            byte r = 0;
            byte g = 0;
            if (ratio < 0.5) { r = (byte)(ratio * 2 * 255); g = 255; }
            else { r = 255; g = (byte)((1 - ratio) * 2 * 255); }
            
            ResistanceBrush = new SolidColorBrush(Color.FromRgb(r, g, 0));
        }
    }
}

