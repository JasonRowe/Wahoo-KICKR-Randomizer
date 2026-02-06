using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BikeFitnessApp.Models;
using BikeFitnessApp.MVVM;
using BikeFitnessApp.Services;
using Microsoft.Win32;

namespace BikeFitnessApp.ViewModels
{
    public class WorkoutViewModel : ObservableObject, IDisposable
    {
        private readonly IBluetoothService _bluetoothService;
        private readonly KickrLogic _logic = new KickrLogic();
        private readonly DispatcherTimer _workoutTimer;
        private readonly DispatcherTimer _dataTimer;

        private int _stepIndex = 0;
        private int _intervalSeconds = 30;
        private int _power;
        private double _resistancePercent;
        private bool _isWorkoutActive;
        private string _status = "CONNECTED";
        private WorkoutMode _selectedMode = WorkoutMode.Random;
        private double _minResistance = 0; // 0% Grade
        private double _maxResistance = 5; // 5% Grade
        private Brush _resistanceBrush = Brushes.White;
        private string _log = "Ready to ride.";

        private List<WorkoutDataPoint> _sessionData = new List<WorkoutDataPoint>();
        private DateTime _sessionStartTime;
        
        // Simulation Mode (Default to Grade)
        private bool _isGradeMode = true;
        private string _minLabel = "MIN GRADE (%)";
        private string _maxLabel = "MAX GRADE (%)";

        // Simulation Properties for Canvas
        private double _currentSpeedKph;
        private double _currentGradePercent;
        private double _currentDistanceMeters;

        public double CurrentSpeedKph
        {
            get => _currentSpeedKph;
            set => SetProperty(ref _currentSpeedKph, value);
        }

        public double CurrentGradePercent
        {
            get => _currentGradePercent;
            set => SetProperty(ref _currentGradePercent, value);
        }

        public double CurrentDistanceMeters
        {
            get => _currentDistanceMeters;
            set => SetProperty(ref _currentDistanceMeters, value);
        }

        public bool IsGradeMode
        {
            get => _isGradeMode;
            set => SetProperty(ref _isGradeMode, value);
        }

        public string MinLabel
        {
            get => _minLabel;
            set => SetProperty(ref _minLabel, value);
        }

        public string MaxLabel
        {
            get => _maxLabel;
            set => SetProperty(ref _maxLabel, value);
        }

        // ... Existing Speed/Distance Properties ...
        private string _speedText = "--";
        private string _distanceText = "0.00";
        private string _speedLabel = "MPH";
        private string _distanceLabel = "Miles";
        private TireSize _selectedTireSize;

        public System.Collections.Generic.List<TireSize> TireSizes => AppSettings.StandardTireSizes;

        public TireSize SelectedTireSize
        {
            get => _selectedTireSize;
            set
            {
                if (SetProperty(ref _selectedTireSize, value))
                {
                    AppSettings.WheelCircumference = value.Circumference;
                }
            }
        }

        public bool IsMetric
        {
            get => AppSettings.UseMetric;
            set
            {
                if (AppSettings.UseMetric != value)
                {
                    AppSettings.UseMetric = value;
                    OnPropertyChanged();
                    UpdateUnitLabels();
                }
            }
        }

        public int Power
        {
            get => _power;
            set
            {
                if (SetProperty(ref _power, value))
                {
                    OnPropertyChanged(nameof(PowerText));
                }
            }
        }

        public string PowerText => $"{Power} W";

        public string SpeedText
        {
            get => _speedText;
            set => SetProperty(ref _speedText, value);
        }

        public string DistanceText
        {
            get => _distanceText;
            set => SetProperty(ref _distanceText, value);
        }

        public string SpeedLabel
        {
            get => _speedLabel;
            set => SetProperty(ref _speedLabel, value);
        }

        public string DistanceLabel
        {
            get => _distanceLabel;
            set => SetProperty(ref _distanceLabel, value);
        }

        public double ResistancePercent
        {
            get => _resistancePercent;
            set
            {
                // Prevent -0.0 display in UI
                double snappedValue = value;
                if (snappedValue < 0 && Math.Round(snappedValue, 1) == 0)
                {
                    snappedValue = 0;
                }

                if (SetProperty(ref _resistancePercent, snappedValue))
                {
                    CurrentGradePercent = snappedValue;
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
        public ICommand SelectTireSizeCommand { get; }

        public event Action? Disconnected;

        public WorkoutViewModel(IBluetoothService bluetoothService)
        {
            _bluetoothService = bluetoothService;
            _bluetoothService.ConnectionLost += OnConnectionLost;
            _bluetoothService.PowerReceived += OnPowerReceived;
            _bluetoothService.SpeedValuesUpdated += OnSpeedValuesUpdated;

            _workoutTimer = new DispatcherTimer();
            _workoutTimer.Interval = TimeSpan.FromSeconds(_intervalSeconds);
            _workoutTimer.Tick += WorkoutTimer_Tick;

            _dataTimer = new DispatcherTimer();
            _dataTimer.Interval = TimeSpan.FromSeconds(1);
            _dataTimer.Tick += DataTimer_Tick;

            StartCommand = new RelayCommand(_ => StartWorkout(), _ => CanStart);
            StopCommand = new RelayCommand(_ => StopWorkout(), _ => CanStop);
            IncreaseIntervalCommand = new RelayCommand(_ => IntervalSeconds += 10);
            DecreaseIntervalCommand = new RelayCommand(_ => { if (IntervalSeconds > 10) IntervalSeconds -= 10; });
            SelectTireSizeCommand = new RelayCommand(param => SelectedTireSize = (TireSize)param!);

            // Initialize Settings
            _selectedTireSize = AppSettings.StandardTireSizes.Find(t => Math.Abs(t.Circumference - AppSettings.WheelCircumference) < 0.01) 
                                ?? AppSettings.StandardTireSizes[0];
            OnPropertyChanged(nameof(SelectedTireSize));

            _ = InitializeTrainer();
            UpdateUnitLabels();
            UpdateModeLabels();
        }

        public void Cleanup()
        {
            Dispose();
        }

        public void Dispose()
        {
            _bluetoothService.ConnectionLost -= OnConnectionLost;
            _bluetoothService.PowerReceived -= OnPowerReceived;
            _bluetoothService.SpeedValuesUpdated -= OnSpeedValuesUpdated;
            _workoutTimer.Stop();
            _dataTimer.Stop();
            PowerManagement.AllowSleep();
            GC.SuppressFinalize(this);
        }

        private void UpdateModeLabels()
        {
            if (IsGradeMode)
            {
                MinLabel = "Min Grade (%)";
                MaxLabel = "Max Grade (%)";
            }
            else
            {
                MinLabel = "Min Resistance (%)";
                MaxLabel = "Max Resistance (%)";
            }
        }

        private void UpdateUnitLabels()
        {
            if (AppSettings.UseMetric)
            {
                SpeedLabel = "KPH";
                DistanceLabel = "KM";
            }
            else
            {
                SpeedLabel = "MPH";
                DistanceLabel = "Miles";
            }
        }

        private void OnSpeedValuesUpdated(double kph, double meters)
        {
            // Update Canvas Properties
            CurrentSpeedKph = kph;
            CurrentDistanceMeters = meters;

            // Ensure labels match current setting
            UpdateUnitLabels();

            if (AppSettings.UseMetric)
            {
                SpeedText = $"{kph:F1}";
                DistanceText = $"{(meters / 1000.0):F2}";
            }
            else
            {
                // Convert to MPH and Miles
                double mph = kph * 0.621371;
                double miles = (meters / 1000.0) * 0.621371;
                
                SpeedText = $"{mph:F1}";
                DistanceText = $"{miles:F2}";
            }
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
            bool wasActive = IsWorkoutActive;
            IsWorkoutActive = false;
            _workoutTimer.Stop();
            _dataTimer.Stop();
            PowerManagement.AllowSleep();
            Status = "DISCONNECTED";
            Log = "Status: Device Disconnected.";
            Disconnected?.Invoke();

            if (wasActive)
            {
                PromptSaveReport();
            }
        }

        private void StartWorkout()
        {
            _stepIndex = 0;
            _sessionData.Clear();
            _sessionStartTime = DateTime.Now;
            IsWorkoutActive = true;
            _workoutTimer.Start();
            _dataTimer.Start();
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
            _dataTimer.Stop();
            PowerManagement.AllowSleep();
            Status = "CONNECTED";
            Log = "Status: Workout Stopped";

            PromptSaveReport();
        }

        private void DataTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsWorkoutActive) return;

            var elapsed = (int)(DateTime.Now - _sessionStartTime).TotalSeconds;
            _sessionData.Add(new WorkoutDataPoint
            {
                ElapsedSeconds = elapsed,
                Power = Power,
                SpeedKph = CurrentSpeedKph,
                DistanceMeters = CurrentDistanceMeters,
                GradePercent = CurrentGradePercent,
                HeartRate = null // TODO: Add HRM support later
            });
        }

        private void PromptSaveReport()
        {
            if (_sessionData.Count == 0) return;

            var result = System.Windows.MessageBox.Show("Workout complete! Would you like to save the report?", "Save Report", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                SaveReport();
            }
        }

        private void WorkoutTimer_Tick(object? sender, EventArgs e)
        {
            if (!_bluetoothService.IsConnected) return;

            // Always treat Min/Max as Grade % (-10 to 20)
            double min = MinResistance;
            double max = MaxResistance;
            
            // Logic calculates intermediate grade based on waveform
            double targetGrade = _logic.CalculateResistance(SelectedMode, min, max, _stepIndex);
            
            // Convert that "Grade" to Resistance (0.0 - 1.0)
            double resistanceFactor = _logic.CalculateResistanceFromGrade(targetGrade);
            
            ResistancePercent = targetGrade; 
            _bluetoothService.QueueResistance(resistanceFactor);

            _stepIndex++;
        }

        private void SaveReport()
        {
            if (_sessionData.Count == 0) return;

            var summary = new WorkoutSummary
            {
                Date = _sessionStartTime,
                DurationSeconds = _sessionData.Last().ElapsedSeconds,
                TotalDistanceMeters = _sessionData.Last().DistanceMeters,
                AvgPower = _sessionData.Average(d => d.Power),
                MaxPower = _sessionData.Max(d => d.Power),
                WorkoutMode = SelectedMode.ToString()
            };

            var report = new WorkoutReport
            {
                Summary = summary,
                DataPoints = _sessionData
            };

            var timestamp = _sessionStartTime.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"Workout_{timestamp}";

            var sfd = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = "JSON Report (*.json)|*.json|CSV Data (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Save Workout Report"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string content = "";
                    if (sfd.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        content = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                    }
                    else if (sfd.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        content = GenerateCsv(report);
                    }
                    else
                    {
                        // Default to JSON if unknown extension
                        content = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                    }

                    System.IO.File.WriteAllText(sfd.FileName, content);
                    Log = $"Status: Report saved to {System.IO.Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save report: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private string GenerateCsv(WorkoutReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,Duration (s),Total Distance (m),Avg Power (W),Max Power (W),Mode");
            sb.AppendLine($"{report.Summary.Date},{report.Summary.DurationSeconds},{report.Summary.TotalDistanceMeters:F2},{report.Summary.AvgPower:F1},{report.Summary.MaxPower},{report.Summary.WorkoutMode}");
            sb.AppendLine();
            sb.AppendLine("Elapsed (s),Power (W),Speed (KPH),Distance (m),Grade (%),Heart Rate");
            foreach (var dp in report.DataPoints)
            {
                sb.AppendLine($"{dp.ElapsedSeconds},{dp.Power},{dp.SpeedKph:F2},{dp.DistanceMeters:F2},{dp.GradePercent:F1},{dp.HeartRate}");
            }
            return sb.ToString();
        }

        private void UpdateResistanceBrush()
        {
            // Range: -10 to 20
            const double MinG = -10.0;
            const double MaxG = 20.0;
            double res = ResistancePercent;

            double range = MaxG - MinG;
            double ratio = (res - MinG) / range;
            ratio = Math.Clamp(ratio, 0, 1);
            
            byte r = 0;
            byte g = 0;
            if (ratio < 0.5) { r = (byte)(ratio * 2 * 255); g = 255; }
            else { r = 255; g = (byte)((1 - ratio) * 2 * 255); }
            
            ResistanceBrush = new SolidColorBrush(Color.FromRgb(r, g, 0));
        }
    }
}

