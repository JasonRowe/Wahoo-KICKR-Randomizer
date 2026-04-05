using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.ViewModels;

namespace BikeFitness.Avalonia.Views
{
    public partial class SetupView : UserControl, IDisposable
    {
        private SetupViewModel? _viewModel;

        public SetupView()
        {
            InitializeComponent();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _viewModel = App.Current.Services?.GetRequiredService<SetupViewModel>();
            DataContext = _viewModel;
            
            if (_viewModel != null)
            {
                _viewModel.ConnectionSuccessful += OnConnectionSuccessful;
            }
        }

        public void Dispose()
        {
            if (_viewModel != null)
            {
                _viewModel.ConnectionSuccessful -= OnConnectionSuccessful;
                _viewModel.Dispose();
            }
        }

        private void OnConnectionSuccessful()
        {
            var mainViewModel = App.Current.Services?.GetRequiredService<MainViewModel>();
            if (mainViewModel != null)
            {
                mainViewModel.CurrentView = App.Current.Services?.GetRequiredService<WorkoutViewModel>();
            }
        }
    }
}
