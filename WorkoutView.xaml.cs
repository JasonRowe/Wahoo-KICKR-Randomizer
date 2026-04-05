using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.ViewModels;

namespace BikeFitnessApp
{
    public partial class WorkoutView : UserControl, IDisposable
    {
        private readonly WorkoutViewModel _viewModel;

        public WorkoutView()
        {
            InitializeComponent();
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
