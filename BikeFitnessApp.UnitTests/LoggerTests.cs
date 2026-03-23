using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class LoggerTests
    {
        [TestMethod]
        public void Logger_IsEnabled_CanBeSet()
        {
            // Act
            Logger.IsEnabled = true;
            bool enabledAfterSet = Logger.IsEnabled;
            
            Logger.IsEnabled = false;
            bool disabledAfterSet = Logger.IsEnabled;

            // Assert
            Assert.IsTrue(enabledAfterSet);
            Assert.IsFalse(disabledAfterSet);
        }

        [TestMethod]
        public void Logger_Log_DoesNotThrowWhenDisabled()
        {
            // Act
            Logger.IsEnabled = false;
            Logger.Log("This should not be logged and should not throw");

            // Assert
            Assert.IsFalse(Logger.IsEnabled);
        }
    }
}
