using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BikeFitness.Shared
{
    public class SimulationCanvas : FrameworkElement
    {
        private readonly VisualCollection _children;
        private readonly DrawingVisual _drawingVisual;
        private readonly Stopwatch _gameTimer = new Stopwatch();
        private double _lastTickElapsed;
        
        // Physics / State
        private double _totalDistanceMeters = 0;
        private double _currentSlopeAngle = 0; // Degrees

        // Assets
        private BitmapSource? _cyclistSprite;

        // Background segments
        private readonly List<BackgroundSegment> _backgroundSegments = new List<BackgroundSegment>();
        private double _backgroundCycleLengthMeters;

        // Roadside objects
        private readonly List<RoadsideObject> _roadsideObjects = new List<RoadsideObject>();
        private readonly Random _rng = new Random();
        private double _nextRoadsideSpawnDistance = 0;
        private readonly List<BitmapSource> _bushSprites = new List<BitmapSource>();
        
        // Pens & Brushes
        private static readonly Brush GrassBrush;
        private static readonly Pen PathPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 135, 100)), 10); // Lighter, tan brown
        private static readonly Pen MarkerPen = new Pen(Brushes.White, 2);
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

        static SimulationCanvas()
        {
            // Create a muted but slightly more vibrant gradient for the grass
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            // Top: Muted yellow-green (added a bit more saturation)
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(185, 200, 130), 0.0)); 
            // Bottom: Muted olive-green
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(100, 130, 80), 1.0));  
            gradient.Freeze();
            GrassBrush = gradient;

            PathPen.Freeze();
            MarkerPen.Freeze();
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
        }

        // Terrain History
        private struct TerrainVertex
        {
            public double Distance;
            public double Height;
            public double GradeOut;
        }

        private enum BackgroundTheme
        {
            Mountain,
            Plain,
            Desert,
            Ocean,
            Transition
        }

        private sealed class BackgroundSegment
        {
            public string Name { get; }
            public BackgroundTheme Theme { get; }
            public BitmapSource Image { get; }
            public double LengthMeters { get; }
            public bool MirrorTiles { get; }

            public BackgroundSegment(string name, BackgroundTheme theme, BitmapSource image, double lengthMeters, bool mirrorTiles)
            {
                Name = name;
                Theme = theme;
                Image = image;
                LengthMeters = lengthMeters;
                MirrorTiles = mirrorTiles;
            }
        }

        private struct BackgroundSegmentInfo
        {
            public BackgroundSegment? Segment;
            public BackgroundSegment? NextSegment;
            public double BlendToNext;
            public double LocalDistance;
            public double SegmentLength;
        }

        private enum RoadsideObjectType
        {
            Shrub,
            Tree,
            Rock
        }

        private enum RoadsideDrawPass
        {
            Background,
            Foreground
        }

        private struct RoadsideObject
        {
            public double Distance;
            public double SideOffsetMeters;
            public double HeightOffsetMeters;
            public double SizeMeters;
            public RoadsideObjectType Type;
            public int SpriteIndex;
        }

        private struct TransitionParticle
        {
            public Point Position;
            public Vector Velocity;
            public double Life;
            public double MaxLife;
            public double Size;
            public BackgroundTheme Theme;
        }

        private sealed class RoadsidePalette
        {
            public Brush Shrub { get; }
            public Brush Tree { get; }
            public Brush Rock { get; }
            public Brush Trunk { get; }

            public RoadsidePalette(Brush shrub, Brush tree, Brush rock, Brush trunk)
            {
                Shrub = shrub;
                Tree = tree;
                Rock = rock;
                Trunk = trunk;
            }
        }

        private readonly List<TerrainVertex> _terrainHistory = new List<TerrainVertex>();

        private const double PixelsPerMeter = 50.0;
        private const double BackgroundPixelsPerMeter = 5.0;
        private const double BiomeSegmentLengthMeters = 600.0;
        private const double TransitionSegmentLengthMeters = 140.0;
        private const double BackgroundBlendMeters = 0.0;
        private const double BackgroundFadeMeters = 25.0;
        private const double BackgroundTileOverlapPx = 1.0;
        private const int BackgroundEdgeBlendPixels = 12;
        private const bool UseMirroredBackgroundTiles = true;
        private const bool ApplyEdgeBlendToBackgrounds = false;
        private static readonly bool DrawDistanceMarkers = false;
        private const double BiomeLabelDurationSeconds = 1.6;
        private const double BiomeLabelFadeSeconds = 0.35;
        private const double BiomeLabelTopRatio = 0.08;
        private const double TransitionParticleSpawnRate = 18.0;
        private const int TransitionParticleMaxCount = 70;
        private const double TransitionParticleMinLife = 0.6;
        private const double TransitionParticleMaxLife = 1.4;
        private const double TransitionParticleMinSize = 4.0;
        private const double TransitionParticleMaxSize = 12.0;
        private const double TransitionParticleMinSpeed = 10.0;
        private const double TransitionParticleMaxSpeed = 28.0;
        private const double TransitionParticleMinYRatio = 0.08;
        private const double TransitionParticleMaxYRatio = 0.55;

        private const double RoadsideSpawnAheadMeters = 180.0;
        private const double RoadsideDespawnBehindMeters = 40.0;
        private const double RoadsideMinSpacingMeters = 6.0;
        private const double RoadsideMaxSpacingMeters = 16.0;
        private const double ShrubGroundSinkFactor = 0.18;

        private double _backgroundFadeStartDistance = 0.0;
        private BackgroundTheme _currentBiomeTheme = BackgroundTheme.Plain;
        private BackgroundTheme _lastAnnouncedBiome = BackgroundTheme.Plain;
        private BackgroundTheme _biomeLabelTheme = BackgroundTheme.Plain;
        private double _biomeLabelTimer = 0.0;
        private bool _wasInTransitionSegment = false;
        private readonly List<TransitionParticle> _transitionParticles = new List<TransitionParticle>();
        private double _transitionParticleSpawnAccumulator = 0.0;
        private double _transitionIntensity = 0.0;

        #region Dependency Properties

        public static readonly DependencyProperty SpeedKphProperty =
            DependencyProperty.Register(nameof(SpeedKph), typeof(double), typeof(SimulationCanvas), 
                new PropertyMetadata(0.0));

        public double SpeedKph
        {
            get => (double)GetValue(SpeedKphProperty);
            set => SetValue(SpeedKphProperty, value);
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
                
                // Only snap if we are just starting up (visual is near 0 but ride is in progress)
                // We ignore drift correction because snapping causes terrain geometry gaps and visual jumping.
                if (canvas._totalDistanceMeters < 5.0 && newVal > 5.0)
                {
                    canvas._totalDistanceMeters = newVal;
                    
                    // CRITICAL: Reset terrain history to match new distance to avoid geometry gaps/spikes
                    canvas._terrainHistory.Clear();
                    canvas._terrainHistory.Add(new TerrainVertex 
                    { 
                        Distance = newVal, 
                        Height = 0, // Reset height to 0 relative to camera
                        GradeOut = canvas.GradePercent 
                    });

                    canvas._roadsideObjects.Clear();
                    canvas._nextRoadsideSpawnDistance = newVal;
                    canvas._backgroundFadeStartDistance = newVal;
                    canvas.ResetTransitionEffects();
                }
            }
        }

        private static void OnGradeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimulationCanvas canvas)
            {
                canvas.RecordGradeChange((double)e.NewValue);
            }
        }

        #endregion

        public SimulationCanvas()
        {
            _children = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _children.Add(_drawingVisual);

            // Initialize terrain
            _terrainHistory.Add(new TerrainVertex { Distance = 0, Height = 0, GradeOut = 0 });

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void LoadAssets()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var imagesDir = Path.Combine(baseDir, "Images");
                Log($"Loading assets from: {imagesDir}");

                _cyclistSprite = LoadBitmap(Path.Combine(imagesDir, "cyclist_sprite.png"), "Cyclist");

                var mountain = LoadBitmap(Path.Combine(imagesDir, "biome_mountain.png"), "Biome Mountain", ApplyEdgeBlendToBackgrounds);
                var plain = LoadBitmap(Path.Combine(imagesDir, "biome_plain.png"), "Biome Plain", ApplyEdgeBlendToBackgrounds);
                var desert = LoadBitmap(Path.Combine(imagesDir, "biome_desert.png"), "Biome Desert", ApplyEdgeBlendToBackgrounds);
                var ocean = LoadBitmap(Path.Combine(imagesDir, "biome_ocean.png"), "Biome Ocean", ApplyEdgeBlendToBackgrounds);

                var transitionMountainPlain = LoadBitmap(Path.Combine(imagesDir, "transition_mountain_plain.png"), "Transition Mountain->Plain", ApplyEdgeBlendToBackgrounds);
                var transitionPlainDesert = LoadBitmap(Path.Combine(imagesDir, "transition_plain_desert.png"), "Transition Plain->Desert", ApplyEdgeBlendToBackgrounds);
                var transitionDesertOcean = LoadBitmap(Path.Combine(imagesDir, "transition_desert_ocean.png"), "Transition Desert->Ocean", ApplyEdgeBlendToBackgrounds);
                var transitionOceanMountain = LoadBitmap(Path.Combine(imagesDir, "transition_ocean_mountain.png"), "Transition Ocean->Mountain", ApplyEdgeBlendToBackgrounds);

                BuildBackgroundSegments(
                    mountain,
                    transitionMountainPlain,
                    plain,
                    transitionPlainDesert,
                    desert,
                    transitionDesertOcean,
                    ocean,
                    transitionOceanMountain);

                _bushSprites.Clear();
                AddBushSprite(LoadBitmap(Path.Combine(imagesDir, "sm_bush.png"), "Small Bush"));
                AddBushSprite(LoadBitmap(Path.Combine(imagesDir, "big_bush.png"), "Big Bush"));
                AddBushSprite(LoadBitmap(Path.Combine(imagesDir, "tall_bush.png"), "Tall Bush"));
            }
            catch (Exception ex)
            {
                Log($"Failed to load assets: {ex.Message}");
                Debug.WriteLine($"Failed to load assets: {ex.Message}");
            }
        }

        private void AddBushSprite(BitmapSource? sprite)
        {
            if (sprite != null)
            {
                _bushSprites.Add(sprite);
            }
        }

        private BitmapSource? LoadBitmap(string path, string name, bool makeSeamless = false)
        {
            if (!File.Exists(path)) 
            {
                Log($"Asset not found: {name} at {path}");
                return null;
            }
            try 
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                Log($"Loaded asset: {name}");
                if (!makeSeamless || BackgroundEdgeBlendPixels <= 0)
                {
                    return bi;
                }

                return CreateSeamlessTile(bi, BackgroundEdgeBlendPixels);
            }
            catch (Exception ex)
            {
                Log($"Error loading {name}: {ex.Message}");
                return null;
            }
        }

        private void BuildBackgroundSegments(
            BitmapSource? mountain,
            BitmapSource? transitionMountainPlain,
            BitmapSource? plain,
            BitmapSource? transitionPlainDesert,
            BitmapSource? desert,
            BitmapSource? transitionDesertOcean,
            BitmapSource? ocean,
            BitmapSource? transitionOceanMountain)
        {
            _backgroundSegments.Clear();
            _backgroundCycleLengthMeters = 0;

            AddBackgroundSegment("Mountain", BackgroundTheme.Mountain, mountain, BiomeSegmentLengthMeters, UseMirroredBackgroundTiles);
            AddBackgroundSegment("Transition Mountain->Plain", BackgroundTheme.Transition, transitionMountainPlain, TransitionSegmentLengthMeters, false);
            AddBackgroundSegment("Plain", BackgroundTheme.Plain, plain, BiomeSegmentLengthMeters, UseMirroredBackgroundTiles);
            AddBackgroundSegment("Transition Plain->Desert", BackgroundTheme.Transition, transitionPlainDesert, TransitionSegmentLengthMeters, false);
            AddBackgroundSegment("Desert", BackgroundTheme.Desert, desert, BiomeSegmentLengthMeters, UseMirroredBackgroundTiles);
            AddBackgroundSegment("Transition Desert->Ocean", BackgroundTheme.Transition, transitionDesertOcean, TransitionSegmentLengthMeters, false);
            AddBackgroundSegment("Ocean", BackgroundTheme.Ocean, ocean, BiomeSegmentLengthMeters, UseMirroredBackgroundTiles);
            AddBackgroundSegment("Transition Ocean->Mountain", BackgroundTheme.Transition, transitionOceanMountain, TransitionSegmentLengthMeters, false);

            if (_backgroundSegments.Count == 0)
            {
                Log("No background segments loaded. Falling back to sky fill.");
            }
        }

        private void AddBackgroundSegment(string name, BackgroundTheme theme, BitmapSource? image, double lengthMeters, bool mirrorTiles)
        {
            if (image == null)
            {
                Log($"Background segment '{name}' skipped (missing image).");
                return;
            }

            _backgroundSegments.Add(new BackgroundSegment(name, theme, image, lengthMeters, mirrorTiles));
            _backgroundCycleLengthMeters += lengthMeters;
        }

        private void RecordGradeChange(double newGrade)
        {
            var last = _terrainHistory[_terrainHistory.Count - 1];
            if (_totalDistanceMeters <= last.Distance + 0.1)
            {
                _terrainHistory[_terrainHistory.Count - 1] = new TerrainVertex 
                {
                    Distance = last.Distance, 
                    Height = last.Height, 
                    GradeOut = newGrade 
                };
                return;
            }

            double distTraveled = _totalDistanceMeters - last.Distance;
            double heightChange = distTraveled * (last.GradeOut / 100.0);
            double newHeight = last.Height + heightChange;

            _terrainHistory.Add(new TerrainVertex
            {
                Distance = _totalDistanceMeters,
                Height = newHeight,
                GradeOut = newGrade
            });
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Log("SimulationCanvas OnLoaded");
            LoadAssets();
            _roadsideObjects.Clear();
            _nextRoadsideSpawnDistance = _totalDistanceMeters;
            _backgroundFadeStartDistance = _totalDistanceMeters;
            ResetTransitionEffects();
            _gameTimer.Restart(); // Reset timer to avoid huge delta jumps on resume
            _lastTickElapsed = 0;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Log("SimulationCanvas OnUnloaded");
            CompositionTarget.Rendering -= OnRendering;
            _gameTimer.Stop();
        }

        private void ResetTransitionEffects()
        {
            _transitionParticles.Clear();
            _transitionParticleSpawnAccumulator = 0.0;
            _transitionIntensity = 0.0;
            _biomeLabelTimer = 0.0;

            var info = GetBackgroundSegmentInfo(_totalDistanceMeters);
            var theme = GetTransitionTargetTheme(info, BackgroundTheme.Plain);
            _currentBiomeTheme = theme;
            _lastAnnouncedBiome = theme;
            _biomeLabelTheme = theme;
            _wasInTransitionSegment = info.Segment?.Theme == BackgroundTheme.Transition;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            try
            {
                double currentElapsed = _gameTimer.Elapsed.TotalSeconds;
                double deltaTime = currentElapsed - _lastTickElapsed;
                _lastTickElapsed = currentElapsed;

                // Prevent huge jumps if thread sleeps or debug pause
                if (deltaTime > 0.1) deltaTime = 0.1; 
                if (deltaTime <= 0) return;

                Update(deltaTime);
                DrawFrame();
            }
            catch (Exception ex)
            {
                Log($"Error in OnRendering: {ex}");
            }
        }

        private void Log(string message)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simulation_log.txt");
                File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff}: {message}{Environment.NewLine}");
            }
            catch { /* Ignore logging errors */ }
        }

        private void Update(double deltaTime)
        {
            if (ActualWidth == 0) return;

            double metersPerSecond = (SpeedKph * 1000.0) / 3600.0;
            _totalDistanceMeters += metersPerSecond * deltaTime;

            double currentGrade = GetGradeAt(_totalDistanceMeters);
            _currentSlopeAngle = Math.Atan(currentGrade / 100.0) * (180.0 / Math.PI);

            var bgInfo = GetBackgroundSegmentInfo(_totalDistanceMeters);
            UpdateTransitionEffects(deltaTime, bgInfo);

            UpdateRoadsideObjects();
        }

        private void UpdateTransitionEffects(double deltaTime, BackgroundSegmentInfo info)
        {
            UpdateBiomeLabel(deltaTime, info);
            UpdateTransitionParticles(deltaTime, info);
        }

        private void UpdateBiomeLabel(double deltaTime, BackgroundSegmentInfo info)
        {
            if (_biomeLabelTimer > 0)
            {
                _biomeLabelTimer = Math.Max(0, _biomeLabelTimer - deltaTime);
            }

            if (info.Segment == null)
            {
                _wasInTransitionSegment = false;
                return;
            }

            bool inTransition = info.Segment.Theme == BackgroundTheme.Transition;
            if (!inTransition)
            {
                _currentBiomeTheme = info.Segment.Theme;
            }

            if (_totalDistanceMeters - _backgroundFadeStartDistance <= BackgroundFadeMeters)
            {
                _wasInTransitionSegment = inTransition;
                return;
            }

            if (inTransition && !_wasInTransitionSegment)
            {
                var targetTheme = GetTransitionTargetTheme(info, _currentBiomeTheme);
                if (targetTheme != _lastAnnouncedBiome)
                {
                    _biomeLabelTheme = targetTheme;
                    _biomeLabelTimer = BiomeLabelDurationSeconds;
                    _lastAnnouncedBiome = targetTheme;
                }
            }

            _wasInTransitionSegment = inTransition;
        }

        private void UpdateTransitionParticles(double deltaTime, BackgroundSegmentInfo info)
        {
            for (int i = _transitionParticles.Count - 1; i >= 0; i--)
            {
                var particle = _transitionParticles[i];
                particle.Life -= deltaTime;
                if (particle.Life <= 0)
                {
                    _transitionParticles.RemoveAt(i);
                    continue;
                }

                particle.Position = new Point(
                    particle.Position.X + (particle.Velocity.X * deltaTime),
                    particle.Position.Y + (particle.Velocity.Y * deltaTime));

                _transitionParticles[i] = particle;
            }

            if (_totalDistanceMeters - _backgroundFadeStartDistance <= BackgroundFadeMeters)
            {
                _transitionParticles.Clear();
                _transitionParticleSpawnAccumulator = 0.0;
                _transitionIntensity = 0.0;
                return;
            }

            _transitionIntensity = GetTransitionIntensity(info);
            if (_transitionIntensity <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                _transitionParticleSpawnAccumulator = 0.0;
                return;
            }

            double spawnRate = TransitionParticleSpawnRate * _transitionIntensity;
            _transitionParticleSpawnAccumulator += deltaTime * spawnRate;
            int spawnCount = (int)_transitionParticleSpawnAccumulator;
            if (spawnCount <= 0)
            {
                return;
            }

            _transitionParticleSpawnAccumulator -= spawnCount;
            var targetTheme = GetTransitionTargetTheme(info, _currentBiomeTheme);

            for (int i = 0; i < spawnCount && _transitionParticles.Count < TransitionParticleMaxCount; i++)
            {
                double x = _rng.NextDouble() * ActualWidth;
                double y = (TransitionParticleMinYRatio + (_rng.NextDouble() * (TransitionParticleMaxYRatio - TransitionParticleMinYRatio))) * ActualHeight;
                double size = TransitionParticleMinSize + (_rng.NextDouble() * (TransitionParticleMaxSize - TransitionParticleMinSize));
                double life = TransitionParticleMinLife + (_rng.NextDouble() * (TransitionParticleMaxLife - TransitionParticleMinLife));
                double speed = TransitionParticleMinSpeed + (_rng.NextDouble() * (TransitionParticleMaxSpeed - TransitionParticleMinSpeed));
                double drift = (_rng.NextDouble() - 0.5) * 6.0;

                var particle = new TransitionParticle
                {
                    Position = new Point(x, y),
                    Velocity = new Vector(-speed, drift),
                    Life = life,
                    MaxLife = life,
                    Size = size,
                    Theme = targetTheme
                };

                _transitionParticles.Add(particle);
            }
        }

        private double GetHeightAt(double distance)
        {
            for (int i = _terrainHistory.Count - 1; i >= 0; i--)
            {
                var v = _terrainHistory[i];
                if (distance >= v.Distance)
                {
                    double d = distance - v.Distance;
                    return v.Height + (d * (v.GradeOut / 100.0));
                }
            }
            return 0;
        }

        private double GetGradeAt(double distance)
        {
            for (int i = _terrainHistory.Count - 1; i >= 0; i--)
            {
                var v = _terrainHistory[i];
                if (distance >= v.Distance)
                {
                    return v.GradeOut;
                }
            }
            return 0;
        }

        private Point WorldToScreen(double worldDist, double bikeDist, double bikeHeight, double centerY, double bikeScreenX)
        {
            double worldH = GetHeightAt(worldDist);
            double screenX = bikeScreenX + (worldDist - bikeDist) * PixelsPerMeter;
            double screenY = centerY - (worldH - bikeHeight) * PixelsPerMeter;
            return new Point(screenX, screenY);
        }

        private BackgroundSegmentInfo GetBackgroundSegmentInfo(double distanceMeters)
        {
            if (_backgroundSegments.Count == 0 || _backgroundCycleLengthMeters <= 0)
            {
                return new BackgroundSegmentInfo();
            }

            double cycleDistance = distanceMeters % _backgroundCycleLengthMeters;
            if (cycleDistance < 0)
            {
                cycleDistance += _backgroundCycleLengthMeters;
            }

            double cursor = 0;
            for (int i = 0; i < _backgroundSegments.Count; i++)
            {
                var segment = _backgroundSegments[i];
                double nextCursor = cursor + segment.LengthMeters;
                if (cycleDistance <= nextCursor)
                {
                    double local = cycleDistance - cursor;
                    double blend = 0;
                    if (BackgroundBlendMeters > 0 && segment.LengthMeters > 0)
                    {
                        double distanceToEnd = segment.LengthMeters - local;
                        if (distanceToEnd < BackgroundBlendMeters)
                        {
                            blend = 1.0 - (distanceToEnd / BackgroundBlendMeters);
                        }
                    }

                    var nextSegment = _backgroundSegments[(i + 1) % _backgroundSegments.Count];
                    return new BackgroundSegmentInfo
                    {
                        Segment = segment,
                        NextSegment = nextSegment,
                        BlendToNext = blend,
                        LocalDistance = local,
                        SegmentLength = segment.LengthMeters
                    };
                }

                cursor = nextCursor;
            }

            return new BackgroundSegmentInfo
            {
                Segment = _backgroundSegments[0],
                NextSegment = _backgroundSegments.Count > 1 ? _backgroundSegments[1] : _backgroundSegments[0],
                BlendToNext = 0,
                LocalDistance = 0,
                SegmentLength = _backgroundSegments[0].LengthMeters
            };
        }

        private double GetTransitionIntensity(BackgroundSegmentInfo info)
        {
            if (info.Segment == null)
            {
                return 0.0;
            }

            if (info.Segment.Theme == BackgroundTheme.Transition)
            {
                if (info.SegmentLength <= 0)
                {
                    return 0.0;
                }

                double progress = Clamp01(info.LocalDistance / info.SegmentLength);
                return Math.Max(0.25, 1.0 - SmoothStep(progress) * 0.2);
            }

            if (info.NextSegment?.Theme == BackgroundTheme.Transition && BackgroundFadeMeters > 0)
            {
                double fadeOutMeters = Math.Min(BackgroundFadeMeters, info.SegmentLength);
                if (fadeOutMeters <= 0)
                {
                    return 0.0;
                }

                double distanceToEnd = info.SegmentLength - info.LocalDistance;
                double progress = Clamp01(1.0 - (distanceToEnd / fadeOutMeters));
                return SmoothStep(progress);
            }

            return 0.0;
        }

        private BackgroundTheme GetTransitionTargetTheme(BackgroundSegmentInfo info, BackgroundTheme fallback)
        {
            if (info.Segment == null)
            {
                return fallback;
            }

            if (info.Segment.Theme == BackgroundTheme.Transition)
            {
                if (info.NextSegment != null && info.NextSegment.Theme != BackgroundTheme.Transition)
                {
                    return info.NextSegment.Theme;
                }

                return fallback;
            }

            return info.Segment.Theme;
        }

        private static string GetBiomeLabelText(BackgroundTheme theme)
        {
            return theme switch
            {
                BackgroundTheme.Mountain => "Entering Mountains",
                BackgroundTheme.Plain => "Entering Plains",
                BackgroundTheme.Desert => "Entering Desert",
                BackgroundTheme.Ocean => "Entering Ocean",
                _ => "Entering Plains"
            };
        }

        private static Brush GetTransitionParticleBrush(BackgroundTheme theme)
        {
            return theme switch
            {
                BackgroundTheme.Mountain => MountainParticleBrush,
                BackgroundTheme.Desert => DesertParticleBrush,
                BackgroundTheme.Ocean => OceanParticleBrush,
                _ => PlainParticleBrush
            };
        }

        private void DrawBackground(DrawingContext dc, BackgroundSegmentInfo info)
        {
            dc.DrawRectangle(Brushes.LightSkyBlue, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (info.Segment?.Image == null)
            {
                return;
            }

            double scrollPx = _totalDistanceMeters * BackgroundPixelsPerMeter;
            double segmentOpacity = GetBackgroundOpacity(info);
            DrawTiledImage(dc, info.Segment.Image, scrollPx, info.Segment.MirrorTiles, segmentOpacity * (1.0 - info.BlendToNext));

            if (info.BlendToNext > 0 && info.NextSegment?.Image != null)
            {
                DrawTiledImage(dc, info.NextSegment.Image, scrollPx, info.NextSegment.MirrorTiles, segmentOpacity * info.BlendToNext);
            }
        }

        private void DrawTransitionParticles(DrawingContext dc)
        {
            if (_transitionParticles.Count == 0 || _transitionIntensity <= 0)
            {
                return;
            }

            double intensity = Clamp01(_transitionIntensity);

            foreach (var particle in _transitionParticles)
            {
                double lifeRatio = particle.MaxLife > 0 ? particle.Life / particle.MaxLife : 0;
                double alpha = intensity * Clamp01(lifeRatio);
                if (alpha <= 0)
                {
                    continue;
                }

                Brush brush = GetTransitionParticleBrush(particle.Theme);
                dc.PushOpacity(alpha * 0.85);
                dc.DrawEllipse(brush, null, particle.Position, particle.Size, particle.Size * 0.6);
                dc.Pop();
            }
        }

        private void DrawBiomeLabel(DrawingContext dc)
        {
            if (_biomeLabelTimer <= 0)
            {
                return;
            }

            string label = GetBiomeLabelText(_biomeLabelTheme);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            double duration = BiomeLabelDurationSeconds;
            double fade = Math.Min(BiomeLabelFadeSeconds, duration * 0.5);
            double elapsed = duration - _biomeLabelTimer;
            double alpha;

            if (elapsed < fade)
            {
                alpha = SmoothStep(elapsed / fade);
            }
            else if (_biomeLabelTimer < fade)
            {
                alpha = SmoothStep(_biomeLabelTimer / fade);
            }
            else
            {
                alpha = 1.0;
            }

            if (alpha <= 0)
            {
                return;
            }

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
            double y = ActualHeight * BiomeLabelTopRatio;
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

        private double GetBackgroundOpacity(BackgroundSegmentInfo info)
        {
            if (info.Segment == null || info.SegmentLength <= 0 || BackgroundFadeMeters <= 0)
            {
                return 1.0;
            }

            if (_totalDistanceMeters - _backgroundFadeStartDistance <= BackgroundFadeMeters)
            {
                return 1.0;
            }

            bool isTransition = info.Segment.Theme == BackgroundTheme.Transition;
            bool fadeIn = isTransition;
            bool fadeOut = !isTransition && info.NextSegment?.Theme == BackgroundTheme.Transition;

            if (!fadeIn && !fadeOut)
            {
                return 1.0;
            }

            double fade = Math.Min(BackgroundFadeMeters, info.SegmentLength / 2.0);
            if (fade <= 0)
            {
                return 1.0;
            }

            double opacity = 1.0;
            if (fadeIn)
            {
                double fadeInMeters = Math.Max(1.0, info.SegmentLength);
                opacity = Math.Min(opacity, Clamp01(info.LocalDistance / fadeInMeters));
            }

            if (fadeOut)
            {
                double fadeOutMeters = Math.Max(1.0, fade);
                opacity = Math.Min(opacity, Clamp01((info.SegmentLength - info.LocalDistance) / fadeOutMeters));
            }

            return opacity;
        }

        private void DrawTiledImage(DrawingContext dc, BitmapSource img, double scrollPx, bool mirrorTiles, double opacity)
        {
            if (opacity <= 0)
            {
                return;
            }

            if (img.PixelHeight <= 0)
            {
                return;
            }

            double tileScale = ActualHeight / img.PixelHeight;
            if (tileScale <= 0 || double.IsInfinity(tileScale) || double.IsNaN(tileScale))
            {
                return;
            }

            double tileWidth = Math.Round(img.PixelWidth * tileScale);
            if (tileWidth <= 0)
            {
                return;
            }

            double tileHeight = Math.Round(img.PixelHeight * tileScale);
            double offset = scrollPx % tileWidth;
            if (offset < 0)
            {
                offset += tileWidth;
            }

            long firstTileIndex = (long)Math.Floor(scrollPx / tileWidth);
            double startX = Math.Floor(-offset);
            double drawWidth = tileWidth + BackgroundTileOverlapPx;

            if (opacity < 1.0)
            {
                dc.PushOpacity(opacity);
            }

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

            if (opacity < 1.0)
            {
                dc.Pop();
            }
        }

        private void UpdateRoadsideObjects()
        {
            _roadsideObjects.RemoveAll(o => o.Type == RoadsideObjectType.Rock);

            double spawnTarget = _totalDistanceMeters + RoadsideSpawnAheadMeters;
            if (_nextRoadsideSpawnDistance < _totalDistanceMeters)
            {
                _nextRoadsideSpawnDistance = _totalDistanceMeters;
            }

            while (_nextRoadsideSpawnDistance <= spawnTarget)
            {
                var type = PickRoadsideType();
                double sideOffsetMeters = (_rng.NextDouble() < 0.5 ? -1 : 1) * (0.5 + _rng.NextDouble() * 1.5);
                double heightOffsetMeters = 0.2 + _rng.NextDouble() * 0.6;
                double sizeMeters = 0.4 + _rng.NextDouble() * 0.9;

                if (type == RoadsideObjectType.Shrub)
                {
                    // Keep shrubs anchored to the road surface.
                    sideOffsetMeters = (_rng.NextDouble() - 0.5) * 0.2;
                    heightOffsetMeters = 0.0;
                    sizeMeters = 0.35 + _rng.NextDouble() * 0.5;
                }
                else if (type == RoadsideObjectType.Tree)
                {
                    // Trees should sit on the road plane, not hover.
                    heightOffsetMeters = 0.0;
                }

                var obj = new RoadsideObject
                {
                    Distance = _nextRoadsideSpawnDistance,
                    SideOffsetMeters = sideOffsetMeters,
                    HeightOffsetMeters = heightOffsetMeters,
                    SizeMeters = sizeMeters,
                    Type = type,
                    SpriteIndex = _bushSprites.Count > 0 ? _rng.Next(_bushSprites.Count) : 0
                };

                _roadsideObjects.Add(obj);
                _nextRoadsideSpawnDistance += RoadsideMinSpacingMeters + (_rng.NextDouble() * (RoadsideMaxSpacingMeters - RoadsideMinSpacingMeters));
            }

            double despawnCutoff = _totalDistanceMeters - RoadsideDespawnBehindMeters;
            _roadsideObjects.RemoveAll(o => o.Distance < despawnCutoff);
        }

        private RoadsideObjectType PickRoadsideType()
        {
            double roll = _rng.NextDouble();
            if (roll < 0.75)
            {
                return RoadsideObjectType.Shrub;
            }
            return RoadsideObjectType.Tree;
        }

        private RoadsidePalette GetPalette(BackgroundTheme theme)
        {
            return theme switch
            {
                BackgroundTheme.Mountain => MountainPalette,
                BackgroundTheme.Desert => DesertPalette,
                BackgroundTheme.Ocean => OceanPalette,
                _ => PlainPalette
            };
        }

        private Brush GetTreeCanopyBrush(BackgroundTheme theme)
        {
            return theme switch
            {
                BackgroundTheme.Mountain => MountainTreeCanopy,
                BackgroundTheme.Desert => DesertTreeCanopy,
                BackgroundTheme.Ocean => OceanTreeCanopy,
                _ => PlainTreeCanopy
            };
        }

        private static Brush CreateTreeCanopyBrush(Color light, Color dark)
        {
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            gradient.GradientStops.Add(new GradientStop(light, 0.0));
            gradient.GradientStops.Add(new GradientStop(dark, 1.0));
            gradient.Freeze();
            return gradient;
        }

        private double GetSlopeAngleAt(double distanceMeters)
        {
            double grade = GetGradeAt(distanceMeters);
            return Math.Atan(grade / 100.0) * (180.0 / Math.PI);
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private static double SmoothStep(double value)
        {
            value = Clamp01(value);
            return value * value * (3 - (2 * value));
        }

        private void DrawRoadsideObjects(
            DrawingContext dc,
            double leftWorldDist,
            double rightWorldDist,
            double bikeWorldDist,
            double bikeWorldHeight,
            double visualCenterY,
            double bikeScreenX,
            BackgroundTheme theme,
            RoadsideDrawPass pass)
        {
            if (_roadsideObjects.Count == 0)
            {
                return;
            }

            var palette = GetPalette(theme);

            foreach (var obj in _roadsideObjects)
            {
                if (obj.Distance < leftWorldDist || obj.Distance > rightWorldDist)
                {
                    continue;
                }

                if (pass == RoadsideDrawPass.Background && obj.Type == RoadsideObjectType.Tree)
                {
                    continue;
                }

                if (pass == RoadsideDrawPass.Foreground && obj.Type != RoadsideObjectType.Tree)
                {
                    continue;
                }

                Point roadPoint = WorldToScreen(obj.Distance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                double sizePx = Math.Max(12, obj.SizeMeters * PixelsPerMeter);

                switch (obj.Type)
                {
                    case RoadsideObjectType.Tree:
                        double objX = roadPoint.X + (obj.SideOffsetMeters * PixelsPerMeter);
                        double objY = roadPoint.Y - (obj.HeightOffsetMeters * PixelsPerMeter);

                        if (objX < -sizePx || objX > ActualWidth + sizePx)
                        {
                            continue;
                        }

                        double trunkWidth = sizePx * 0.22;
                        double trunkHeight = sizePx * 0.9;
                        double trunkBaseY = objY + (sizePx * 0.2);
                        dc.DrawRectangle(palette.Trunk, RoadsideOutlinePen, new Rect(objX - trunkWidth / 2.0, trunkBaseY - trunkHeight, trunkWidth, trunkHeight));

                        var canopyBrush = GetTreeCanopyBrush(theme);
                        double canopyRadius = sizePx * 0.5;
                        double canopyCenterY = trunkBaseY - trunkHeight + (canopyRadius * 0.7);
                        dc.DrawEllipse(canopyBrush, null, new Point(objX, canopyCenterY), canopyRadius, canopyRadius);
                        break;
                    case RoadsideObjectType.Rock:
                        objX = roadPoint.X + (obj.SideOffsetMeters * PixelsPerMeter);
                        objY = roadPoint.Y - (obj.HeightOffsetMeters * PixelsPerMeter);

                        if (objX < -sizePx || objX > ActualWidth + sizePx)
                        {
                            continue;
                        }

                        dc.DrawEllipse(palette.Rock, RoadsideOutlinePen, new Point(objX, objY), sizePx * 0.45, sizePx * 0.3);
                        break;
                    default:
                        double slopeAngle = GetSlopeAngleAt(obj.Distance);
                        double slopeRadians = slopeAngle * (Math.PI / 180.0);
                        var normal = new Vector(-Math.Sin(slopeRadians), -Math.Cos(slopeRadians));
                        double offsetPx = (PathPen.Thickness * 0.5) + (obj.HeightOffsetMeters * PixelsPerMeter);

                        double baseX = roadPoint.X + (normal.X * offsetPx);
                        double baseY = roadPoint.Y + (normal.Y * offsetPx);

                        if (_bushSprites.Count > 0)
                        {
                            var sprite = _bushSprites[obj.SpriteIndex % _bushSprites.Count];
                            double targetWidth = Math.Max(28, sizePx * 1.6);
                            double targetHeight = targetWidth * (sprite.PixelHeight / (double)sprite.PixelWidth);

                            if (baseX < -targetWidth || baseX > ActualWidth + targetWidth)
                            {
                                continue;
                            }

                            dc.PushTransform(new TranslateTransform(baseX, baseY));
                            dc.PushTransform(new RotateTransform(-slopeAngle));
                            double groundSink = targetHeight * ShrubGroundSinkFactor;
                            dc.DrawImage(sprite, new Rect(-targetWidth / 2.0, -targetHeight + groundSink, targetWidth, targetHeight));
                            dc.Pop();
                            dc.Pop();
                        }
                        else
                        {
                            if (baseX < -sizePx || baseX > ActualWidth + sizePx)
                            {
                                continue;
                            }

                            dc.PushTransform(new TranslateTransform(baseX, baseY));
                            dc.PushTransform(new RotateTransform(-slopeAngle));
                            double groundSink = sizePx * ShrubGroundSinkFactor;
                            dc.DrawEllipse(palette.Shrub, RoadsideOutlinePen, new Point(0, groundSink), sizePx * 0.5, sizePx * 0.35);
                            dc.Pop();
                            dc.Pop();
                        }
                        break;
                }
            }
        }

        private static BitmapSource CreateSeamlessTile(BitmapSource source, int blendWidth)
        {
            if (blendWidth <= 0 || source.PixelWidth <= blendWidth * 2)
            {
                return source;
            }

            var formatted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            formatted.CopyPixels(pixels, stride, 0);

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < blendWidth; x++)
                {
                    double t = (x + 0.5) / blendWidth;
                    int leftIndex = row + (x * 4);
                    int rightIndex = row + ((width - blendWidth + x) * 4);

                    byte bL = pixels[leftIndex];
                    byte gL = pixels[leftIndex + 1];
                    byte rL = pixels[leftIndex + 2];
                    byte aL = pixels[leftIndex + 3];

                    byte bR = pixels[rightIndex];
                    byte gR = pixels[rightIndex + 1];
                    byte rR = pixels[rightIndex + 2];
                    byte aR = pixels[rightIndex + 3];

                    byte b = (byte)((bL * (1 - t)) + (bR * t));
                    byte g = (byte)((gL * (1 - t)) + (gR * t));
                    byte r = (byte)((rL * (1 - t)) + (rR * t));
                    byte a = (byte)((aL * (1 - t)) + (aR * t));

                    pixels[leftIndex] = b;
                    pixels[leftIndex + 1] = g;
                    pixels[leftIndex + 2] = r;
                    pixels[leftIndex + 3] = a;

                    pixels[rightIndex] = b;
                    pixels[rightIndex + 1] = g;
                    pixels[rightIndex + 2] = r;
                    pixels[rightIndex + 3] = a;
                }
            }

            var wb = new WriteableBitmap(width, height, formatted.DpiX, formatted.DpiY, PixelFormats.Pbgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            wb.Freeze();
            return wb;
        }

        private void DrawFrame()
        {
            if (ActualWidth == 0 || ActualHeight == 0) return;

            using (DrawingContext dc = _drawingVisual.RenderOpen())
            {
                // 1. Background
                var bgInfo = GetBackgroundSegmentInfo(_totalDistanceMeters);
                DrawBackground(dc, bgInfo);
                DrawTransitionParticles(dc);

                // 2. Terrain Setup
                double bikeScreenX = ActualWidth * 0.3;
                double bikeWorldDist = _totalDistanceMeters;
                double bikeWorldHeight = GetHeightAt(bikeWorldDist);
                double visualCenterY = ActualHeight * 0.75;

                double leftWorldDist = bikeWorldDist - (bikeScreenX / PixelsPerMeter) - 5;
                double rightWorldDist = bikeWorldDist + ((ActualWidth - bikeScreenX) / PixelsPerMeter) + 5;

                // 3. Build Ground Path (The Hill)
                var geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    Point startP = WorldToScreen(leftWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                    ctx.BeginFigure(startP, true, true); 

                    foreach (var v in _terrainHistory)
                    {
                        if (v.Distance > leftWorldDist && v.Distance < rightWorldDist)
                        {
                            ctx.LineTo(WorldToScreen(v.Distance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX), true, false);
                        }
                    }

                    Point endP = WorldToScreen(rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                    ctx.LineTo(endP, true, false);
                    ctx.LineTo(new Point(endP.X, ActualHeight), true, false);
                    ctx.LineTo(new Point(startP.X, ActualHeight), true, false);
                }
                geometry.Freeze();

                // Draw the Grass
                dc.DrawGeometry(GrassBrush, null, geometry);

                // 4. Draw the Dirt Path (Stroke on top of grass)
                var pathGeometry = new StreamGeometry();
                using (StreamGeometryContext ctx = pathGeometry.Open())
                {
                    Point startP = WorldToScreen(leftWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                    ctx.BeginFigure(startP, false, false); 

                    foreach (var v in _terrainHistory)
                    {
                        if (v.Distance > leftWorldDist && v.Distance < rightWorldDist)
                        {
                            ctx.LineTo(WorldToScreen(v.Distance, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX), true, false);
                        }
                    }

                    ctx.LineTo(WorldToScreen(rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX), true, false);
                }
                pathGeometry.Freeze();

                // Draw the Dirt Path stroke (Solid)
                dc.DrawGeometry(null, PathPen, pathGeometry);

                var roadsideTheme = bgInfo.Segment?.Theme ?? BackgroundTheme.Plain;
                if (roadsideTheme == BackgroundTheme.Transition && bgInfo.NextSegment != null)
                {
                    roadsideTheme = bgInfo.NextSegment.Theme;
                }

                DrawRoadsideObjects(dc, leftWorldDist, rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX, roadsideTheme, RoadsideDrawPass.Background);

                if (DrawDistanceMarkers)
                {
                    // 5. Draw Markers
                    double markerInterval = 10.0;
                    double firstMarker = Math.Ceiling(leftWorldDist / markerInterval) * markerInterval;

                    for (double d = firstMarker; d <= rightWorldDist; d += markerInterval)
                    {
                        Point p = WorldToScreen(d, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX);
                        dc.DrawLine(MarkerPen, p, new Point(p.X, p.Y + 10));
                        
                        var distText = new FormattedText(
                            $"{d:F0}m",
                            System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"),
                            10,
                            Brushes.White,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(distText, new Point(p.X - 10, p.Y + 12));
                    }
                }

                // 6. Draw Bike Sprite
                dc.PushTransform(new TranslateTransform(bikeScreenX, visualCenterY));
                dc.PushTransform(new RotateTransform(-_currentSlopeAngle)); 

                if (_cyclistSprite != null)
                {
                    // Lifted Y by 10 pixels (from -120 to -130) to sit on top of the 10px line
                    dc.DrawImage(_cyclistSprite, new Rect(-75, -130, 150, 150));
                }
                else
                {
                    dc.DrawRectangle(Brushes.Red, new Pen(Brushes.Black, 2), new Rect(-25, -40, 50, 40));
                }
                
                dc.Pop();
                dc.Pop();

                // 7. Foreground Trees
                DrawRoadsideObjects(dc, leftWorldDist, rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX, roadsideTheme, RoadsideDrawPass.Foreground);

                // 8. Biome Label
                DrawBiomeLabel(dc);
            }
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                throw new System.ArgumentOutOfRangeException();
            }

            return _children[index];
        }
    }
}
