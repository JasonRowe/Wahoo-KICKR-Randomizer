using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using BikeFitness.Shared.Services;

namespace BikeFitness.Avalonia.Services
{
    public class AvaloniaUserInterfaceService : IUserInterfaceService
    {
        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public async Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter)
        {
            var window = GetMainWindow();
            if (window == null) return null;

            var options = new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName
            };

            // Basic parsing of WPF-style filter to Avalonia
            // E.g. "FIT Activity (*.fit)|*.fit|JSON Report (*.json)|*.json"
            var parts = filter.Split('|');
            var types = new System.Collections.Generic.List<FilePickerFileType>();
            for (int i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 < parts.Length)
                {
                    var name = parts[i].Split('(')[0].Trim();
                    var ext = parts[i+1].Replace("*", "");
                    types.Add(new FilePickerFileType(name) { Patterns = new[] { "*" + ext } });
                }
            }
            options.FileTypeChoices = types;

            var result = await window.StorageProvider.SaveFilePickerAsync(options);
            return result?.Path.LocalPath;
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            // Simple message box using a built-in Avalonia way or custom.
            // For now, we'll just log or use a simple dialog if available.
            // Avalonia doesn't have a built-in MessageBox like WPF, so we might need a library like MessageBox.Avalonia
            // but for now let's just use Console for prototype or a simple Window.
            System.Diagnostics.Debug.WriteLine($"DIALOG [{title}]: {message}");
            await Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            System.Diagnostics.Debug.WriteLine($"CONFIRM [{title}]: {message}");
            return Task.FromResult(true); // Default to yes for prototype
        }

        public void InvokeOnUIThread(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
