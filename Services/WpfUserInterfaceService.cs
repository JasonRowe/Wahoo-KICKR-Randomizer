using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using BikeFitness.Shared.Services;

namespace BikeFitnessApp.Services
{
    public class WpfUserInterfaceService : IUserInterfaceService
    {
        public Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter)
        {
            var sfd = new SaveFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                Filter = filter
            };

            if (sfd.ShowDialog() == true)
            {
                return Task.FromResult<string?>(sfd.FileName);
            }
            return Task.FromResult<string?>(null);
        }

        public Task ShowMessageAsync(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return Task.FromResult(result == MessageBoxResult.Yes);
        }

        public void InvokeOnUIThread(Action action)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }
    }
}
