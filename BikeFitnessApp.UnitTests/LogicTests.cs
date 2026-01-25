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
            double min = 0;
            double max = 0.20;

            // Act
            double resistance = logic.CalculateResistance(min, max);
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
            double resistanceTooLow = logic.CalculateResistance(-10, 0.05);
            double resistanceTooHigh = logic.CalculateResistance(.15, 0.25);
            double resistanceMinMaxSwapped = logic.CalculateResistance(.08, .07);


            // Assert
            Assert.IsTrue(resistanceTooLow >= 0 && resistanceTooLow <= 0.05, "Resistance should be clamped to a minimum of 0.20");
            Assert.IsTrue(resistanceTooHigh >= 0.15 && resistanceTooHigh <= 0.20, "Resistance should be clamped to a maximum of 0.20");
            Assert.IsTrue(resistanceMinMaxSwapped >= 0.07 && resistanceMinMaxSwapped <= 0.07, "Min should be clamped to max if min > max.");
        }
    }
}
