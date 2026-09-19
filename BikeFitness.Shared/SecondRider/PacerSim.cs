using System;
using System.Collections.Generic;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>Everything a duel produced. Used by tests and by the harness's end-of-run verdict.</summary>
    public sealed class PacerRunResult
    {
        public List<double> TimesSeconds { get; } = new List<double>();
        public List<double> GapsMeters { get; } = new List<double>();
        public List<double> PacerSpeedsKph { get; } = new List<double>();
        public List<double> RiderSpeedsKph { get; } = new List<double>();
        public List<double> RiderDistancesMeters { get; } = new List<double>();
        public List<PacerState> States { get; } = new List<PacerState>();

        /// <summary>Number of state changes over the run (a naive threshold implementation scores huge here).</summary>
        public int StateTransitions { get; internal set; }

        /// <summary>Frames where the pacer broke its relative-speed or physical-speed cap.</summary>
        public int SpeedCapViolations { get; internal set; }

        public double DurationSeconds => TimesSeconds.Count == 0 ? 0 : TimesSeconds[TimesSeconds.Count - 1];

        public double FinalGapMeters => GapsMeters.Count == 0 ? 0 : GapsMeters[GapsMeters.Count - 1];

        /// <summary>Seconds until the gap was inside <paramref name="toleranceMeters"/> of <paramref name="targetMeters"/>, or NaN.</summary>
        public double SecondsToReachBand(double targetMeters, double toleranceMeters)
        {
            for (int i = 0; i < GapsMeters.Count; i++)
            {
                if (Math.Abs(GapsMeters[i] - targetMeters) <= toleranceMeters) return TimesSeconds[i];
            }

            return double.NaN;
        }

        /// <summary>Peak-to-peak gap swing over the part of the run at or after <paramref name="afterSeconds"/>.</summary>
        public double GapAmplitudeAfter(double afterSeconds)
        {
            double min = double.MaxValue;
            double max = double.MinValue;

            for (int i = 0; i < GapsMeters.Count; i++)
            {
                if (TimesSeconds[i] < afterSeconds) continue;
                min = Math.Min(min, GapsMeters[i]);
                max = Math.Max(max, GapsMeters[i]);
            }

            return max < min ? 0 : max - min;
        }

        /// <summary>Largest absolute gap over the part of the run at or after <paramref name="afterSeconds"/>.</summary>
        public double MaxAbsGapAfter(double afterSeconds)
        {
            double worst = 0;
            for (int i = 0; i < GapsMeters.Count; i++)
            {
                if (TimesSeconds[i] < afterSeconds) continue;
                worst = Math.Max(worst, Math.Abs(GapsMeters[i]));
            }

            return worst;
        }

        public int CountState(PacerState state)
        {
            int count = 0;
            foreach (PacerState s in States)
            {
                if (s == state) count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Headless runner for <see cref="PacerModel"/>: replays a rider trace against the pacer and records the
    /// duel. This is the harness for the model itself — no UI, no BLE, deterministic.
    /// </summary>
    public static class PacerSim
    {
        /// <summary>
        /// Runs a duel. <paramref name="riderTrace"/> entries are (seconds, distanceMeters, speedKph,
        /// gradePercent), sampled at whatever resolution the caller has; they are interpolated onto
        /// <paramref name="deltaTime"/> steps so the pacer sees a uniform clock.
        /// </summary>
        public static PacerRunResult Run(
            PacerConfig config,
            IReadOnlyList<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> riderTrace,
            double deltaTime,
            double? initialGapMeters = null)
        {
            var result = new PacerRunResult();
            if (riderTrace == null || riderTrace.Count == 0 || !double.IsFinite(deltaTime) || deltaTime <= 0)
            {
                return result;
            }

            double dt = Math.Min(deltaTime, 2.0);
            var model = new PacerModel(config);
            double startDistance = riderTrace[0].DistanceMeters;
            model.Reset(startDistance, initialGapMeters ?? config.GapTargetMeters);

            double endTime = riderTrace[riderTrace.Count - 1].T;
            double previousState = (double)(int)model.State;

            for (double t = riderTrace[0].T; t <= endTime + 1e-9; t += dt)
            {
                (double distance, double speed, double grade) = SampleTrace(riderTrace, t);

                model.Advance(dt, distance, speed, grade);

                if ((int)model.State != (int)previousState)
                {
                    result.StateTransitions++;
                    previousState = (int)model.State;
                }

                if (riderTrace.Count > 0 && speed > 0.01)
                {
                    if (model.SpeedKph > (speed * config.MaxRelativeSpeed) + 1e-6) result.SpeedCapViolations++;
                }

                double physicalCap = RiderPowerModel.SpeedFromPower(
                    config.PacerWPerKg * config.Rider.TotalMassKg, grade, config.Rider);

                if (model.SpeedKph > physicalCap + 1e-6) result.SpeedCapViolations++;

                result.TimesSeconds.Add(t);
                result.GapsMeters.Add(model.GapMeters(distance));
                result.PacerSpeedsKph.Add(model.SpeedKph);
                result.RiderSpeedsKph.Add(speed);
                result.RiderDistancesMeters.Add(distance);
                result.States.Add(model.State);
            }

            return result;
        }

        /// <summary>
        /// Builds a constant-effort rider trace: the simplest fair test of the pacer's convergence.
        /// </summary>
        public static List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> ConstantEffortTrace(
            double seconds,
            double speedKph,
            double gradePercent = 0.0,
            double sampleSeconds = 1.0)
        {
            var trace = new List<(double, double, double, double)>();
            double distance = 0;
            double sample = sampleSeconds > 0 ? sampleSeconds : 1.0;

            for (double t = 0; t <= seconds + 1e-9; t += sample)
            {
                trace.Add((t, distance, speedKph, gradePercent));
                distance += speedKph / 3.6 * sample;
            }

            return trace;
        }

        private static (double Distance, double Speed, double Grade) SampleTrace(
            IReadOnlyList<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> trace,
            double t)
        {
            if (t <= trace[0].T) return (trace[0].DistanceMeters, trace[0].SpeedKph, trace[0].GradePercent);

            int last = trace.Count - 1;
            if (t >= trace[last].T) return (trace[last].DistanceMeters, trace[last].SpeedKph, trace[last].GradePercent);

            for (int i = 0; i < last; i++)
            {
                if (t < trace[i].T || t > trace[i + 1].T) continue;

                double span = trace[i + 1].T - trace[i].T;
                double f = span > 0 ? (t - trace[i].T) / span : 0;

                return (
                    trace[i].DistanceMeters + ((trace[i + 1].DistanceMeters - trace[i].DistanceMeters) * f),
                    trace[i].SpeedKph + ((trace[i + 1].SpeedKph - trace[i].SpeedKph) * f),
                    trace[i].GradePercent + ((trace[i + 1].GradePercent - trace[i].GradePercent) * f));
            }

            return (trace[last].DistanceMeters, trace[last].SpeedKph, trace[last].GradePercent);
        }
    }
}
