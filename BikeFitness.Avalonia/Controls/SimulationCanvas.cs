using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;

namespace BikeFitness.Avalonia.Controls
{
    public class SimulationCanvas : Control
    {
        private readonly SimulationEngine<Bitmap, Bitmap> _engine = new SimulationEngine<Bitmap, Bitmap>();
        private readonly DispatcherTimer _timer;
        private DateTime _lastTick;

        // Pens & Brushes (Avalonia equivalents)
        private static readonly IBrush GrassBrush;
        private static readonly IPen PathPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 135, 100)), 10);
        private static readonly IPen RoadsideOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(40, 50, 40)), 1);
        private static readonly IBrush MountainTreeCanopy;
        private static readonly IBrush PlainTreeCanopy;
        private static readonly IBrush DesertTreeCanopy;
        private static readonly IBrush OceanTreeCanopy;
        private static readonly IBrush MountainParticleBrush;
        private static readonly IBrush PlainParticleBrush;
        private static readonly IBrush DesertParticleBrush;
        private static readonly IBrush OceanParticleBrush;

        // Second-rider chrome — kept identical to the WPF canvas so both apps render the rival the same way.
        private static readonly IBrush RivalChipBrush = new SolidColorBrush(Color.FromArgb(205, 12, 14, 18));
        private static readonly IBrush RivalTextBrush = new SolidColorBrush(Color.FromArgb(245, 245, 250, 255));
        private static readonly IPen RivalChipPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1);
        private static readonly IBrush GapStripBandBrush = new SolidColorBrush(Color.FromArgb(90, 90, 220, 120));
        private static readonly IBrush GapStripRiderBrush = new SolidColorBrush(Color.FromArgb(235, 245, 250, 255));
        private static readonly IBrush GapStripRivalBrush = new SolidColorBrush(Color.FromArgb(240, 255, 176, 60));

        static SimulationCanvas()
        {
            var grassGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            grassGradient.GradientStops.Add(new GradientStop(Color.FromRgb(185, 200, 130), 0.0));
            grassGradient.GradientStops.Add(new GradientStop(Color.FromRgb(100, 130, 80), 1.0));
            GrassBrush = grassGradient;

            MountainTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(120, 165, 105), Color.FromRgb(60, 95, 55));
            PlainTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(140, 190, 125), Color.FromRgb(80, 125, 70));
            DesertTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(170, 200, 135), Color.FromRgb(120, 150, 95));
            OceanTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(105, 175, 150), Color.FromRgb(60, 110, 90));

            MountainParticleBrush = new SolidColorBrush(Color.FromRgb(235, 245, 255));
            PlainParticleBrush = new SolidColorBrush(Color.FromRgb(250, 245, 210));
            DesertParticleBrush = new SolidColorBrush(Color.FromRgb(245, 220, 170));
            OceanParticleBrush = new SolidColorBrush(Color.FromRgb(210, 240, 250));
        }

        private static IBrush CreateTreeCanopyBrush(Color light, Color dark)
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            gradient.GradientStops.Add(new GradientStop(light, 0.0));
            gradient.GradientStops.Add(new GradientStop(dark, 1.0));
            return gradient;
        }

        public static readonly DirectProperty<SimulationCanvas, double> SpeedKphProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(SpeedKph),
                o => o.SpeedKph,
                (o, v) => o.SpeedKph = v);

        private double _speedKph;
        public double SpeedKph
        {
            get => _speedKph;
            set
            {
                SetAndRaise(SpeedKphProperty, ref _speedKph, value);
                _engine.SpeedKph = value;
            }
        }

        public static readonly DirectProperty<SimulationCanvas, double> GradePercentProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(GradePercent),
                o => o.GradePercent,
                (o, v) => o.GradePercent = v);

        private double _gradePercent;
        public double GradePercent
        {
            get => _gradePercent;
            set
            {
                SetAndRaise(GradePercentProperty, ref _gradePercent, value);
                _engine.RecordGradeChange(value);
            }
        }

        public static readonly DirectProperty<SimulationCanvas, double> SyncedDistanceMetersProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(SyncedDistanceMeters),
                o => o.SyncedDistanceMeters,
                (o, v) => o.SyncedDistanceMeters = v);

        private double _syncedDistanceMeters;
        public double SyncedDistanceMeters
        {
            get => _syncedDistanceMeters;
            set
            {
                SetAndRaise(SyncedDistanceMetersProperty, ref _syncedDistanceMeters, value);
            }
        }

        public static readonly DirectProperty<SimulationCanvas, string> PedalSheetSourceProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, string>(
                nameof(PedalSheetSource),
                o => o.PedalSheetSource,
                (o, v) => o.PedalSheetSource = v);

        private string _pedalSheetSource = string.Empty;

        /// <summary>
        /// Path to the pedal-cycle sprite sheet. Empty (the default) keeps the legacy static
        /// cyclist sprite, so the canvas renders exactly as before unless a sheet is supplied.
        /// </summary>
        public string PedalSheetSource
        {
            get => _pedalSheetSource;
            set
            {
                if (SetAndRaise(PedalSheetSourceProperty, ref _pedalSheetSource, value))
                    LoadPedalSheet();
            }
        }

        public static readonly DirectProperty<SimulationCanvas, double> PedalMetersPerRevolutionProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(PedalMetersPerRevolution),
                o => o.PedalMetersPerRevolution,
                (o, v) => o.PedalMetersPerRevolution = v);

        private double _pedalMetersPerRevolution = PedalAnimation.DefaultMetersPerRevolution;

        /// <summary>Metres travelled per crank revolution — the gear the animation pedals at.</summary>
        public double PedalMetersPerRevolution
        {
            get => _pedalMetersPerRevolution;
            set => SetAndRaise(PedalMetersPerRevolutionProperty, ref _pedalMetersPerRevolution, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> PedalDrawSizePxProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(PedalDrawSizePx),
                o => o.PedalDrawSizePx,
                (o, v) => o.PedalDrawSizePx = v);

        private double _pedalDrawSizePx = PedalAnimation.DefaultDrawWidthPx;

        /// <summary>Width, in canvas units, that one sheet cell is drawn at.</summary>
        public double PedalDrawSizePx
        {
            get => _pedalDrawSizePx;
            set => SetAndRaise(PedalDrawSizePxProperty, ref _pedalDrawSizePx, value);
        }

        // Pre-cropped sheet frames, one per crank phase; empty unless PedalSheetSource is set.
        private readonly List<IImage> _pedalFrames = new List<IImage>();

        // --- Second rider (ghost / pacer) -----------------------------------------------------------------
        // Mirrors the WPF SimulationCanvas property set name-for-name, so the two apps stay in sync. All of
        // it is off by default: with GhostEnabled false the rendered output is exactly what it was before.

        /// <summary>Raised once per frame, after the engine has advanced and before the frame is drawn.</summary>
        public event EventHandler<SimulationFrameEventArgs>? FrameRendered;

        public static readonly DirectProperty<SimulationCanvas, bool> GhostEnabledProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, bool>(
                nameof(GhostEnabled),
                o => o.GhostEnabled,
                (o, v) => o.GhostEnabled = v);

        private bool _ghostEnabled;

        /// <summary>Master flag for the second-rider draw path. Off by default.</summary>
        public bool GhostEnabled
        {
            get => _ghostEnabled;
            set => SetAndRaise(GhostEnabledProperty, ref _ghostEnabled, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> GhostDistanceMetersProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(GhostDistanceMeters),
                o => o.GhostDistanceMeters,
                (o, v) => o.GhostDistanceMeters = v);

        private double _ghostDistanceMeters;

        /// <summary>Rival's cumulative distance. Callers own the replay maths; the canvas only draws it.</summary>
        public double GhostDistanceMeters
        {
            get => _ghostDistanceMeters;
            set => SetAndRaise(GhostDistanceMetersProperty, ref _ghostDistanceMeters, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> GhostSpeedKphProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(GhostSpeedKph),
                o => o.GhostSpeedKph,
                (o, v) => o.GhostSpeedKph = v);

        private double _ghostSpeedKph;

        /// <summary>Rival's reported speed, used by the marker/HUD delta only.</summary>
        public double GhostSpeedKph
        {
            get => _ghostSpeedKph;
            set => SetAndRaise(GhostSpeedKphProperty, ref _ghostSpeedKph, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> GhostOpacityProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(GhostOpacity),
                o => o.GhostOpacity,
                (o, v) => o.GhostOpacity = v);

        private double _ghostOpacity = 0.4;

        /// <summary>Sprite opacity for the rival.</summary>
        public double GhostOpacity
        {
            get => _ghostOpacity;
            set => SetAndRaise(GhostOpacityProperty, ref _ghostOpacity, value);
        }

        public static readonly DirectProperty<SimulationCanvas, bool> GhostShowMarkerProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, bool>(
                nameof(GhostShowMarker),
                o => o.GhostShowMarker,
                (o, v) => o.GhostShowMarker = v);

        private bool _ghostShowMarker = true;

        /// <summary>Draw the off-screen gap chip when the rival is outside the visible road window.</summary>
        public bool GhostShowMarker
        {
            get => _ghostShowMarker;
            set => SetAndRaise(GhostShowMarkerProperty, ref _ghostShowMarker, value);
        }

        public static readonly DirectProperty<SimulationCanvas, bool> GhostShowHudProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, bool>(
                nameof(GhostShowHud),
                o => o.GhostShowHud,
                (o, v) => o.GhostShowHud = v);

        private bool _ghostShowHud = true;

        /// <summary>Draw the in-canvas GAP / Δ readout.</summary>
        public bool GhostShowHud
        {
            get => _ghostShowHud;
            set => SetAndRaise(GhostShowHudProperty, ref _ghostShowHud, value);
        }

        public static readonly DirectProperty<SimulationCanvas, string> SecondRiderLabelProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, string>(
                nameof(SecondRiderLabel),
                o => o.SecondRiderLabel,
                (o, v) => o.SecondRiderLabel = v);

        private string _secondRiderLabel = "ghost";

        /// <summary>What to call the rival in the HUD ("ghost" or "pacer").</summary>
        public string SecondRiderLabel
        {
            get => _secondRiderLabel;
            set => SetAndRaise(SecondRiderLabelProperty, ref _secondRiderLabel, value);
        }

        public static readonly DirectProperty<SimulationCanvas, bool> SecondRiderGapStripProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, bool>(
                nameof(SecondRiderGapStrip),
                o => o.SecondRiderGapStrip,
                (o, v) => o.SecondRiderGapStrip = v);

        private bool _secondRiderGapStrip;

        /// <summary>Compressed gap strip under the canvas — the only way a 10–40 m band is legible on screen.</summary>
        public bool SecondRiderGapStrip
        {
            get => _secondRiderGapStrip;
            set => SetAndRaise(SecondRiderGapStripProperty, ref _secondRiderGapStrip, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> SecondRiderGapTargetMetersProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(SecondRiderGapTargetMeters),
                o => o.SecondRiderGapTargetMeters,
                (o, v) => o.SecondRiderGapTargetMeters = v);

        private double _secondRiderGapTargetMeters = 25.0;

        /// <summary>Gap the rival is trying to hold — drawn as the band on the strip.</summary>
        public double SecondRiderGapTargetMeters
        {
            get => _secondRiderGapTargetMeters;
            set => SetAndRaise(SecondRiderGapTargetMetersProperty, ref _secondRiderGapTargetMeters, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> SecondRiderGapBandMetersProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(SecondRiderGapBandMeters),
                o => o.SecondRiderGapBandMeters,
                (o, v) => o.SecondRiderGapBandMeters = v);

        private double _secondRiderGapBandMeters = 15.0;

        /// <summary>Half-width of the rival's band, drawn either side of the target.</summary>
        public double SecondRiderGapBandMeters
        {
            get => _secondRiderGapBandMeters;
            set => SetAndRaise(SecondRiderGapBandMetersProperty, ref _secondRiderGapBandMeters, value);
        }

        public static readonly DirectProperty<SimulationCanvas, double> GapStripBottomInsetProperty =
            AvaloniaProperty.RegisterDirect<SimulationCanvas, double>(
                nameof(GapStripBottomInset),
                o => o.GapStripBottomInset,
                (o, v) => o.GapStripBottomInset = v);

        private double _gapStripBottomInset = 30.0;

        /// <summary>
        /// How far above the bottom of the canvas the gap strip is drawn, in pixels. The strip is the only
        /// instrument that shows a rival behind the rider, so it must not be drawn where the telemetry row
        /// is: the view sets this to the telemetry row's height (see <c>WorkoutView</c>). Defaults to the
        /// old fixed position for anything that has no telemetry row.
        /// </summary>
        public double GapStripBottomInset
        {
            get => _gapStripBottomInset;
            set => SetAndRaise(GapStripBottomInsetProperty, ref _gapStripBottomInset, value);
        }

        public SimulationCanvas()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += (s, e) => {
                var now = DateTime.Now;
                double dt = (now - _lastTick).TotalSeconds;
                _lastTick = now;

                if (dt > 0.1) dt = 0.1;
                if (dt > 0)
                {
                    // Align with WPF logic: Only hard-reset if we are at start and get first real distance
                    if (_engine.TotalDistanceMeters < 5.0 && SyncedDistanceMeters > 5.0)
                    {
                        _engine.Reset(SyncedDistanceMeters);
                    }

                    _engine.Update(dt);

                    // Let subscribers (the pacer panel) advance their own state on this exact frame.
                    FrameRendered?.Invoke(this, new SimulationFrameEventArgs(dt, _engine.TotalDistanceMeters, _engine.SpeedKph));

                    InvalidateVisual();
                }
            };

            _lastTick = DateTime.Now;
            _timer.Start();

            LoadAssets();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            _engine.ActualWidth = e.NewSize.Width;
            _engine.ActualHeight = e.NewSize.Height;
        }

        private void LoadAssets()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string imageDir = ResolveImageDirectory(baseDir);

                if (!string.IsNullOrEmpty(imageDir))
                {
                    _engine.CyclistSprite = LoadBitmap(Path.Combine(imageDir, "cyclist_sprite.png"));

                    var mountain = LoadBitmap(Path.Combine(imageDir, "biome_mountain.png"));
                    var plain = LoadBitmap(Path.Combine(imageDir, "biome_plain.png"));
                    var desert = LoadBitmap(Path.Combine(imageDir, "biome_desert.png"));
                    var ocean = LoadBitmap(Path.Combine(imageDir, "biome_ocean.png"));

                    var tMP = LoadBitmap(Path.Combine(imageDir, "transition_mountain_plain.png"));
                    var tPD = LoadBitmap(Path.Combine(imageDir, "transition_plain_desert.png"));
                    var tDO = LoadBitmap(Path.Combine(imageDir, "transition_desert_ocean.png"));
                    var tOM = LoadBitmap(Path.Combine(imageDir, "transition_ocean_mountain.png"));

                    _engine.ClearBackgroundSegments();
                    double biomeLen = SimulationEngine<Bitmap, Bitmap>.BiomeSegmentLengthMeters;
                    double transLen = SimulationEngine<Bitmap, Bitmap>.TransitionSegmentLengthMeters;
                    bool mirror = SimulationEngine<Bitmap, Bitmap>.UseMirroredBackgroundTiles;

                    if (mountain != null) _engine.AddBackgroundSegment("Mountain", BackgroundTheme.Mountain, mountain, biomeLen, mirror);
                    if (tMP != null) _engine.AddBackgroundSegment("T MP", BackgroundTheme.Transition, tMP, transLen, false);
                    if (plain != null) _engine.AddBackgroundSegment("Plain", BackgroundTheme.Plain, plain, biomeLen, mirror);
                    if (tPD != null) _engine.AddBackgroundSegment("T PD", BackgroundTheme.Transition, tPD, transLen, false);
                    if (desert != null) _engine.AddBackgroundSegment("Desert", BackgroundTheme.Desert, desert, biomeLen, mirror);
                    if (tDO != null) _engine.AddBackgroundSegment("T DO", BackgroundTheme.Transition, tDO, transLen, false);
                    if (ocean != null) _engine.AddBackgroundSegment("Ocean", BackgroundTheme.Ocean, ocean, biomeLen, mirror);
                    if (tOM != null) _engine.AddBackgroundSegment("T OM", BackgroundTheme.Transition, tOM, transLen, false);

                    _engine.BushSprites.Clear();
                    AddBush(Path.Combine(imageDir, "sm_bush.png"));
                    AddBush(Path.Combine(imageDir, "big_bush.png"));
                    AddBush(Path.Combine(imageDir, "tall_bush.png"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load assets: {ex.Message}");
            }
        }

        private void AddBush(string path)
        {
            var b = LoadBitmap(path);
            if (b != null) _engine.BushSprites.Add(b);
        }

        /// <summary>
        /// Walks up from <paramref name="startDir"/> to the nearest directory containing an
        /// <c>Images</c> folder. Public so views resolve asset paths the same way the canvas does.
        /// </summary>
        public static string ResolveImageDirectory(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var imgDir = Path.Combine(dir.FullName, "Images");
                if (Directory.Exists(imgDir)) return imgDir;
                dir = dir.Parent;
            }
            return "";
        }

        private Bitmap? LoadBitmap(string path)
        {
            if (File.Exists(path)) return new Bitmap(path);
            return null;
        }

        /// <summary>
        /// Crops the sheet into one frame per crank phase. Called whenever
        /// <see cref="PedalSheetSource"/> changes; frames are pre-cropped so the render loop
        /// only ever does a straight image draw.
        /// </summary>
        private void LoadPedalSheet()
        {
            _pedalFrames.Clear();

            string source = PedalSheetSource;
            if (string.IsNullOrWhiteSpace(source)) return;

            var sheet = LoadBitmap(source);
            if (sheet == null)
            {
                Debug.WriteLine($"Pedal sheet not found or failed to load: {source}");
                return;
            }

            for (int i = 0; i < PedalAnimation.FrameCount; i++)
            {
                var (x, y, w, h) = PedalAnimation.GetSourceRect(i);
                _pedalFrames.Add(new CroppedBitmap(sheet, new PixelRect(x, y, w, h)));
            }
        }

        public override void Render(DrawingContext context)
        {
            if (_engine.ActualWidth <= 0 || _engine.ActualHeight <= 0) return;

            var bgInfo = _engine.GetBackgroundSegmentInfo(_engine.TotalDistanceMeters);
            DrawBackground(context, bgInfo);
            DrawTransitionParticles(context);

            double bikeScreenX = _engine.ActualWidth * 0.3;
            double bikeDist = _engine.TotalDistanceMeters;
            double bikeHeight = _engine.Terrain.GetHeightAt(bikeDist);
            double visualCenterY = _engine.ActualHeight * 0.75;

            double leftDist = bikeDist - (bikeScreenX / SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter) - 5;
            double rightDist = bikeDist + ((_engine.ActualWidth - bikeScreenX) / SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter) + 5;

            // Terrain & Path
            DrawTerrain(context, leftDist, rightDist, bikeDist, bikeHeight, visualCenterY, bikeScreenX);

            var theme = bgInfo.Segment?.Theme ?? BackgroundTheme.Plain;
            if (theme == BackgroundTheme.Transition && bgInfo.NextSegment != null) theme = bgInfo.NextSegment.Theme;

            // Objects Behind
            DrawRoadside(context, leftDist, rightDist, bikeDist, bikeHeight, visualCenterY, bikeScreenX, theme, true);

            // Second rider (POC). Own transform, exactly like the WPF canvas: it sits on its own terrain height
            // and tilts with the grade at its own distance, and the player still draws on top.
            if (GhostEnabled)
            {
                DrawSecondRider(context, bikeDist, bikeHeight, visualCenterY, bikeScreenX);
            }

            // Biker
            using (context.PushTransform(Matrix.CreateRotation(-_engine.CurrentSlopeAngle * (Math.PI / 180.0)) * Matrix.CreateTranslation(bikeScreenX, visualCenterY)))
            {
                DrawCyclist(context);
            }

            // Objects Front
            DrawRoadside(context, leftDist, rightDist, bikeDist, bikeHeight, visualCenterY, bikeScreenX, theme, false);

            if (GhostEnabled)
            {
                if (GhostShowMarker) DrawSecondRiderMarker(context);
                if (GhostShowHud) DrawSecondRiderHud(context);
            }

            if (SecondRiderGapStrip) DrawGapStrip(context);

            DrawBiomeLabel(context);
        }

        /// <summary>
        /// Draws the rider in bike-local space (origin = ground contact under the rider).
        /// Uses the animated sheet when one is loaded, otherwise the legacy static sprite.
        /// </summary>
        private void DrawCyclist(DrawingContext context)
        {
            if (_pedalFrames.Count == PedalAnimation.FrameCount)
            {
                DrawPedalCyclist(context);
                return;
            }

            if (_engine.CyclistSprite != null)
                context.DrawImage(_engine.CyclistSprite, new Rect(0, 0, _engine.CyclistSprite.Size.Width, _engine.CyclistSprite.Size.Height), new Rect(-75, -130, 150, 150));
            else
                context.FillRectangle(Brushes.Red, new Rect(-25, -40, 50, 40));
        }

        /// <summary>
        /// Pedal animation driven by distance (fixed-gear assumption), mirroring the WPF canvas:
        /// straight 12-frame loop, D = <see cref="PedalMetersPerRevolution"/>. The code wheel-spin
        /// overlay is deliberately not ported yet.
        /// </summary>
        private void DrawPedalCyclist(DrawingContext context)
        {
            double phase = PedalAnimation.GetCrankPhase(_engine.TotalDistanceMeters, PedalMetersPerRevolution);
            DrawSheetFrame(context, PedalAnimation.GetFrameIndex(phase));
        }

        private void DrawSheetFrame(DrawingContext context, int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= _pedalFrames.Count) return;

            var (x, y, width, height) = PedalAnimation.GetFrameDestRect(frameIndex, PedalDrawSizePx);
            context.DrawImage(_pedalFrames[frameIndex], new Rect(x, y, width, height));
        }

        // --- Second rider (ghost / pacer) drawing — mirrors the WPF canvas ---------------------------------

        private void DrawSecondRider(DrawingContext context, double bikeDist, double bikeHeight, double visualCenterY, double bikeScreenX)
        {
            double rivalDistance = GhostDistanceMeters;
            Point ground = WorldToScreen(rivalDistance, bikeDist, bikeHeight, visualCenterY, bikeScreenX);

            double rivalGrade = _engine.Terrain.GetGradeAt(rivalDistance);
            double rivalSlopeRadians = Math.Atan(rivalGrade / 100.0);

            using (context.PushOpacity(Math.Clamp(GhostOpacity, 0.05, 1.0)))
            using (context.PushTransform(Matrix.CreateRotation(-rivalSlopeRadians) * Matrix.CreateTranslation(ground.X, ground.Y)))
            {
                if (_pedalFrames.Count == PedalAnimation.FrameCount)
                {
                    // Animated from the rival's own distance, so its legs turn at its own speed.
                    DrawSheetFrame(context, PedalAnimation.GetFrameIndexForDistance(rivalDistance, PedalMetersPerRevolution));
                }
                else if (_engine.CyclistSprite != null)
                {
                    context.DrawImage(
                        _engine.CyclistSprite,
                        new Rect(0, 0, _engine.CyclistSprite.Size.Width, _engine.CyclistSprite.Size.Height),
                        new Rect(-75, -130, 150, 150));
                }
                else
                {
                    context.FillRectangle(Brushes.Red, new Rect(-25, -40, 50, 40));
                }
            }
        }

        /// <summary>Off-screen gap chip: mandatory, since only ~12 m ahead is visible at 50 px/m.</summary>
        private void DrawSecondRiderMarker(DrawingContext context)
        {
            double width = _engine.ActualWidth;
            double height = _engine.ActualHeight;
            if (width <= 0 || height <= 0) return;

            bool onScreen = SecondRiderGeometry.TryGetScreenPosition(
                _engine.TotalDistanceMeters,
                GhostDistanceMeters,
                width,
                height,
                SecondRiderGeometry.DefaultBikeScreenRatio,
                SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter,
                out double screenX,
                out _);

            if (onScreen) return;

            double gap = DuelMath.GapMeters(GhostDistanceMeters, _engine.TotalDistanceMeters);
            double delta = DuelMath.DeltaSeconds(GhostDistanceMeters, _engine.TotalDistanceMeters, _engine.SpeedKph, GhostSpeedKph);

            var text = new FormattedText(
                DuelMath.FormatGapChip(gap, delta),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                20,
                RivalTextBrush);

            double markerX = SecondRiderGeometry.GetMarkerX(screenX, width);
            double centerY = height * 0.62;
            double padX = 10;
            double padY = 5;

            var chip = new Rect(
                markerX - (text.Width / 2.0) - padX,
                centerY - (text.Height / 2.0) - padY,
                text.Width + (padX * 2.0),
                text.Height + (padY * 2.0));

            context.DrawRectangle(RivalChipBrush, RivalChipPen, chip, 6, 6);
            context.DrawText(text, new Point(chip.X + padX, chip.Y + padY));
        }

        /// <summary>In-canvas GAP / Δ readout (top-left).</summary>
        private void DrawSecondRiderHud(DrawingContext context)
        {
            double width = _engine.ActualWidth;
            double height = _engine.ActualHeight;
            if (width <= 0 || height <= 0) return;

            double gap = DuelMath.GapMeters(GhostDistanceMeters, _engine.TotalDistanceMeters);
            double delta = DuelMath.DeltaSeconds(GhostDistanceMeters, _engine.TotalDistanceMeters, _engine.SpeedKph, GhostSpeedKph);
            string label = string.IsNullOrWhiteSpace(SecondRiderLabel) ? "rival" : SecondRiderLabel;

            var big = new FormattedText(
                $"GAP {DuelMath.FormatGapMeters(gap)}   \u0394 {DuelMath.FormatDelta(delta)}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                22,
                RivalTextBrush);

            var small = new FormattedText(
                $"{label} {GhostSpeedKph:F1} kph   {DuelMath.FormatGapChip(gap, delta)}",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                14,
                RivalTextBrush);

            double x = 16;
            double y = 14;
            double pad = 8;

            context.DrawRectangle(
                RivalChipBrush,
                RivalChipPen,
                new Rect(x, y, Math.Max(big.Width, small.Width) + (pad * 2.0), big.Height + small.Height + (pad * 2.0)),
                6,
                6);

            context.DrawText(big, new Point(x + pad, y + pad));
            context.DrawText(small, new Point(x + pad, y + pad + big.Height));
        }

        /// <summary>Compressed gap strip: you as a fixed tick, the rival as a dot, the target band in green.</summary>
        private void DrawGapStrip(DrawingContext context)
        {
            double canvasWidth = _engine.ActualWidth;
            double canvasHeight = _engine.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            const double metersBehind = 60.0;
            const double metersAhead = 70.0;

            double left = 40;
            double right = canvasWidth - 40;
            if (right - left < 80) return;

            double height = 12;
            double top = canvasHeight - Math.Max(12.0, GapStripBottomInset);
            double span = metersBehind + metersAhead;

            double Map(double gapMeters) => left + (((gapMeters + metersBehind) / span) * (right - left));

            double gap = DuelMath.GapMeters(GhostDistanceMeters, _engine.TotalDistanceMeters);
            double target = SecondRiderGapTargetMeters;
            double band = Math.Max(0, SecondRiderGapBandMeters);

            context.DrawRectangle(RivalChipBrush, RivalChipPen, new Rect(left, top, right - left, height), 4, 4);

            double bandLeft = Map(Math.Max(-metersBehind, target - band));
            double bandRight = Map(Math.Min(metersAhead, target + band));
            if (bandRight > bandLeft)
            {
                context.FillRectangle(GapStripBandBrush, new Rect(bandLeft, top, bandRight - bandLeft, height));
            }

            double riderX = Map(0);
            context.FillRectangle(GapStripRiderBrush, new Rect(riderX - 1.5, top - 4, 3, height + 8));

            double rivalX = Map(Math.Clamp(gap, -metersBehind, metersAhead));
            context.DrawEllipse(GapStripRivalBrush, null, new Point(rivalX, top + (height / 2.0)), 5, 5);
        }

        private void DrawBackground(DrawingContext context, SimulationEngine<Bitmap, Bitmap>.BackgroundSegmentInfo info)
        {
            context.FillRectangle(Brushes.LightSkyBlue, new Rect(0, 0, _engine.ActualWidth, _engine.ActualHeight));

            if (info.Segment?.Image == null) return;

            double scrollPx = _engine.TotalDistanceMeters * SimulationEngine<Bitmap, Bitmap>.BackgroundPixelsPerMeter;
            double opacity = _engine.GetBackgroundOpacity(info);

            if (info.Segment.Theme == BackgroundTheme.Transition)
            {
                double progress = info.SegmentLength > 0 ? SimulationMath.Clamp01(info.LocalDistance / info.SegmentLength) : 0;
                double blend = 0;
                if (info.NextSegment?.Image != null && info.SegmentLength > 0)
                {
                    double distToEnd = info.SegmentLength - info.LocalDistance;
                    if (distToEnd < SimulationEngine<Bitmap, Bitmap>.TransitionToBiomeBlendMeters)
                        blend = SimulationMath.Clamp01(1.0 - (distToEnd / SimulationEngine<Bitmap, Bitmap>.TransitionToBiomeBlendMeters));
                }

                DrawTransitionImage(context, info.Segment.Image, progress, opacity * (1.0 - blend));
                if (blend > 0 && info.NextSegment?.Image != null)
                    DrawTiledImage(context, info.NextSegment.Image, scrollPx, info.NextSegment.MirrorTiles, opacity * blend);
                return;
            }

            DrawTiledImage(context, info.Segment.Image, scrollPx, info.Segment.MirrorTiles, opacity * (1.0 - info.BlendToNext));
            if (info.BlendToNext > 0 && info.NextSegment?.Image != null)
                DrawTiledImage(context, info.NextSegment.Image, scrollPx, info.NextSegment.MirrorTiles, opacity * info.BlendToNext);
        }

        private void DrawTransitionImage(DrawingContext context, Bitmap img, double progress, double opacity)
        {
            if (opacity <= 0) return;
            double scale = Math.Max(_engine.ActualHeight / img.Size.Height, _engine.ActualWidth / img.Size.Width);
            double dw = img.Size.Width * scale;
            double dh = img.Size.Height * scale;
            double offX = -Math.Max(0, dw - _engine.ActualWidth) * progress;
            double offY = _engine.ActualHeight - dh;

            using (context.PushOpacity(opacity))
                context.DrawImage(img, new Rect(0, 0, img.Size.Width, img.Size.Height), new Rect(offX, offY, dw, dh));
        }

        private void DrawTiledImage(DrawingContext context, Bitmap img, double scrollPx, bool mirror, double opacity)
        {
            if (opacity <= 0) return;
            double scale = _engine.ActualHeight / img.Size.Height;
            double tw = Math.Round(img.Size.Width * scale);
            if (tw <= 0) return;
            double th = _engine.ActualHeight;
            double off = scrollPx % tw;
            if (off < 0) off += tw;

            long firstIdx = (long)Math.Floor(scrollPx / tw);
            double startX = Math.Floor(-off);
            double dw = tw + SimulationEngine<Bitmap, Bitmap>.BackgroundTileOverlapPx;

            using (context.PushOpacity(opacity))
            {
                int i = 0;
                while (startX < _engine.ActualWidth + tw)
                {
                    long idx = firstIdx + i;
                    bool doMirror = mirror && (idx % 2 != 0);
                    if (doMirror)
                    {
                        using (context.PushTransform(Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(startX * 2 + dw, 0)))
                            context.DrawImage(img, new Rect(0, 0, img.Size.Width, img.Size.Height), new Rect(startX, 0, dw, th));
                    }
                    else
                    {
                        context.DrawImage(img, new Rect(0, 0, img.Size.Width, img.Size.Height), new Rect(startX, 0, dw, th));
                    }
                    startX += tw;
                    i++;
                }
            }
        }

        private void DrawTerrain(DrawingContext context, double left, double right, double bikeDist, double bikeH, double cy, double bx)
        {
            var geo = new StreamGeometry();
            using (var sgc = geo.Open())
            {
                var p1 = WorldToScreen(left, bikeDist, bikeH, cy, bx);
                sgc.BeginFigure(p1, true);
                foreach (var v in _engine.Terrain.History)
                {
                    if (v.Distance > left && v.Distance < right)
                        sgc.LineTo(WorldToScreen(v.Distance, bikeDist, bikeH, cy, bx));
                }
                var p2 = WorldToScreen(right, bikeDist, bikeH, cy, bx);
                sgc.LineTo(p2);
                sgc.LineTo(new Point(p2.X, _engine.ActualHeight));
                sgc.LineTo(new Point(p1.X, _engine.ActualHeight));
                sgc.EndFigure(true);
            }
            context.DrawGeometry(GrassBrush, null, geo);

            var path = new StreamGeometry();
            using (var sgc = path.Open())
            {
                var p = WorldToScreen(left, bikeDist, bikeH, cy, bx);
                sgc.BeginFigure(p, false);
                foreach (var v in _engine.Terrain.History)
                {
                    if (v.Distance > left && v.Distance < right)
                        sgc.LineTo(WorldToScreen(v.Distance, bikeDist, bikeH, cy, bx));
                }
                sgc.LineTo(WorldToScreen(right, bikeDist, bikeH, cy, bx));
                sgc.EndFigure(false);
            }
            context.DrawGeometry(null, PathPen, path);
        }

        private Point WorldToScreen(double wd, double bd, double bh, double cy, double bx)
        {
            double wh = _engine.Terrain.GetHeightAt(wd);
            double x = bx + (wd - bd) * SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter;
            double y = cy - (wh - bh) * SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter;
            return new Point(x, y);
        }

        private void DrawRoadside(DrawingContext context, double left, double right, double bd, double bh, double cy, double bx, BackgroundTheme theme, bool isBack)
        {
            foreach (var obj in _engine.RoadsideObjects)
            {
                if (obj.Distance < left || obj.Distance > right) continue;
                bool isTree = obj.Type == SimulationEngine<Bitmap, Bitmap>.RoadsideObjectType.Tree;
                if (isBack && isTree) continue;
                if (!isBack && !isTree) continue;

                var rp = WorldToScreen(obj.Distance, bd, bh, cy, bx);
                double sz = Math.Max(12, obj.SizeMeters * SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter);

                if (isTree)
                {
                    double tx = rp.X + obj.SideOffsetMeters * SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter;
                    double ty = rp.Y - obj.HeightOffsetMeters * SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter;
                    double tw = sz * 0.22;
                    double th = sz * 0.9;
                    context.FillRectangle(new SolidColorBrush(Color.FromRgb(95, 75, 55)), new Rect(tx - tw / 2, ty + sz * 0.2 - th, tw, th));
                    context.DrawEllipse(GetCanopy(theme), null, new Point(tx, ty + sz * 0.2 - th + sz * 0.5 * 0.7), sz * 0.5, sz * 0.5);
                }
                else
                {
                    double grade = _engine.Terrain.GetGradeAt(obj.Distance);
                    double ang = Math.Atan(grade / 100.0) * (180 / Math.PI);
                    double rad = ang * (Math.PI / 180);
                    var norm = new Point(-Math.Sin(rad), -Math.Cos(rad));
                    double off = 10 + obj.HeightOffsetMeters * SimulationEngine<Bitmap, Bitmap>.PixelsPerMeter;
                    double ox = rp.X + norm.X * off;
                    double oy = rp.Y + norm.Y * off;

                    using (context.PushTransform(Matrix.CreateRotation(-rad) * Matrix.CreateTranslation(ox, oy)))
                    {
                        if (_engine.BushSprites.Count > 0)
                        {
                            var s = _engine.BushSprites[obj.SpriteIndex % _engine.BushSprites.Count];
                            double w = Math.Max(28, sz * 1.6);
                            double h = w * (s.Size.Height / s.Size.Width);
                            // Using 0.5 sink factor to ensure bushes stay on ground during downhill
                            context.DrawImage(s, new Rect(0,0,s.Size.Width, s.Size.Height), new Rect(-w / 2, -h + h * 0.5, w, h));
                        }
                    }
                }
            }
        }

        private IBrush GetCanopy(BackgroundTheme t) => t switch {
            BackgroundTheme.Mountain => MountainTreeCanopy,
            BackgroundTheme.Desert => DesertTreeCanopy,
            BackgroundTheme.Ocean => OceanTreeCanopy,
            _ => PlainTreeCanopy
        };

        private void DrawTransitionParticles(DrawingContext context)
        {
            if (_engine.TransitionIntensity <= 0) return;
            foreach (var p in _engine.TransitionParticles)
            {
                double alpha = (p.Life / p.MaxLife) * _engine.TransitionIntensity;
                if (alpha <= 0) continue;
                using (context.PushOpacity(alpha * 0.85))
                    context.DrawEllipse(GetPartBrush(p.Theme), null, new Point(p.Position.X, p.Position.Y), p.Size, p.Size * 0.6);
            }
        }

        private IBrush GetPartBrush(BackgroundTheme t) => t switch {
            BackgroundTheme.Mountain => MountainParticleBrush,
            BackgroundTheme.Desert => DesertParticleBrush,
            BackgroundTheme.Ocean => OceanParticleBrush,
            _ => PlainParticleBrush
        };

        private void DrawBiomeLabel(DrawingContext context)
        {
            if (_engine.BiomeLabelTimer <= 0) return;
            string txt = SimulationMath.GetBiomeLabelText(_engine.BiomeLabelTheme);
            double alpha = Math.Min(1.0, _engine.BiomeLabelTimer / 0.35); // Simple fade
            using (context.PushOpacity(alpha))
            {
                var ft = new FormattedText(txt, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 24, Brushes.White);
                double x = (_engine.ActualWidth - ft.Width) / 2;
                double y = _engine.ActualHeight * 0.1;
                context.FillRectangle(new SolidColorBrush(Color.Parse("#AA000000")), new Rect(x - 10, y - 5, ft.Width + 20, ft.Height + 10));
                context.DrawText(ft, new Point(x, y));
            }
        }
    }
}
