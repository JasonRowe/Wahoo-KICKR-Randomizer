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

        [TestMethod]
        public void CanConnect_IsTrue_WhenDeviceSelected()
        {
            // Arrange
            var mockService = new MockBluetoothService();
            var viewModel = new SetupViewModel(mockService);
            var device = new DeviceDisplay { Name = "Test", Address = 1 };

            // Act
            viewModel.Devices.Add(device);
            viewModel.SelectedDevice = device;

            // Assert
            Assert.IsTrue(viewModel.CanConnect);
            Assert.IsTrue(viewModel.ConnectCommand.CanExecute(null));
        }

        [TestMethod]
        public void CanConnect_IsTrue_WhenSingleDeviceFound_AndNoneSelected()
        {
            // Arrange
            var mockService = new MockBluetoothService();
            var viewModel = new SetupViewModel(mockService);
            var device = new DeviceDisplay { Name = "Test", Address = 1 };

            // Act
            // We manually add it to bypass the auto-select logic in OnDeviceDiscovered for this specific test case check,
            // or just use OnDeviceDiscovered but verify null selection scenario if needed.
            // But since OnDeviceDiscovered auto-selects, we can manually unset it to test the Fallback logic.
            
            mockService.FireDeviceDiscovered(device);
            viewModel.SelectedDevice = null; // Force deselect

            // Assert
            Assert.AreEqual(1, viewModel.Devices.Count);
            Assert.IsNull(viewModel.SelectedDevice);
            Assert.IsTrue(viewModel.CanConnect, "CanConnect should be true if only 1 device exists even if not selected");
            Assert.IsTrue(viewModel.ConnectCommand.CanExecute(null));
        }

        [TestMethod]
        public void CanConnect_IsFalse_WhenNoDevices()
        {
            // Arrange
            var mockService = new MockBluetoothService();
            var viewModel = new SetupViewModel(mockService);

            // Act
            viewModel.Devices.Clear();

            // Assert
            Assert.IsFalse(viewModel.CanConnect);
            Assert.IsFalse(viewModel.ConnectCommand.CanExecute(null));
        }

        [TestMethod]
        public void CanScan_IsFalse_WhenAlreadyScanning()
        {
            // Arrange
            var mockService = new MockBluetoothService();
            // Constructor starts scanning
            var viewModel = new SetupViewModel(mockService);

            // Assert
            Assert.IsTrue(viewModel.IsScanning);
            Assert.IsFalse(viewModel.CanScan);
            Assert.IsFalse(viewModel.ScanCommand.CanExecute(null));
        }
    }
}
