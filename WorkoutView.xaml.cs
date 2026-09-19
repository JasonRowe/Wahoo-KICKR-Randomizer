using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared;
using BikeFitness.Shared.ViewModels;

namespace BikeFitnessApp
{
    public partial class WorkoutView : UserControl, IDisposable
    {
        private readonly WorkoutViewModel _viewModel;

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
        }

        public void Dispose()
        {
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
    }
}
