using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.ViewModels;
using BikeFitness.Shared.Services;

namespace BikeFitnessApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _services;
        private bool _disconnectOnClose;

        public MainWindow(MainViewModel viewModel, IServiceProvider services)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _services = services;
            this.DataContext = _viewModel;
            ShowSetup();
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_disconnectOnClose)
            {
                e.Cancel = true;
                _disconnectOnClose = true;

                PowerManagement.AllowSleep();

                var bluetooth = _services.GetService<IBluetoothService>();
                if (bluetooth != null && bluetooth.IsConnected)
                {
                    try { await bluetooth.DisconnectAsync(); } catch { }
                }

                Close();
                return;
            }

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
