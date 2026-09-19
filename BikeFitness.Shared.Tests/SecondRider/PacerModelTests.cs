using System;
using System.Collections.Generic;
using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.SecondRider
{
    /// <summary>
    /// L0/L1 tests for the rubber-band pacer (POC #2, PR A). The two that matter most are
    /// <c>ConvergesIntoBand_*</c> (does it actually hold a gap?) and <c>Hysteresis_DoesNotChatter</c>
    /// (does it flicker, which reads as a bug on screen?).
    /// </summary>
    [TestClass]
    public class PacerModelTests
    {
        private const double Dt = 0.05;

        [TestMethod]
        public void ConvergesIntoBand_From100mAhead_Within60Seconds()
        {
            var config = new PacerConfig();
            var trace = PacerSim.ConstantEffortTrace(180, speedKph: 25.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt, initialGapMeters: 100.0);

            double intoBand = result.SecondsToReachBand(config.GapTargetMeters, config.BandMeters);
            Assert.IsTrue(double.IsFinite(intoBand), "the pacer never returned to the band");
            Assert.IsTrue(intoBand <= 60.0, $"took {intoBand:F1} s to get back inside the band");

            // And it keeps tightening: within 3 m of the target inside two minutes.
            double tight = result.SecondsToReachBand(config.GapTargetMeters, 3.0);
            Assert.IsTrue(double.IsFinite(tight) && tight <= 120.0, $"never settled near the target (t={tight:F1})");

            Assert.AreEqual(0, result.SpeedCapViolations, "pacer broke a speed cap");
        }

        [TestMethod]
        public void ConvergesIntoBand_From100mBehind_Within60Seconds()
        {
            var config = new PacerConfig();
            var trace = PacerSim.ConstantEffortTrace(180, speedKph: 25.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt, initialGapMeters: -100.0);

            double intoBand = result.SecondsToReachBand(config.GapTargetMeters, config.BandMeters);
            Assert.IsTrue(double.IsFinite(intoBand), "the pacer never pulled back into the band");
            Assert.IsTrue(intoBand <= 60.0, $"took {intoBand:F1} s to pull back inside the band");

            double tight = result.SecondsToReachBand(config.GapTargetMeters, 3.0);
            Assert.IsTrue(double.IsFinite(tight) && tight <= 120.0, $"never settled near the target (t={tight:F1})");

            Assert.AreEqual(0, result.SpeedCapViolations, "pacer broke a speed cap");
        }

        [TestMethod]
        public void NoOscillation_AfterConvergence()
        {
            var config = new PacerConfig();
            var trace = PacerSim.ConstantEffortTrace(200, speedKph: 28.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt, initialGapMeters: 100.0);

            double amplitude = result.GapAmplitudeAfter(90.0);
            Assert.IsTrue(
                amplitude <= 0.25 * config.BandMeters,
                $"gap swung {amplitude:F2} m after convergence (limit {0.25 * config.BandMeters:F2} m)");
        }

        [TestMethod]
        public void Hysteresis_DoesNotChatter()
        {
            var config = new PacerConfig();
            double amplitude = config.BandMeters + 1.0;

            // The rider jumps ±amplitude every second, so the gap crosses the band twice a second — far
            // faster than SurgeEnterSeconds / EaseEnterSeconds. A naive threshold state machine flickers
            // here; the hysteretic one must barely move.
            var trace = AlternatingGapTrace(seconds: 120, speedKph: 25.0, amplitudeMeters: amplitude, holdSeconds: 1.0);
            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            Assert.IsTrue(
                result.StateTransitions <= 2,
                $"{result.StateTransitions} state transitions in 120 s of alternating gap (chatter)");
        }

        [TestMethod]
        public void Hysteresis_StillSurges_WhenTheGapPersists()
        {
            var config = new PacerConfig();
            double amplitude = config.BandMeters + 1.0;

            // Same swing, but held long enough to satisfy the persistence requirement — proving the
            // previous test isn't passing just because the state machine is inert.
            var trace = AlternatingGapTrace(seconds: 60, speedKph: 25.0, amplitudeMeters: amplitude, holdSeconds: 3.0);
            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            Assert.IsTrue(result.StateTransitions > 2, "a persistently stretched gap must change state");
            Assert.IsTrue(result.CountState(PacerState.Surging) > 0, "Surging was never entered");
        }

        [TestMethod]
        public void SurgeEntersAfterPersistSeconds_NotBefore()
        {
            var config = new PacerConfig();
            var model = new PacerModel(config);
            model.Reset(0.0, 100.0);

            double t = 0;
            PacerState stateAtOneSecond = PacerState.Contested;
            bool captured = false;

            while (t < 3.0)
            {
                t += Dt;
                model.Advance(Dt, 25.0 / 3.6 * t, 25.0, 0.0);

                if (!captured && t >= 1.0 - 1e-9)
                {
                    stateAtOneSecond = model.State;
                    captured = true;
                }
            }

            Assert.AreEqual(PacerState.Contested, stateAtOneSecond, "Surging entered before the persistence window elapsed");
            Assert.AreEqual(PacerState.Surging, model.State, "Surging never entered for a stretched gap");
        }

        [TestMethod]
        public void EaseEnteredWhenRiderDropsBelowBand()
        {
            var config = new PacerConfig();
            var model = new PacerModel(config);
            model.Reset(0.0, -100.0);

            double t = 0;
            PacerState stateAtOneSecond = PacerState.Contested;
            bool captured = false;

            while (t < 4.0)
            {
                t += Dt;
                model.Advance(Dt, 25.0 / 3.6 * t, 25.0, 0.0);

                if (!captured && t >= 1.0 - 1e-9)
                {
                    stateAtOneSecond = model.State;
                    captured = true;
                }
            }

            Assert.AreEqual(PacerState.Contested, stateAtOneSecond, "Easing entered before the persistence window elapsed");
            Assert.AreEqual(PacerState.Easing, model.State, "Easing never entered while being dropped");
        }

        [TestMethod]
        public void Mercy_TriggersDuringSustainedDrop()
        {
            var config = new PacerConfig();
            var trace = StageTrace(("fast", 60.0, 30.0), ("fade", 40.0, 15.0));

            PacerRunResult result = PacerSim.Run(config, trace, 0.25);

            int firstMercy = result.States.IndexOf(PacerState.Mercy);
            Assert.IsTrue(firstMercy > 0, "mercy never triggered while fading against the best 30 s");

            double mercyTime = result.TimesSeconds[firstMercy];
            double dropStart = 60.0;

            Assert.IsTrue(mercyTime > dropStart + 3.0, $"mercy gave no grace ({mercyTime - dropStart:F1} s after the drop)");
            Assert.IsTrue(mercyTime <= dropStart + 25.0, $"mercy took {mercyTime - dropStart:F1} s to appear");
        }

        [TestMethod]
        public void Mercy_WidensBandShortensTargetAndExpires()
        {
            var config = new PacerConfig();
            var model = new PacerModel(config);
            double t = 0;

            // 60 s of 30 kph (sets the best 30 s), then a long fade.
            while (t < 60.0)
            {
                t += 0.25;
                model.Advance(0.25, 30.0 / 3.6 * t, 30.0, 0.0);
            }

            double fadeStartDistance = 30.0 / 3.6 * t;
            double fadeTime = 0;
            double mercyStart = double.NaN;
            double mercyEnd = double.NaN;
            double bandAfterMercy = double.NaN;
            double targetAfterMercy = double.NaN;

            while (fadeTime < 90.0)
            {
                fadeTime += 0.25;
                model.Advance(0.25, fadeStartDistance + (15.0 / 3.6 * fadeTime), 15.0, 0.0);

                if (model.State == PacerState.Mercy)
                {
                    if (double.IsNaN(mercyStart)) mercyStart = fadeTime;

                    Assert.AreEqual(
                        config.BandMeters * (1.0 + config.MercyBandBonus), model.EffectiveBandMeters, 1e-9,
                        "mercy must widen the hysteresis band");
                    Assert.AreEqual(
                        config.GapTargetMeters * (1.0 - config.MercyBandBonus), model.EffectiveGapTargetMeters, 1e-9,
                        "mercy must shorten the gap the pacer holds");
                }
                else if (!double.IsNaN(mercyStart) && double.IsNaN(mercyEnd))
                {
                    mercyEnd = fadeTime;
                    bandAfterMercy = model.EffectiveBandMeters;
                    targetAfterMercy = model.EffectiveGapTargetMeters;
                }
            }

            Assert.IsFalse(double.IsNaN(mercyStart), "mercy never triggered");
            Assert.IsFalse(double.IsNaN(mercyEnd), "mercy never expired");

            double duration = mercyEnd - mercyStart;
            Assert.AreEqual(PacerModel.MercyDurationSeconds, duration, 0.6, $"mercy lasted {duration:F1} s");

            // The fade is still going, so mercy re-arms ~8 s later — check the values on the first frame
            // after expiry rather than at the end of the run.
            Assert.AreEqual(config.BandMeters, bandAfterMercy, 1e-9, "band must return to normal after mercy");
            Assert.AreEqual(config.GapTargetMeters, targetAfterMercy, 1e-9, "target must return to normal after mercy");
        }

        [TestMethod]
        public void MercyDisabled_NeverTriggers()
        {
            var config = new PacerConfig { MercyEnabled = false };
            var trace = StageTrace(("fast", 60.0, 30.0), ("fade", 40.0, 15.0));

            PacerRunResult result = PacerSim.Run(config, trace, 0.25);

            Assert.AreEqual(0, result.CountState(PacerState.Mercy), "mercy ran while disabled");
        }

        [TestMethod]
        public void PacerSpeedNeverExceedsPhysicalCap_On15PercentClimb()
        {
            var config = new PacerConfig();
            var trace = PacerSim.ConstantEffortTrace(120, speedKph: 20.0, gradePercent: 15.0, sampleSeconds: 1.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt, initialGapMeters: 0.0);

            Assert.AreEqual(0, result.SpeedCapViolations, "the pacer teleported up a 15 % climb");

            double cap = RiderPowerModel.SpeedFromPower(config.PacerWPerKg * config.Rider.TotalMassKg, 15.0, config.Rider);
            foreach (double speed in result.PacerSpeedsKph)
            {
                Assert.IsTrue(speed <= cap + 1e-6, $"pacer at {speed:F2} kph exceeds the {cap:F2} kph physical cap");
            }
        }

        [TestMethod]
        public void StoppedRider_PacerStops_NoNaN()
        {
            var config = new PacerConfig();
            var trace = StageTrace(("rolling", 20.0, 25.0), ("stopped", 40.0, 0.0));

            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            int stopIndex = result.TimesSeconds.FindIndex(t => t >= 20.0);
            Assert.IsTrue(stopIndex > 0);

            int twoSecondsLater = result.TimesSeconds.FindIndex(t => t >= 22.0);
            Assert.AreEqual(0.0, result.PacerSpeedsKph[twoSecondsLater], 1e-9, "pacer must stop within 2 s of the rider");

            for (int i = stopIndex; i < result.GapsMeters.Count; i++)
            {
                Assert.IsTrue(double.IsFinite(result.GapsMeters[i]), $"gap not finite at step {i}");
                Assert.IsTrue(double.IsFinite(result.PacerSpeedsKph[i]), $"speed not finite at step {i}");
                Assert.IsTrue(result.PacerSpeedsKph[i] >= 0, $"negative speed at step {i}");
            }

            double frozenGap = result.GapsMeters[twoSecondsLater];
            Assert.AreEqual(frozenGap, result.FinalGapMeters, 1e-9, "the gap drifted while both were stopped");
        }

        [TestMethod]
        public void Result_IsDeterministic_ForFixedTrace()
        {
            var config = new PacerConfig();
            var trace = StageTrace(("a", 30.0, 22.0), ("b", 30.0, 34.0), ("c", 30.0, 12.0));

            PacerRunResult first = PacerSim.Run(config, trace, Dt);
            PacerRunResult second = PacerSim.Run(config, trace, Dt);

            Assert.AreEqual(first.GapsMeters.Count, second.GapsMeters.Count);
            for (int i = 0; i < first.GapsMeters.Count; i++)
            {
                Assert.AreEqual(first.GapsMeters[i], second.GapsMeters[i], 0.0, $"gap diverged at {i}");
                Assert.AreEqual(first.PacerSpeedsKph[i], second.PacerSpeedsKph[i], 0.0, $"speed diverged at {i}");
            }
        }

        [TestMethod]
        public void ProjectedFinishDelta_IsNegativeWhenRiderAhead()
        {
            var model = new PacerModel(new PacerConfig());
            model.Reset(1000.0, -50.0);   // pacer 50 m behind the rider

            model.Advance(Dt, 1000.0 + (25.0 / 3.6 * Dt), 25.0, 0.0);

            Assert.IsTrue(model.ProjectedFinishDeltaSeconds < 0, "the rider is ahead, so the verdict must be negative");

            model.Reset(1000.0, 50.0);    // pacer 50 m ahead of the rider
            model.Advance(Dt, 1000.0 + (25.0 / 3.6 * Dt), 25.0, 0.0);

            Assert.IsTrue(model.ProjectedFinishDeltaSeconds > 0, "the pacer is ahead, so the verdict must be positive");
        }

        [TestMethod]
        public void GapSignConvention_MatchesDuelMath()
        {
            var model = new PacerModel(new PacerConfig());
            model.Reset(1000.0, 30.0);

            Assert.AreEqual(30.0, model.GapMeters(1000.0), 1e-9);
            Assert.AreEqual(DuelMath.GapMeters(model.DistanceMeters, 1000.0), model.GapMeters(1000.0), 1e-12);
            Assert.AreEqual(-30.0, model.GapMeters(1060.0), 1e-9);
        }

        [TestMethod]
        public void Reset_HoldsConfiguredGapAndClearsHistory()
        {
            var model = new PacerModel(new PacerConfig { GapTargetMeters = 18.0 });
            model.Reset(500.0);

            Assert.AreEqual(518.0, model.DistanceMeters, 1e-9);
            Assert.AreEqual(0.0, model.SpeedKph, 1e-9);
            Assert.AreEqual(PacerState.Contested, model.State);
            Assert.AreEqual(0.0, model.BestThirtySecondAverageKph, 1e-9);
            Assert.AreEqual(0.0, model.RecentAverageKph, 1e-9);
            Assert.AreEqual(0.0, model.MercyRemainingSeconds, 1e-9);
        }

        [TestMethod]
        public void Advance_NonFiniteOrZeroDelta_IsSafe()
        {
            var model = new PacerModel(new PacerConfig());
            model.Reset(0.0, 25.0);

            double before = model.DistanceMeters;
            model.Advance(0.0, 10.0, 25.0, 0.0);
            model.Advance(double.NaN, 10.0, 25.0, 0.0);
            model.Advance(-1.0, 10.0, 25.0, 0.0);

            Assert.AreEqual(before, model.DistanceMeters, 1e-12);
            Assert.IsTrue(double.IsFinite(model.SpeedKph));

            model.Advance(0.05, double.NaN, double.NaN, double.NaN);
            Assert.IsTrue(double.IsFinite(model.DistanceMeters), "non-finite rider input must not corrupt the pacer");
            Assert.IsTrue(double.IsFinite(model.SpeedKph));
            Assert.IsTrue(double.IsFinite(model.PseudoWatts));
        }

        [TestMethod]
        public void EffortSliderRange_ActuallyChangesClosingSpeed()
        {
            // Elasticity is the harness's "rubber band on a stick" slider: 0.1 should feel lazy, 0.8 snappy.
            var lazy = new PacerConfig { Elasticity = 0.10 };
            var snappy = new PacerConfig { Elasticity = 0.80 };
            var trace = PacerSim.ConstantEffortTrace(120, speedKph: 25.0);

            PacerRunResult lazyRun = PacerSim.Run(lazy, trace, Dt, initialGapMeters: 60.0);
            PacerRunResult snappyRun = PacerSim.Run(snappy, trace, Dt, initialGapMeters: 60.0);

            double lazyTime = lazyRun.SecondsToReachBand(lazy.GapTargetMeters, 5.0);
            double snappyTime = snappyRun.SecondsToReachBand(snappy.GapTargetMeters, 5.0);

            Assert.IsTrue(double.IsFinite(lazyTime) && double.IsFinite(snappyTime));
            Assert.IsTrue(snappyTime < lazyTime, $"elasticity 0.8 ({snappyTime:F1} s) must converge faster than 0.1 ({lazyTime:F1} s)");
        }

        /// <summary>Rider trace whose distance alternates by ±<paramref name="amplitudeMeters"/> every <paramref name="holdSeconds"/>.</summary>
        private static List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> AlternatingGapTrace(
            double seconds,
            double speedKph,
            double amplitudeMeters,
            double holdSeconds)
        {
            var trace = new List<(double, double, double, double)>();
            double metersPerSecond = speedKph / 3.6;
            double t = 0;
            int index = 0;

            while (t <= seconds + 1e-9)
            {
                double offset = index % 2 == 0 ? amplitudeMeters : -amplitudeMeters;
                trace.Add((t, (metersPerSecond * t) + offset, speedKph, 0.0));

                t += holdSeconds;
                index++;
            }

            return trace;
        }

        /// <summary>Rider trace with named constant-effort stages.</summary>
        private static List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> StageTrace(
            params (string Name, double Seconds, double SpeedKph)[] stages)
        {
            var trace = new List<(double, double, double, double)>();
            double t = 0;
            double distance = 0;

            trace.Add((0, 0, stages.Length > 0 ? stages[0].SpeedKph : 0, 0));

            foreach ((string _, double seconds, double speedKph) in stages)
            {
                double end = t + seconds;
                while (t < end - 1e-9)
                {
                    t += 1.0;
                    distance += speedKph / 3.6;
                    trace.Add((t, distance, speedKph, 0));
                }
            }

            return trace;
        }
    }
}
