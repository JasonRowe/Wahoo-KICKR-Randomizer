using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.Services;
using BikeFitness.Shared.ViewModels;
using BikeFitnessApp.Services;

namespace BikeFitnessApp
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public new static App Current => (App)Application.Current;

        private bool _reportingDispatcherException;

        public App()
        {
            Services = ConfigureServices();
            
            // Register UI Thread Dispatcher for shared ViewModels
            SetupViewModel.UIDispatcher = (action) => Dispatcher.Invoke(action);
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<IBluetoothService, WindowsBluetoothService>();
            services.AddSingleton<IStravaService, StravaService>();
            services.AddSingleton<IUserInterfaceService, WpfUserInterfaceService>();
            services.AddSingleton<IPowerManagementService, WindowsPowerManagementService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SetupViewModel>();
            services.AddTransient<WorkoutViewModel>();

            // Main Window
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // The WPF app asked users to read BikeFitnessApp.log but never enabled logging, so nothing was
            // ever written. Enable it up front (the ride screen's SETTINGS menu can still turn it off).
            BikeFitness.Shared.Logger.IsEnabled = true;
            BikeFitness.Shared.Logger.Log("WPF app starting.");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                BikeFitness.Shared.Logger.Log($"CRITICAL UNHANDLED EXCEPTION: {ex.ExceptionObject}");
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                BikeFitness.Shared.Logger.Log($"DISPATCHER UNHANDLED EXCEPTION: {ex.Exception}");

                // MessageBox.Show pumps the dispatcher. If the exception came from work still in flight (a
                // view being constructed, a template being applied), showing the dialog re-enters that work,
                // which throws again, which shows the dialog again — recursing until the stack overflows and
                // the process dies uncatchably. Report once; swallow any repeats while the dialog is up.
                if (!_reportingDispatcherException)
                {
                    _reportingDispatcherException = true;
                    try
                    {
                        MessageBox.Show($"An unexpected error occurred: {ex.Exception.Message}\n\nDetails in BikeFitnessApp.log", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        _reportingDispatcherException = false;
                    }
                }

                ex.Handled = true;
            };

            base.OnStartup(e);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
