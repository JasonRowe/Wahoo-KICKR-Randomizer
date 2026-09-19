using System;
using System.Collections.Generic;
using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.SecondRider
{
    /// <summary>
    /// L0/L1 tests for the ghost replay engine (POC #1, PR A).
    /// The 1 Hz telemetry and the no-teleport / no-stair-step invariants are the point of this file.
    /// </summary>
    [TestClass]
    public class GhostReplayTests
    {
        private const double FrameSeconds = 0.016;

        [TestMethod]
        public void Advance_DtZero_DoesNotMove()
        {
            var ghost = new GhostReplay(RideProfile.Synthetic(60, seed: 11));
            ghost.Advance(0.5);

            double distance = ghost.DistanceMeters;
            double speed = ghost.SpeedKph;
            double profileTime = ghost.ProfileTime;

            ghost.Advance(0.0);

            Assert.AreEqual(distance, ghost.DistanceMeters, 1e-12);
            Assert.AreEqual(speed, ghost.SpeedKph, 1e-12);
            Assert.AreEqual(profileTime, ghost.ProfileTime, 1e-12);
        }

        [TestMethod]
        public void Advance_NegativeDt_DoesNotMove()
        {
            var ghost = new GhostReplay(RideProfile.Synthetic(60, seed: 11));
            ghost.Advance(0.5);

            double distance = ghost.DistanceMeters;
            ghost.Advance(-1.0);

            Assert.AreEqual(distance, ghost.DistanceMeters, 1e-12);
            Assert.AreEqual(0.5, ghost.ProfileTime, 1e-9);
        }

        [TestMethod]
        public void Advance_DtSpike2s_DoesNotTeleport()
        {
            var ghost = new GhostReplay(RideProfile.Synthetic(600, seed: 3));
            ghost.Advance(FrameSeconds);

            double before = ghost.DistanceMeters;
            double speedBefore = ghost.SpeedKph;

            ghost.Advance(2.0);

            double moved = ghost.DistanceMeters - before;
            double bound = (Math.Max(speedBefore, ghost.SpeedKph) / 3.6) * 2.0 * 1.25;

            Assert.IsTrue(moved > 0, "a 2 s pause must still advance the ghost");
            Assert.IsTrue(moved <= bound, $"ghost moved {moved:F3} m, bound {bound:F3} m (teleport)");
            Assert.IsTrue(ghost.ProfileTime <= 2.0 + FrameSeconds + 1e-9, "profile time may not jump past the applied dt");
        }

        [TestMethod]
        public void Advance_DistanceIsMonotonic_Over10kRandomDtSteps()
        {
            var ghost = new GhostReplay(RideProfile.Synthetic(300, seed: 42));
            var rng = new PocRandom(99);
            double previous = ghost.DistanceMeters;

            for (int i = 0; i < 10000; i++)
            {
                ghost.Advance(rng.NextRange(0.001, 0.5));

                Assert.IsTrue(ghost.DistanceMeters >= previous, $"distance went backwards at step {i}");
                Assert.IsTrue(double.IsFinite(ghost.DistanceMeters), $"distance not finite at step {i}");
                Assert.IsTrue(double.IsFinite(ghost.SpeedKph), $"speed not finite at step {i}");
                Assert.IsTrue(ghost.SpeedKph >= 0 && ghost.SpeedKph <= GhostReplay.MaxSpeedKph, $"speed out of range at step {i}");

                previous = ghost.DistanceMeters;
            }
        }

        [TestMethod]
        public void Advance_Effort90Percent_FinishesLater_Than100Percent()
        {
            RideProfile profile = RideProfile.Synthetic(120, seed: 5);

            int atFullEffort = StepsToFinish(profile, 1.0);
            int atNinetyPercent = StepsToFinish(profile, 0.90);

            Assert.IsTrue(atFullEffort > 0, "the ghost must eventually finish");
            Assert.IsTrue(
                atNinetyPercent > atFullEffort,
                $"90 % effort finished in {atNinetyPercent} steps vs {atFullEffort} at 100 %");

            double observedRatio = atNinetyPercent / (double)atFullEffort;
            Assert.AreEqual(1.0 / 0.90, observedRatio, 0.02, "effort must scale replay duration linearly");
        }

        [TestMethod]
        public void Advance_InterpolatesBetween1HzSamples_NoStairStepping()
        {
            // Hand-built profile with a big speed step at every 1 Hz boundary: the worst case for the
            // step-function speed that raw piecewise-linear distance resampling produces.
            var profile = new RideProfile { Label = "steps", Source = RideProfile.SourceSynthetic };
            double distance = 0;
            double[] speeds = { 12.0, 25.0, 14.0, 32.0, 18.0 };
            for (int i = 0; i < speeds.Length; i++)
            {
                profile.Samples.Add(new RideProfileSample
                {
                    T = i,
                    DistanceMeters = distance,
                    SpeedKph = speeds[i],
                    GradePercent = i - 2,
                });

                distance += speeds[i] * 1000.0 / 3600.0;
            }

            var ghost = new GhostReplay(profile);
            double maxAllowedStep = GhostReplay.MaxSpeedSlewKphPerSecond * FrameSeconds;
            double previousSpeed = ghost.SpeedKph;
            bool crossedBoundary = false;

            for (int frame = 0; frame < 400; frame++)
            {
                ghost.Advance(FrameSeconds);

                double step = Math.Abs(ghost.SpeedKph - previousSpeed);
                Assert.IsTrue(
                    step <= maxAllowedStep + 1e-9,
                    $"speed jumped {step:F4} kph in one {FrameSeconds}s frame (max {maxAllowedStep:F4})");
                previousSpeed = ghost.SpeedKph;

                if (ghost.ProfileTime > 3.1) crossedBoundary = true;
            }

            Assert.IsTrue(crossedBoundary, "the test must actually cross several 1 Hz boundaries");
        }

        [TestMethod]
        public void Advance_PastProfileEnd_ClampsAndSetsFinished()
        {
            RideProfile profile = RideProfile.Synthetic(30, seed: 8);
            var ghost = new GhostReplay(profile);

            for (int i = 0; i < 2000; i++) ghost.Advance(0.05);

            Assert.IsTrue(ghost.Finished);
            Assert.AreEqual(profile.DurationSeconds, ghost.ProfileTime, 1e-9);
            Assert.AreEqual(profile.TotalDistanceMeters, ghost.DistanceMeters, 1e-9);
        }

        [TestMethod]
        public void Advance_EmptyProfile_IsNoOp()
        {
            var ghost = new GhostReplay(new RideProfile());

            Assert.IsTrue(ghost.IsEmpty);
            ghost.Advance(1.0);

            Assert.AreEqual(0.0, ghost.DistanceMeters, 1e-12);
            Assert.AreEqual(0.0, ghost.SpeedKph, 1e-12);
            Assert.IsFalse(ghost.Finished);
            Assert.AreEqual(0.0, ghost.ProfileTime, 1e-12);
        }

        [TestMethod]
        public void Reset_StartsAtProfileSpeed_ThenRewinds()
        {
            RideProfile profile = RideProfile.Synthetic(60, seed: 13);
            var ghost = new GhostReplay(profile);

            double startSpeed = ghost.SpeedKph;
            Assert.IsTrue(startSpeed > 0, "a reset ghost must start at its profile speed, not 0");

            for (int i = 0; i < 200; i++) ghost.Advance(0.05);
            Assert.IsTrue(ghost.DistanceMeters > 0);

            ghost.Reset();

            Assert.AreEqual(profile.Samples[0].DistanceMeters, ghost.DistanceMeters, 1e-9);
            Assert.AreEqual(startSpeed, ghost.SpeedKph, 1e-9);
            Assert.AreEqual(0.0, ghost.ProfileTime, 1e-12);
            Assert.IsFalse(ghost.Finished);
        }

        [TestMethod]
        public void EffortFactor_IsClamped_ToleratesNaN()
        {
            var ghost = new GhostReplay(RideProfile.Synthetic(30, seed: 2));

            ghost.EffortFactor = 10.0;
            Assert.AreEqual(GhostReplay.MaxEffortFactor, ghost.EffortFactor, 1e-9);

            ghost.EffortFactor = -3.0;
            Assert.AreEqual(GhostReplay.MinEffortFactor, ghost.EffortFactor, 1e-9);

            ghost.EffortFactor = double.NaN;
            Assert.AreEqual(1.0, ghost.EffortFactor, 1e-9);
        }

        [TestMethod]
        public void Advance_IsDeterministic_ForFixedDtSequence()
        {
            RideProfile profile = RideProfile.Synthetic(120, seed: 21);
            var a = new GhostReplay(profile);
            var b = new GhostReplay(profile);

            var deltas = new List<double>();
            var rng = new PocRandom(7);
            for (int i = 0; i < 500; i++) deltas.Add(rng.NextRange(0.001, 0.4));

            foreach (double dt in deltas) a.Advance(dt);
            foreach (double dt in deltas) b.Advance(dt);

            Assert.AreEqual(a.DistanceMeters, b.DistanceMeters, 0.0);
            Assert.AreEqual(a.SpeedKph, b.SpeedKph, 0.0);
            Assert.AreEqual(a.ProfileTime, b.ProfileTime, 0.0);
        }

        private static int StepsToFinish(RideProfile profile, double effortFactor)
        {
            var ghost = new GhostReplay(profile) { EffortFactor = effortFactor };
            const double dt = 0.05;
            const int maxSteps = 200000;

            for (int i = 0; i < maxSteps; i++)
            {
                ghost.Advance(dt);
                if (ghost.Finished) return i + 1;
            }

            Assert.Fail($"ghost never finished at effort {effortFactor}");
            return -1;
        }
    }
}
