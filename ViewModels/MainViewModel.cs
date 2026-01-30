using BikeFitnessApp.MVVM;

namespace BikeFitnessApp.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public MainViewModel()
        {
            // Initial view is handled in MainWindow.xaml.cs for now or we can move it here.
        }
    }
}
