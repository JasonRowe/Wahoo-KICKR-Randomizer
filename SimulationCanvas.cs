using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;

namespace BikeFitnessApp
{
    public class SimulationCanvas : FrameworkElement
    {
        private readonly VisualCollection _children;
        private readonly DrawingVisual _drawingVisual;
        private readonly Stopwatch _gameTimer = new Stopwatch();
        private double _lastTickElapsed;
        
        private readonly SimulationEngine<BitmapSource, BitmapSource> _engine = new SimulationEngine<BitmapSource, BitmapSource>();

        // Pedal-cycle sprite sheet (prototype). Null unless PedalSheetSource is set, so the
        // main app is unaffected.
        private BitmapSource? _pedalSheet;
        private readonly List<BitmapSource> _pedalFrames = new List<BitmapSource>();
        private int _lastPedalFrameIndex = -1;

        // Wheel hub positions in sheet-cell pixel coordinates (measured from the sheet; verify
        // against the art with the PedalHubMarker overlay). Cell is 290x322; wheels bottom out
        // around y=273-278 (see PedalAnimation.WheelBottomY) and are ~108px across.
        private static readonly Point FrontHubCell = new Point(212, 222);
        private static readonly Point RearHubCell = new Point(78, 222);
        private const double WheelRadiusCell = 54.0;

        // Pens & Brushes (Keep in WPF)
        private static readonly Brush GrassBrush;
        private static readonly Pen PathPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 135, 100)), 10);
        private static readonly Pen RoadsideOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(40, 50, 40)), 1);
        private static readonly RoadsidePalette MountainPalette;
        private static readonly RoadsidePalette PlainPalette;
        private static readonly RoadsidePalette DesertPalette;
        private static readonly RoadsidePalette OceanPalette;
        private static readonly Brush MountainTreeCanopy;
        private static readonly Brush PlainTreeCanopy;
        private static readonly Brush DesertTreeCanopy;
        private static readonly Brush OceanTreeCanopy;
        private static readonly Brush MountainParticleBrush;
        private static readonly Brush PlainParticleBrush;
        private static readonly Brush DesertParticleBrush;
        private static readonly Brush OceanParticleBrush;
        private static readonly Pen WheelSpokePen;
        private static readonly Pen WheelRimPen;
        private static readonly Pen HubMarkerPen;
        private static readonly Brush WheelHubBrush;

        // Second-rider (POC) chrome. Created through factories so the existing static constructor
        // stays untouched; all frozen, so nothing is allocated per frame.
        private static readonly Brush GhostChipBrush = CreateGhostChipBrush();
        private static readonly Brush GhostChipTextBrush = CreateGhostChipTextBrush();
        private static readonly Pen GhostChipPen = CreateGhostChipPen();

        // Gap-strip chrome (POC #2).
        private static readonly Brush GapStripBandBrush = CreateSolid(Color.FromArgb(90, 90, 220, 120));
        private static readonly Brush GapStripRiderBrush = CreateSolid(Color.FromArgb(235, 245, 250, 255));
        private static readonly Brush GapStripRivalBrush = CreateSolid(Color.FromArgb(240, 255, 176, 60));

        static SimulationCanvas()
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(185, 200, 130), 0.0)); 
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(100, 130, 80), 1.0));  
            gradient.Freeze();
            GrassBrush = gradient;

            PathPen.Freeze();
            RoadsideOutlinePen.Freeze();

            var trunk = new SolidColorBrush(Color.FromRgb(95, 75, 55));
            trunk.Freeze();

            var mountainShrub = new SolidColorBrush(Color.FromRgb(76, 120, 70));
            mountainShrub.Freeze();
            var mountainTree = new SolidColorBrush(Color.FromRgb(70, 110, 60));
            mountainTree.Freeze();
            var mountainRock = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            mountainRock.Freeze();
            MountainPalette = new RoadsidePalette(mountainShrub, mountainTree, mountainRock, trunk);

            var plainShrub = new SolidColorBrush(Color.FromRgb(90, 140, 80));
            plainShrub.Freeze();
            var plainTree = new SolidColorBrush(Color.FromRgb(80, 130, 70));
            plainTree.Freeze();
            var plainRock = new SolidColorBrush(Color.FromRgb(130, 120, 110));
            plainRock.Freeze();
            PlainPalette = new RoadsidePalette(plainShrub, plainTree, plainRock, trunk);

            var desertShrub = new SolidColorBrush(Color.FromRgb(150, 170, 110));
            desertShrub.Freeze();
            var desertTree = new SolidColorBrush(Color.FromRgb(125, 150, 100));
            desertTree.Freeze();
            var desertRock = new SolidColorBrush(Color.FromRgb(160, 150, 130));
            desertRock.Freeze();
            DesertPalette = new RoadsidePalette(desertShrub, desertTree, desertRock, trunk);

            var oceanShrub = new SolidColorBrush(Color.FromRgb(70, 130, 110));
            oceanShrub.Freeze();
            var oceanTree = new SolidColorBrush(Color.FromRgb(60, 120, 100));
            oceanTree.Freeze();
            var oceanRock = new SolidColorBrush(Color.FromRgb(110, 130, 140));
            oceanRock.Freeze();
            OceanPalette = new RoadsidePalette(oceanShrub, oceanTree, oceanRock, trunk);

            MountainTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(120, 165, 105), Color.FromRgb(60, 95, 55));
            PlainTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(140, 190, 125), Color.FromRgb(80, 125, 70));
            DesertTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(170, 200, 135), Color.FromRgb(120, 150, 95));
            OceanTreeCanopy = CreateTreeCanopyBrush(Color.FromRgb(105, 175, 150), Color.FromRgb(60, 110, 90));

            var mountainParticle = new SolidColorBrush(Color.FromRgb(235, 245, 255));
            mountainParticle.Freeze();
            MountainParticleBrush = mountainParticle;

            var plainParticle = new SolidColorBrush(Color.FromRgb(250, 245, 210));
            plainParticle.Freeze();
            PlainParticleBrush = plainParticle;

            var desertParticle = new SolidColorBrush(Color.FromRgb(245, 220, 170));
            desertParticle.Freeze();
            DesertParticleBrush = desertParticle;

            var oceanParticle = new SolidColorBrush(Color.FromRgb(210, 240, 250));
            oceanParticle.Freeze();
            OceanParticleBrush = oceanParticle;

            var wheelSpoke = new SolidColorBrush(Color.FromArgb(190, 25, 25, 25));
            wheelSpoke.Freeze();
            WheelSpokePen = new Pen(wheelSpoke, 2.0);
            WheelSpokePen.Freeze();

            var wheelRim = new SolidColorBrush(Color.FromArgb(190, 25, 25, 25));
            wheelRim.Freeze();
            WheelRimPen = new Pen(wheelRim, 3.0);
            WheelRimPen.Freeze();

            var hubMarker = new SolidColorBrush(Color.FromArgb(255, 255, 60, 60));
            hubMarker.Freeze();
            HubMarkerPen = new Pen(hubMarker, 2.0);
            HubMarkerPen.Freeze();

            var wheelHub = new SolidColorBrush(Color.FromArgb(220, 25, 25, 25));
            wheelHub.Freeze();
            WheelHubBrush = wheelHub;
        }

        private enum RoadsideDrawPass
        {
            Background,
            Foreground
        }

        #region Dependency Properties

        public static readonly DependencyProperty SpeedKphProperty =
            DependencyProperty.Register(nameof(SpeedKph), typeof(double), typeof(SimulationCanvas), 
                new PropertyMetadata(0.0, OnSpeedKphChanged));

        public double SpeedKph
        {
            get => (double)GetValue(SpeedKphProperty);
            set => SetValue(SpeedKphProperty, value);
        }

        private static void OnSpeedKphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimulationCanvas canvas)
            {
                canvas._engine.SpeedKph = (double)e.NewValue;
            }
        }

        public static readonly DependencyProperty SyncedDistanceMetersProperty =
            DependencyProperty.Register(nameof(SyncedDistanceMeters), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(0.0, OnSyncedDistanceChanged));

        public double SyncedDistanceMeters
        {
            get => (double)GetValue(SyncedDistanceMetersProperty);
            set => SetValue(SyncedDistanceMetersProperty, value);
        }

        public static readonly DependencyProperty GradePercentProperty =
            DependencyProperty.Register(nameof(GradePercent), typeof(double), typeof(SimulationCanvas), 
                new PropertyMetadata(0.0, OnGradeChanged));

        public double GradePercent
        {
            get => (double)GetValue(GradePercentProperty);
            set => SetValue(GradePercentProperty, value);
        }

        private static void OnSyncedDistanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimulationCanvas canvas)
            {
                double newVal = (double)e.NewValue;
                if (canvas._engine.TotalDistanceMeters < 5.0 && newVal > 5.0)
                {
                    canvas._engine.Reset(newVal);
                }
            }
        }

        private static void OnGradeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimulationCanvas canvas)
            {
                canvas._engine.RecordGradeChange((double)e.NewValue);
            }
        }

        public static readonly DependencyProperty PedalSheetSourceProperty =
            DependencyProperty.Register(nameof(PedalSheetSource), typeof(string), typeof(SimulationCanvas),
                new PropertyMetadata(string.Empty, OnPedalSheetSourceChanged));

        public string PedalSheetSource
        {
            get => (string)GetValue(PedalSheetSourceProperty);
            set => SetValue(PedalSheetSourceProperty, value);
        }

        private static void OnPedalSheetSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimulationCanvas canvas) canvas.LoadPedalSheet();
        }

        public static readonly DependencyProperty PedalMetersPerRevolutionProperty =
            DependencyProperty.Register(nameof(PedalMetersPerRevolution), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(PedalAnimation.DefaultMetersPerRevolution));

        public double PedalMetersPerRevolution
        {
            get => (double)GetValue(PedalMetersPerRevolutionProperty);
            set => SetValue(PedalMetersPerRevolutionProperty, value);
        }

        public static readonly DependencyProperty PedalFrameOverrideProperty =
            DependencyProperty.Register(nameof(PedalFrameOverride), typeof(int), typeof(SimulationCanvas),
                new PropertyMetadata(-1));

        public int PedalFrameOverride
        {
            get => (int)GetValue(PedalFrameOverrideProperty);
            set => SetValue(PedalFrameOverrideProperty, value);
        }

        public static readonly DependencyProperty PedalSeamModeProperty =
            DependencyProperty.Register(nameof(PedalSeamMode), typeof(SeamMode), typeof(SimulationCanvas),
                new PropertyMetadata(SeamMode.Straight));

        public SeamMode PedalSeamMode
        {
            get => (SeamMode)GetValue(PedalSeamModeProperty);
            set => SetValue(PedalSeamModeProperty, value);
        }

        public static readonly DependencyProperty PedalBlendAlphaProperty =
            DependencyProperty.Register(nameof(PedalBlendAlpha), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(0.0));

        public double PedalBlendAlpha
        {
            get => (double)GetValue(PedalBlendAlphaProperty);
            set => SetValue(PedalBlendAlphaProperty, value);
        }

        public static readonly DependencyProperty PedalWheelSpinProperty =
            DependencyProperty.Register(nameof(PedalWheelSpin), typeof(bool), typeof(SimulationCanvas),
                new PropertyMetadata(false));

        public bool PedalWheelSpin
        {
            get => (bool)GetValue(PedalWheelSpinProperty);
            set => SetValue(PedalWheelSpinProperty, value);
        }

        public static readonly DependencyProperty PedalHubMarkerProperty =
            DependencyProperty.Register(nameof(PedalHubMarker), typeof(bool), typeof(SimulationCanvas),
                new PropertyMetadata(false));

        public bool PedalHubMarker
        {
            get => (bool)GetValue(PedalHubMarkerProperty);
            set => SetValue(PedalHubMarkerProperty, value);
        }

        public static readonly DependencyProperty PedalDrawSizePxProperty =
            DependencyProperty.Register(nameof(PedalDrawSizePx), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(PedalAnimation.DefaultDrawWidthPx));

        public double PedalDrawSizePx
        {
            get => (double)GetValue(PedalDrawSizePxProperty);
            set => SetValue(PedalDrawSizePxProperty, value);
        }

        // --- Second rider / ghost (POC #1) -------------------------------------------------
        // All default-off / default-to-today's-behaviour, so with the ghost flag off the render
        // output is byte-for-byte what it was before this feature existed.

        public static readonly DependencyProperty GhostEnabledProperty =
            DependencyProperty.Register(nameof(GhostEnabled), typeof(bool), typeof(SimulationCanvas),
                new PropertyMetadata(false));

        /// <summary>Master flag for the second-rider (ghost) draw path. Off by default.</summary>
        public bool GhostEnabled
        {
            get => (bool)GetValue(GhostEnabledProperty);
            set => SetValue(GhostEnabledProperty, value);
        }

        public static readonly DependencyProperty GhostDistanceMetersProperty =
            DependencyProperty.Register(nameof(GhostDistanceMeters), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(0.0));

        /// <summary>
        /// Ghost's cumulative distance. The harness owns the replay and pushes this every frame; the
        /// canvas never does replay maths (see POC doctrine: logic in BikeFitness.Shared).
        /// </summary>
        public double GhostDistanceMeters
        {
            get => (double)GetValue(GhostDistanceMetersProperty);
            set => SetValue(GhostDistanceMetersProperty, value);
        }

        public static readonly DependencyProperty GhostSpeedKphProperty =
            DependencyProperty.Register(nameof(GhostSpeedKph), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(0.0));

        /// <summary>Ghost's reported speed, for the marker/HUD delta only.</summary>
        public double GhostSpeedKph
        {
            get => (double)GetValue(GhostSpeedKphProperty);
            set => SetValue(GhostSpeedKphProperty, value);
        }

        public static readonly DependencyProperty GhostOpacityProperty =
            DependencyProperty.Register(nameof(GhostOpacity), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(0.4));

        /// <summary>Sprite opacity for the ghost (0.2–0.7 in the harness).</summary>
        public double GhostOpacity
        {
            get => (double)GetValue(GhostOpacityProperty);
            set => SetValue(GhostOpacityProperty, value);
        }

        public static readonly DependencyProperty GhostShowMarkerProperty =
            DependencyProperty.Register(nameof(GhostShowMarker), typeof(bool), typeof(SimulationCanvas),
                new PropertyMetadata(true));

        /// <summary>Draw the off-screen gap chip when the ghost is outside the visible road window.</summary>
        public bool GhostShowMarker
        {
            get => (bool)GetValue(GhostShowMarkerProperty);
            set => SetValue(GhostShowMarkerProperty, value);
        }

        public static readonly DependencyProperty GhostShowHudProperty =
            DependencyProperty.Register(nameof(GhostShowHud), typeof(bool), typeof(SimulationCanvas),
                new PropertyMetadata(true));

        /// <summary>Draw the in-canvas GAP / Δ readout.</summary>
        public bool GhostShowHud
        {
            get => (bool)GetValue(GhostShowHudProperty);
            set => SetValue(GhostShowHudProperty, value);
        }

        // --- Shared second-rider chrome (POC #2 pacer reuses the sprite/marker/HUD above) -------------

        public static readonly DependencyProperty SecondRiderLabelProperty =
            DependencyProperty.Register(nameof(SecondRiderLabel), typeof(string), typeof(SimulationCanvas),
                new PropertyMetadata("ghost"));

        /// <summary>What to call the rival in the HUD ("ghost" for POC 1, "pacer" for POC 2).</summary>
        public string SecondRiderLabel
        {
            get => (string)GetValue(SecondRiderLabelProperty);
            set => SetValue(SecondRiderLabelProperty, value);
        }

        public static readonly DependencyProperty SecondRiderGapStripProperty =
            DependencyProperty.Register(nameof(SecondRiderGapStrip), typeof(bool), typeof(SimulationCanvas),
                new PropertyMetadata(false));

        /// <summary>
        /// Compressed gap strip under the canvas. Not optional for a pacer holding a 10–40 m band: only
        /// ~12.3 m ahead is visible at 50 px/m, so the band is otherwise off screen.
        /// </summary>
        public bool SecondRiderGapStrip
        {
            get => (bool)GetValue(SecondRiderGapStripProperty);
            set => SetValue(SecondRiderGapStripProperty, value);
        }

        public static readonly DependencyProperty SecondRiderGapTargetMetersProperty =
            DependencyProperty.Register(nameof(SecondRiderGapTargetMeters), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(25.0));

        /// <summary>Gap the rival is trying to hold — drawn as the target band on the strip.</summary>
        public double SecondRiderGapTargetMeters
        {
            get => (double)GetValue(SecondRiderGapTargetMetersProperty);
            set => SetValue(SecondRiderGapTargetMetersProperty, value);
        }

        public static readonly DependencyProperty SecondRiderGapBandMetersProperty =
            DependencyProperty.Register(nameof(SecondRiderGapBandMeters), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(15.0));

        /// <summary>Half-width of the rival's band, drawn either side of the target.</summary>
        public double SecondRiderGapBandMeters
        {
            get => (double)GetValue(SecondRiderGapBandMetersProperty);
            set => SetValue(SecondRiderGapBandMetersProperty, value);
        }

        public static readonly DependencyProperty GapStripBottomInsetProperty =
            DependencyProperty.Register(nameof(GapStripBottomInset), typeof(double), typeof(SimulationCanvas),
                new PropertyMetadata(30.0));

        /// <summary>
        /// How far above the bottom of the canvas the gap strip is drawn, in pixels. The strip is the only
        /// instrument that shows a rival behind the rider, so it must not be drawn where the telemetry row
        /// is: the view sets this to the telemetry row's height (see <c>WorkoutView</c>). Defaults to the
        /// old fixed position for the harness, which has no telemetry row.
        /// </summary>
        public double GapStripBottomInset
        {
            get => (double)GetValue(GapStripBottomInsetProperty);
            set => SetValue(GapStripBottomInsetProperty, value);
        }

        #endregion

        /// <summary>
        /// Raised once per rendered frame, after the simulation engine has advanced and before the frame
        /// is drawn. Exists so the harness can drive a second rider (ghost / pacer / scoring) on exactly
        /// the same clock as the scene instead of a parallel timer, which would make the rival jitter
        /// against the road.
        /// </summary>
        public event EventHandler<SimulationFrameEventArgs>? FrameRendered;

        public SimulationCanvas()
        {
            _children = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _children.Add(_drawingVisual);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += (s, e) => {
                _engine.ActualWidth = ActualWidth;
                _engine.ActualHeight = ActualHeight;
            };
        }

        private void LoadAssets()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var imagesDir = Path.Combine(baseDir, "Images");
                Log($"Loading assets from: {imagesDir}");

                _engine.CyclistSprite = LoadBitmap(Path.Combine(imagesDir, "cyclist_sprite.png"), "Cyclist");

                var mountain = LoadBitmap(Path.Combine(imagesDir, "biome_mountain.png"), "Biome Mountain");
                var plain = LoadBitmap(Path.Combine(imagesDir, "biome_plain.png"), "Biome Plain");
                var desert = LoadBitmap(Path.Combine(imagesDir, "biome_desert.png"), "Biome Desert");
                var ocean = LoadBitmap(Path.Combine(imagesDir, "biome_ocean.png"), "Biome Ocean");

                var transitionMountainPlain = LoadBitmap(Path.Combine(imagesDir, "transition_mountain_plain.png"), "Transition Mountain->Plain");
                var transitionPlainDesert = LoadBitmap(Path.Combine(imagesDir, "transition_plain_desert.png"), "Transition Plain->Desert");
                var transitionDesertOcean = LoadBitmap(Path.Combine(imagesDir, "transition_desert_ocean.png"), "Transition Desert->Ocean");
                var transitionOceanMountain = LoadBitmap(Path.Combine(imagesDir, "transition_ocean_mountain.png"), "Transition Ocean->Mountain");

                _engine.ClearBackgroundSegments();
                _engine.AddBackgroundSegment("Mountain", BackgroundTheme.Mountain, mountain!, SimulationEngine<BitmapSource, BitmapSource>.BiomeSegmentLengthMeters, SimulationEngine<BitmapSource, BitmapSource>.UseMirroredBackgroundTiles);
                _engine.AddBackgroundSegment("Transition Mountain->Plain", BackgroundTheme.Transition, transitionMountainPlain!, SimulationEngine<BitmapSource, BitmapSource>.TransitionSegmentLengthMeters, false);
                _engine.AddBackgroundSegment("Plain", BackgroundTheme.Plain, plain!, SimulationEngine<BitmapSource, BitmapSource>.BiomeSegmentLengthMeters, SimulationEngine<BitmapSource, BitmapSource>.UseMirroredBackgroundTiles);
                _engine.AddBackgroundSegment("Transition Plain->Desert", BackgroundTheme.Transition, transitionPlainDesert!, SimulationEngine<BitmapSource, BitmapSource>.TransitionSegmentLengthMeters, false);
                _engine.AddBackgroundSegment("Desert", BackgroundTheme.Desert, desert!, SimulationEngine<BitmapSource, BitmapSource>.BiomeSegmentLengthMeters, SimulationEngine<BitmapSource, BitmapSource>.UseMirroredBackgroundTiles);
                _engine.AddBackgroundSegment("Transition Desert->Ocean", BackgroundTheme.Transition, transitionDesertOcean!, SimulationEngine<BitmapSource, BitmapSource>.TransitionSegmentLengthMeters, false);
                _engine.AddBackgroundSegment("Ocean", BackgroundTheme.Ocean, ocean!, SimulationEngine<BitmapSource, BitmapSource>.BiomeSegmentLengthMeters, SimulationEngine<BitmapSource, BitmapSource>.UseMirroredBackgroundTiles);
                _engine.AddBackgroundSegment("Transition Ocean->Mountain", BackgroundTheme.Transition, transitionOceanMountain!, SimulationEngine<BitmapSource, BitmapSource>.TransitionSegmentLengthMeters, false);

                _engine.BushSprites.Clear();
                AddBushSprite(LoadBitmap(Path.Combine(imagesDir, "sm_bush.png"), "Small Bush"));
                AddBushSprite(LoadBitmap(Path.Combine(imagesDir, "big_bush.png"), "Big Bush"));
                AddBushSprite(LoadBitmap(Path.Combine(imagesDir, "tall_bush.png"), "Tall Bush"));

                LoadPedalSheet();
            }
            catch (Exception ex)
            {
                Log($"Failed to load assets: {ex.Message}");
            }
        }

        private void AddBushSprite(BitmapSource? sprite)
        {
            if (sprite != null) _engine.BushSprites.Add(sprite);
        }

        private void LoadPedalSheet()
        {
            _pedalFrames.Clear();
            _pedalSheet = null;
            _lastPedalFrameIndex = -1;

            string source = PedalSheetSource;
            if (string.IsNullOrWhiteSpace(source)) return;

            var sheet = LoadBitmap(source, "PedalSheet");
            if (sheet == null)
            {
                Log($"Pedal sheet not found or failed to load: {source}");
                return;
            }

            _pedalSheet = sheet;
            for (int i = 0; i < PedalAnimation.FrameCount; i++)
            {
                var (x, y, w, h) = PedalAnimation.GetSourceRect(i);
                var cropped = new CroppedBitmap(sheet, new Int32Rect(x, y, w, h));
                cropped.Freeze();
                _pedalFrames.Add(cropped);
            }
        }

        private BitmapSource? LoadBitmap(string path, string name)
        {
            if (!File.Exists(path)) return null;
            try 
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            LoadAssets();
            _engine.ActualWidth = ActualWidth;
            _engine.ActualHeight = ActualHeight;
            _engine.Reset(_engine.TotalDistanceMeters);
            _gameTimer.Restart();
            _lastTickElapsed = 0;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            _gameTimer.Stop();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double currentElapsed = _gameTimer.Elapsed.TotalSeconds;
            double deltaTime = currentElapsed - _lastTickElapsed;
            _lastTickElapsed = currentElapsed;

            if (deltaTime > 0.1) deltaTime = 0.1; 
            if (deltaTime <= 0) return;

            _engine.Update(deltaTime);

            // Give subscribers (the harness ghost/pacer panels) a chance to advance their own state on
            // this exact frame before it is drawn.
            FrameRendered?.Invoke(this, new SimulationFrameEventArgs(deltaTime, _engine.TotalDistanceMeters, _engine.SpeedKph));

            DrawFrame();
        }

        private void Log(string message) => Debug.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {message}");

        private Point WorldToScreen(double worldDist, double bikeDist, double bikeHeight, double centerY, double bikeScreenX)
        {
            double worldH = _engine.Terrain.GetHeightAt(worldDist);
            double screenX = bikeScreenX + (worldDist - bikeDist) * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter;
            double screenY = centerY - (worldH - bikeHeight) * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter;
            return new Point(screenX, screenY);
        }

        private void DrawFrame()
        {
            if (ActualWidth == 0 || ActualHeight == 0) return;

            using (DrawingContext dc = _drawingVisual.RenderOpen())
            {
                var bgInfo = _engine.GetBackgroundSegmentInfo(_engine.TotalDistanceMeters);
                DrawBackground(dc, bgInfo);
                DrawTransitionParticles(dc);

                double bikeScreenX = ActualWidth * 0.3;
                double bikeWorldDist = _engine.TotalDistanceMeters;
                double bikeWorldHeight = _engine.Terrain.GetHeightAt(bikeWorldDist);
                double visualCenterY = ActualHeight * 0.75;

                double leftWorldDist = bikeWorldDist - (bikeScreenX / SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter) - 5;
                double rightWorldDist = bikeWorldDist + ((ActualWidth - bikeScreenX) / SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter) + 5;

                var geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    Point startP = WorldToScreen(leftWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                    ctx.BeginFigure(startP, true, true); 

                    foreach (var v in _engine.Terrain.History)
                    {
                        if (v.Distance > leftWorldDist && v.Distance < rightWorldDist)
                            ctx.LineTo(WorldToScreen(v.Distance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX), true, false);
                    }

                    Point endP = WorldToScreen(rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                    ctx.LineTo(endP, true, false);
                    ctx.LineTo(new Point(endP.X, ActualHeight), true, false);
                    ctx.LineTo(new Point(startP.X, ActualHeight), true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(GrassBrush, null, geometry);

                var pathGeometry = new StreamGeometry();
                using (StreamGeometryContext ctx = pathGeometry.Open())
                {
                    Point startP = WorldToScreen(leftWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                    ctx.BeginFigure(startP, false, false); 

                    foreach (var v in _engine.Terrain.History)
                    {
                        if (v.Distance > leftWorldDist && v.Distance < rightWorldDist)
                            ctx.LineTo(WorldToScreen(v.Distance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX), true, false);
                    }
                    ctx.LineTo(WorldToScreen(rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX), true, false);
                }
                pathGeometry.Freeze();
                dc.DrawGeometry(null, PathPen, pathGeometry);

                var roadsideTheme = bgInfo.Segment?.Theme ?? BackgroundTheme.Plain;
                if (roadsideTheme == BackgroundTheme.Transition && bgInfo.NextSegment != null)
                    roadsideTheme = bgInfo.NextSegment.Theme;

                DrawRoadsideObjects(dc, leftWorldDist, rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX, roadsideTheme, RoadsideDrawPass.Background);

                // POC: second rider gets its own translate/rotate so it sits on its own terrain height and
                // tilts with the slope at its own distance, then the existing cyclist draws on top.
                if (GhostEnabled)
                {
                    DrawSecondRider(dc, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                }

                dc.PushTransform(new TranslateTransform(bikeScreenX, visualCenterY));
                dc.PushTransform(new RotateTransform(-_engine.CurrentSlopeAngle)); 

                DrawCyclist(dc);
                
                dc.Pop();
                dc.Pop();

                DrawRoadsideObjects(dc, leftWorldDist, rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX, roadsideTheme, RoadsideDrawPass.Foreground);

                if (GhostEnabled)
                {
                    // Outside the transform stack: the marker pins to the canvas edge, not to the road.
                    if (GhostShowMarker) DrawGhostMarker(dc);
                    if (GhostShowHud) DrawGhostHud(dc);
                }

                if (SecondRiderGapStrip) DrawGapStrip(dc);

                DrawBiomeLabel(dc);
            }
        }

        private void DrawCyclist(DrawingContext dc)
        {
            if (_pedalSheet != null && _pedalFrames.Count == PedalAnimation.FrameCount)
            {
                DrawPedalCyclist(dc);
                return;
            }

            if (_engine.CyclistSprite != null)
                dc.DrawImage(_engine.CyclistSprite, new Rect(-75, -130, 150, 150));
            else
                dc.DrawRectangle(Brushes.Red, new Pen(Brushes.Black, 2), new Rect(-25, -40, 50, 40));
        }

        private void DrawPedalCyclist(DrawingContext dc)
        {
            double phase = PedalAnimation.GetCrankPhase(_engine.TotalDistanceMeters, PedalMetersPerRevolution);
            int overlayFrame = 0;

            if (PedalFrameOverride >= 0)
            {
                _lastPedalFrameIndex = -1;
                int index = Math.Clamp(PedalFrameOverride, 0, PedalAnimation.FrameCount - 1);
                DrawSheetFrame(dc, index, 1.0);
                overlayFrame = index;
            }
            else if (PedalSeamMode == SeamMode.CrossFade)
            {
                double fadeWindow = PedalAnimation.GetCrossFadeWindowPhase(_engine.SpeedKph, PedalMetersPerRevolution);
                double t = PedalAnimation.GetCrossFadeFactor(phase, fadeWindow);
                if (t > 0)
                {
                    _lastPedalFrameIndex = -1;
                    DrawSheetFrame(dc, PedalAnimation.FrameCount - 1, 1.0 - t);
                    DrawSheetFrame(dc, 0, t);
                }
                else
                {
                    overlayFrame = PedalAnimation.GetFrameIndex(phase, PedalSeamMode);
                    DrawCurrentFrameWithBlend(dc, overlayFrame);
                }
            }
            else
            {
                overlayFrame = PedalAnimation.GetFrameIndex(phase, PedalSeamMode);
                DrawCurrentFrameWithBlend(dc, overlayFrame);
            }

            DrawWheelOverlays(dc, overlayFrame);
        }

        private void DrawCurrentFrameWithBlend(DrawingContext dc, int currentIndex)
        {
            double blend = PedalBlendAlpha;
            if (blend > 0 && _lastPedalFrameIndex >= 0 && _lastPedalFrameIndex != currentIndex)
            {
                // Moving average to damp frame "boil": previous frame full, current on top at blend alpha.
                DrawSheetFrame(dc, _lastPedalFrameIndex, 1.0);
                DrawSheetFrame(dc, currentIndex, blend);
            }
            else
            {
                DrawSheetFrame(dc, currentIndex, 1.0);
            }
            _lastPedalFrameIndex = currentIndex;
        }

        private void DrawSheetFrame(DrawingContext dc, int frameIndex, double opacity)
        {
            if (frameIndex < 0 || frameIndex >= _pedalFrames.Count) return;

            Rect dest = GetFrameDest(frameIndex);
            if (opacity < 1.0) dc.PushOpacity(opacity);
            dc.DrawImage(_pedalFrames[frameIndex], dest);
            if (opacity < 1.0) dc.Pop();
        }

        private double GetPedalScale()
        {
            return PedalAnimation.GetDrawScale(PedalDrawSizePx);
        }

        private Rect GetFrameDest(int frameIndex)
        {
            // Shared with the Avalonia canvas so both apps ground-align frames identically.
            var (x, y, width, height) = PedalAnimation.GetFrameDestRect(frameIndex, PedalDrawSizePx);
            return new Rect(x, y, width, height);
        }

        private void DrawWheelOverlays(DrawingContext dc, int frameIndex)
        {
            if (!PedalWheelSpin && !PedalHubMarker) return;

            double scale = GetPedalScale();
            Rect dest = GetFrameDest(frameIndex);
            double angleDeg = (_engine.TotalDistanceMeters / PedalAnimation.WheelCircumferenceMeters) * 360.0;
            DrawWheelOverlay(dc, dest, scale, FrontHubCell, WheelRadiusCell, angleDeg);
            DrawWheelOverlay(dc, dest, scale, RearHubCell, WheelRadiusCell, angleDeg);
        }

        private void DrawWheelOverlay(DrawingContext dc, Rect dest, double scale, Point hubCell, double radiusCell, double angleDeg)
        {
            var hub = new Point(dest.X + (hubCell.X * scale), dest.Y + (hubCell.Y * scale));
            double radius = radiusCell * scale;

            if (PedalHubMarker)
            {
                dc.DrawEllipse(null, HubMarkerPen, hub, radius, radius);
                dc.DrawLine(HubMarkerPen, new Point(hub.X - radius - 6, hub.Y), new Point(hub.X + radius + 6, hub.Y));
                dc.DrawLine(HubMarkerPen, new Point(hub.X, hub.Y - radius - 6), new Point(hub.X, hub.Y + radius + 6));
            }

            if (!PedalWheelSpin) return;

            dc.PushClip(new EllipseGeometry(hub, radius, radius));
            dc.DrawEllipse(null, WheelRimPen, hub, radius, radius);
            for (int i = 0; i < 6; i++)
            {
                double a = (angleDeg + (i * 60.0)) * (Math.PI / 180.0);
                var dir = new Vector(Math.Cos(a), Math.Sin(a));
                dc.DrawLine(WheelSpokePen, hub - (dir * radius), hub + (dir * radius));
            }
            dc.Pop();
            dc.DrawEllipse(WheelHubBrush, null, hub, radius * 0.14, radius * 0.14);
        }

        // --- Second rider / ghost (POC #1) -------------------------------------------------
        // Additive: two call sites in DrawFrame plus these methods. Nothing above is re-ordered.

        /// <summary>
        /// Draws the ghost sprite on its own terrain height, tilted with the slope at its own distance and
        /// animated from its own travelled distance. Must be called outside the player's transform stack.
        /// </summary>
        private void DrawSecondRider(DrawingContext dc, double bikeWorldDist, double bikeWorldHeight, double visualCenterY, double bikeScreenX)
        {
            double ghostDistance = GhostDistanceMeters;
            Point ground = WorldToScreen(ghostDistance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);

            double ghostGrade = _engine.Terrain.GetGradeAt(ghostDistance);
            double ghostSlopeDegrees = Math.Atan(ghostGrade / 100.0) * (180.0 / Math.PI);

            dc.PushOpacity(Math.Clamp(GhostOpacity, 0.05, 1.0));
            dc.PushTransform(new TranslateTransform(ground.X, ground.Y));
            dc.PushTransform(new RotateTransform(-ghostSlopeDegrees));

            if (_pedalSheet != null && _pedalFrames.Count == PedalAnimation.FrameCount)
            {
                // Same frame-indexing path as the player, driven by the ghost's own distance so its legs
                // turn at its own speed. The temporal-blend and wheel-spin overlays are intentionally
                // skipped: they are per-rider debug affordances that use the player's frame state.
                int frame = PedalFrameOverride >= 0
                    ? Math.Clamp(PedalFrameOverride, 0, PedalAnimation.FrameCount - 1)
                    : PedalAnimation.GetFrameIndexForDistance(ghostDistance, PedalMetersPerRevolution);
                DrawSheetFrame(dc, frame, 1.0);
            }
            else if (_engine.CyclistSprite != null)
            {
                dc.DrawImage(_engine.CyclistSprite, new Rect(-75, -130, 150, 150));
            }
            else
            {
                dc.DrawRectangle(Brushes.Red, null, new Rect(-25, -40, 50, 40));
            }

            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        /// <summary>
        /// Off-screen gap chip. Mandatory, not decorative: at 50 px/m only ~12.3 m ahead is visible on the
        /// harness canvas, so a rival 20 m up the road would otherwise vanish (spec §6).
        /// </summary>
        private void DrawGhostMarker(DrawingContext dc)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            bool onScreen = SecondRiderGeometry.TryGetScreenPosition(
                _engine.TotalDistanceMeters,
                GhostDistanceMeters,
                ActualWidth,
                ActualHeight,
                SecondRiderGeometry.DefaultBikeScreenRatio,
                SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter,
                out double ghostScreenX,
                out _);

            if (onScreen) return;

            double gap = DuelMath.GapMeters(GhostDistanceMeters, _engine.TotalDistanceMeters);
            double delta = DuelMath.DeltaSeconds(GhostDistanceMeters, _engine.TotalDistanceMeters, _engine.SpeedKph, GhostSpeedKph);
            string label = DuelMath.FormatGapChip(gap, delta);

            double markerX = SecondRiderGeometry.GetMarkerX(ghostScreenX, ActualWidth);
            double centerY = ActualHeight * 0.62;

            var text = new FormattedText(
                label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                20,
                GhostChipTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double padX = 10;
            double padY = 5;
            var chip = new Rect(
                markerX - (text.Width / 2.0) - padX,
                centerY - (text.Height / 2.0) - padY,
                text.Width + (padX * 2.0),
                text.Height + (padY * 2.0));

            dc.DrawRoundedRectangle(GhostChipBrush, GhostChipPen, chip, 6, 6);
            dc.DrawText(text, new Point(chip.X + padX, chip.Y + padY));
        }

        /// <summary>In-canvas GAP / Δ readout — the "legible at a glance" hypothesis, top-left.</summary>
        private void DrawGhostHud(DrawingContext dc)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            double gap = DuelMath.GapMeters(GhostDistanceMeters, _engine.TotalDistanceMeters);
            double delta = DuelMath.DeltaSeconds(GhostDistanceMeters, _engine.TotalDistanceMeters, _engine.SpeedKph, GhostSpeedKph);

            string line1 = $"GAP {DuelMath.FormatGapMeters(gap)}   \u0394 {DuelMath.FormatDelta(delta)}";
            string label = string.IsNullOrWhiteSpace(SecondRiderLabel) ? "rival" : SecondRiderLabel;
            string line2 = $"{label} {GhostSpeedKph:F1} kph   {DuelMath.FormatGapChip(gap, delta)}";

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var big = new FormattedText(line1, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Consolas"), 22, GhostChipTextBrush, pixelsPerDip);
            var small = new FormattedText(line2, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Consolas"), 14, GhostChipTextBrush, pixelsPerDip);

            double x = 16;
            double y = 14;
            double pad = 8;
            double width = Math.Max(big.Width, small.Width) + (pad * 2.0);
            double height = big.Height + small.Height + (pad * 2.0);

            dc.DrawRoundedRectangle(GhostChipBrush, GhostChipPen, new Rect(x, y, width, height), 6, 6);
            dc.DrawText(big, new Point(x + pad, y + pad));
            dc.DrawText(small, new Point(x + pad, y + pad + big.Height));
        }

        /// <summary>
        /// Compressed 0–60 m gap strip: the rival's position on a fixed axis with the target band drawn as a
        /// green window. This is the pacer's primary instrument — at 50 px/m only ~12.3 m ahead is visible, so
        /// a rival holding a 10–40 m band spends most of the ride off screen.
        /// </summary>
        private void DrawGapStrip(DrawingContext dc)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            // Six times as much road behind as in front: the camera shows ~12 m ahead, so anything off the
            // back is only legible here, and being dropped is the case the rider actually has to diagnose.
            const double metersBehind = 60.0;
            const double metersAhead = 70.0;

            double left = 40;
            double right = ActualWidth - 40;
            if (right - left < 80) return;

            double height = 12;
            double top = ActualHeight - Math.Max(12.0, GapStripBottomInset);
            double span = metersBehind + metersAhead;

            double Map(double gapMeters) => left + (((gapMeters + metersBehind) / span) * (right - left));

            double gap = DuelMath.GapMeters(GhostDistanceMeters, _engine.TotalDistanceMeters);
            double target = SecondRiderGapTargetMeters;
            double band = Math.Max(0, SecondRiderGapBandMeters);

            dc.DrawRoundedRectangle(GhostChipBrush, GhostChipPen, new Rect(left, top, right - left, height), 4, 4);

            double bandLeft = Map(Math.Max(-metersBehind, target - band));
            double bandRight = Map(Math.Min(metersAhead, target + band));
            if (bandRight > bandLeft)
            {
                dc.DrawRectangle(GapStripBandBrush, null, new Rect(bandLeft, top, bandRight - bandLeft, height));
            }

            // You are always the fixed zero point; the rival dot moves. Clamped to the ends when out of range,
            // which reads as "beyond the strip" rather than silently disappearing.
            double riderX = Map(0);
            dc.DrawRectangle(GapStripRiderBrush, null, new Rect(riderX - 1.5, top - 4, 3, height + 8));

            double rivalX = Map(Math.Clamp(gap, -metersBehind, metersAhead));
            dc.DrawEllipse(GapStripRivalBrush, null, new Point(rivalX, top + (height / 2.0)), 5, 5);
        }

        private static Brush CreateSolid(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Brush CreateGhostChipBrush()
        {
            var brush = new SolidColorBrush(Color.FromArgb(205, 12, 14, 18));
            brush.Freeze();
            return brush;
        }

        private static Brush CreateGhostChipTextBrush()
        {
            var brush = new SolidColorBrush(Color.FromArgb(245, 245, 250, 255));
            brush.Freeze();
            return brush;
        }

        private static Pen CreateGhostChipPen()
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.0);
            pen.Freeze();
            return pen;
        }

        private void DrawBackground(DrawingContext dc, SimulationEngine<BitmapSource, BitmapSource>.BackgroundSegmentInfo info)
        {
            dc.DrawRectangle(Brushes.LightSkyBlue, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (info.Segment?.Image == null) return;

            double scrollPx = _engine.TotalDistanceMeters * SimulationEngine<BitmapSource, BitmapSource>.BackgroundPixelsPerMeter;
            double segmentOpacity = _engine.GetBackgroundOpacity(info);
            
            if (info.Segment.Theme == BackgroundTheme.Transition)
            {
                double progress = info.SegmentLength > 0 ? SimulationMath.Clamp01(info.LocalDistance / info.SegmentLength) : 0.0;
                double transitionBlend = 0.0;
                if (info.NextSegment?.Image != null && SimulationEngine<BitmapSource, BitmapSource>.TransitionToBiomeBlendMeters > 0 && info.SegmentLength > 0)
                {
                    double distanceToEnd = info.SegmentLength - info.LocalDistance;
                    if (distanceToEnd < SimulationEngine<BitmapSource, BitmapSource>.TransitionToBiomeBlendMeters)
                    {
                        transitionBlend = 1.0 - (distanceToEnd / SimulationEngine<BitmapSource, BitmapSource>.TransitionToBiomeBlendMeters);
                        transitionBlend = SimulationMath.Clamp01(transitionBlend);
                    }
                }

                DrawTransitionImage(dc, info.Segment.Image, progress, segmentOpacity * (1.0 - transitionBlend));
                if (transitionBlend > 0 && info.NextSegment?.Image != null)
                    DrawTiledImage(dc, info.NextSegment.Image, scrollPx, info.NextSegment.MirrorTiles, segmentOpacity * transitionBlend);

                return;
            }

            DrawTiledImage(dc, info.Segment.Image, scrollPx, info.Segment.MirrorTiles, segmentOpacity * (1.0 - info.BlendToNext));
            if (info.BlendToNext > 0 && info.NextSegment?.Image != null)
                DrawTiledImage(dc, info.NextSegment.Image, scrollPx, info.NextSegment.MirrorTiles, segmentOpacity * info.BlendToNext);
        }

        private void DrawTransitionImage(DrawingContext dc, BitmapSource img, double progress, double opacity)
        {
            if (opacity <= 0 || img.PixelHeight <= 0 || img.PixelWidth <= 0) return;

            double scale = Math.Max(ActualHeight / img.PixelHeight, ActualWidth / img.PixelWidth);
            if (scale <= 0 || double.IsInfinity(scale) || double.IsNaN(scale)) return;

            double drawWidth = img.PixelWidth * scale;
            double drawHeight = img.PixelHeight * scale;
            double maxScroll = Math.Max(0, drawWidth - ActualWidth);
            double offsetX = -maxScroll * SimulationMath.Clamp01(progress);
            double offsetY = ActualHeight - drawHeight;

            if (opacity < 1.0) dc.PushOpacity(opacity);
            dc.DrawImage(img, new Rect(offsetX, offsetY, drawWidth, drawHeight));
            if (opacity < 1.0) dc.Pop();
        }

        private void DrawTiledImage(DrawingContext dc, BitmapSource img, double scrollPx, bool mirrorTiles, double opacity)
        {
            if (opacity <= 0 || img.PixelHeight <= 0) return;

            double tileScale = ActualHeight / img.PixelHeight;
            if (tileScale <= 0 || double.IsInfinity(tileScale) || double.IsNaN(tileScale)) return;

            double tileWidth = Math.Round(img.PixelWidth * tileScale);
            if (tileWidth <= 0) return;

            double tileHeight = Math.Round(img.PixelHeight * tileScale);
            double offset = scrollPx % tileWidth;
            if (offset < 0) offset += tileWidth;

            long firstTileIndex = (long)Math.Floor(scrollPx / tileWidth);
            double startX = Math.Floor(-offset);
            double drawWidth = tileWidth + SimulationEngine<BitmapSource, BitmapSource>.BackgroundTileOverlapPx;

            if (opacity < 1.0) dc.PushOpacity(opacity);

            int i = 0;
            while (startX < ActualWidth + tileWidth)
            {
                long currentTileIndex = firstTileIndex + i;
                bool mirror = mirrorTiles && (currentTileIndex % 2 != 0);

                if (mirror)
                {
                    dc.PushTransform(new ScaleTransform(-1, 1, startX + drawWidth / 2.0, 0));
                    dc.DrawImage(img, new Rect(startX, 0, drawWidth, tileHeight));
                    dc.Pop();
                }
                else
                {
                    dc.DrawImage(img, new Rect(startX, 0, drawWidth, tileHeight));
                }

                startX += tileWidth;
                i++;
            }

            if (opacity < 1.0) dc.Pop();
        }

        private void DrawTransitionParticles(DrawingContext dc)
        {
            if (_engine.TransitionParticles.Count == 0 || _engine.TransitionIntensity <= 0) return;

            double intensity = SimulationMath.Clamp01(_engine.TransitionIntensity);

            foreach (var particle in _engine.TransitionParticles)
            {
                double lifeRatio = particle.MaxLife > 0 ? particle.Life / particle.MaxLife : 0;
                double alpha = intensity * SimulationMath.Clamp01(lifeRatio);
                if (alpha <= 0) continue;

                Brush brush = GetTransitionParticleBrush(particle.Theme);
                dc.PushOpacity(alpha * 0.85);
                dc.DrawEllipse(brush, null, new Point(particle.Position.X, particle.Position.Y), particle.Size, particle.Size * 0.6);
                dc.Pop();
            }
        }

        private void DrawBiomeLabel(DrawingContext dc)
        {
            if (_engine.BiomeLabelTimer <= 0) return;

            string label = SimulationMath.GetBiomeLabelText(_engine.BiomeLabelTheme);
            if (string.IsNullOrWhiteSpace(label)) return;

            double duration = SimulationEngine<BitmapSource, BitmapSource>.BiomeLabelDurationSeconds;
            double fade = Math.Min(SimulationEngine<BitmapSource, BitmapSource>.BiomeLabelFadeSeconds, duration * 0.5);
            double elapsed = duration - _engine.BiomeLabelTimer;
            double alpha;

            if (elapsed < fade) alpha = SimulationMath.SmoothStep(elapsed / fade);
            else if (_engine.BiomeLabelTimer < fade) alpha = SimulationMath.SmoothStep(_engine.BiomeLabelTimer / fade);
            else alpha = 1.0;

            if (alpha <= 0) return;

            double fontSize = Math.Max(18, Math.Min(32, ActualWidth * 0.028));
            var text = new FormattedText(
                label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                fontSize,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double x = (ActualWidth - text.Width) / 2.0;
            double y = ActualHeight * SimulationEngine<BitmapSource, BitmapSource>.BiomeLabelTopRatio;
            double padX = fontSize * 0.6;
            double padY = fontSize * 0.35;
            var bgRect = new Rect(x - padX, y - padY, text.Width + (padX * 2.0), text.Height + (padY * 2.0));

            dc.PushOpacity(alpha * 0.65);
            dc.DrawRoundedRectangle(Brushes.Black, null, bgRect, fontSize * 0.35, fontSize * 0.35);
            dc.Pop();

            dc.PushOpacity(alpha);
            dc.DrawText(text, new Point(x, y));
            dc.Pop();
        }

        private void DrawRoadsideObjects(DrawingContext dc, double leftWorldDist, double rightWorldDist, double bikeWorldDist, double bikeWorldHeight, double visualCenterY, double bikeScreenX, BackgroundTheme theme, RoadsideDrawPass pass)
        {
            if (_engine.RoadsideObjects.Count == 0) return;
            var palette = GetPalette(theme);

            foreach (var obj in _engine.RoadsideObjects)
            {
                if (obj.Distance < leftWorldDist || obj.Distance > rightWorldDist) continue;
                if (pass == RoadsideDrawPass.Background && obj.Type == SimulationEngine<BitmapSource, BitmapSource>.RoadsideObjectType.Tree) continue;
                if (pass == RoadsideDrawPass.Foreground && obj.Type != SimulationEngine<BitmapSource, BitmapSource>.RoadsideObjectType.Tree) continue;

                Point roadPoint = WorldToScreen(obj.Distance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                double sizePx = Math.Max(12, obj.SizeMeters * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter);

                switch (obj.Type)
                {
                    case SimulationEngine<BitmapSource, BitmapSource>.RoadsideObjectType.Tree:
                        double objX = roadPoint.X + (obj.SideOffsetMeters * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter);
                        double objY = roadPoint.Y - (obj.HeightOffsetMeters * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter);
                        if (objX < -sizePx || objX > ActualWidth + sizePx) continue;

                        double trunkWidth = sizePx * 0.22;
                        double trunkHeight = sizePx * 0.9;
                        double trunkBaseY = objY + (sizePx * 0.2);
                        dc.DrawRectangle(palette.Trunk, RoadsideOutlinePen, new Rect(objX - trunkWidth / 2.0, trunkBaseY - trunkHeight, trunkWidth, trunkHeight));

                        var canopyBrush = GetTreeCanopyBrush(theme);
                        double canopyRadius = sizePx * 0.5;
                        double canopyCenterY = trunkBaseY - trunkHeight + (canopyRadius * 0.7);
                        dc.DrawEllipse(canopyBrush, null, new Point(objX, canopyCenterY), canopyRadius, canopyRadius);
                        break;
                    case SimulationEngine<BitmapSource, BitmapSource>.RoadsideObjectType.Rock:
                        objX = roadPoint.X + (obj.SideOffsetMeters * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter);
                        objY = roadPoint.Y - (obj.HeightOffsetMeters * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter);
                        if (objX < -sizePx || objX > ActualWidth + sizePx) continue;
                        dc.DrawEllipse(palette.Rock, RoadsideOutlinePen, new Point(objX, objY), sizePx * 0.45, sizePx * 0.3);
                        break;
                    default:
                        double grade = _engine.Terrain.GetGradeAt(obj.Distance);
                        double slopeAngle = Math.Atan(grade / 100.0) * (180.0 / Math.PI);
                        double slopeRadians = slopeAngle * (Math.PI / 180.0);
                        var normal = new Vector(-Math.Sin(slopeRadians), -Math.Cos(slopeRadians));
                        double offsetPx = (PathPen.Thickness * 0.5) + (obj.HeightOffsetMeters * SimulationEngine<BitmapSource, BitmapSource>.PixelsPerMeter);

                        double baseX = roadPoint.X + (normal.X * offsetPx);
                        double baseY = roadPoint.Y + (normal.Y * offsetPx);

                        if (_engine.BushSprites.Count > 0)
                        {
                            var sprite = _engine.BushSprites[obj.SpriteIndex % _engine.BushSprites.Count];
                            double targetWidth = Math.Max(28, sizePx * 1.6);
                            double targetHeight = targetWidth * (sprite.PixelHeight / (double)sprite.PixelWidth);
                            if (baseX < -targetWidth || baseX > ActualWidth + targetWidth) continue;

                            dc.PushTransform(new TranslateTransform(baseX, baseY));
                            dc.PushTransform(new RotateTransform(-slopeAngle));
                            double groundSink = targetHeight * SimulationEngine<BitmapSource, BitmapSource>.ShrubGroundSinkFactor;
                            dc.DrawImage(sprite, new Rect(-targetWidth / 2.0, -targetHeight + groundSink, targetWidth, targetHeight));
                            dc.Pop();
                            dc.Pop();
                        }
                        else
                        {
                            if (baseX < -sizePx || baseX > ActualWidth + sizePx) continue;
                            dc.PushTransform(new TranslateTransform(baseX, baseY));
                            dc.PushTransform(new RotateTransform(-slopeAngle));
                            double groundSink = sizePx * SimulationEngine<BitmapSource, BitmapSource>.ShrubGroundSinkFactor;
                            dc.DrawEllipse(palette.Shrub, RoadsideOutlinePen, new Point(0, groundSink), sizePx * 0.5, sizePx * 0.35);
                            dc.Pop();
                            dc.Pop();
                        }
                        break;
                }
            }
        }

        private RoadsidePalette GetPalette(BackgroundTheme theme) => theme switch
        {
            BackgroundTheme.Mountain => MountainPalette,
            BackgroundTheme.Desert => DesertPalette,
            BackgroundTheme.Ocean => OceanPalette,
            _ => PlainPalette
        };

        private Brush GetTreeCanopyBrush(BackgroundTheme theme) => theme switch
        {
            BackgroundTheme.Mountain => MountainTreeCanopy,
            BackgroundTheme.Desert => DesertTreeCanopy,
            BackgroundTheme.Ocean => OceanTreeCanopy,
            _ => PlainTreeCanopy
        };

        private static Brush GetTransitionParticleBrush(BackgroundTheme theme) => theme switch
        {
            BackgroundTheme.Mountain => MountainParticleBrush,
            BackgroundTheme.Desert => DesertParticleBrush,
            BackgroundTheme.Ocean => OceanParticleBrush,
            _ => PlainParticleBrush
        };

        private static Brush CreateTreeCanopyBrush(Color light, Color dark)
        {
            var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            gradient.GradientStops.Add(new GradientStop(light, 0.0));
            gradient.GradientStops.Add(new GradientStop(dark, 1.0));
            gradient.Freeze();
            return gradient;
        }

        protected override int VisualChildrenCount => _children.Count;
        protected override Visual GetVisualChild(int index) => _children[index];
    }

    /// <summary>
    /// Per-frame state handed to <see cref="SimulationCanvas.FrameRendered"/> subscribers: the same
    /// delta the engine just integrated, plus the rider's resulting distance for gap maths.
    /// </summary>
    public sealed class SimulationFrameEventArgs : EventArgs
    {
        public SimulationFrameEventArgs(double deltaSeconds, double riderDistanceMeters, double riderSpeedKph)
        {
            DeltaSeconds = deltaSeconds;
            RiderDistanceMeters = riderDistanceMeters;
            RiderSpeedKph = riderSpeedKph;
        }

        /// <summary>Seconds since the previous rendered frame (already clamped by the canvas).</summary>
        public double DeltaSeconds { get; }

        /// <summary>The player's cumulative distance after this frame's update.</summary>
        public double RiderDistanceMeters { get; }

        /// <summary>The player's speed for this frame.</summary>
        public double RiderSpeedKph { get; }
    }
}
