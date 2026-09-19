using System;
using BikeFitness.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests
{
    /// <summary>
    /// L1 tests for the shared rider power model (POC PR 0). Pure maths: this file is the reason the pacer
    /// (POC #2) and the combo engine (POC #3) cannot silently drift apart on "how fast is 250 W on 6 %".
    /// </summary>
    [TestClass]
    public class RiderPowerModelTests
    {
        [TestMethod]
        public void PowerFromSpeed_IsMonotonicIncreasing_InSpeed()
        {
            foreach (double grade in new[] { 0.0, 3.0, 6.0, 9.0, 12.0 })
            {
                double previous = -1;
                for (double speedKph = 0; speedKph <= 50; speedKph += 2.5)
                {
                    double power = RiderPowerModel.PowerFromSpeed(speedKph, grade);
                    Assert.IsTrue(power > previous, $"power not increasing at {speedKph} kph / {grade} %");
                    previous = power;
                }
            }
        }

        [TestMethod]
        public void SpeedFromPower_IsMonotonicIncreasing_InPower()
        {
            foreach (double grade in new[] { 0.0, 4.0, 8.0 })
            {
                double previous = -1;
                for (double power = 50; power <= 2000; power += 50)
                {
                    double speed = RiderPowerModel.SpeedFromPower(power, grade);
                    Assert.IsTrue(speed > previous, $"speed not increasing at {power} W / {grade} %");
                    previous = speed;
                }
            }
        }

        [TestMethod]
        public void SpeedFromPower_IsMonotonicDecreasing_InGrade()
        {
            foreach (double power in new[] { 120.0, 200.0, 300.0 })
            {
                double previous = double.MaxValue;
                for (double grade = -5; grade <= 15; grade += 2.5)
                {
                    double speed = RiderPowerModel.SpeedFromPower(power, grade);
                    Assert.IsTrue(speed < previous, $"speed not decreasing at {power} W / {grade} %");
                    previous = speed;
                }
            }
        }

        [TestMethod]
        public void RoundTrip_SpeedPowerSpeed_WithinTolerance()
        {
            // Grades are kept non-negative: below the freewheeling speed on a descent the required power is
            // zero, and a round trip through a clamped 0 W cannot recover the original speed (by design).
            double worst = 0;
            for (int g = 0; g <= 4; g++)
            {
                double grade = g * 2.5;
                for (int s = 0; s <= 4; s++)
                {
                    double speedKph = 10.0 + (s * 8.75);

                    double power = RiderPowerModel.PowerFromSpeed(speedKph, grade);
                    double back = RiderPowerModel.SpeedFromPower(power, grade);

                    worst = Math.Max(worst, Math.Abs(back - speedKph));
                }
            }

            Assert.IsTrue(worst <= 0.05, $"worst round-trip error {worst:F4} kph exceeds the 0.05 kph tolerance");
        }

        [TestMethod]
        public void ZeroPower_OnFlat_GivesZeroSpeed()
        {
            Assert.AreEqual(0.0, RiderPowerModel.SpeedFromPower(0.0, 0.0), 1e-9);
            Assert.AreEqual(0.0, RiderPowerModel.SpeedFromPower(-50.0, 0.0), 1e-9);
            Assert.AreEqual(0.0, RiderPowerModel.PowerFromSpeed(0.0, 5.0), 1e-9);
        }

        [TestMethod]
        public void ReferenceTable_FrozenWithTolerance()
        {
            // Generated once from the implementation, then frozen. If the physics changes silently (a
            // constant edited, a term dropped), this fails instead of quietly moving the pacer's speed.
            double[] powers = { 120, 180, 240, 300 };
            double[] grades = { 0, 3, 6, 9 };
            double[,] expected =
            {
                { 27.41, 13.81, 8.08, 5.61 },   // 120 W
                { 32.15, 19.08, 11.85, 8.35 },  // 180 W
                { 35.86, 23.44, 15.38, 11.02 }, // 240 W
                { 38.97, 27.15, 18.65, 13.60 }, // 300 W
            };

            for (int p = 0; p < powers.Length; p++)
            {
                for (int g = 0; g < grades.Length; g++)
                {
                    double actual = RiderPowerModel.SpeedFromPower(powers[p], grades[g]);
                    Assert.AreEqual(
                        expected[p, g], actual, 0.5,
                        $"{powers[p]} W on {grades[g]} % moved from {expected[p, g]} kph to {actual:F2} kph");
                }
            }
        }

        [TestMethod]
        public void Clamps_DoNotProduceNaNOrInfinity()
        {
            double[] grades = { -25, -10, 0, 10, 25, 1000, -1000 };
            double[] powers = { 0, 1, 200, 2000, 100000, -100 };

            foreach (double grade in grades)
            {
                foreach (double power in powers)
                {
                    double speed = RiderPowerModel.SpeedFromPower(power, grade);
                    Assert.IsTrue(double.IsFinite(speed), $"speed not finite for {power} W / {grade} %");
                    Assert.IsTrue(speed >= 0, $"negative speed for {power} W / {grade} %");

                    double watts = RiderPowerModel.PowerFromSpeed(speed, grade);
                    Assert.IsTrue(double.IsFinite(watts), $"power not finite for {speed} kph / {grade} %");
                    Assert.IsTrue(watts >= 0, $"negative power for {speed} kph / {grade} %");
                }
            }
        }

        [TestMethod]
        public void NonFiniteInputs_FallBackToZero()
        {
            Assert.IsTrue(double.IsFinite(RiderPowerModel.PowerFromSpeed(double.NaN, double.NaN)));
            Assert.IsTrue(double.IsFinite(RiderPowerModel.SpeedFromPower(double.NaN, double.NaN)));
            Assert.IsTrue(double.IsFinite(RiderPowerModel.SpeedFromPower(double.PositiveInfinity, 0)));
            Assert.AreEqual(0.0, RiderPowerModel.SpeedFromPower(double.NaN, 0.0), 1e-9);
        }

        [TestMethod]
        public void PowerFromSpeed_OnDescentBelowFreewheelSpeed_ClampsToZero()
        {
            // −5 %: freewheeling terminal speed is well above walking pace, so a slow rider is braking and
            // the model reports 0 W rather than a nonsensical negative.
            Assert.AreEqual(0.0, RiderPowerModel.PowerFromSpeed(10.0, -5.0), 1e-9);

            // And at the same grade, a fast rider above terminal speed needs positive power to hold it.
            Assert.IsTrue(RiderPowerModel.PowerFromSpeed(55.0, -5.0) > 0);
        }

        [TestMethod]
        public void SpeedFromPower_OnDescent_ExceedsFlatSpeed()
        {
            Assert.IsTrue(
                RiderPowerModel.SpeedFromPower(200, -5.0) > RiderPowerModel.SpeedFromPower(200, 0.0),
                "a descent must be faster than the flat for the same power");
        }

        [TestMethod]
        public void CustomConstants_ChangeTheAnswer()
        {
            var heavier = new RiderConstants { TotalMassKg = 120.0 };
            var lighter = new RiderConstants { TotalMassKg = 60.0 };

            double heavySpeed = RiderPowerModel.SpeedFromPower(250, 6.0, heavier);
            double lightSpeed = RiderPowerModel.SpeedFromPower(250, 6.0, lighter);

            Assert.IsTrue(lightSpeed > heavySpeed, "a lighter rider must climb faster for the same watts");
        }

        [TestMethod]
        public void WPerKgFromWatts_GuardsAgainstZeroMass()
        {
            Assert.AreEqual(2.5, RiderPowerModel.WPerKgFromWatts(200, 80), 1e-9);
            Assert.AreEqual(0.0, RiderPowerModel.WPerKgFromWatts(200, 0), 1e-9);
            Assert.AreEqual(0.0, RiderPowerModel.WPerKgFromWatts(double.NaN, 80), 1e-9);
        }
    }
}
