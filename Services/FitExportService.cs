using System;
using System.Collections.Generic;
using System.IO;
using BikeFitnessApp.Models;
using Dynastream.Fit;

namespace BikeFitnessApp.Services
{
    /// <summary>
    /// Provides FIT file export functionality for workout data.
    /// FIT (Flexible and Interoperable Data Transfer) is Garmin's binary format
    /// for fitness data, compatible with Strava and other platforms.
    /// </summary>
    public static class FitExportService
    {
        /// <summary>
        /// Exports a workout report to a FIT Activity file.
        /// </summary>
        /// <param name="report">The workout report containing summary and data points.</param>
        /// <param name="filePath">The destination file path.</param>
        public static void ExportToFit(WorkoutReport report, string filePath)
        {
            // Create the FIT file encoder
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            var encoder = new Encode(ProtocolVersion.V20);
            encoder.Open(fileStream);

            // Write File ID Message (required first message)
            WriteFileIdMessage(encoder, report.Summary.Date);

            // Write Record Messages (one per data point)
            WriteRecordMessages(encoder, report);

            // Write Lap Message (summary of entire workout as single lap)
            WriteLapMessage(encoder, report);

            // Write Session Message
            WriteSessionMessage(encoder, report);

            // Write Activity Message
            WriteActivityMessage(encoder, report);

            encoder.Close();
        }

        private static void WriteFileIdMessage(Encode encoder, System.DateTime startTime)
        {
            var fileIdMsg = new FileIdMesg();
            fileIdMsg.SetType(Dynastream.Fit.File.Activity);
            fileIdMsg.SetManufacturer(Manufacturer.Development);
            fileIdMsg.SetProduct(1);
            fileIdMsg.SetSerialNumber(12345);
            fileIdMsg.SetTimeCreated(new Dynastream.Fit.DateTime(startTime));
            encoder.Write(fileIdMsg);
        }

        private static void WriteRecordMessages(Encode encoder, WorkoutReport report)
        {
            var startTime = report.Summary.Date;

            foreach (var dp in report.DataPoints)
            {
                var recordMsg = new RecordMesg();
                
                // Timestamp for this record
                var recordTime = startTime.AddSeconds(dp.ElapsedSeconds);
                recordMsg.SetTimestamp(new Dynastream.Fit.DateTime(recordTime));

                // Power in watts
                if (dp.Power > 0)
                {
                    recordMsg.SetPower((ushort)dp.Power);
                }

                // Speed: convert km/h to m/s (FIT uses m/s)
                if (dp.SpeedKph > 0)
                {
                    var speedMps = dp.SpeedKph / 3.6;
                    recordMsg.SetSpeed((float)speedMps);
                }

                // Distance in meters
                if (dp.DistanceMeters > 0)
                {
                    recordMsg.SetDistance((float)dp.DistanceMeters);
                }

                // Grade percentage
                recordMsg.SetGrade((float)dp.GradePercent);

                // Heart rate (optional)
                if (dp.HeartRate.HasValue && dp.HeartRate.Value > 0)
                {
                    recordMsg.SetHeartRate((byte)dp.HeartRate.Value);
                }

                encoder.Write(recordMsg);
            }
        }

        private static void WriteLapMessage(Encode encoder, WorkoutReport report)
        {
            var lapMsg = new LapMesg();
            var startTime = report.Summary.Date;
            var endTime = startTime.AddSeconds(report.Summary.DurationSeconds);

            lapMsg.SetTimestamp(new Dynastream.Fit.DateTime(endTime));
            lapMsg.SetStartTime(new Dynastream.Fit.DateTime(startTime));
            lapMsg.SetTotalElapsedTime(report.Summary.DurationSeconds);
            lapMsg.SetTotalTimerTime(report.Summary.DurationSeconds);
            lapMsg.SetTotalDistance((float)report.Summary.TotalDistanceMeters);
            lapMsg.SetSport(Sport.Cycling);
            lapMsg.SetSubSport(SubSport.IndoorCycling);
            lapMsg.SetEvent(Event.Lap);
            lapMsg.SetEventType(EventType.Stop);

            // Power stats
            if (report.Summary.AvgPower > 0)
            {
                lapMsg.SetAvgPower((ushort)report.Summary.AvgPower);
            }
            if (report.Summary.MaxPower > 0)
            {
                lapMsg.SetMaxPower((ushort)report.Summary.MaxPower);
            }

            // Heart rate stats (if available)
            if (report.Summary.AvgHeartRate.HasValue)
            {
                lapMsg.SetAvgHeartRate((byte)report.Summary.AvgHeartRate.Value);
            }
            if (report.Summary.MaxHeartRate.HasValue)
            {
                lapMsg.SetMaxHeartRate((byte)report.Summary.MaxHeartRate.Value);
            }

            encoder.Write(lapMsg);
        }

        private static void WriteSessionMessage(Encode encoder, WorkoutReport report)
        {
            var sessionMsg = new SessionMesg();
            var startTime = report.Summary.Date;
            var endTime = startTime.AddSeconds(report.Summary.DurationSeconds);

            sessionMsg.SetTimestamp(new Dynastream.Fit.DateTime(endTime));
            sessionMsg.SetStartTime(new Dynastream.Fit.DateTime(startTime));
            sessionMsg.SetTotalElapsedTime(report.Summary.DurationSeconds);
            sessionMsg.SetTotalTimerTime(report.Summary.DurationSeconds);
            sessionMsg.SetTotalDistance((float)report.Summary.TotalDistanceMeters);
            sessionMsg.SetSport(Sport.Cycling);
            sessionMsg.SetSubSport(SubSport.IndoorCycling);
            sessionMsg.SetFirstLapIndex(0);
            sessionMsg.SetNumLaps(1);
            sessionMsg.SetEvent(Event.Session);
            sessionMsg.SetEventType(EventType.Stop);
            sessionMsg.SetTrigger(SessionTrigger.ActivityEnd);

            // Power stats
            if (report.Summary.AvgPower > 0)
            {
                sessionMsg.SetAvgPower((ushort)report.Summary.AvgPower);
            }
            if (report.Summary.MaxPower > 0)
            {
                sessionMsg.SetMaxPower((ushort)report.Summary.MaxPower);
            }

            // Heart rate stats
            if (report.Summary.AvgHeartRate.HasValue)
            {
                sessionMsg.SetAvgHeartRate((byte)report.Summary.AvgHeartRate.Value);
            }
            if (report.Summary.MaxHeartRate.HasValue)
            {
                sessionMsg.SetMaxHeartRate((byte)report.Summary.MaxHeartRate.Value);
            }

            encoder.Write(sessionMsg);
        }

        private static void WriteActivityMessage(Encode encoder, WorkoutReport report)
        {
            var activityMsg = new ActivityMesg();
            var endTime = report.Summary.Date.AddSeconds(report.Summary.DurationSeconds);

            activityMsg.SetTimestamp(new Dynastream.Fit.DateTime(endTime));
            activityMsg.SetTotalTimerTime(report.Summary.DurationSeconds);
            activityMsg.SetNumSessions(1);
            activityMsg.SetType(Activity.Manual);
            activityMsg.SetEvent(Event.Activity);
            activityMsg.SetEventType(EventType.Stop);

            encoder.Write(activityMsg);
        }
    }
}
