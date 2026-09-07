using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using BikeFitness.Shared.Services;

namespace BikeFitness.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private bool _disconnectOnClose;

        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_disconnectOnClose) return;

            e.Cancel = true;
            _disconnectOnClose = true;

            var bluetooth = App.Current.Services?.GetService<IBluetoothService>();
            if (bluetooth != null && bluetooth.IsConnected)
            {
                try { await bluetooth.DisconnectAsync(); } catch { }
            }

            Close();
        }
    }
}
