using CommunityToolkit.Mvvm.ComponentModel;

namespace BikeFitness.Shared.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        partial void OnCurrentViewChanged(object? value)
        {
            if (value is System.IDisposable disposable)
            {
                // Note: Be careful with disposing if the same view is reused.
                // In our current simple setup, views are transient.
            }
        }

        public MainViewModel()
        {
        }
    }
}
