using System;
using System.Collections.Generic;

namespace BikeFitness.Shared.Scoring
{
    /// <summary>
    /// A finished (or previewed) scoring session. Produced by <see cref="ComboEngine.Snapshot"/> and turned
    /// into a <see cref="ScoreCard"/> by <see cref="ComboScoring.BuildCard"/>.
    /// </summary>
    public sealed class ComboSession
    {
        public string WorkoutMode { get; set; } = "";

        public double Score { get; set; }

        public double XpEarned { get; set; }

        public double BestComboSeconds { get; set; }

        public double TimeInZoneSeconds { get; set; }

        public double DurationSeconds { get; set; }

        public int CombosLost { get; set; }

        /// <summary>
        /// Every event the engine raised this session, in order. Accrual events carry the points actually
        /// banked since the previous popup, so the sum of all event amounts equals <see cref="Score"/>.
        /// </summary>
        public List<ScoreEvent> Events { get; set; } = new List<ScoreEvent>();
    }

    /// <summary>The end-of-ride summary. Pure data — the harness decides how to draw it.</summary>
    public sealed class ScoreCard
    {
        public string WorkoutMode { get; set; } = "";
        public double Score { get; set; }
        public double XpEarned { get; set; }
        public double BestComboSeconds { get; set; }
        public double TimeInZoneSeconds { get; set; }
        public double TimeInZonePercent { get; set; }
        public double PointsPerMinute { get; set; }
        public int CombosLost { get; set; }
        public int EventCount { get; set; }

        /// <summary>Points contributed per event kind (ZONE, CLIMB, SPRINT, ...).</summary>
        public IReadOnlyDictionary<string, double> TotalsByKind { get; set; } = new Dictionary<string, double>();

        /// <summary>Sum of every event amount — must equal <see cref="Score"/> (cross-check for the card).</summary>
        public double TotalFromEvents { get; set; }
    }

    /// <summary>Tier thresholds, multipliers, and the score card builder.</summary>
    public static class ComboScoring
    {
        /// <summary>In-zone seconds needed for ×1.5.</summary>
        public const double Tier2Seconds = 15.0;

        /// <summary>In-zone seconds needed for ×2.0.</summary>
        public const double Tier3Seconds = 30.0;

        /// <summary>In-zone seconds needed for ×3.0.</summary>
        public const double Tier4Seconds = 60.0;

        /// <summary>
        /// Tier for a run of in-zone seconds. Boundaries are inclusive on the lower edge: exactly 15 s is
        /// already ×1.5, exactly 60 s is already ×3.
        /// </summary>
        public static ComboTier TierFor(double comboSeconds)
        {
            if (!double.IsFinite(comboSeconds)) return ComboTier.Base;
            if (comboSeconds >= Tier4Seconds) return ComboTier.Tier4;
            if (comboSeconds >= Tier3Seconds) return ComboTier.Tier3;
            if (comboSeconds >= Tier2Seconds) return ComboTier.Tier2;
            return ComboTier.Base;
        }

        /// <summary>Score multiplier for a tier: ×1.0 / ×1.5 / ×2.0 / ×3.0.</summary>
        public static double MultiplierFor(ComboTier tier)
        {
            return tier switch
            {
                ComboTier.Tier2 => 1.5,
                ComboTier.Tier3 => 2.0,
                ComboTier.Tier4 => 3.0,
                _ => 1.0,
            };
        }

        /// <summary>One tier down, never below base — what a broken combo costs.</summary>
        public static ComboTier OneTierDown(ComboTier tier)
        {
            return tier <= ComboTier.Base ? ComboTier.Base : (ComboTier)((int)tier - 1);
        }

        /// <summary>Builds the card, deriving every total from the session's own event log.</summary>
        public static ScoreCard BuildCard(ComboSession? session)
        {
            var card = new ScoreCard();
            if (session == null) return card;

            var totals = new Dictionary<string, double>();
            double sum = 0;
            int count = 0;

            foreach (ScoreEvent scoreEvent in session.Events)
            {
                if (scoreEvent == null) continue;

                count++;
                sum += scoreEvent.Amount;

                if (!totals.TryGetValue(scoreEvent.Kind, out double existing)) existing = 0;
                totals[scoreEvent.Kind] = existing + scoreEvent.Amount;
            }

            card.WorkoutMode = session.WorkoutMode;
            card.Score = session.Score;
            card.XpEarned = session.XpEarned;
            card.BestComboSeconds = session.BestComboSeconds;
            card.TimeInZoneSeconds = session.TimeInZoneSeconds;
            card.CombosLost = session.CombosLost;
            card.EventCount = count;
            card.TotalFromEvents = sum;
            card.TotalsByKind = totals;

            card.TimeInZonePercent = session.DurationSeconds > 0
                ? session.TimeInZoneSeconds / session.DurationSeconds * 100.0
                : 0;
            card.PointsPerMinute = session.DurationSeconds > 0
                ? session.Score / (session.DurationSeconds / 60.0)
                : 0;

            return card;
        }
    }
}
