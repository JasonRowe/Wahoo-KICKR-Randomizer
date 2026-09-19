using System;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>
    /// Pure "duel" maths shared by every second-rider POC: the gap between the two riders and the
    /// time delta the HUD shows.
    /// <para>
    /// Sign conventions, used by the ghost chip, the gap strip and the end-of-run verdict:
    /// positive gap = <b>the second rider is ahead</b> of you; negative delta = <b>you are ahead</b>
    /// (which is why the ghost HUD shows <c>-0:08</c> when you are beating your PB).
    /// </para>
    /// </summary>
    public static class DuelMath
    {
        /// <summary>Ghost/second-rider distance minus rider distance, in metres. Positive = rival ahead.</summary>
        public static double GapMeters(double secondRiderDistanceMeters, double riderDistanceMeters)
        {
            return secondRiderDistanceMeters - riderDistanceMeters;
        }

        /// <summary>
        /// Seconds the rider is ahead (negative) or behind (positive), i.e. how long the gap would take
        /// to close at the current combined speed. Zero when nobody is moving, because a time delta
        /// against a stopped rider is meaningless rather than infinite.
        /// </summary>
        public static double DeltaSeconds(
            double secondRiderDistanceMeters,
            double riderDistanceMeters,
            double riderSpeedKph,
            double secondRiderSpeedKph)
        {
            double gap = GapMeters(secondRiderDistanceMeters, riderDistanceMeters);
            double averageKph = (Math.Max(0, riderSpeedKph) + Math.Max(0, secondRiderSpeedKph)) * 0.5;
            double metersPerSecond = averageKph / 3.6;

            if (!double.IsFinite(metersPerSecond) || metersPerSecond < 0.1) return 0.0;
            return gap / metersPerSecond;
        }

        /// <summary>Formats a delta as <c>-0:08</c> / <c>+1:12</c> (sign included, for the HUD).</summary>
        public static string FormatDelta(double seconds)
        {
            if (!double.IsFinite(seconds)) seconds = 0;

            string sign = seconds < 0 ? "-" : "+";
            int totalSeconds = (int)Math.Round(Math.Abs(seconds), MidpointRounding.AwayFromZero);

            return $"{sign}{totalSeconds / 60}:{totalSeconds % 60:00}";
        }

        /// <summary>Gap with one decimal, absolute value — the number the HUD prints next to "GAP".</summary>
        public static string FormatGapMeters(double gapMeters)
        {
            if (!double.IsFinite(gapMeters)) gapMeters = 0;
            return $"{Math.Abs(gapMeters):F1} m";
        }

        /// <summary>
        /// One-line marker chip: direction arrow, gap in whole metres and the time delta,
        /// e.g. <c>▲ 22 m · +6:00</c>. Shared by the on-screen label and the off-screen chevron so the
        /// two can never disagree.
        /// </summary>
        public static string FormatGapChip(double gapMeters, double deltaSeconds)
        {
            if (!double.IsFinite(gapMeters)) gapMeters = 0;

            string arrow = gapMeters >= 0 ? "\u25B2" : "\u25BC";
            return $"{arrow} {Math.Abs(gapMeters):F0} m \u00B7 {FormatDelta(deltaSeconds)}";
        }
    }
}
