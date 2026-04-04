using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BikeFitnessApp.Services;
using BikeFitness.Shared.Services;
using BikeFitnessApp.ViewModels;

namespace BikeFitnessApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public new static App Current => (App)Application.Current;

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<IBluetoothService, WindowsBluetoothService>();
            services.AddSingleton<IStravaService, StravaService>();

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
                Logger.Log($"CRITICAL UNHANDLED EXCEPTION: {ex.ExceptionObject}");
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                Logger.Log($"DISPATCHER UNHANDLED EXCEPTION: {ex.Exception}");
                MessageBox.Show($"An unexpected error occurred: {ex.Exception.Message}\n\nDetails in BikeFitnessApp.log", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            base.OnStartup(e);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
