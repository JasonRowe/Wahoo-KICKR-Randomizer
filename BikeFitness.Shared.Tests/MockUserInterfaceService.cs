using System;
using System.Threading.Tasks;
using BikeFitness.Shared.Services;

namespace BikeFitnessApp.UnitTests
{
    public class MockUserInterfaceService : IUserInterfaceService
    {
        public Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter) => Task.FromResult<string?>(null);
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public void InvokeOnUIThread(Action action) => action();
    }
}
