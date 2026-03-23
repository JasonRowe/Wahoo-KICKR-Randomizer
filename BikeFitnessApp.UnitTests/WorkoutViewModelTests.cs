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
        private MockStravaService _mockStravaService = null!;
        private WorkoutViewModel _viewModel = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockBluetoothService = new MockBluetoothService();
            _mockStravaService = new MockStravaService();
            _viewModel = new WorkoutViewModel(_mockBluetoothService, _mockStravaService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _viewModel.Dispose();
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

        [TestMethod]
        public void StartWorkout_SetsPropertiesCorrectly()
        {
            // Act
            _viewModel.StartCommand.Execute(null);

            // Assert
            Assert.IsTrue(_viewModel.IsWorkoutActive);
            Assert.IsFalse(_viewModel.CanStart);
            Assert.IsTrue(_viewModel.CanStop);
            Assert.AreEqual("WORKOUT ACTIVE", _viewModel.Status);
            Assert.AreEqual("Status: Workout Started", _viewModel.Log);
            Assert.IsFalse(_viewModel.ShowPostWorkoutOptions);
            Assert.IsFalse(_viewModel.HasWorkoutData);
        }

        [TestMethod]
        public void StopWorkout_SetsPropertiesCorrectly()
        {
            // Arrange
            _viewModel.StartCommand.Execute(null);

            // Act
            _viewModel.StopCommand.Execute(null);

            // Assert
            Assert.IsFalse(_viewModel.IsWorkoutActive);
            Assert.IsTrue(_viewModel.CanStart);
            Assert.IsFalse(_viewModel.CanStop);
            Assert.AreEqual("CONNECTED", _viewModel.Status);
            Assert.AreEqual("Status: Workout Stopped", _viewModel.Log);
        }

        [TestMethod]
        public void OnConnectionLost_SetsPropertiesCorrectly()
        {
            // Arrange
            _viewModel.StartCommand.Execute(null);

            // Act
            _mockBluetoothService.FireConnectionLost();

            // Assert
            Assert.IsFalse(_viewModel.IsWorkoutActive);
            Assert.AreEqual("DISCONNECTED", _viewModel.Status);
            Assert.AreEqual("Status: Device Disconnected.", _viewModel.Log);
        }

        [TestMethod]
        public void OnPowerReceived_UpdatesPower()
        {
            // Act
            _mockBluetoothService.FirePowerReceived(150);

            // Assert
            Assert.AreEqual(150, _viewModel.Power);
        }

        [TestMethod]
        public void OnSpeedValuesUpdated_Metric_UpdatesTexts()
        {
            // Arrange
            _viewModel.IsMetric = true;

            // Act
            _mockBluetoothService.FireSpeedValuesUpdated(36.0, 1000.0);

            // Assert
            Assert.AreEqual("36.0", _viewModel.SpeedText);
            Assert.AreEqual("1.00", _viewModel.DistanceText);
            Assert.AreEqual(36.0, _viewModel.CurrentSpeedKph);
            Assert.AreEqual(1000.0, _viewModel.CurrentDistanceMeters);
        }

        [TestMethod]
        public void OnSpeedValuesUpdated_Imperial_UpdatesTexts()
        {
            // Arrange
            _viewModel.IsMetric = false;

            // Act
            _mockBluetoothService.FireSpeedValuesUpdated(36.0, 1000.0);

            // Assert
            // 36.0 * 0.621371 = 22.369356 -> 22.4
            // (1000.0 / 1000.0) * 0.621371 = 0.621371 -> 0.62
            Assert.AreEqual("22.4", _viewModel.SpeedText);
            Assert.AreEqual("0.62", _viewModel.DistanceText);
        }
    }
}
