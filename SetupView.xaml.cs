using System;
using System.Windows;
using System.Windows.Controls;
using BikeFitnessApp.ViewModels;

namespace BikeFitnessApp
{
    public partial class SetupView : UserControl
    {
        public SetupView()
        {
            InitializeComponent();
            this.Unloaded += SetupView_Unloaded;
        }

        private void SetupView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SetupViewModel viewModel)
            {
                viewModel.Cleanup();
            }
        }
    }
}
