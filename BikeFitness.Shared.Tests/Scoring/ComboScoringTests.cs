using System.Collections.Generic;
using BikeFitness.Shared.Scoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.Scoring
{
    /// <summary>L1/L2 tests for the tier maths and the end-of-ride score card.</summary>
    [TestClass]
    public class ComboScoringTests
    {
        [TestMethod]
        public void TierFor_Boundaries_AreInclusiveExclusive_AsDocumented()
        {
            Assert.AreEqual(ComboTier.Base, ComboScoring.TierFor(0.0));
            Assert.AreEqual(ComboTier.Base, ComboScoring.TierFor(14.999));
            Assert.AreEqual(ComboTier.Tier2, ComboScoring.TierFor(15.0));
            Assert.AreEqual(ComboTier.Tier2, ComboScoring.TierFor(29.999));
            Assert.AreEqual(ComboTier.Tier3, ComboScoring.TierFor(30.0));
            Assert.AreEqual(ComboTier.Tier3, ComboScoring.TierFor(59.999));
            Assert.AreEqual(ComboTier.Tier4, ComboScoring.TierFor(60.0));
            Assert.AreEqual(ComboTier.Tier4, ComboScoring.TierFor(600.0));

            Assert.AreEqual(ComboTier.Base, ComboScoring.TierFor(double.NaN));
            Assert.AreEqual(ComboTier.Base, ComboScoring.TierFor(-5.0));
        }

        [TestMethod]
        public void MultiplierFor_MatchesTheDocumentedTiers()
        {
            Assert.AreEqual(1.0, ComboScoring.MultiplierFor(ComboTier.Base), 1e-9);
            Assert.AreEqual(1.5, ComboScoring.MultiplierFor(ComboTier.Tier2), 1e-9);
            Assert.AreEqual(2.0, ComboScoring.MultiplierFor(ComboTier.Tier3), 1e-9);
            Assert.AreEqual(3.0, ComboScoring.MultiplierFor(ComboTier.Tier4), 1e-9);
        }

        [TestMethod]
        public void OneTierDown_StopsAtBase()
        {
            Assert.AreEqual(ComboTier.Tier3, ComboScoring.OneTierDown(ComboTier.Tier4));
            Assert.AreEqual(ComboTier.Tier2, ComboScoring.OneTierDown(ComboTier.Tier3));
            Assert.AreEqual(ComboTier.Base, ComboScoring.OneTierDown(ComboTier.Tier2));
            Assert.AreEqual(ComboTier.Base, ComboScoring.OneTierDown(ComboTier.Base));
        }

        [TestMethod]
        public void BuildCard_TotalsMatchSessionEvents()
        {
            var session = new ComboSession
            {
                WorkoutMode = "Hilly",
                Score = 425,
                XpEarned = 4,
                BestComboSeconds = 72,
                TimeInZoneSeconds = 300,
                DurationSeconds = 600,
                CombosLost = 1,
                Events = new List<ScoreEvent>
                {
                    new ScoreEvent("ZONE", 50, "+50 ZONE"),
                    new ScoreEvent("CLIMB", 75, "+75 CLIMB"),
                    new ScoreEvent("COMBO", 0, "x2 COMBO"),
                    new ScoreEvent("SPRINT", 300, "+300 SPRINT"),
                    new ScoreEvent("LOST", 0, "COMBO LOST"),
                },
            };

            ScoreCard card = ComboScoring.BuildCard(session);

            Assert.AreEqual(425, card.TotalFromEvents, 1e-9);
            Assert.AreEqual(card.Score, card.TotalFromEvents, 1e-9, "the card must reconcile with its own events");
            Assert.AreEqual(5, card.EventCount);
            Assert.AreEqual(50, card.TotalsByKind["ZONE"], 1e-9);
            Assert.AreEqual(75, card.TotalsByKind["CLIMB"], 1e-9);
            Assert.AreEqual(300, card.TotalsByKind["SPRINT"], 1e-9);
            Assert.AreEqual(0, card.TotalsByKind["COMBO"], 1e-9);
            Assert.AreEqual("Hilly", card.WorkoutMode);
            Assert.AreEqual(4, card.XpEarned, 1e-9);
            Assert.AreEqual(72, card.BestComboSeconds, 1e-9);
            Assert.AreEqual(1, card.CombosLost);
            Assert.AreEqual(50.0, card.TimeInZonePercent, 1e-9);
            Assert.AreEqual(42.5, card.PointsPerMinute, 1e-9);
        }

        [TestMethod]
        public void BuildCard_NullOrEmptySession_IsSafe()
        {
            ScoreCard nullCard = ComboScoring.BuildCard(null);
            Assert.AreEqual(0.0, nullCard.Score, 1e-9);
            Assert.AreEqual(0, nullCard.EventCount);

            ScoreCard emptyCard = ComboScoring.BuildCard(new ComboSession());
            Assert.AreEqual(0.0, emptyCard.PointsPerMinute, 1e-9);
            Assert.AreEqual(0.0, emptyCard.TimeInZonePercent, 1e-9);
        }

        [TestMethod]
        public void BuildCard_ReconcilesWithALiveEngineSession()
        {
            // End-to-end: whatever the engine scores, the card built from its own event log must agree.
            var config = new ZoneConfig();
            var engine = new ComboEngine(config);

            for (int i = 0; i < 18000; i++)   // 5 minutes at 60 Hz
            {
                double t = i / 60.0;
                double watts = ((i / 600) % 4) switch
                {
                    0 => 200.0,   // in zone
                    1 => 205.0,   // in zone, climbing below
                    2 => 120.0,   // out of zone (combo breaks)
                    _ => 320.0,   // sprint
                };

                engine.Advance(1.0 / 60.0, watts, ((i / 600) % 4) == 1 ? 6.0 : 0.0);
            }

            ScoreCard card = ComboScoring.BuildCard(engine.Snapshot("Pyramid"));

            Assert.AreEqual(engine.Score, card.TotalFromEvents, 1e-6, "event amounts must sum to the score");
            Assert.AreEqual(engine.Score, card.Score, 1e-6);
            Assert.AreEqual(engine.XpEarned, card.XpEarned, 1e-9);
            Assert.AreEqual("Pyramid", card.WorkoutMode);
            Assert.IsTrue(card.EventCount > 0, "a 5 minute session must produce events");
            Assert.IsTrue(card.CombosLost > 0, "the out-of-zone blocks must break the combo at least once");
        }
    }
}
