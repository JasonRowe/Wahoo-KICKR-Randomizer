using System;
using System.Collections.Generic;
using System.Linq;
using BikeFitness.Shared.Scoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.Scoring
{
    /// <summary>
    /// L0/L1 tests for the combo engine (POC #3, PR A). The load-bearing ones are the boundary-flicker and
    /// popup-rate-limit tests: those are the two ways this feature fails as a matter of *feel*, so they are
    /// pinned as maths rather than left to the eye.
    /// </summary>
    [TestClass]
    public class ComboEngineTests
    {
        private const double Frame = 1.0 / 60.0;

        [TestMethod]
        public void InZone_Accrues10PointsPerSecond_TimesMultiplier()
        {
            var engine = new ComboEngine();

            AdvanceFor(engine, 1.0, 200.0, 0.0);
            Assert.AreEqual(10.0, engine.Score, 0.3, "base accrual is 10 points per second");
            Assert.IsTrue(engine.InZone);

            // Push past the 15 s tier threshold, then measure the rate again: it must be ×1.5.
            AdvanceFor(engine, 20.0, 200.0, 0.0);
            Assert.AreEqual(ComboTier.Tier2, engine.Tier);

            double before = engine.Score;
            AdvanceFor(engine, 1.0, 200.0, 0.0);

            Assert.AreEqual(15.0, engine.Score - before, 0.3, "×1.5 must be applied to accrual");
        }

        [TestMethod]
        public void BoundaryFlicker_ProducesAtMostOneTransitionPer5Seconds()
        {
            var engine = new ComboEngine();
            double threshold = engine.Config.TargetWatts + engine.Config.BandWatts;
            int transitions = 0;
            bool previous = false;

            // 30 s of a rider hovering 0.3 W either side of the *entry* threshold at 60 Hz.
            for (int i = 0; i < (int)(30.0 / Frame); i++)
            {
                double t = i * Frame;
                double watts = threshold + (0.3 * Math.Sin(t * Math.PI * 2.0 * 30.0));
                engine.Advance(Frame, watts, 0.0);

                if (engine.InZone != previous)
                {
                    transitions++;
                    previous = engine.InZone;
                }
            }

            Assert.IsTrue(transitions <= 6, $"{transitions} zone transitions in 30 s at a boundary (flicker)");
        }

        [TestMethod]
        public void Tiers_ClimbAt15_30_60SecondsInZone()
        {
            var engine = new ComboEngine();

            AdvanceFor(engine, 14.9, 200.0, 0.0);
            Assert.AreEqual(ComboTier.Base, engine.Tier);
            Assert.AreEqual(1.0, engine.Multiplier, 1e-9);

            AdvanceFor(engine, 0.2, 200.0, 0.0);
            Assert.AreEqual(ComboTier.Tier2, engine.Tier);
            Assert.AreEqual(1.5, engine.Multiplier, 1e-9);

            AdvanceFor(engine, 15.0, 200.0, 0.0);
            Assert.AreEqual(ComboTier.Tier3, engine.Tier);
            Assert.AreEqual(2.0, engine.Multiplier, 1e-9);

            AdvanceFor(engine, 30.0, 200.0, 0.0);
            Assert.AreEqual(ComboTier.Tier4, engine.Tier);
            Assert.AreEqual(3.0, engine.Multiplier, 1e-9);
        }

        [TestMethod]
        public void LeavingZone_BreaksAfter3sGrace_AndDropsOneTier()
        {
            var engine = new ComboEngine();
            AdvanceFor(engine, 35.0, 200.0, 0.0);
            Assert.AreEqual(ComboTier.Tier3, engine.Tier);

            double scoreBefore = engine.Score;

            // 150 W is below target − band (170), so the combo is at risk but within the grace window.
            AdvanceFor(engine, 2.8, 150.0, 0.0);
            Assert.IsTrue(engine.InZone == false);
            Assert.IsFalse(engine.ComboBroken, "the 3 s grace must protect the combo");

            AdvanceFor(engine, 0.5, 150.0, 0.0);
            Assert.IsTrue(engine.ComboBroken, "the combo must break after the grace window");
            Assert.AreEqual(ComboTier.Tier2, engine.Tier, "a break drops exactly one tier");
            Assert.AreEqual(0.0, engine.ComboSeconds, 1e-9);
            Assert.AreEqual(scoreBefore, engine.Score, 1e-6, "a break must not cost points");
            Assert.AreEqual(1, engine.CombosLost);
        }

        [TestMethod]
        public void ClimbBonus_Pays50Percent()
        {
            var flat = new ComboEngine();
            var climbing = new ComboEngine();

            AdvanceFor(flat, 10.0, 200.0, 0.0);
            AdvanceFor(climbing, 10.0, 200.0, 5.0);

            Assert.AreEqual(1.5, climbing.Score / flat.Score, 0.01, "climbing must pay +50 %");
        }

        [TestMethod]
        public void SprintBonus_Pays300_OncePer30sCooldown()
        {
            var config = new ZoneConfig();
            var engine = new ComboEngine(config);

            var awardTimes = new List<double>();
            double t = 0;
            double previousScore = 0;

            for (int i = 0; i < (int)(60.0 / Frame); i++)
            {
                t += Frame;
                engine.Advance(Frame, config.TargetWatts * 1.6, 0.0);

                if (engine.Score - previousScore >= config.SprintBonusPoints - 0.001)
                {
                    awardTimes.Add(t);
                }

                previousScore = engine.Score;
            }

            Assert.IsTrue(awardTimes.Count >= 2, "sustained sprinting must pay the bonus more than once in 60 s");
            Assert.AreEqual(5.0, awardTimes[0], 0.1, "the bonus pays after 5 s above the sprint threshold");
            Assert.IsTrue(
                awardTimes[1] - awardTimes[0] >= config.SprintCooldownSeconds - 0.05,
                $"the second bonus came {awardTimes[1] - awardTimes[0]:F2} s after the first (cooldown {config.SprintCooldownSeconds} s)");
        }

        [TestMethod]
        public void PopupRateLimit_MaxOnePer800ms_Max3Alive()
        {
            // A deliberately absurd accrual rate so the milestone events out-run the limit.
            var config = new ZoneConfig { PointsPerSecond = 200.0 };
            var engine = new ComboEngine(config);

            var releaseTimes = new List<double>();
            double t = 0;
            double lastRelease = double.NegativeInfinity;
            double minGap = double.MaxValue;

            for (int i = 0; i < (int)(60.0 / Frame); i++)
            {
                t += Frame;
                engine.Advance(Frame, config.TargetWatts, 0.0);

                foreach (ScoreEvent _ in engine.DrainEvents())
                {
                    if (double.IsFinite(lastRelease)) minGap = Math.Min(minGap, t - lastRelease);
                    lastRelease = t;
                    releaseTimes.Add(t);
                }

                Assert.IsTrue(engine.AlivePopupCount <= config.MaxAlivePopups, $"alive popups {engine.AlivePopupCount}");
            }

            Assert.IsTrue(releaseTimes.Count > 10, "the limit must still release popups");
            Assert.IsTrue(
                minGap >= config.PopupMinIntervalSeconds - 1e-6,
                $"two popups were released only {minGap:F3} s apart (limit {config.PopupMinIntervalSeconds} s)");
        }

        [TestMethod]
        public void Score_IsMonotonicNonDecreasing()
        {
            var engine = new ComboEngine();
            double previous = 0;

            // In zone, out of zone, sprinting: score must never go down, including across a break.
            for (int cycle = 0; cycle < 4; cycle++)
            {
                for (int i = 0; i < (int)(20.0 / Frame); i++)
                {
                    double t = (cycle * 20.0) + (i * Frame);
                    double watts = (i < 600) ? 200.0 : 120.0;
                    if (cycle % 2 == 1 && i > 900) watts = 320.0;

                    engine.Advance(Frame, watts, cycle is 1 or 3 ? 6.0 : 0.0);

                    Assert.IsTrue(
                        engine.Score >= previous - 1e-9,
                        $"score fell from {previous:F2} to {engine.Score:F2} at t={t:F1}");
                    previous = engine.Score;
                }
            }
        }

        [TestMethod]
        public void ZeroEffort_AccruesNothing()
        {
            var engine = new ComboEngine();
            AdvanceFor(engine, 30.0, 0.0, 0.0);

            Assert.AreEqual(0.0, engine.Score, 1e-9);
            Assert.AreEqual(0.0, engine.XpEarned, 1e-9);
            Assert.IsFalse(engine.InZone);
            Assert.IsFalse(engine.ComboBroken, "a rider who never had a combo cannot break one");
            Assert.AreEqual(0, engine.DrainEvents().Count);
        }

        [TestMethod]
        public void Deterministic_ForFixedDtSequence()
        {
            var config = new ZoneConfig();
            var first = new ComboEngine(config);
            var second = new ComboEngine(config);

            var deltas = new List<double>();
            var rng = new Random(7);
            for (int i = 0; i < 2000; i++) deltas.Add(0.005 + (rng.NextDouble() * (0.05 - 0.005)));

            foreach (double dt in deltas) first.Advance(dt, 205.0, 4.0);

            foreach (double dt in deltas) second.Advance(dt, 205.0, 4.0);

            Assert.AreEqual(first.Score, second.Score, 0.0);
            Assert.AreEqual(first.Tier, second.Tier);
            Assert.AreEqual(first.ComboSeconds, second.ComboSeconds, 0.0);
        }

        [TestMethod]
        public void XpEarned_IsFloorOfScoreOver100_AndMonotonic()
        {
            var engine = new ComboEngine();

            AdvanceFor(engine, 25.0, 220.0, 0.0);
            Assert.AreEqual(Math.Floor(engine.Score / 100.0), engine.XpEarned, 1e-9);

            double xp = engine.XpEarned;
            AdvanceFor(engine, 25.0, 220.0, 0.0);
            Assert.IsTrue(engine.XpEarned >= xp, "XP must never decrease within a session");
        }

        [TestMethod]
        public void Advance_NonFiniteOrZeroDelta_IsSafe()
        {
            var engine = new ComboEngine();
            AdvanceFor(engine, 5.0, 200.0, 0.0);

            double score = engine.Score;
            engine.Advance(0.0, 200.0);
            engine.Advance(double.NaN, 200.0);
            engine.Advance(-1.0, 200.0);
            engine.Advance(Frame, double.NaN, double.NaN);

            Assert.IsTrue(double.IsFinite(engine.Score), "score must stay finite");
            Assert.IsTrue(engine.Score >= score);
        }

        [TestMethod]
        public void Reset_ClearsSessionState()
        {
            var engine = new ComboEngine();
            AdvanceFor(engine, 20.0, 220.0, 6.0);

            Assert.IsTrue(engine.Score > 0);
            Assert.IsTrue(engine.Tier > ComboTier.Base);

            engine.Reset();

            Assert.AreEqual(0.0, engine.Score, 1e-9);
            Assert.AreEqual(ComboTier.Base, engine.Tier);
            Assert.AreEqual(0.0, engine.ComboSeconds, 1e-9);
            Assert.IsFalse(engine.ComboBroken);
            Assert.AreEqual(0, engine.CombosLost);
            Assert.AreEqual(0, engine.AlivePopupCount);
            Assert.AreEqual(0, engine.Snapshot().Events.Count);
        }

        private static void AdvanceFor(ComboEngine engine, double seconds, double watts, double gradePercent)
        {
            int frames = (int)Math.Round(seconds / Frame);
            for (int i = 0; i < frames; i++) engine.Advance(Frame, watts, gradePercent);
        }
    }
}
