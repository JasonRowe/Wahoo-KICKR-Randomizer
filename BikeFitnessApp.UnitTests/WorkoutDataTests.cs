using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.Models;
using System;
using System.Collections.Generic;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class WorkoutDataTests
    {
        [TestMethod]
        public void WorkoutDataPoint_Properties_Work()
        {
            var point = new WorkoutDataPoint
            {
                ElapsedSeconds = 10,
                Power = 200,
                SpeedKph = 30.5,
                DistanceMeters = 100.0,
                GradePercent = 2.0,
                HeartRate = 150
            };

            Assert.AreEqual(10, point.ElapsedSeconds);
            Assert.AreEqual(200, point.Power);
            Assert.AreEqual(30.5, point.SpeedKph);
            Assert.AreEqual(100.0, point.DistanceMeters);
            Assert.AreEqual(2.0, point.GradePercent);
            Assert.AreEqual(150, point.HeartRate);
        }

        [TestMethod]
        public void WorkoutSummary_Properties_Work()
        {
            var now = DateTime.Now;
            var summary = new WorkoutSummary
            {
                Date = now,
                DurationSeconds = 3600,
                TotalDistanceMeters = 30000,
                AvgPower = 180.5,
                MaxPower = 350,
                AvgHeartRate = 145.2,
                MaxHeartRate = 170,
                WorkoutMode = "Hilly"
            };

            Assert.AreEqual(now, summary.Date);
            Assert.AreEqual(3600, summary.DurationSeconds);
            Assert.AreEqual(30000, summary.TotalDistanceMeters);
            Assert.AreEqual(180.5, summary.AvgPower);
            Assert.AreEqual(350, summary.MaxPower);
            Assert.AreEqual(145.2, summary.AvgHeartRate);
            Assert.AreEqual(170, summary.MaxHeartRate);
            Assert.AreEqual("Hilly", summary.WorkoutMode);
        }

        [TestMethod]
        public void WorkoutReport_Properties_Work()
        {
            var report = new WorkoutReport();
            Assert.IsNotNull(report.Summary);
            Assert.IsNotNull(report.DataPoints);

            var summary = new WorkoutSummary { MaxPower = 400 };
            var points = new List<WorkoutDataPoint> { new WorkoutDataPoint { Power = 100 } };

            report.Summary = summary;
            report.DataPoints = points;

            Assert.AreEqual(summary, report.Summary);
            Assert.AreEqual(points, report.DataPoints);
        }
    }
}
