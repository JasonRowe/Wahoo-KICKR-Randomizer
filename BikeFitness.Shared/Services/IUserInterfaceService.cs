using System.Threading.Tasks;

namespace BikeFitness.Shared.Services
{
    public interface IUserInterfaceService
    {
        Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter);
        Task ShowMessageAsync(string title, string message);
        Task<bool> ShowConfirmAsync(string title, string message);
        void InvokeOnUIThread(System.Action action);
    }
}
