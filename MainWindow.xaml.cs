using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BikeFitnessApp.ViewModels;
using BikeFitnessApp.Services;

namespace BikeFitnessApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _services;

        public MainWindow(MainViewModel viewModel, IServiceProvider services)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _services = services;
            this.DataContext = _viewModel;
            ShowSetup();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            PowerManagement.AllowSleep();
            base.OnClosing(e);
        }

        private void ShowSetup()
        {
            var setupVM = _services.GetRequiredService<SetupViewModel>();
            setupVM.ConnectionSuccessful += () => ShowWorkout();
            _viewModel.CurrentView = setupVM;
        }

        private void ShowWorkout()
        {
            var workoutVM = _services.GetRequiredService<WorkoutViewModel>();
            workoutVM.Disconnected += () =>
            {
                Dispatcher.Invoke(() => ShowSetup());
            };
            _viewModel.CurrentView = workoutVM;
        }
    }
}
