using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BikeFitness.Shared.Models;
using BikeFitness.Shared.Services;

namespace BikeFitness.Shared.ViewModels
{
    public partial class WorkoutViewModel : ObservableObject, IDisposable
    {
        private readonly IBluetoothService _bluetoothService;
        private readonly IStravaService _stravaService;
        private readonly IUserInterfaceService _uiService;
        private readonly KickrLogic _logic = new KickrLogic();
        private readonly System.Timers.Timer _workoutTimer;
        private readonly System.Timers.Timer _dataTimer;

        private int _stepIndex = 0;
        
        [ObservableProperty]
        private int _intervalSeconds = 30;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PowerText))]
        private int _power;

        [ObservableProperty]
        private double _resistancePercent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStart))]
        [NotifyPropertyChangedFor(nameof(CanStop))]
        private bool _isWorkoutActive;

        [ObservableProperty]
        private bool _showPostWorkoutOptions;

        [ObservableProperty]
        private bool _hasWorkoutData;

        [ObservableProperty]
        private string _status = "CONNECTED";

        [ObservableProperty]
        private WorkoutMode _selectedMode = WorkoutMode.Random;

        public int SelectedModeIndex
        {
            get => (int)SelectedMode;
            set => SelectedMode = (WorkoutMode)value;
        }

        partial void OnSelectedModeChanged(WorkoutMode value)
        {
            OnPropertyChanged(nameof(SelectedModeIndex));
        }

        [ObservableProperty]
        private double _minResistance = 0; // 0% Grade

        [ObservableProperty]
        private double _maxResistance = 5; // 5% Grade

        [ObservableProperty]
        private SharedColor _resistanceColor = SharedColor.White;

        [ObservableProperty]
        private string _log = "Ready to ride.";

        private List<WorkoutDataPoint> _sessionData = new List<WorkoutDataPoint>();
        private DateTime _sessionStartTime;
        
        [ObservableProperty]
        private bool _isGradeMode = true;

        [ObservableProperty]
        private string _minLabel = "MIN GRADE (%)";

        [ObservableProperty]
        private string _maxLabel = "MAX GRADE (%)";

        [ObservableProperty]
        private double _currentSpeedKph;

        [ObservableProperty]
        private double _currentGradePercent;

        [ObservableProperty]
        private double _currentDistanceMeters;

        public string PowerText => $"{Power} W";

        [ObservableProperty]
        private string _speedText = "--";

        [ObservableProperty]
        private string _distanceText = "0.00";

        [ObservableProperty]
        private string _speedLabel = "MPH";

        [ObservableProperty]
        private string _distanceLabel = "Miles";

        [ObservableProperty]
        private TireSize _selectedTireSize;

        public List<TireSize> TireSizes => AppSettings.StandardTireSizes;

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
                    OnSpeedValuesUpdated(CurrentSpeedKph, CurrentDistanceMeters);
                }
            }
        }

        public string IntervalText => $"Interval: {IntervalSeconds}s";

        public bool CanStart => !IsWorkoutActive;
        public bool CanStop => IsWorkoutActive;

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

        public IRelayCommand StartCommand { get; }
        public IRelayCommand StopCommand { get; }
        public IRelayCommand DismissPostWorkoutCommand { get; }
        public IRelayCommand IncreaseIntervalCommand { get; }
        public IRelayCommand DecreaseIntervalCommand { get; }
        public IRelayCommand<TireSize> SelectTireSizeCommand { get; }
        public IAsyncRelayCommand SaveFitCommand { get; }
        public IAsyncRelayCommand UploadStravaCommand { get; }

        public event Action? Disconnected;

        public WorkoutViewModel(IBluetoothService bluetoothService, IStravaService stravaService, IUserInterfaceService uiService)
        {
            _bluetoothService = bluetoothService;
            _stravaService = stravaService;
            _uiService = uiService;
            
            _bluetoothService.ConnectionLost += OnConnectionLost;
            _bluetoothService.PowerReceived += OnPowerReceived;
            _bluetoothService.SpeedValuesUpdated += OnSpeedValuesUpdated;

            _workoutTimer = new System.Timers.Timer();
            _workoutTimer.Interval = _intervalSeconds * 1000;
            _workoutTimer.Elapsed += (s, e) => WorkoutTimer_Tick();

            _dataTimer = new System.Timers.Timer();
            _dataTimer.Interval = 1000;
            _dataTimer.Elapsed += (s, e) => DataTimer_Tick();

            StartCommand = new RelayCommand(StartWorkout, () => CanStart);
            StopCommand = new RelayCommand(StopWorkout, () => CanStop);
            DismissPostWorkoutCommand = new RelayCommand(() => ShowPostWorkoutOptions = false);
            IncreaseIntervalCommand = new RelayCommand(() => IntervalSeconds += 10);
            DecreaseIntervalCommand = new RelayCommand(() => { if (IntervalSeconds > 10) IntervalSeconds -= 10; });
            SelectTireSizeCommand = new RelayCommand<TireSize>(tire => { if (tire != null) SelectedTireSize = tire; });
            SaveFitCommand = new AsyncRelayCommand(SaveReport);
            UploadStravaCommand = new AsyncRelayCommand(ManualStravaUpload);

            _selectedTireSize = AppSettings.StandardTireSizes.Find(t => Math.Abs(t.Circumference - AppSettings.WheelCircumference) < 0.01) 
                                ?? AppSettings.StandardTireSizes[0];
            OnPropertyChanged(nameof(SelectedTireSize));

            _ = InitializeTrainer();
            UpdateUnitLabels();
            UpdateModeLabels();
        }

        public void Dispose()
        {
            _bluetoothService.ConnectionLost -= OnConnectionLost;
            _bluetoothService.PowerReceived -= OnPowerReceived;
            _bluetoothService.SpeedValuesUpdated -= OnSpeedValuesUpdated;
            _workoutTimer.Stop();
            _workoutTimer.Dispose();
            _dataTimer.Stop();
            _dataTimer.Dispose();
            
            // We'll let the UI handle PowerManagement or add a service for it.
            System.GC.SuppressFinalize(this);
        }

        partial void OnResistancePercentChanged(double value)
        {
            CurrentGradePercent = value;
            UpdateResistanceColor();
        }

        partial void OnIntervalSecondsChanged(int value)
        {
            _workoutTimer.Interval = value * 1000;
            OnPropertyChanged(nameof(IntervalText));
        }

        partial void OnSelectedTireSizeChanged(TireSize value)
        {
            AppSettings.WheelCircumference = value.Circumference;
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
            CurrentSpeedKph = kph;
            CurrentDistanceMeters = meters;

            _uiService.InvokeOnUIThread(() => {
                if (AppSettings.UseMetric)
                {
                    SpeedText = $"{kph:F1}";
                    DistanceText = $"{(meters / 1000.0):F2}";
                }
                else
                {
                    double mph = kph * 0.621371;
                    double miles = (meters / 1000.0) * 0.621371;
                    SpeedText = $"{mph:F1}";
                    DistanceText = $"{miles:F2}";
                }
            });
        }

        private async Task InitializeTrainer()
        {
            await _bluetoothService.SendInitCommand();
        }

        private void OnPowerReceived(int watts)
        {
            _uiService.InvokeOnUIThread(() => Power = watts);
        }

        private void OnConnectionLost()
        {
            _uiService.InvokeOnUIThread(() => {
                IsWorkoutActive = false;
                _workoutTimer.Stop();
                _dataTimer.Stop();
                Status = "DISCONNECTED";
                Log = "Status: Device Disconnected.";
                Disconnected?.Invoke();

                if (_sessionData.Count > 0)
                {
                    HasWorkoutData = true;
                    ShowPostWorkoutOptions = true;
                }
            });
        }

        private void StartWorkout()
        {
            _stepIndex = 0;
            _sessionData.Clear();
            _sessionStartTime = DateTime.Now;
            IsWorkoutActive = true;
            ShowPostWorkoutOptions = false;
            HasWorkoutData = false;
            _workoutTimer.Start();
            _dataTimer.Start();
            Status = "WORKOUT ACTIVE";
            Log = "Status: Workout Started";
            
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            
            WorkoutTimer_Tick();
        }

        private void StopWorkout()
        {
            IsWorkoutActive = false;
            _workoutTimer.Stop();
            _dataTimer.Stop();
            Status = "CONNECTED";
            Log = "Status: Workout Stopped";

            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();

            if (_sessionData.Count > 0)
            {
                HasWorkoutData = true;
                ShowPostWorkoutOptions = true;
            }
        }

        private void DataTimer_Tick()
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
                HeartRate = null 
            });
        }

        private async Task ManualStravaUpload()
        {
            if (_sessionData.Count == 0) return;

            var tempPath = Path.Combine(Path.GetTempPath(), $"StravaUpload_{DateTime.Now:yyyyMMdd_HHmmss}.fit");
            
            try
            {
                var report = CreateReport();
                FitExportService.ExportToFit(report, tempPath);
                await HandleStravaUpload(tempPath);
            }
            catch (Exception ex)
            {
                await _uiService.ShowMessageAsync("Upload Failed", $"Failed to prepare Strava upload: {ex.Message}");
            }
        }

        private void WorkoutTimer_Tick()
        {
            if (!_bluetoothService.IsConnected) return;

            double min = MinResistance;
            double max = MaxResistance;
            double targetGrade = _logic.CalculateResistance(SelectedMode, min, max, _stepIndex);
            double resistanceFactor = _logic.CalculateResistanceFromGrade(targetGrade);
            
            _uiService.InvokeOnUIThread(() => ResistancePercent = targetGrade);
            _bluetoothService.QueueResistance(resistanceFactor);

            _stepIndex++;
        }

        private WorkoutReport CreateReport()
        {
            var summary = new WorkoutSummary
            {
                Date = _sessionStartTime,
                DurationSeconds = _sessionData.Count > 0 ? _sessionData.Last().ElapsedSeconds : 0,
                TotalDistanceMeters = _sessionData.Count > 0 ? _sessionData.Last().DistanceMeters : 0,
                AvgPower = _sessionData.Count > 0 ? _sessionData.Average(d => d.Power) : 0,
                MaxPower = _sessionData.Count > 0 ? _sessionData.Max(d => d.Power) : 0,
                WorkoutMode = SelectedMode.ToString()
            };

            return new WorkoutReport
            {
                Summary = summary,
                DataPoints = _sessionData
            };
        }

        private async Task SaveReport()
        {
            if (_sessionData.Count == 0) return;

            var report = CreateReport();
            var timestamp = _sessionStartTime.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"Workout_{timestamp}";

            var filePath = await _uiService.SaveFileDialogAsync(
                "Save Workout Report",
                defaultFileName,
                "FIT Activity (*.fit)|*.fit|JSON Report (*.json)|*.json|CSV Data (*.csv)|*.csv|All Files (*.*)|*.*");

            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    if (filePath.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
                    {
                        FitExportService.ExportToFit(report, filePath);
                    }
                    else if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        var content = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(filePath, content);
                    }
                    else if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        var content = GenerateCsv(report);
                        File.WriteAllText(filePath, content);
                    }
                    
                    Log = $"Status: Report saved to {Path.GetFileName(filePath)}";
                    ShowPostWorkoutOptions = false;
                }
                catch (Exception ex)
                {
                    await _uiService.ShowMessageAsync("Error", $"Failed to save report: {ex.Message}");
                }
            }
        }

        private async Task HandleStravaUpload(string filePath)
        {
            try
            {
                if (!_stravaService.IsAuthorized)
                {
                    bool auth = await _uiService.ShowConfirmAsync("Connect Strava", "Strava is not connected. Connect now?");
                    if (auth)
                    {
                        bool success = await _stravaService.AuthorizeAsync();
                        if (!success)
                        {
                            await _uiService.ShowMessageAsync("Auth Error", "Failed to authorize with Strava.");
                            return;
                        }
                    }
                    else return;
                }

                Status = "UPLOADING...";
                Log = "Status: Uploading to Strava...";
                bool uploaded = await _stravaService.UploadActivityAsync(filePath, $"Indoor Ride - {SelectedMode}");
                
                if (uploaded)
                {
                    await _uiService.ShowMessageAsync("Success", "Activity uploaded successfully to Strava!");
                    Log = "Status: Strava Upload Complete";
                    ShowPostWorkoutOptions = false;
                }
                else
                {
                    await _uiService.ShowMessageAsync("Upload Failed", "Failed to upload to Strava. Check your connection or API configuration.");
                    Log = "Status: Strava Upload Failed";
                }
                Status = "CONNECTED";
            }
            catch (Exception ex)
            {
                await _uiService.ShowMessageAsync("Strava Error", $"Strava error: {ex.Message}");
                Status = "CONNECTED";
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

        private void UpdateResistanceColor()
        {
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
            
            ResistanceColor = new SharedColor(r, g, 0);
        }
    }
}
