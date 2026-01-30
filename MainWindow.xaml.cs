using System;
using System.Windows;

namespace BikeFitnessApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowSetup();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            PowerManagement.AllowSleep();
            // Disconnect service if needed
            base.OnClosing(e);
        }

        private void ShowSetup()
        {
            var setupView = new SetupView();
            setupView.ConnectionSuccessful += () =>
            {
                ShowWorkout();
            };
            MainContainer.Children.Clear();
            MainContainer.Children.Add(setupView);
        }

        private void ShowWorkout()
        {
            var workoutView = new WorkoutView();
            workoutView.Disconnected += () =>
            {
                Dispatcher.Invoke(() => ShowSetup());
            };
            MainContainer.Children.Clear();
            MainContainer.Children.Add(workoutView);
        }
    }
}