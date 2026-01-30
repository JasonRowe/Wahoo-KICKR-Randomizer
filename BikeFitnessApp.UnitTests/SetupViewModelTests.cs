using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.ViewModels;
using BikeFitnessApp.Services;
using System;
using System.Threading.Tasks;
using BikeFitnessApp.UnitTests;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class SetupViewModelTests
    {
        [TestMethod]
        public void Constructor_StartsScanning_Automatically()
        {
            // Arrange
            var mockService = new MockBluetoothService();

            // Act
            // The constructor should trigger StartScanning()
            var viewModel = new SetupViewModel(mockService);

            // Assert
            Assert.IsTrue(mockService.StartScanningCalled, "SetupViewModel should automatically start scanning upon initialization.");
            Assert.IsTrue(viewModel.IsScanning, "ViewModel IsScanning property should be true.");
        }

        [TestMethod]
        public void Connect_WithSingleDevice_ConnectsWithoutSelection()
        {
            // Arrange
            var mockService = new MockBluetoothService();
            var viewModel = new SetupViewModel(mockService);
            
            // Simulate finding a device
            mockService.FireDeviceDiscovered(new DeviceDisplay { Name = "Test Device", Address = 12345 });

            // Ensure it was added
            Assert.AreEqual(1, viewModel.Devices.Count);
            
            // Act
            // Manually clear selection to test the "single device fallback" logic 
            // (though normally it auto-selects now, we want to ensure the logic holds if selection is lost)
            viewModel.SelectedDevice = null; 
            
            viewModel.ConnectCommand.Execute(null);

            // Assert
            Assert.IsTrue(mockService.ConnectAsyncCalled, "Should call ConnectAsync when only one device is present.");
        }
    }
}
