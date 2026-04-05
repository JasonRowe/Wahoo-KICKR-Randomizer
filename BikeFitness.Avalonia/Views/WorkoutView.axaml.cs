using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.ViewModels;

namespace BikeFitness.Avalonia.Views
{
    public partial class WorkoutView : UserControl, IDisposable
    {
        private WorkoutViewModel? _viewModel;

        public WorkoutView()
        {
            InitializeComponent();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _viewModel = App.Current.Services?.GetRequiredService<WorkoutViewModel>();
            DataContext = _viewModel;
            
            if (_viewModel != null)
            {
                _viewModel.Disconnected += OnDisconnected;
            }
        }

        public void Dispose()
        {
            if (_viewModel != null)
            {
                _viewModel.Disconnected -= OnDisconnected;
                _viewModel.Dispose();
            }
        }

        private void OnDisconnected()
        {
            // Optional: return to setup view
        }
    }
}
