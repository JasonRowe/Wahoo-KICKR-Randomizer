using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using BikeFitnessApp;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void TestCalculateResistance()
        {
            // Arrange
            var seededRandom = new Random(12345); // Use a fixed seed for predictability
            var logic = new KickrLogic(seededRandom);
            double min = 10;
            double max = 20;

            // Act
            int resistance = logic.CalculateResistance(min, max);

            // Assert
            Assert.IsTrue(resistance >= min, "Resistance should be greater than or equal to the minimum value.");
            Assert.IsTrue(resistance <= max, "Resistance should be less than or equal to the maximum value.");
        }

        [TestMethod]
        public void TestCalculateResistance_ClampsValues()
        {
            // Arrange
            var logic = new KickrLogic();

            // Act
            int resistanceTooLow = logic.CalculateResistance(-10, 5);
            int resistanceTooHigh = logic.CalculateResistance(95, 110);
            int resistanceMinMaxSwapped = logic.CalculateResistance(80, 70);


            // Assert
            Assert.IsTrue(resistanceTooLow >= 0 && resistanceTooLow <= 5, "Resistance should be clamped to a minimum of 0.");
            Assert.IsTrue(resistanceTooHigh >= 95 && resistanceTooHigh <= 100, "Resistance should be clamped to a maximum of 100.");
            Assert.IsTrue(resistanceMinMaxSwapped >= 70 && resistanceMinMaxSwapped <= 70, "Min should be clamped to max if min > max.");
        }
    }
}
