using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using BikeFitness.Shared.Services;

using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

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
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok);
            var window = GetMainWindow();
            if (window != null)
            {
                await box.ShowWindowDialogAsync(window);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task<bool> ShowConfirmAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.YesNo);
            var window = GetMainWindow();
            
            ButtonResult result;
            if (window != null)
            {
                result = await box.ShowWindowDialogAsync(window);
            }
            else
            {
                result = await box.ShowAsync();
            }
            
            return result == ButtonResult.Yes;
        }

        public void InvokeOnUIThread(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
