using System;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.ViewModels;

namespace BikeFitnessApp
{
    public partial class SetupView : UserControl, IDisposable
    {
        private readonly SetupViewModel _viewModel;

        public SetupView()
        {
            InitializeComponent();
            _viewModel = App.Current.Services.GetRequiredService<SetupViewModel>();
            DataContext = _viewModel;

            _viewModel.ConnectionSuccessful += OnConnectionSuccessful;
        }

        public void Dispose()
        {
            _viewModel.ConnectionSuccessful -= OnConnectionSuccessful;
            _viewModel.Dispose();
        }

        private void OnConnectionSuccessful()
        {
            var mainViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            mainViewModel.CurrentView = App.Current.Services.GetRequiredService<WorkoutViewModel>();
        }
    }
}
