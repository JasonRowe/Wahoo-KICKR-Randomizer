using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BikeFitnessApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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
        }
    }
}
