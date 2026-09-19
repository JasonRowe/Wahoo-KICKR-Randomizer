using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BikeFitness.Shared.Models;
using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.SecondRider
{
    /// <summary>
    /// L0/L1 tests for the ghost-racer profile adapter (POC #1, PR A).
    /// Pure logic: no WPF, no BLE, no clock.
    /// </summary>
    [TestClass]
    public class RideProfileTests
    {
        [TestMethod]
        public void FromWorkoutReport_MapsEverySample_AndPreservesDistanceOrder()
        {
            WorkoutReport report = BuildReport(60);

            RideProfile profile = RideProfile.FromWorkoutReport(report);

            Assert.AreEqual(60, profile.Samples.Count, "every data point must become a sample");
            Assert.AreEqual(RideProfile.SourceWorkoutReport, profile.Source);

            for (int i = 0; i < report.DataPoints.Count; i++)
            {
                Assert.AreEqual(report.DataPoints[i].ElapsedSeconds, profile.Samples[i].T, 1e-9, $"T at {i}");
                Assert.AreEqual(report.DataPoints[i].DistanceMeters, profile.Samples[i].DistanceMeters, 1e-9, $"distance at {i}");
                Assert.AreEqual(report.DataPoints[i].SpeedKph, profile.Samples[i].SpeedKph, 1e-9, $"speed at {i}");
                Assert.AreEqual(report.DataPoints[i].GradePercent, profile.Samples[i].GradePercent, 1e-9, $"grade at {i}");
                Assert.AreEqual(report.DataPoints[i].Power, profile.Samples[i].Power, 1e-9, $"power at {i}");
            }

            for (int i = 1; i < profile.Samples.Count; i++)
            {
                Assert.IsTrue(
                    profile.Samples[i].DistanceMeters >= profile.Samples[i - 1].DistanceMeters,
                    $"distance order preserved at sample {i}");
                Assert.IsTrue(profile.Samples[i].T >= profile.Samples[i - 1].T, $"time order preserved at sample {i}");
            }
        }

        [TestMethod]
        public void FromWorkoutReport_EmptyReport_YieldsEmptyProfile_NoThrow()
        {
            RideProfile profile = RideProfile.FromWorkoutReport(new WorkoutReport());

            Assert.IsTrue(profile.IsEmpty);
            Assert.AreEqual(0.0, profile.DurationSeconds, 1e-9);
            Assert.AreEqual(0.0, profile.TotalDistanceMeters, 1e-9);

            RideProfile fromNull = RideProfile.FromWorkoutReport(null);
            Assert.IsTrue(fromNull.IsEmpty);
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyProfile_DoesNotThrow()
        {
            string missing = Path.Combine(Path.GetTempPath(), $"BikeFitnessPoc-missing-{Guid.NewGuid():N}.json");

            Assert.IsTrue(RideProfile.Load(missing).IsEmpty);
            Assert.IsTrue(RideProfile.Load(null).IsEmpty);
            Assert.IsTrue(RideProfile.Load(string.Empty).IsEmpty);
        }

        [TestMethod]
        public void Load_WorkoutReportJson_AdaptsRealReportShape()
        {
            // The app writes the report with default (PascalCase) property names, so the loader has to
            // accept that shape as well as its own profile shape.
            PocScratch.EnsureDirectory();
            string path = PocScratch.FilePath($"report-test-{Guid.NewGuid():N}.json");
            WorkoutReport report = BuildReport(30);
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

            try
            {
                RideProfile profile = RideProfile.Load(path);

                Assert.AreEqual(30, profile.Samples.Count);
                Assert.AreEqual(RideProfile.SourceWorkoutReport, profile.Source);
                Assert.AreEqual(report.DataPoints[29].DistanceMeters, profile.TotalDistanceMeters, 1e-6);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_CorruptJson_ReturnsEmptyProfile_NoThrow()
        {
            PocScratch.EnsureDirectory();
            string path = PocScratch.FilePath($"corrupt-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, "{ this is not json");

            try
            {
                Assert.IsTrue(RideProfile.Load(path).IsEmpty);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Save_RoundTripsThroughScratchDirectory()
        {
            RideProfile original = RideProfile.Synthetic(30, seed: 7);
            original.Label = "round trip";
            string path = PocScratch.FilePath($"profile-roundtrip-{Guid.NewGuid():N}.json");

            try
            {
                original.Save(path);
                RideProfile reloaded = RideProfile.Load(path);

                Assert.AreEqual(original.Samples.Count, reloaded.Samples.Count);
                Assert.AreEqual("round trip", reloaded.Label);
                Assert.AreEqual(original.TotalDistanceMeters, reloaded.TotalDistanceMeters, 1e-6);
                Assert.AreEqual(original.Samples[7].GradePercent, reloaded.Samples[7].GradePercent, 1e-6);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Save_OutsideScratchDirectory_Throws()
        {
            RideProfile profile = RideProfile.Synthetic(5);
            string outside = Path.Combine(Path.GetTempPath(), $"not-poc-{Guid.NewGuid():N}.json");

            Assert.ThrowsExactly<InvalidOperationException>(() => profile.Save(outside));
            Assert.IsFalse(File.Exists(outside), "a refused save must not create the file");
        }

        [TestMethod]
        public void PocScratch_PathsStayUnderTempDirectory()
        {
            Assert.IsTrue(PocScratch.DirectoryPath.StartsWith(Path.GetTempPath(), StringComparison.Ordinal));
            Assert.IsTrue(PocScratch.IsInScratch(PocScratch.FilePath("xp.poc.json")));
            Assert.IsFalse(PocScratch.IsInScratch(Path.Combine(Path.GetTempPath(), "elsewhere.json")));
        }

        [TestMethod]
        public void Synthetic_SameSeed_ProducesIdenticalTrace()
        {
            RideProfile a = RideProfile.Synthetic(120, seed: 20260918);
            RideProfile b = RideProfile.Synthetic(120, seed: 20260918);

            Assert.AreEqual(a.Samples.Count, b.Samples.Count);
            for (int i = 0; i < a.Samples.Count; i++)
            {
                Assert.AreEqual(a.Samples[i].DistanceMeters, b.Samples[i].DistanceMeters, 0.0, $"distance at {i}");
                Assert.AreEqual(a.Samples[i].SpeedKph, b.Samples[i].SpeedKph, 0.0, $"speed at {i}");
                Assert.AreEqual(a.Samples[i].GradePercent, b.Samples[i].GradePercent, 0.0, $"grade at {i}");
            }
        }

        [TestMethod]
        public void Synthetic_DifferentSeed_ChangesTrace()
        {
            RideProfile a = RideProfile.Synthetic(120, seed: 1);
            RideProfile b = RideProfile.Synthetic(120, seed: 2);

            bool anyDifference = false;
            for (int i = 0; i < a.Samples.Count && !anyDifference; i++)
            {
                anyDifference = Math.Abs(a.Samples[i].DistanceMeters - b.Samples[i].DistanceMeters) > 1e-6;
            }

            Assert.IsTrue(anyDifference, "two seeds must not produce the same trace");
        }

        [TestMethod]
        public void Synthetic_Defaults_ProduceMonotonicDistance()
        {
            RideProfile profile = RideProfile.Synthetic();

            Assert.AreEqual(1201, profile.Samples.Count, "1200 s at 1 Hz plus the t=0 sample");
            Assert.AreEqual(1200.0, profile.DurationSeconds, 1e-9);
            Assert.IsTrue(profile.TotalDistanceMeters > 1000.0, "a 20 minute ride must cover kilometres");

            for (int i = 1; i < profile.Samples.Count; i++)
            {
                Assert.IsTrue(
                    profile.Samples[i].DistanceMeters > profile.Samples[i - 1].DistanceMeters,
                    $"distance must strictly increase at sample {i}");
                Assert.AreEqual(i, profile.Samples[i].T, 1e-9, "samples are 1 Hz");
            }
        }

        [TestMethod]
        public void Synthetic_StaysInsideHarnessAutoDriveEnvelope()
        {
            RideProfile profile = RideProfile.Synthetic(600);

            foreach (RideProfileSample sample in profile.Samples)
            {
                Assert.IsTrue(sample.SpeedKph >= 5.0 && sample.SpeedKph <= 40.0, $"speed {sample.SpeedKph} out of envelope");
                Assert.IsTrue(sample.GradePercent >= -5.0 && sample.GradePercent <= 8.0, $"grade {sample.GradePercent} out of envelope");
            }
        }

        [TestMethod]
        public void Normalise_ForcesMonotonicTimeAndDistance()
        {
            var profile = new RideProfile
            {
                Samples = new List<RideProfileSample>
                {
                    new RideProfileSample { T = 0, DistanceMeters = 0, SpeedKph = 25, GradePercent = 0 },
                    new RideProfileSample { T = 2, DistanceMeters = 20, SpeedKph = 25, GradePercent = 1 },
                    new RideProfileSample { T = 1, DistanceMeters = 10, SpeedKph = double.NaN, GradePercent = 1 },
                    new RideProfileSample { T = double.NaN, DistanceMeters = 40, SpeedKph = 25, GradePercent = 1 },
                    new RideProfileSample { T = 4, DistanceMeters = 30, SpeedKph = 25, GradePercent = 1 },
                },
            };

            profile.Normalise();

            Assert.AreEqual(4, profile.Samples.Count, "non-finite samples are dropped");
            for (int i = 1; i < profile.Samples.Count; i++)
            {
                Assert.IsTrue(profile.Samples[i].T >= profile.Samples[i - 1].T, $"T monotonic at {i}");
                Assert.IsTrue(profile.Samples[i].DistanceMeters >= profile.Samples[i - 1].DistanceMeters, $"distance monotonic at {i}");
            }

            Assert.AreEqual(0.0, profile.Samples[2].SpeedKph, 1e-9, "NaN speed is zeroed");
        }

        private static WorkoutReport BuildReport(int seconds)
        {
            var report = new WorkoutReport
            {
                Summary = new WorkoutSummary
                {
                    Date = new DateTime(2026, 9, 15, 6, 30, 0),
                    DurationSeconds = seconds,
                    WorkoutMode = "Hilly",
                },
            };

            double distance = 0;
            for (int t = 0; t < seconds; t++)
            {
                double speed = 24.0 + (4.0 * Math.Sin(t / 10.0));
                report.DataPoints.Add(new WorkoutDataPoint
                {
                    ElapsedSeconds = t,
                    DistanceMeters = distance,
                    SpeedKph = speed,
                    GradePercent = 2.5 * Math.Sin(t / 20.0),
                    Power = 180 + t,
                });

                distance += speed * 1000.0 / 3600.0;
            }

            return report;
        }
    }
}
