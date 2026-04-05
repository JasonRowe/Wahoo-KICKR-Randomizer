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
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                BikeFitness.Shared.Logger.Log($"CRITICAL UNHANDLED EXCEPTION: {ex.ExceptionObject}");
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                BikeFitness.Shared.Logger.Log($"DISPATCHER UNHANDLED EXCEPTION: {ex.Exception}");
                MessageBox.Show($"An unexpected error occurred: {ex.Exception.Message}\n\nDetails in BikeFitnessApp.log", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            base.OnStartup(e);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
