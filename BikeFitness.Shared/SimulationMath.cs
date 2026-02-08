using System.Windows.Media;

namespace BikeFitness.Shared
{
    /// <summary>
    /// Pure math and mapping functions extracted from SimulationCanvas for testability.
    /// </summary>
    public static class SimulationMath
    {
        /// <summary>
        /// Clamps a value to the range [0, 1].
        /// </summary>
        public static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        /// <summary>
        /// Hermite smoothstep interpolation. Input is clamped to [0, 1].
        /// Returns smooth S-curve: 3t² - 2t³
        /// </summary>
        public static double SmoothStep(double value)
        {
            value = Clamp01(value);
            return value * value * (3 - (2 * value));
        }

        /// <summary>
        /// Returns the display text for a biome theme label.
        /// </summary>
        public static string GetBiomeLabelText(BackgroundTheme theme)
        {
            return theme switch
            {
                BackgroundTheme.Mountain => "Entering Mountains",
                BackgroundTheme.Plain => "Entering Plains",
                BackgroundTheme.Desert => "Entering Desert",
                BackgroundTheme.Ocean => "Entering Ocean",
                _ => "Entering Plains"
            };
        }
    }

    /// <summary>
    /// Background biome themes used in simulation rendering.
    /// </summary>
    public enum BackgroundTheme
    {
        Mountain,
        Plain,
        Desert,
        Ocean,
        Transition
    }
}
