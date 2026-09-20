using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;
using BikeFitness.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BikeFitnessApp
{
    public partial class WorkoutView : UserControl, IDisposable
    {
        private readonly WorkoutViewModel _viewModel;

        // --- Pacer (POC 2 wiring). View-local and off by default: this feature renders a rival and moves
        // --- only that rival's virtual speed. It never touches resistance, the trainer, or session data.
        private readonly PacerModel _pacer = new PacerModel(new PacerConfig());
        private readonly Stopwatch _pacerReadoutClock = new Stopwatch();
        private double _riderDistanceMeters;

        public WorkoutView()
        {
            InitializeComponent();

            // Rider pedal animation: the 12-frame sheet ships under Images/ (see PedalAnimation).
            // Wheel-spin overlay is deliberately not enabled yet.
            SimCanvas.PedalSheetSource = PedalAnimation.GetDefaultSheetPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images"));

            _viewModel = App.Current.Services.GetRequiredService<WorkoutViewModel>();
            DataContext = _viewModel;

            _viewModel.Disconnected += OnDisconnected;

            // The pacer advances on the canvas's own frame clock — the same clock the scene integrates on —
            // so the rival cannot jitter against the road.
            SimCanvas.FrameRendered += OnFrameRendered;

            // The gap strip is the only thing that shows a rival behind the rider, so it must sit above the
            // telemetry row rather than under it. The row's height is only known once it is laid out.
            TelemetryBar.SizeChanged += (_, _) => SimCanvas.GapStripBottomInset = TelemetryBar.ActualHeight + 24.0;

            ApplyPacerSettings();
        }

        public void Dispose()
        {
            SimCanvas.FrameRendered -= OnFrameRendered;
            _viewModel.Disconnected -= OnDisconnected;
            _viewModel.Dispose();
        }

        private void OnDisconnected()
        {
            // Optional: return to setup view
            // var mainViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            // mainViewModel.CurrentView = App.Current.Services.GetRequiredService<SetupViewModel>();
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowPostWorkoutOptions = false;
        }

        // --- Pacer (POC 2) ---

        private void OnFrameRendered(object? sender, SimulationFrameEventArgs e)
        {
            _riderDistanceMeters = e.RiderDistanceMeters;

            if (ChkPacerEnabled.IsChecked != true) return;

            _pacer.Advance(e.DeltaSeconds, e.RiderDistanceMeters, e.RiderSpeedKph, SimCanvas.GradePercent);

            SimCanvas.GhostDistanceMeters = _pacer.DistanceMeters;
            SimCanvas.GhostSpeedKph = _pacer.SpeedKph;

            if (_pacerReadoutClock.IsRunning && _pacerReadoutClock.Elapsed.TotalSeconds < 0.1) return;
            _pacerReadoutClock.Restart();
            UpdatePacerHud(e.RiderSpeedKph);
        }

        /// <summary>
        /// Pacer HUD. Every watt here is pseudo-power from the shared model (there is no power meter on this
        /// ride), so it is always shown with a ≈ and must never be read as a measurement.
        /// </summary>
        private void UpdatePacerHud(double riderSpeedKph)
        {
            double gap = _pacer.GapMeters(_riderDistanceMeters);
            double delta = _pacer.ProjectedFinishDeltaSeconds;

            TxtPacerState.Text = _pacer.State.ToString().ToUpperInvariant();
            TxtPacerState.Foreground = _pacer.State switch
            {
                PacerState.Surging => Brushes.OrangeRed,
                PacerState.Easing => Brushes.SteelBlue,
                PacerState.Mercy => Brushes.MediumPurple,
                _ => Brushes.MediumSeaGreen,
            };

            TxtPacerGap.Text = $"GAP {DuelMath.FormatGapMeters(gap)}";
            TxtPacerDelta.Text = $"\u0394 {DuelMath.FormatDelta(delta)}";

            string magnitude = DuelMath.FormatDelta(delta).TrimStart('+', '-');
            TxtPacerVerdict.Text = delta >= 0 ? $"pacer ahead by {magnitude}" : $"you lead by {magnitude}";

            double riderWatts = RiderPowerModel.PowerFromSpeed(riderSpeedKph, SimCanvas.GradePercent);
            TxtPacerWatts.Text = $"\u2248pacer {_pacer.PseudoWatts:F0} W   \u2248you {riderWatts:F0} W   {_pacer.SpeedKph:F1} kph";

            TxtPacerReadout.Text = $"{TxtPacerState.Text} · GAP {DuelMath.FormatGapMeters(gap)} · \u0394 {DuelMath.FormatDelta(delta)}";
        }

        private void ChkPacerEnabled_Changed(object sender, RoutedEventArgs e)
        {
            bool on = ChkPacerEnabled.IsChecked == true;

            ApplyPacerSettings();   // the reset below needs the current mode to pick the station

            if (on)
            {
                // The model picks the station: alongside gap in ride-along mode, the target gap otherwise.
                _pacer.Reset(_riderDistanceMeters);

                SimCanvas.SecondRiderLabel = "pacer";
                SimCanvas.GhostOpacity = 0.55;
                SimCanvas.GhostShowMarker = true;
                SimCanvas.GhostShowHud = false;   // the app has its own HUD card (canvas text would clash with it)
                SimCanvas.GhostDistanceMeters = _pacer.DistanceMeters;
                SimCanvas.GhostSpeedKph = _pacer.SpeedKph;
            }
            else
            {
                SimCanvas.GhostDistanceMeters = 0;
                SimCanvas.GhostSpeedKph = 0;
            }

            SimCanvas.GhostEnabled = on;
            CardPacerHud.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            ApplyPacerSettings();
            UpdatePacerHud(_viewModel.CurrentSpeedKph);
        }

        private void PacerSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyPacerSettings();
        }

        private void PacerCheck_Changed(object sender, RoutedEventArgs e)
        {
            // Ride-along is a mode the pacer runs in, not a rival of its own: ticking it with the pacer off
            // used to do nothing at all, which reads as a broken feature. Switch the pacer on instead — that
            // raises ChkPacerEnabled_Changed, which resets the duel with the alongside station.
            if (ChkPacerRideAlong.IsChecked == true && ChkPacerEnabled.IsChecked != true)
            {
                ChkPacerEnabled.IsChecked = true;
                return;
            }

            ApplyPacerSettings();
        }

        private void BtnPacerReset_Click(object sender, RoutedEventArgs e)
        {
            _pacer.Reset(_riderDistanceMeters);

            if (ChkPacerEnabled.IsChecked == true)
            {
                SimCanvas.GhostDistanceMeters = _pacer.DistanceMeters;
                SimCanvas.GhostSpeedKph = _pacer.SpeedKph;
                UpdatePacerHud(_viewModel.CurrentSpeedKph);
            }
        }

        /// <summary>
        /// Pushes the live settings into the pacer's config. The band stays at its default in-app, on both
        /// sides of the ride-along flag.
        /// </summary>
        private void ApplyPacerSettings()
        {
            if (SliderPacerStrength == null) return;   // XAML not built yet

            PacerConfig config = _pacer.Config;
            config.PacerWPerKg = SliderPacerStrength.Value;
            config.GapTargetMeters = SliderPacerGap.Value;
            config.Elasticity = SliderPacerElasticity.Value;
            config.MercyEnabled = ChkPacerMercy.IsChecked == true;

            config.RideAlongMode = ChkPacerRideAlong.IsChecked == true;
            config.AlongsideGapMeters = SliderPacerAlongside.Value;
            config.AttackPushMeters = SliderPacerAttackPush.Value;
            config.AttackIntervalSeconds = SliderPacerAttackInterval.Value;
            config.AttackLengthSeconds = SliderPacerAttackLength.Value;
            config.RecoverRelativeSpeed = SliderPacerRecover.Value;
            config.CatchOvertakeMeters = SliderPacerCatch.Value;

            PanelPacerRideAlong.Visibility = config.RideAlongMode ? Visibility.Visible : Visibility.Collapsed;

            // The gap strip has to track the gap he is actually holding, not the one the old slider asks for.
            SimCanvas.SecondRiderGapTargetMeters = config.RideAlongMode
                ? config.AlongsideGapMeters
                : config.GapTargetMeters;
            SimCanvas.SecondRiderGapBandMeters = config.RideAlongMode
                ? config.AlongsideBandMeters
                : config.BandMeters;
            SimCanvas.SecondRiderGapStrip = ChkPacerEnabled.IsChecked == true && ChkPacerStrip.IsChecked == true;
        }
    }
}
