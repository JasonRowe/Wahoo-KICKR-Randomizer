using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BikeFitness.Shared;

namespace BikeFitnessApp
{
    public class SimulationCanvas : FrameworkElement
    {
        private readonly VisualCollection _children;
        private readonly DrawingVisual _drawingVisual;
        private readonly Stopwatch _gameTimer = new Stopwatch();
        private double _lastTickElapsed;
        
        private readonly SimulationEngine<BitmapSource, BitmapSource> _engine = new SimulationEngine<BitmapSource, BitmapSource>();

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

        #endregion

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

                dc.PushTransform(new TranslateTransform(bikeScreenX, visualCenterY));
                dc.PushTransform(new RotateTransform(-_engine.CurrentSlopeAngle)); 

                if (_engine.CyclistSprite != null)
                    dc.DrawImage(_engine.CyclistSprite, new Rect(-75, -130, 150, 150));
                else
                    dc.DrawRectangle(Brushes.Red, new Pen(Brushes.Black, 2), new Rect(-25, -40, 50, 40));
                
                dc.Pop();
                dc.Pop();

                DrawRoadsideObjects(dc, leftWorldDist, rightWorldDist, bikeWorldDist, bikeWorldHeight, visualCenterY, bikeScreenX, roadsideTheme, RoadsideDrawPass.Foreground);

                DrawBiomeLabel(dc);
            }
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
}
