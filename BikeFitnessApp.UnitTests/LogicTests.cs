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
            double max = 1.0;

            // Act
            double resistance = logic.CalculateResistance(min, max);
            // Assert
            Assert.IsTrue(resistance >= min, "Resistance should be greater than or equal to the minimum value.");
            Assert.IsTrue(resistance <= max, "Resistance should be less than or equal to the maximum value.");
        }

        [TestMethod]
        public void TestCalculateResistance_HandlesOutliers()
        {
            // Arrange
            var logic = new KickrLogic();

            // Act
            // Now that clamping is removed, these inputs should produce outputs in their respective ranges.
            
            // "TooLow" input (-10 to 0.05) -> Output between -10 and 0.05
            double outputLow = logic.CalculateResistance(-10, 0.05);
            Assert.IsTrue(outputLow >= -10 && outputLow <= 0.05, "Should respect negative input range.");

            // "WayTooHigh" input (1.5 to 2.0) -> Output between 1.5 and 2.0
            double outputHigh = logic.CalculateResistance(1.5, 2.0);
            Assert.IsTrue(outputHigh >= 1.5 && outputHigh <= 2.0, "Should respect high input range.");

            // "WayTooLow" input (-5.0 to -1.0) -> Output between -5.0 and -1.0
            double outputWayLow = logic.CalculateResistance(-5.0, -1.0);
            Assert.IsTrue(outputWayLow >= -5.0 && outputWayLow <= -1.0, "Should respect negative-only input range.");

            // "Swapped" (0.8, 0.7) -> Logic swaps to (0.7, 0.8)
            double outputSwapped = logic.CalculateResistance(0.8, 0.7);
            Assert.IsTrue(outputSwapped >= 0.7 && outputSwapped <= 0.8, "Should handle min > max by swapping.");
        }
    }
}
