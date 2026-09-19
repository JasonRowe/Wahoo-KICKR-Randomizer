using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;
using BikeFitnessApp;

namespace BikeFitness.Harness;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _autoDriveTimer;
    private readonly Stopwatch _autoDriveClock = new Stopwatch();

    // --- POC 1 (ghost racer) state. Harness-local: nothing here is persisted, and the only
    // --- shared-state mutation is pushing the ghost's position into the canvas DPs.
    private GhostReplay _ghost = new GhostReplay(RideProfile.Synthetic());
    private double _ghostOffsetMeters;
    private double _riderDistanceMeters;
    private bool _uiReady;
    private readonly Stopwatch _readoutClock = new Stopwatch();

    // --- Perf HUD (L4 layer): ring buffer of frame deltas, filled only while the HUD is on.
    private readonly double[] _frameDeltas = new double[600];
    private int _frameDeltaCount;
    private int _frameDeltaIndex;
    private double _perfWindowSeconds;
    private double _allocBytesPerFrame;
    private long _lastAllocationBytes;

    public MainWindow()
    {
        InitializeComponent();

        // Point the harness at the shipped sheet (Images/, same asset the two apps use).
        string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
        string sheetPath = PedalAnimation.GetDefaultSheetPath(imagesDir);
        SimCanvas.PedalSheetSource = File.Exists(sheetPath) ? sheetPath : string.Empty;

        _autoDriveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _autoDriveTimer.Tick += AutoDriveTick;

        // Drive the ghost from the canvas's own frame clock (not a parallel timer), so the rival cannot
        // jitter against the road.
        SimCanvas.FrameRendered += SimCanvas_FrameRendered;

        _uiReady = true;
        ApplyGhostProfile();
        SimCanvas.GhostOpacity = SliderGhostOpacity.Value / 100.0;
        SimCanvas.GhostShowMarker = ChkGhostMarker.IsChecked == true;
        SimCanvas.GhostShowHud = ChkGhostHud.IsChecked == true;
    }

    // --- Drive ---

    private void ChkAutoDrive_Changed(object sender, RoutedEventArgs e)
    {
        bool on = ChkAutoDrive.IsChecked == true;
        SliderSpeed.IsEnabled = !on;
        SliderGrade.IsEnabled = !on;

        if (on)
        {
            _autoDriveClock.Restart();
            _autoDriveTimer.Start();
        }
        else
        {
            _autoDriveTimer.Stop();
            _autoDriveClock.Stop();
        }
    }

    /// <summary>Auto-drive speed law: sinusoidal 5..40 kph (also the source of the "mirror" ghost profile).</summary>
    private static double GetAutoDriveSpeedKph(double t) => Math.Clamp(22.5 + (17.5 * Math.Sin(t * 0.6)), 0.0, 60.0);

    /// <summary>Auto-drive grade law: sinusoidal −5..+5 %.</summary>
    private static double GetAutoDriveGradePercent(double t) => Math.Clamp(5.0 * Math.Sin(t * 0.3), -10.0, 20.0);

    private void AutoDriveTick(object? sender, EventArgs e)
    {
        double t = _autoDriveClock.Elapsed.TotalSeconds;
        // Sinusoidal speed 5..40 kph and grade -5..5% for a representative ride.
        SliderSpeed.Value = GetAutoDriveSpeedKph(t);
        SliderGrade.Value = GetAutoDriveGradePercent(t);
    }

    // --- Ghost racer (POC 1) ---

    private void ChkGhost_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;

        bool on = ChkGhost.IsChecked == true;
        SimCanvas.GhostEnabled = on;

        if (on)
        {
            // Start the duel level with the rider, wherever they already are on the road.
            _ghost.Reset();
            AlignGhostToRider(0.0);
        }

        UpdateGhostReadouts(riderSpeedKph: SimCanvas.SpeedKph);
    }

    private void CmbGhostProfile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        ApplyGhostProfile();
    }

    private void SliderGhostEffort_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady) return;
        _ghost.EffortFactor = e.NewValue / 100.0;
    }

    private void SliderGhostOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady) return;
        SimCanvas.GhostOpacity = e.NewValue / 100.0;
    }

    private void ChkGhostMarker_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        SimCanvas.GhostShowMarker = ChkGhostMarker.IsChecked == true;
    }

    private void ChkGhostHud_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        SimCanvas.GhostShowHud = ChkGhostHud.IsChecked == true;
    }

    private void BtnGhostHeadStart_Click(object sender, RoutedEventArgs e)
    {
        // Give the rider a 5 s head start at their current speed (i.e. the ghost starts behind by 5 s).
        double headStartMeters = Math.Max(0.0, SimCanvas.SpeedKph / 3.6 * 5.0);
        AlignGhostToRider(-headStartMeters);
    }

    private void BtnGhostReset_Click(object sender, RoutedEventArgs e)
    {
        _ghost.Reset();
        AlignGhostToRider(0.0);
    }

    /// <summary>Builds the chosen profile and restarts the duel from the profile's beginning.</summary>
    private void ApplyGhostProfile()
    {
        RideProfile profile;
        bool labelHandledByLoader = false;

        switch (CmbGhostProfile.SelectedIndex)
        {
            case 1:
                profile = BuildMirrorProfile();
                break;
            case 2:
                RideProfile? loaded = LoadProfileFromFile();
                labelHandledByLoader = true;
                profile = loaded ?? _ghost.Profile;   // cancelled or unusable: keep the current profile
                break;
            default:
                profile = RideProfile.Synthetic();
                break;
        }

        if (!ReferenceEquals(profile, _ghost.Profile))
        {
            _ghost = new GhostReplay(profile);
        }
        else
        {
            _ghost.Reset();
        }

        _ghost.EffortFactor = SliderGhostEffort.Value / 100.0;
        AlignGhostToRider(0.0);

        if (!labelHandledByLoader) UpdateGhostProfileLabel();
    }

    /// <summary>
    /// A ghost profile that replays the harness's own auto-drive law, so at 100 % effort the ghost holds
    /// station with the rider and the effort slider is a clean "+/- % of my PB" control.
    /// </summary>
    private static RideProfile BuildMirrorProfile(int seconds = 1200)
    {
        var profile = new RideProfile
        {
            Label = "Mirror of harness auto-drive",
            Source = RideProfile.SourceHarnessLive,
        };

        double distanceMeters = 0;
        for (int t = 0; t <= seconds; t++)
        {
            double speed = GetAutoDriveSpeedKph(t);
            profile.Samples.Add(new RideProfileSample
            {
                T = t,
                DistanceMeters = distanceMeters,
                SpeedKph = speed,
                GradePercent = GetAutoDriveGradePercent(t),
                Power = 0,
            });

            distanceMeters += speed * 1000.0 / 3600.0;
        }

        return profile;
    }

    /// <summary>Read-only picker for a saved <c>Workout_*.json</c> report. Never modifies the file.</summary>
    private RideProfile? LoadProfileFromFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load a saved workout report (read-only)",
            Filter = "Workout report (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            TxtGhostProfile.Text = "Load cancelled — keeping the current profile.";
            return null;
        }

        RideProfile profile = RideProfile.Load(dialog.FileName);
        if (profile.IsEmpty)
        {
            TxtGhostProfile.Text = $"No usable samples in {Path.GetFileName(dialog.FileName)} — keeping the current profile.";
            return null;
        }

        TxtGhostProfile.Text = $"{Path.GetFileName(dialog.FileName)} · {profile.Samples.Count} samples · " +
                               $"{profile.TotalDistanceMeters / 1000.0:F2} km / {profile.DurationSeconds / 60.0:F1} min";
        return profile;
    }

    /// <summary>
    /// Offsets the ghost so the gap equals <paramref name="gapMeters"/> (negative = rider ahead) right now.
    /// Needed because the rider is already part-way down the road by the time the ghost starts: the spec's
    /// "reset to 0 m" would leave the ghost kilometres behind and permanently off screen.
    /// </summary>
    private void AlignGhostToRider(double gapMeters)
    {
        _ghostOffsetMeters = (_riderDistanceMeters + gapMeters) - _ghost.DistanceMeters;
        PushGhostState();
    }

    private void PushGhostState()
    {
        SimCanvas.GhostDistanceMeters = Math.Max(0.0, _ghost.DistanceMeters + _ghostOffsetMeters);
        SimCanvas.GhostSpeedKph = _ghost.SpeedKph;
    }

    private void SimCanvas_FrameRendered(object? sender, SimulationFrameEventArgs e)
    {
        _riderDistanceMeters = e.RiderDistanceMeters;

        bool ghostOn = ChkGhost.IsChecked == true;
        if (ghostOn)
        {
            _ghost.Advance(e.DeltaSeconds);
            PushGhostState();
        }

        UpdatePerfHud(e.DeltaSeconds, ghostOn);
        UpdateGhostReadouts(e.RiderSpeedKph);
    }

    /// <summary>Panel readouts, throttled to ~10 Hz so text layout never shows up in the frame budget.</summary>
    private void UpdateGhostReadouts(double riderSpeedKph)
    {
        if (_readoutClock.IsRunning && _readoutClock.Elapsed.TotalSeconds < 0.1) return;
        _readoutClock.Restart();

        if (ChkGhost.IsChecked != true)
        {
            TxtGhostGap.Text = string.Empty;
            TxtGhostDelta.Text = string.Empty;
            TxtGhostSpeed.Text = string.Empty;
            return;
        }

        double ghostDistance = Math.Max(0.0, _ghost.DistanceMeters + _ghostOffsetMeters);
        double gap = DuelMath.GapMeters(ghostDistance, _riderDistanceMeters);
        double delta = DuelMath.DeltaSeconds(ghostDistance, _riderDistanceMeters, riderSpeedKph, _ghost.SpeedKph);

        TxtGhostGap.Text = $"GAP {DuelMath.FormatGapMeters(gap)}";
        TxtGhostDelta.Text = $"\u0394 {DuelMath.FormatDelta(delta)}";
        TxtGhostSpeed.Text = $"ghost {_ghost.SpeedKph:F1} / you {riderSpeedKph:F1} kph · {_ghost.GradePercent:F1} %";
    }

    private void UpdateGhostProfileLabel()
    {
        TxtGhostProfile.Text = $"{_ghost.Profile.Label} · {_ghost.Profile.Samples.Count} samples · " +
                               $"{_ghost.Profile.TotalDistanceMeters / 1000.0:F2} km / {_ghost.Profile.DurationSeconds / 60.0:F1} min";
    }

    // --- Perf HUD (L4) ---

    private void ChkPerfHud_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;

        _frameDeltaCount = 0;
        _frameDeltaIndex = 0;
        _readoutClock.Restart();
        _lastAllocationBytes = 0;
        TxtPerf.Text = string.Empty;
    }

    private void UpdatePerfHud(double deltaSeconds, bool ghostOn)
    {
        if (ChkPerfHud.IsChecked != true) return;

        _frameDeltas[_frameDeltaIndex] = deltaSeconds;
        _frameDeltaIndex = (_frameDeltaIndex + 1) % _frameDeltas.Length;
        if (_frameDeltaCount < _frameDeltas.Length) _frameDeltaCount++;

        // Managed allocations on the render thread, smoothed — the POC's "no per-frame allocations" check.
        long allocationBytes = GC.GetAllocatedBytesForCurrentThread();
        if (_lastAllocationBytes > 0)
        {
            double perFrame = allocationBytes - _lastAllocationBytes;
            _allocBytesPerFrame = (_allocBytesPerFrame * 0.9) + (perFrame * 0.1);
        }

        _lastAllocationBytes = allocationBytes;

        _perfWindowSeconds += deltaSeconds;
        if (_perfWindowSeconds < 0.5) return;
        _perfWindowSeconds = 0;

        if (_frameDeltaCount == 0) return;

        var window = new double[_frameDeltaCount];
        Array.Copy(_frameDeltas, window, _frameDeltaCount);
        Array.Sort(window);

        double sum = 0;
        foreach (double d in window) sum += d;

        double averageSeconds = sum / window.Length;
        double fps = averageSeconds > 0 ? 1.0 / averageSeconds : 0;
        double p99Ms = window[Math.Min(window.Length - 1, (int)(window.Length * 0.99))] * 1000.0;

        TxtPerf.Text = $"fps {fps:F1} · p99 {p99Ms:F1} ms · riders {(ghostOn ? 2 : 1)} · alloc {_allocBytesPerFrame:F0} B/frame";
    }

    // --- Pedal controls ---

    private void SliderD_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SimCanvas.PedalMetersPerRevolution = SliderD.Value;
    }

    private void SliderFrame_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ChkManualFrame.IsChecked == true)
            SimCanvas.PedalFrameOverride = (int)SliderFrame.Value;
    }

    private void ChkManualFrame_Changed(object sender, RoutedEventArgs e)
    {
        SimCanvas.PedalFrameOverride = ChkManualFrame.IsChecked == true ? (int)SliderFrame.Value : -1;
    }

    private void StepFrame(int delta)
    {
        ChkManualFrame.IsChecked = true;
        int frame = ((int)SliderFrame.Value + delta) % 12;
        if (frame < 0) frame += 12;
        SliderFrame.Value = frame;
    }

    private void BtnFramePrev_Click(object sender, RoutedEventArgs e) => StepFrame(-1);

    private void BtnFrameNext_Click(object sender, RoutedEventArgs e) => StepFrame(1);

    private void CmbSeam_Changed(object sender, SelectionChangedEventArgs e)
    {
        SimCanvas.PedalSeamMode = CmbSeam.SelectedIndex switch
        {
            1 => SeamMode.CrossFade,
            2 => SeamMode.DropLast,
            _ => SeamMode.Straight,
        };
    }

    private void CmbBlend_Changed(object sender, SelectionChangedEventArgs e)
    {
        SimCanvas.PedalBlendAlpha = CmbBlend.SelectedIndex switch
        {
            1 => 0.20,
            2 => 0.30,
            _ => 0.0,
        };
    }

    private void CmbScale_Changed(object sender, SelectionChangedEventArgs e)
    {
        SimCanvas.PedalDrawSizePx = CmbScale.SelectedIndex == 1 ? 290.0 : 150.0;
    }

    private void ChkWheelSpin_Changed(object sender, RoutedEventArgs e)
    {
        SimCanvas.PedalWheelSpin = ChkWheelSpin.IsChecked == true;
    }

    private void ChkHubMarker_Changed(object sender, RoutedEventArgs e)
    {
        SimCanvas.PedalHubMarker = ChkHubMarker.IsChecked == true;
    }
}
