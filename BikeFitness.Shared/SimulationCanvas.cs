using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace BikeFitness.Shared
{
    public class SimulationCanvas : FrameworkElement
    {
        private readonly VisualCollection _children;
        private readonly DrawingVisual _drawingVisual;
        private readonly Stopwatch _gameTimer = new Stopwatch();
        private double _lastTickElapsed;
        private double _circleX = 100;

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
                new PropertyMetadata(0.0));

        public double GradePercent
        {
            get => (double)GetValue(GradePercentProperty);
            set => SetValue(GradePercentProperty, value);
        }

        #endregion

        public SimulationCanvas()
        {
            _children = new VisualCollection(this);
            _drawingVisual = new DrawingVisual();
            _children.Add(_drawingVisual);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _gameTimer.Start();
            _lastTickElapsed = 0;
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            _gameTimer.Stop();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double currentElapsed = _gameTimer.Elapsed.TotalSeconds;
            double deltaTime = currentElapsed - _lastTickElapsed;
            _lastTickElapsed = currentElapsed;

            Update(deltaTime);
            DrawFrame();
        }

        private void Update(double deltaTime)
        {
            // Move circle based on speed: 1 kph = roughly 10 pixels per second for visualization
            // In the real app, this will be more precise distance mapping
            double pixelsPerSecond = SpeedKph * 10; 
            _circleX += pixelsPerSecond * deltaTime;

            // Wrap around screen
            if (_circleX > ActualWidth + 50)
            {
                _circleX = -50;
            }
        }

        private void DrawFrame()
        {
            if (ActualWidth == 0 || ActualHeight == 0) return;

            using (DrawingContext dc = _drawingVisual.RenderOpen())
            {
                // Background
                dc.DrawRectangle(Brushes.AliceBlue, null, new Rect(0, 0, ActualWidth, ActualHeight));

                // Draw the animated red circle
                dc.DrawEllipse(Brushes.Red, null, new Point(_circleX, 150), 50, 50);
                
                // Debug Text
                var debugText = $"Speed: {SpeedKph:F1} kph\nGrade: {GradePercent:F1} %\nX Pos: {_circleX:F0}";
                
                var formattedText = new FormattedText(
                    debugText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Bold"),
                    24,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(formattedText, new Point(20, 20));
            }
        }

        // Required overrides to bridge the visual tree
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
