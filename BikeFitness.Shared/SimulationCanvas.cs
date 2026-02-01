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
        private BitmapImage? _cyclistSprite;
        private BitmapImage? _backgroundLayer;
        
        // Pens & Brushes
        private static readonly Brush GrassBrush;
        private static readonly Pen PathPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 135, 100)), 10); // Lighter, tan brown
        private static readonly Pen MarkerPen = new Pen(Brushes.White, 2);

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
        }

        // Terrain History
        private struct TerrainVertex
        {
            public double Distance;
            public double Height;
            public double GradeOut;
        }
        private readonly List<TerrainVertex> _terrainHistory = new List<TerrainVertex>();
        private const double PixelsPerMeter = 50;

        #region Dependency Properties

        public static readonly DependencyProperty SpeedKphProperty =
            DependencyProperty.Register(nameof(SpeedKph), typeof(double), typeof(SimulationCanvas), 
                new PropertyMetadata(0.0));

        public double SpeedKph
        {
            get => (double)GetValue(SpeedKphProperty);
            set => SetValue(SpeedKphProperty, value);
        }

        public static readonly DependencyProperty GradePercentProperty =
            DependencyProperty.Register(nameof(GradePercent), typeof(double), typeof(SimulationCanvas), 
                new PropertyMetadata(0.0, OnGradeChanged));

        public double GradePercent
        {
            get => (double)GetValue(GradePercentProperty);
            set => SetValue(GradePercentProperty, value);
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
                
                _cyclistSprite = LoadBitmap(Path.Combine(baseDir, "Images", "cyclist_sprite.png"));
                _backgroundLayer = LoadBitmap(Path.Combine(baseDir, "Images", "scenic_background.png"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load assets: {ex.Message}");
            }
        }

        private BitmapImage? LoadBitmap(string path)
        {
            if (!File.Exists(path)) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path, UriKind.Absolute);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
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
            LoadAssets();
            _gameTimer.Start();
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

            if (deltaTime <= 0) return;

            Update(deltaTime);
            DrawFrame();
        }

        private void Update(double deltaTime)
        {
            if (ActualWidth == 0) return;

            double metersPerSecond = (SpeedKph * 1000.0) / 3600.0;
            _totalDistanceMeters += metersPerSecond * deltaTime;

            double currentGrade = GetGradeAt(_totalDistanceMeters);
            _currentSlopeAngle = Math.Atan(currentGrade / 100.0) * (180.0 / Math.PI);
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

        private void DrawFrame()
        {
            if (ActualWidth == 0 || ActualHeight == 0) return;

            using (DrawingContext dc = _drawingVisual.RenderOpen())
            {
                // 1. Background
                dc.DrawRectangle(Brushes.LightSkyBlue, null, new Rect(0, 0, ActualWidth, ActualHeight));

                if (_backgroundLayer != null)
                {
                    double bgWidth = _backgroundLayer.PixelWidth;
                    double bgHeight = ActualHeight;
                    double bgOffset = (_totalDistanceMeters * 5) % bgWidth; 
                    dc.DrawImage(_backgroundLayer, new Rect(-bgOffset, 0, bgWidth, bgHeight));
                    dc.DrawImage(_backgroundLayer, new Rect(-bgOffset + bgWidth, 0, bgWidth, bgHeight));
                }

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
