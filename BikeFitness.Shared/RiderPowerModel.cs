using System;

namespace BikeFitness.Shared
{
    /// <summary>
    /// Physical constants for the pseudo-power model. Defaults are a road bike plus an 80 kg rider on the
    /// hoods — good enough to make a virtual rival's speed believable, which is the only job it has.
    /// </summary>
    public sealed class RiderConstants
    {
        /// <summary>Rider + bike mass, kg.</summary>
        public double TotalMassKg = 80.0;

        /// <summary>Coefficient of rolling resistance (typical asphalt + road tyre).</summary>
        public double Crr = 0.005;

        /// <summary>Frontal area × drag coefficient, m² (hoods position).</summary>
        public double CdA = 0.32;

        /// <summary>Air density, kg/m³ (sea level, 15 °C).</summary>
        public double AirDensity = 1.225;

        /// <summary>Drivetrain efficiency, 0–1.</summary>
        public double DrivetrainEfficiency = 0.97;
    }

    /// <summary>
    /// Pure, stateless power ↔ speed conversion for a rider on a gradient — the shared foundation of the
    /// rubber-band pacer (POC #2) and the combo/XP engine (POC #3).
    /// <para>
    /// <c>P = (m·g·(sinθ + Crr·cosθ) + ½·ρ·CdA·v²) · v / η</c>, solved for <c>v</c> by bisection (monotonic
    /// in <c>v</c> for a non-negative gradient), tolerance 1e-3 m/s, 60-iteration cap.
    /// </para>
    /// <para>
    /// <b>This is a model, not a measurement.</b> The harness has no power meter, so anything derived here
    /// must be surfaced with an approximation marker (<c>≈214 W</c>), never as a measured wattage.
    /// </para>
    /// </summary>
    public static class RiderPowerModel
    {
        public const double Gravity = 9.80665;
        public const double MaxSpeedMetersPerSecond = 25.0;
        public const double BisectionToleranceMetersPerSecond = 1e-3;
        public const int MaxBisectionIterations = 60;

        public const double MinGradePercent = -25.0;
        public const double MaxGradePercent = 25.0;
        public const double MinPowerWatts = 0.0;
        public const double MaxPowerWatts = 2000.0;

        private static readonly RiderConstants DefaultConstants = new RiderConstants();

        /// <summary>
        /// Power needed to hold <paramref name="speedKph"/> on <paramref name="gradePercent"/>.
        /// Never negative: below the freewheeling speed on a descent the rider is braking, and a negative
        /// "power" is not something the caller can use — the model reports 0 instead.
        /// </summary>
        public static double PowerFromSpeed(double speedKph, double gradePercent, RiderConstants? constants = null)
        {
            RiderConstants c = constants ?? DefaultConstants;
            double speedMetersPerSecond = Math.Clamp(Finite(speedKph, 0.0) / 3.6, 0.0, MaxSpeedMetersPerSecond);

            double grade = Math.Clamp(Finite(gradePercent, 0.0), MinGradePercent, MaxGradePercent);
            double theta = Math.Atan(grade / 100.0);

            return Math.Max(0.0, PowerAt(speedMetersPerSecond, theta, c));
        }

        /// <summary>
        /// Speed in km/h for a held <paramref name="powerWatts"/> on <paramref name="gradePercent"/>.
        /// Returns 0 for zero/negative power, and saturates at 25 m/s (90 km/h) rather than diverging.
        /// </summary>
        public static double SpeedFromPower(double powerWatts, double gradePercent, RiderConstants? constants = null)
        {
            RiderConstants c = constants ?? DefaultConstants;
            double power = Math.Clamp(Finite(powerWatts, 0.0), MinPowerWatts, MaxPowerWatts);
            if (power <= 0) return 0.0;

            double grade = Math.Clamp(Finite(gradePercent, 0.0), MinGradePercent, MaxGradePercent);
            double theta = Math.Atan(grade / 100.0);

            double low = 0.0;
            double high = MaxSpeedMetersPerSecond;
            if (PowerAt(high, theta, c) <= power) return high * 3.6;
            if (PowerAt(low, theta, c) >= power) return 0.0;

            for (int i = 0; i < MaxBisectionIterations && (high - low) > BisectionToleranceMetersPerSecond; i++)
            {
                double mid = 0.5 * (low + high);
                if (PowerAt(mid, theta, c) < power) low = mid;
                else high = mid;
            }

            return 0.5 * (low + high) * 3.6;
        }

        /// <summary>Watts per kilogram, for the harness's "pacer strength" slider.</summary>
        public static double WPerKgFromWatts(double watts, double massKg)
        {
            if (!double.IsFinite(watts) || !double.IsFinite(massKg) || massKg <= 0) return 0.0;
            return watts / massKg;
        }

        private static double PowerAt(double speedMetersPerSecond, double theta, RiderConstants constants)
        {
            double mass = constants.TotalMassKg > 0 ? constants.TotalMassKg : DefaultConstants.TotalMassKg;
            double crr = constants.Crr >= 0 ? constants.Crr : DefaultConstants.Crr;
            double cda = constants.CdA >= 0 ? constants.CdA : DefaultConstants.CdA;
            double airDensity = constants.AirDensity > 0 ? constants.AirDensity : DefaultConstants.AirDensity;
            double efficiency = constants.DrivetrainEfficiency > 0 ? constants.DrivetrainEfficiency : 1.0;

            double rolling = mass * Gravity * crr * Math.Cos(theta);
            double climbing = mass * Gravity * Math.Sin(theta);
            double drag = 0.5 * airDensity * cda * speedMetersPerSecond * speedMetersPerSecond;

            return (climbing + rolling + drag) * speedMetersPerSecond / efficiency;
        }

        private static double Finite(double value, double fallback)
        {
            return double.IsFinite(value) ? value : fallback;
        }
    }
}
