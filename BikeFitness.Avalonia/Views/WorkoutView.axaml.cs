using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using BikeFitness.Avalonia.Controls;
using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;
using BikeFitness.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BikeFitness.Avalonia.Views
{
    public partial class WorkoutView : UserControl, IDisposable
    {
        private WorkoutViewModel? _viewModel;

        // --- Pacer (POC). View-local and off by default: it renders a rival and moves only that rival's
        // --- virtual speed. Resistance, the trainer and session data are never touched.
        // --- Mirrors the WPF WorkoutView so both apps behave identically.
        private readonly PacerModel _pacer = new PacerModel(new PacerConfig());
        private readonly Stopwatch _pacerReadoutClock = new Stopwatch();
        private double _riderDistanceMeters;

        public WorkoutView()
        {
            InitializeComponent();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Rider pedal animation: same 12-frame sheet the WPF app uses, shipped under Images/.
            // Wheel-spin overlay is deliberately not enabled yet.
            SimCanvas.PedalSheetSource = PedalAnimation.GetDefaultSheetPath(
                SimulationCanvas.ResolveImageDirectory(AppContext.BaseDirectory));

            _viewModel = App.Current.Services?.GetRequiredService<WorkoutViewModel>();
            DataContext = _viewModel;
            
            if (_viewModel != null)
            {
                _viewModel.Disconnected += OnDisconnected;
            }

            // The pacer advances on the canvas's own frame clock, exactly as in the app's WPF view.
            SimCanvas.FrameRendered += OnFrameRendered;

            // Sliders apply live; wired here rather than in XAML because Avalonia's range-base event args
            // type differs from WPF's and a lambda keeps the handler signature irrelevant.
            SliderPacerStrength.ValueChanged += (_, _) => ApplyPacerSettings();
            SliderPacerGap.ValueChanged += (_, _) => ApplyPacerSettings();
            SliderPacerElasticity.ValueChanged += (_, _) => ApplyPacerSettings();

            ApplyPacerSettings();
        }

        public void Dispose()
        {
            SimCanvas.FrameRendered -= OnFrameRendered;

            if (_viewModel != null)
            {
                _viewModel.Disconnected -= OnDisconnected;
                _viewModel.Dispose();
            }
        }

        private void OnDisconnected()
        {
            // Optional: return to setup view
        }

        // --- Pacer (POC) ---

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
        /// Pacer HUD. Every watt here is pseudo-power from the shared model (this ride has no power meter), so it
        /// is always shown with a ≈ and must never be read as a measurement.
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

        private void ChkPacerEnabled_Changed(object? sender, RoutedEventArgs e)
        {
            bool on = ChkPacerEnabled.IsChecked == true;

            if (on)
            {
                _pacer.Reset(_riderDistanceMeters, _pacer.Config.GapTargetMeters);

                SimCanvas.SecondRiderLabel = "pacer";
                SimCanvas.GhostOpacity = 0.55;
                SimCanvas.GhostShowMarker = true;
                SimCanvas.GhostShowHud = false;   // the view has its own HUD card; canvas text would clash with the grade badge
                SimCanvas.GhostDistanceMeters = _pacer.DistanceMeters;
                SimCanvas.GhostSpeedKph = _pacer.SpeedKph;
            }
            else
            {
                SimCanvas.GhostDistanceMeters = 0;
                SimCanvas.GhostSpeedKph = 0;
            }

            SimCanvas.GhostEnabled = on;
            CardPacerHud.IsVisible = on;

            ApplyPacerSettings();
            UpdatePacerHud(_viewModel?.CurrentSpeedKph ?? 0);
        }

        private void PacerCheck_Changed(object? sender, RoutedEventArgs e)
        {
            ApplyPacerSettings();
        }

        private void BtnPacerReset_Click(object? sender, RoutedEventArgs e)
        {
            _pacer.Reset(_riderDistanceMeters, _pacer.Config.GapTargetMeters);

            if (ChkPacerEnabled.IsChecked == true)
            {
                SimCanvas.GhostDistanceMeters = _pacer.DistanceMeters;
                SimCanvas.GhostSpeedKph = _pacer.SpeedKph;
                UpdatePacerHud(_viewModel?.CurrentSpeedKph ?? 0);
            }
        }

        /// <summary>Pushes the live settings into the pacer's config. The band stays at its default in-app.</summary>
        private void ApplyPacerSettings()
        {
            PacerConfig config = _pacer.Config;
            config.PacerWPerKg = SliderPacerStrength.Value;
            config.GapTargetMeters = SliderPacerGap.Value;
            config.Elasticity = SliderPacerElasticity.Value;
            config.MercyEnabled = ChkPacerMercy.IsChecked == true;

            SimCanvas.SecondRiderGapTargetMeters = config.GapTargetMeters;
            SimCanvas.SecondRiderGapBandMeters = config.BandMeters;
            SimCanvas.SecondRiderGapStrip = ChkPacerEnabled.IsChecked == true && ChkPacerStrip.IsChecked == true;
        }
    }
}
