using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BikeFitness.Shared.Models;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>
    /// One telemetry sample of a ride profile. Mirrors the app's existing 1 Hz
    /// <see cref="WorkoutDataPoint"/> so a recorded ride needs no re-encoding to become a ghost.
    /// </summary>
    public sealed class RideProfileSample
    {
        /// <summary>Seconds from the start of the ride.</summary>
        public double T { get; set; }

        /// <summary>Cumulative distance in metres.</summary>
        public double DistanceMeters { get; set; }

        /// <summary>Instantaneous speed in km/h.</summary>
        public double SpeedKph { get; set; }

        /// <summary>Grade in percent; positive is uphill (matches the simulation's convention).</summary>
        public double GradePercent { get; set; }

        /// <summary>
        /// Measured power in watts, when the source ride had a power meter. Zero means "not recorded";
        /// the harness has no power meter, so pseudo-power comes from <c>RiderPowerModel</c> instead.
        /// </summary>
        public double Power { get; set; }
    }

    /// <summary>
    /// A ride's distance/time/grade trace, replayable as a second rider (POC #1 "Ghost Racer").
    /// <para>
    /// Pure data + adapters: no clock, no timers, no BLE, no file writes outside the POC scratch
    /// directory. Samples are 1 Hz (the app's data timer interval), so consumers must interpolate —
    /// see <see cref="GhostReplay"/>.
    /// </para>
    /// </summary>
    public sealed class RideProfile
    {
        public const string SourceWorkoutReport = "workout-report";
        public const string SourceSynthetic = "synthetic";
        public const string SourceHarnessLive = "harness-live";

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        /// <summary>Human-readable name shown in the harness profile picker.</summary>
        public string Label { get; set; } = "";

        /// <summary>Where this profile came from; one of the <c>Source*</c> constants.</summary>
        public string Source { get; set; } = SourceSynthetic;

        /// <summary>Samples in ride order.</summary>
        public List<RideProfileSample> Samples { get; set; } = new List<RideProfileSample>();

        public bool IsEmpty => Samples.Count == 0;

        /// <summary>Profile duration in seconds (time of the last sample).</summary>
        public double DurationSeconds => Samples.Count == 0 ? 0 : Samples[Samples.Count - 1].T;

        /// <summary>Total distance in metres (distance of the last sample).</summary>
        public double TotalDistanceMeters => Samples.Count == 0 ? 0 : Samples[Samples.Count - 1].DistanceMeters;

        /// <summary>
        /// Adapts the report the app already writes ("Save Report" → <c>Workout_*.json</c>) into a
        /// profile. One sample in, one sample out — no resampling, no new capture pipeline.
        /// </summary>
        public static RideProfile FromWorkoutReport(WorkoutReport? report)
        {
            var profile = new RideProfile
            {
                Label = BuildReportLabel(report),
                Source = SourceWorkoutReport,
            };

            if (report?.DataPoints == null)
            {
                return profile;
            }

            foreach (WorkoutDataPoint point in report.DataPoints)
            {
                if (point == null) continue;

                profile.Samples.Add(new RideProfileSample
                {
                    T = point.ElapsedSeconds,
                    DistanceMeters = point.DistanceMeters,
                    SpeedKph = point.SpeedKph,
                    GradePercent = point.GradePercent,
                    Power = point.Power,
                });
            }

            // A recorded report is already well-formed, so this is a no-op for real rides; it only
            // guards the replay against a truncated or hand-edited file (time/distance going
            // backwards would otherwise break interpolation).
            profile.Normalise();
            return profile;
        }

        /// <summary>
        /// Deterministic synthetic ride: <paramref name="seconds"/> of 1 Hz samples inside the harness
        /// auto-drive envelope (grade −5…+8 %, speed 5–40 kph). Same seed ⇒ identical trace, on every
        /// platform and .NET version, so the harness can be feel-tested without owning a recording.
        /// </summary>
        public static RideProfile Synthetic(int seconds = 1200, int seed = 20260918)
        {
            int durationSeconds = Math.Max(1, seconds);
            var profile = new RideProfile
            {
                Label = $"Synthetic {durationSeconds / 60.0:0.#} min",
                Source = SourceSynthetic,
            };

            var rng = new PocRandom(seed);
            double distanceMeters = 0;

            for (int t = 0; t <= durationSeconds; t++)
            {
                double progress = t / (double)durationSeconds;

                // Slow rolling terrain plus a small deterministic wobble.
                double sweep = Math.Sin(progress * Math.PI * 2.0 * 3.0);
                double grade = Clamp(2.0 + (6.5 * sweep) + ((rng.NextDouble() - 0.5) * 1.5), -5.0, 8.0);

                // Speed envelope deliberately matches the harness auto-drive sliders; climbing costs speed.
                double baseSpeed = 24.0 + (8.0 * Math.Sin(progress * Math.PI * 2.0 * 5.0));
                double speed = Clamp(baseSpeed - (grade * 1.1) + ((rng.NextDouble() - 0.5) * 2.0), 5.0, 40.0);

                profile.Samples.Add(new RideProfileSample
                {
                    T = t,
                    DistanceMeters = distanceMeters,
                    SpeedKph = speed,
                    GradePercent = grade,
                    // No power meter in the harness; pseudo-power is derived from speed + grade by
                    // RiderPowerModel. Zero here means "unmeasured", not "zero watts".
                    Power = 0,
                });

                distanceMeters += speed * 1000.0 / 3600.0;
            }

            return profile;
        }

        /// <summary>
        /// Reads a profile from disk. Accepts either this class's own JSON shape or a raw
        /// <c>Workout_*.json</c> report. Read-only: a missing, unreadable or corrupt file yields an
        /// empty profile instead of throwing, because the harness must never fail to open a ride file.
        /// </summary>
        public static RideProfile Load(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new RideProfile();
            }

            try
            {
                string json = File.ReadAllText(path);

                RideProfile? profile = JsonSerializer.Deserialize<RideProfile>(json, ReadOptions);
                if (profile?.Samples is { Count: > 0 })
                {
                    if (string.IsNullOrWhiteSpace(profile.Source)) profile.Source = SourceWorkoutReport;
                    profile.Normalise();
                    return profile;
                }

                WorkoutReport? report = JsonSerializer.Deserialize<WorkoutReport>(json, ReadOptions);
                if (report?.DataPoints is { Count: > 0 })
                {
                    return FromWorkoutReport(report);
                }

                return new RideProfile();
            }
            catch (Exception)
            {
                return new RideProfile();
            }
        }

        /// <summary>
        /// Writes the profile as JSON. Refuses any path outside <see cref="PocScratch"/> so a POC can
        /// never overwrite a real ride report or the app's settings.
        /// </summary>
        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A path is required.", nameof(path));
            }

            if (!PocScratch.IsInScratch(path))
            {
                throw new InvalidOperationException(
                    $"POC profiles may only be written under '{PocScratch.DirectoryPath}'.");
            }

            PocScratch.EnsureDirectory();
            File.WriteAllText(path, JsonSerializer.Serialize(this, WriteOptions));
        }

        /// <summary>
        /// Makes the trace safe to interpolate in place: drops non-finite samples and forces time and
        /// distance to be non-decreasing. Returns a no-op for a well-formed recorded ride.
        /// </summary>
        public void Normalise()
        {
            var clean = new List<RideProfileSample>(Samples.Count);
            double lastT = double.NegativeInfinity;
            double lastDistance = 0;
            bool first = true;

            foreach (RideProfileSample sample in Samples)
            {
                if (sample == null) continue;
                if (!double.IsFinite(sample.T) || !double.IsFinite(sample.DistanceMeters)) continue;

                double t = Math.Max(sample.T, lastT);
                double distance = first ? sample.DistanceMeters : Math.Max(sample.DistanceMeters, lastDistance);

                sample.T = t;
                sample.DistanceMeters = distance;
                if (!double.IsFinite(sample.SpeedKph)) sample.SpeedKph = 0;
                if (!double.IsFinite(sample.GradePercent)) sample.GradePercent = 0;
                if (!double.IsFinite(sample.Power)) sample.Power = 0;

                lastT = t;
                lastDistance = distance;
                first = false;
                clean.Add(sample);
            }

            Samples = clean;
        }

        private static string BuildReportLabel(WorkoutReport? report)
        {
            string mode = report?.Summary?.WorkoutMode ?? string.Empty;
            string label = string.IsNullOrWhiteSpace(mode) ? "Recorded ride" : mode;

            if (report?.Summary != null && report.Summary.Date != default)
            {
                label += $" {report.Summary.Date:yyyy-MM-dd HH:mm}";
            }

            return label;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
