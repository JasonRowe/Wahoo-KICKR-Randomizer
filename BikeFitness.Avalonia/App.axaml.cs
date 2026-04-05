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
            if (OperatingSystem.IsWindows())
            {
                // Windows specific registration if we had a cross-platform reference
                services.AddSingleton<IBluetoothService, BikeFitness.Shared.Services.MockBluetoothService>();
            }
            else if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IBluetoothService, LinuxBluetoothService>();
            }
            else
            {
                services.AddSingleton<IBluetoothService, BikeFitness.Shared.Services.MockBluetoothService>();
            }

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
