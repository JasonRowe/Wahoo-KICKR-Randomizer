using System;
using System.Windows;
using System.Windows.Controls;
using BikeFitnessApp.ViewModels;

namespace BikeFitnessApp
{
    public partial class WorkoutView : UserControl
    {
        public WorkoutView()
        {
            InitializeComponent();
            this.Unloaded += WorkoutView_Unloaded;
        }

        private void WorkoutView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is WorkoutViewModel viewModel)
            {
                viewModel.Cleanup();
            }
        }
    }
}
