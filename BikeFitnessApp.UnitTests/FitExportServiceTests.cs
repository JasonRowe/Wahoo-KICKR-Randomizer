using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.Models;
using BikeFitnessApp.Services;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class FitExportServiceTests
    {
        private string _testFilePath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testFilePath = Path.Combine(Path.GetTempPath(), $"test_workout_{Guid.NewGuid()}.fit");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

        [TestMethod]
        public void ExportToFit_CreatesValidFile()
        {
            // Arrange
            var report = CreateTestReport(10);

            // Act
            FitExportService.ExportToFit(report, _testFilePath);

            // Assert
            Assert.IsTrue(File.Exists(_testFilePath), "FIT file should be created");
            
            var fileInfo = new FileInfo(_testFilePath);
            Assert.IsTrue(fileInfo.Length > 0, "FIT file should not be empty");
        }

        [TestMethod]
        public void ExportToFit_FileContainsFitHeader()
        {
            // Arrange
            var report = CreateTestReport(5);

            // Act
            FitExportService.ExportToFit(report, _testFilePath);

            // Assert - FIT files start with header containing ".FIT" signature at bytes 8-11
            var bytes = File.ReadAllBytes(_testFilePath);
            Assert.IsTrue(bytes.Length >= 14, "FIT file should be at least 14 bytes (header size)");
            
            // Check for ".FIT" signature (bytes 8-11 in 14-byte header)
            Assert.AreEqual((byte)'.', bytes[8]);
            Assert.AreEqual((byte)'F', bytes[9]);
            Assert.AreEqual((byte)'I', bytes[10]);
            Assert.AreEqual((byte)'T', bytes[11]);
        }

        [TestMethod]
        public void ExportToFit_EmptyDataPoints_Succeeds()
        {
            // Arrange - workout with no data points (edge case)
            var report = CreateTestReport(0);

            // Act
            FitExportService.ExportToFit(report, _testFilePath);

            // Assert
            Assert.IsTrue(File.Exists(_testFilePath), "FIT file should be created even with no data points");
            
            var fileInfo = new FileInfo(_testFilePath);
            Assert.IsTrue(fileInfo.Length > 0, "FIT file should contain at least header and metadata");
        }

        [TestMethod]
        public void ExportToFit_LargeWorkout_Succeeds()
        {
            // Arrange - 1 hour workout (3600 data points)
            var report = CreateTestReport(3600);

            // Act
            FitExportService.ExportToFit(report, _testFilePath);

            // Assert
            Assert.IsTrue(File.Exists(_testFilePath), "FIT file should be created for large workout");
            
            var fileInfo = new FileInfo(_testFilePath);
            Assert.IsTrue(fileInfo.Length > 10000, "FIT file for 1hr workout should be substantial size");
        }

        [TestMethod]
        public void ExportToFit_WithHeartRate_Succeeds()
        {
            // Arrange - workout with heart rate data
            var report = CreateTestReportWithHeartRate(10);

            // Act
            FitExportService.ExportToFit(report, _testFilePath);

            // Assert
            Assert.IsTrue(File.Exists(_testFilePath), "FIT file should be created with HR data");
        }

        private WorkoutReport CreateTestReport(int dataPointCount)
        {
            var startTime = DateTime.Now.AddMinutes(-dataPointCount);
            var dataPoints = new List<WorkoutDataPoint>();

            for (int i = 0; i < dataPointCount; i++)
            {
                dataPoints.Add(new WorkoutDataPoint
                {
                    ElapsedSeconds = i,
                    Power = 150 + (i % 50),
                    SpeedKph = 25.0 + (i % 10),
                    DistanceMeters = i * 7.0, // ~25 kph = ~7 m/s
                    GradePercent = (i % 20) - 5.0,
                    HeartRate = null
                });
            }

            return new WorkoutReport
            {
                Summary = new WorkoutSummary
                {
                    Date = startTime,
                    DurationSeconds = dataPointCount,
                    TotalDistanceMeters = dataPointCount * 7.0,
                    AvgPower = 175,
                    MaxPower = 200,
                    WorkoutMode = "Random"
                },
                DataPoints = dataPoints
            };
        }

        private WorkoutReport CreateTestReportWithHeartRate(int dataPointCount)
        {
            var report = CreateTestReport(dataPointCount);
            
            foreach (var dp in report.DataPoints)
            {
                dp.HeartRate = 130 + (dp.ElapsedSeconds % 30);
            }

            report.Summary.AvgHeartRate = 145;
            report.Summary.MaxHeartRate = 160;

            return report;
        }
    }
}
