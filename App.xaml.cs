using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BikeFitnessApp.Services;
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
            services.AddSingleton<IBluetoothService, BluetoothService>();

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
                ex.Handled = false;
            };

            base.OnStartup(e);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
