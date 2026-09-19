using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.SecondRider
{
    /// <summary>L2 tests for the pure duel maths (gap, delta, formatting) used by every POC HUD.</summary>
    [TestClass]
    public class DuelMathTests
    {
        [TestMethod]
        public void DeltaSeconds_NegativeWhenRiderAhead()
        {
            // Rider 40 m up the road on the ghost, both at 25 kph.
            double delta = DuelMath.DeltaSeconds(secondRiderDistanceMeters: 1000, riderDistanceMeters: 1040, riderSpeedKph: 25, secondRiderSpeedKph: 25);

            Assert.IsTrue(delta < 0, $"rider ahead must give a negative delta, got {delta}");
        }

        [TestMethod]
        public void DeltaSeconds_PositiveWhenSecondRiderAhead()
        {
            double delta = DuelMath.DeltaSeconds(secondRiderDistanceMeters: 1040, riderDistanceMeters: 1000, riderSpeedKph: 25, secondRiderSpeedKph: 25);

            Assert.IsTrue(delta > 0);
        }

        [TestMethod]
        public void DeltaSeconds_EqualsGapOverSpeed_WhenSpeedsEqual()
        {
            const double gap = 50.0;
            const double speedKph = 18.0;

            double delta = DuelMath.DeltaSeconds(secondRiderDistanceMeters: 1050, riderDistanceMeters: 1000, riderSpeedKph: speedKph, secondRiderSpeedKph: speedKph);

            double expected = gap / (speedKph / 3.6);
            Assert.AreEqual(expected, delta, 0.05);
        }

        [TestMethod]
        public void DeltaSeconds_ZeroWhenNobodyIsMoving()
        {
            Assert.AreEqual(0.0, DuelMath.DeltaSeconds(500, 100, 0, 0), 1e-9);
            Assert.AreEqual(0.0, DuelMath.DeltaSeconds(500, 100, -5, 0), 1e-9);
        }

        [TestMethod]
        public void GapMeters_UnchangedByEffortFactor_OnlyByDistances()
        {
            Assert.AreEqual(20.0, DuelMath.GapMeters(120, 100), 1e-9);
            Assert.AreEqual(-20.0, DuelMath.GapMeters(80, 100), 1e-9);
            Assert.AreEqual(0.0, DuelMath.GapMeters(100, 100), 1e-9);
        }

        [TestMethod]
        public void FormatDelta_UsesMinutesSeconds_WithSign()
        {
            Assert.AreEqual("-0:08", DuelMath.FormatDelta(-8.4));
            Assert.AreEqual("+0:12", DuelMath.FormatDelta(12.0));
            Assert.AreEqual("+1:12", DuelMath.FormatDelta(72.4));
            Assert.AreEqual("-2:05", DuelMath.FormatDelta(-125.0));
            Assert.AreEqual("+0:00", DuelMath.FormatDelta(0.0));
        }

        [TestMethod]
        public void FormatDelta_RoundingAtSixtySeconds_RollsIntoMinutes()
        {
            Assert.AreEqual("+1:00", DuelMath.FormatDelta(59.6));
        }

        [TestMethod]
        public void FormatDelta_NonFinite_IsZero()
        {
            Assert.AreEqual("+0:00", DuelMath.FormatDelta(double.NaN));
            Assert.AreEqual("+0:00", DuelMath.FormatDelta(double.PositiveInfinity));
        }

        [TestMethod]
        public void FormatGapChip_ShowsDirectionAndDelta()
        {
            Assert.AreEqual("\u25B2 22 m \u00B7 +0:06", DuelMath.FormatGapChip(21.6, 6.2));
            Assert.AreEqual("\u25BC 18 m \u00B7 -0:10", DuelMath.FormatGapChip(-17.5, -10.4));
        }

        [TestMethod]
        public void FormatGapMeters_UsesAbsoluteValue()
        {
            Assert.AreEqual("18.4 m", DuelMath.FormatGapMeters(18.44));
            Assert.AreEqual("18.4 m", DuelMath.FormatGapMeters(-18.44));
        }
    }
}
