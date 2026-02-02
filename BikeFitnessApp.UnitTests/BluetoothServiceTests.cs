using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.Services;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class BluetoothServiceTests
    {
        [TestMethod]
        public void Service_InitialState_IsCorrect()
        {
            // Arrange
            var service = new BluetoothService();

            // Act & Assert
            Assert.IsFalse(service.IsScanning, "Should not be scanning initially");
            Assert.IsFalse(service.IsConnected, "Should not be connected initially");
            Assert.AreEqual("Ready", service.CurrentStatus, "Status should be Ready");
        }

        [TestMethod]
        public void StartScanning_UpdatesState()
        {
            // This test might fail on CI if no Bluetooth adapter is present, 
            // but locally on a dev machine it usually works or throws PlatformNotSupported.
            // We'll wrap in try-catch to be safe, or just skip if logic is too hardware dependent.
            // For now, let's just ensure the method exists and runs without crashing immediately.
            
            var service = new BluetoothService();
            try
            {
                service.StartScanning();
                // If it didn't throw, we assume it started (or at least tried).
                // Note: IsScanning property checks _watcher.Status, which might be 'Created' or 'Started'.
                // Real hardware behavior varies. 
                Assert.IsTrue(service.IsScanning || service.CurrentStatus.Contains("Scanning")); 
                service.StopScanning();
            }
            catch (System.TypeInitializationException) 
            {
                // Happens if Bluetooth API is not available (e.g. server core)
                Assert.Inconclusive("Bluetooth API not available.");
            }
            catch (System.PlatformNotSupportedException)
            {
                 Assert.Inconclusive("Bluetooth API not supported on this platform.");
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Assert.Inconclusive("Bluetooth hardware not available or accessible.");
            }
        }
    }
}
