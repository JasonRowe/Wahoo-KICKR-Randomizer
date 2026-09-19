using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BikeFitness.Shared;

namespace BikeFitness.Harness;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _autoDriveTimer;
    private readonly Stopwatch _autoDriveClock = new Stopwatch();

    public MainWindow()
    {
        InitializeComponent();

        // Point the harness at the shipped sheet (Images/, same asset the two apps use).
        string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
        string sheetPath = PedalAnimation.GetDefaultSheetPath(imagesDir);
        SimCanvas.PedalSheetSource = File.Exists(sheetPath) ? sheetPath : string.Empty;

        _autoDriveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _autoDriveTimer.Tick += AutoDriveTick;
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

    private void AutoDriveTick(object? sender, EventArgs e)
    {
        double t = _autoDriveClock.Elapsed.TotalSeconds;
        // Sinusoidal speed 5..40 kph and grade -5..5% for a representative ride.
        double speed = 22.5 + (17.5 * Math.Sin(t * 0.6));
        double grade = 5.0 * Math.Sin(t * 0.3);
        SliderSpeed.Value = Math.Clamp(speed, 0.0, 60.0);
        SliderGrade.Value = Math.Clamp(grade, -10.0, 20.0);
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
