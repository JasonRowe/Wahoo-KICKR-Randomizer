using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.ViewModels;
using BikeFitnessApp.Services;
using System.Linq;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class WorkoutViewModelTests
    {
        private MockBluetoothService _mockBluetoothService = null!;
        private WorkoutViewModel _viewModel = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockBluetoothService = new MockBluetoothService();
            _viewModel = new WorkoutViewModel(_mockBluetoothService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _viewModel.Cleanup();
        }

        [TestMethod]
        public void SelectedTireSize_UpdatesAppSettings()
        {
            // Arrange
            var initialSize = _viewModel.TireSizes.First();
            var newSize = _viewModel.TireSizes.Last();
            
            // Act
            _viewModel.SelectedTireSize = newSize;

            // Assert
            Assert.AreEqual(newSize, _viewModel.SelectedTireSize);
            Assert.AreEqual(newSize.Circumference, AppSettings.WheelCircumference);
        }

        [TestMethod]
        public void SelectTireSizeCommand_UpdatesSelectedTireSize()
        {
            // Arrange
            var newSize = _viewModel.TireSizes.Last();

            // Act
            _viewModel.SelectTireSizeCommand.Execute(newSize);

            // Assert
            Assert.AreEqual(newSize, _viewModel.SelectedTireSize);
            Assert.AreEqual(newSize.Circumference, AppSettings.WheelCircumference);
        }

        [TestMethod]
        public void IsMetric_Toggles_UpdatesLabels()
        {
            // Act - Set to Metric
            _viewModel.IsMetric = true;

            // Assert
            Assert.IsTrue(AppSettings.UseMetric);
            Assert.AreEqual("KPH", _viewModel.SpeedLabel);
            Assert.AreEqual("KM", _viewModel.DistanceLabel);

            // Act - Set to Imperial
            _viewModel.IsMetric = false;

            // Assert
            Assert.IsFalse(AppSettings.UseMetric);
            Assert.AreEqual("MPH", _viewModel.SpeedLabel);
            Assert.AreEqual("Miles", _viewModel.DistanceLabel);
        }

        [TestMethod]
        public void PowerProperty_UpdatesPowerText()
        {
            // Act
            _viewModel.Power = 250;

            // Assert
            Assert.AreEqual(250, _viewModel.Power);
            Assert.AreEqual("250 W", _viewModel.PowerText);
        }
    }
}
