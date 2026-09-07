using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class PowerManagementTests
    {
        [TestMethod]
        public void PreventSleep_DoesNotThrow()
        {
            // Act & Assert
            try
            {
                PowerManagement.PreventSleep();
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"PreventSleep threw an exception: {ex.Message}");
            }
        }

        [TestMethod]
        public void AllowSleep_DoesNotThrow()
        {
            // Act & Assert
            try
            {
                PowerManagement.AllowSleep();
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"AllowSleep threw an exception: {ex.Message}");
            }
        }
    }
}
