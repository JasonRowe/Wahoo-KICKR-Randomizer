using System;
using System.Collections.Generic;

namespace BikeFitnessApp.Models
{
    public class WorkoutDataPoint
    {
        public int ElapsedSeconds { get; set; }
        public int Power { get; set; }
        public double SpeedKph { get; set; }
        public double DistanceMeters { get; set; }
        public double GradePercent { get; set; }
        public int? HeartRate { get; set; }
    }

    public class WorkoutSummary
    {
        public DateTime Date { get; set; }
        public int DurationSeconds { get; set; }
        public double TotalDistanceMeters { get; set; }
        public double AvgPower { get; set; }
        public int MaxPower { get; set; }
        public double? AvgHeartRate { get; set; }
        public int? MaxHeartRate { get; set; }
        public string WorkoutMode { get; set; } = "";
    }

    public class WorkoutReport
    {
        public WorkoutSummary Summary { get; set; } = new WorkoutSummary();
        public List<WorkoutDataPoint> DataPoints { get; set; } = new List<WorkoutDataPoint>();
    }
}
