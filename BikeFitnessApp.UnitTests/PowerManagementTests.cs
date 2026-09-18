using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class PowerManagementTests
    {
        [TestMethod]
        public void PreventSleep_DoesNotThrow()
        {
            var service = new WindowsPowerManagementService();

            try
            {
                service.PreventSleep();
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"PreventSleep threw an exception: {ex.Message}");
            }
        }

        [TestMethod]
        public void AllowSleep_DoesNotThrow()
        {
            var service = new WindowsPowerManagementService();

            try
            {
                service.AllowSleep();
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"AllowSleep threw an exception: {ex.Message}");
            }
        }
    }
}
