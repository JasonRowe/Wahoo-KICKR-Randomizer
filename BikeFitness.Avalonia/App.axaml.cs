using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.Services;
using BikeFitness.Shared.ViewModels;
using BikeFitness.Avalonia.Services;
using BikeFitness.Avalonia.Views;

namespace BikeFitness.Avalonia
{
    public partial class App : Application
    {
        public IServiceProvider? Services { get; private set; }

        public new static App Current => (App)Application.Current!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            BikeFitness.Shared.Logger.IsEnabled = true;
            BikeFitness.Shared.Logger.Log("Avalonia OnFrameworkInitializationCompleted started.");

            Services = ConfigureServices();
            BikeFitness.Shared.Logger.Log("Services configured.");

            // Register UI Thread Dispatcher for shared ViewModels
            SetupViewModel.UIDispatcher = (action) => global::Avalonia.Threading.Dispatcher.UIThread.Post(action);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();
                var mainViewModel = Services.GetRequiredService<MainViewModel>();
                mainViewModel.CurrentView = Services.GetRequiredService<SetupViewModel>();
                
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            // For now we'll use a Mock Bluetooth service on non-Windows or just provide the interface
            // On Linux we'll eventually use a LinuxBluetoothService
            if (OperatingSystem.IsWindows())
            {
                // We'd need to reference the WPF project or move WindowsBluetoothService to a separate project.
                // For now let's just use a dummy or skip if we can't reference WPF.
                // Actually, WPF project depends on Shared, but Shared cannot depend on WPF.
                // BikeFitness.Avalonia can depend on Shared.
                // To use WindowsBluetoothService in Avalonia, we should move it to a project that can be shared.
                // For now, let's assume we'll use a Cross-Platform service or just skip for the prototype.
            }

            // For the prototype, we'll just register the interface with a mock if we have one, or nothing.
            // Let's check if we have MockBluetoothService.
            services.AddSingleton<IBluetoothService, BikeFitness.Shared.Services.MockBluetoothService>();
            services.AddSingleton<IStravaService, BikeFitness.Shared.Services.StravaService>();
            services.AddSingleton<IUserInterfaceService, AvaloniaUserInterfaceService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SetupViewModel>();
            services.AddTransient<WorkoutViewModel>();

            return services.BuildServiceProvider();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
