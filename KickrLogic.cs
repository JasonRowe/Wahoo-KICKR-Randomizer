using System;

namespace BikeFitnessApp
{
    public enum WorkoutMode
    {
        Random,
        Hilly,     // Smooth Sine Wave
        Mountain,  // Steep Triangle Wave
        Pyramid    // Steady increase then decrease
    }

    public class KickrLogic
    {
        private readonly Random _random;
        private const int WavePeriodSteps = 20; // How many intervals to complete a full wave (e.g. 20 * 30s = 10 mins)

        public KickrLogic()
        {
            _random = new Random();
        }

        // Constructor for dependency injection (useful for testing with a seeded random)
        public KickrLogic(Random random)
        {
            _random = random;
        }

        // Calculate resistance based on mode, min, max, and current step index
        public double CalculateResistance(WorkoutMode mode, double min, double max, int stepIndex)
        {
            // Bounds check - ensure min is actually min
            if (min > max) 
            {
                double temp = min;
                min = max;
                max = temp;
            }
            
            // NOTE: We removed the 0.0-1.0 clamping here to support Grade inputs (e.g. -15 to 20).
            // The clamping happens in the translation layer (CalculateResistanceFromGrade) or the final output.

            switch (mode)
            {
                case WorkoutMode.Hilly:
                    return CalculateSineWave(min, max, stepIndex);
                case WorkoutMode.Mountain:
                    return CalculateTriangleWave(min, max, stepIndex);
                case WorkoutMode.Pyramid:
                    return CalculatePyramid(min, max, stepIndex);
                case WorkoutMode.Random:
                default:
                    return _random.NextDouble() * (max - min) + min;
            }
        }

        // Backwards compatibility for existing tests/code if needed, defaults to Random
        public double CalculateResistance(double min, double max)
        {
            return CalculateResistance(WorkoutMode.Random, min, max, 0);
        }

        private double CalculatePyramid(double min, double max, int stepIndex)
        {
            // Longer period for Pyramid (40 steps = 20 mins @ 30s intervals)
            const int period = 40;
            double range = max - min;
            int cyclePosition = stepIndex % period;
            int halfCycle = period / 2;

            double progress;
            if (cyclePosition < halfCycle)
            {
                progress = (double)cyclePosition / halfCycle;
            }
            else
            {
                progress = 1.0 - ((double)(cyclePosition - halfCycle) / halfCycle);
            }

            return min + (range * progress);
        }

        private double CalculateSineWave(double min, double max, int stepIndex)
        {
            double amplitude = (max - min) / 2.0;
            double midPoint = min + amplitude;
            
            // 2 * PI * (progress through cycle)
            double angle = 2 * Math.PI * (stepIndex % WavePeriodSteps) / WavePeriodSteps;
            
            // Result is mid + amp * sin(angle)
            return midPoint + amplitude * Math.Sin(angle);
        }

        private double CalculateTriangleWave(double min, double max, int stepIndex)
        {
            double range = max - min;
            int cyclePosition = stepIndex % WavePeriodSteps;
            int halfCycle = WavePeriodSteps / 2;

            double progress;
            
            if (cyclePosition < halfCycle)
            {
                // Going Up: 0.0 to 1.0
                progress = (double)cyclePosition / halfCycle;
            }
            else
            {
                // Going Down: 1.0 to 0.0
                progress = 1.0 - ((double)(cyclePosition - halfCycle) / halfCycle);
            }

            return min + (range * progress);
        }

        public byte[] CreateCommandBytes(byte opCode, byte parameter)
        {
            return new byte[] { opCode, parameter };
        }

        public byte[] CreateCommandBytes(byte opCode, ushort parameter)
        {
            byte[] command = new byte[3];
            command[0] = opCode;
            command[1] = (byte)(parameter & 0xFF);
            command[2] = (byte)(parameter >> 8);
            return command;
        }

        public byte[] CreateCommandBytes(byte opCode)
        {
            return new byte[] { opCode };
        }

        public byte[] CreateWahooResistanceCommand(double resistancePercent)
        {
            // Wahoo Resistance Mode: OpCode 0x41
            // Range: 0-100%
            byte opCode = 0x41;
            int value = (int)Math.Clamp(resistancePercent * 100, 0, 100);
            return CreateCommandBytes(opCode, (byte)value);
        }

        public byte[] CreateWahooSimGradeCommand(double gradePercent, double weightKg = 85.0, double crr = 0.004, double cw = 0.6)
        {
            // Wahoo Sim Mode: OpCode 0x43
            // Structure (Hypothetical based on common Wahoo RE): 
            // Byte 0: 0x43
            // Byte 1-2: Weight (kg * 100, LE)
            // Byte 3-4: Crr (val * 10000, LE)
            // Byte 5-6: Cw (val * 1000, LE)
            // Byte 7-8: Grade (% * 100, LE)
            
            byte opCode = 0x43;
            
            ushort weightVal = (ushort)(Math.Clamp(weightKg, 0, 200) * 100);
            ushort crrVal = (ushort)(Math.Clamp(crr, 0, 0.1) * 10000);
            ushort cwVal = (ushort)(Math.Clamp(cw, 0, 2.0) * 1000);
            short gradeVal = (short)(Math.Clamp(gradePercent, -15.0, 20.0) * 100); // Signed for incline/decline

            byte[] command = new byte[9];
            command[0] = opCode;
            
            // Weight
            command[1] = (byte)(weightVal & 0xFF);
            command[2] = (byte)(weightVal >> 8);

            // Crr
            command[3] = (byte)(crrVal & 0xFF);
            command[4] = (byte)(crrVal >> 8);

            // Cw
            command[5] = (byte)(cwVal & 0xFF);
            command[6] = (byte)(cwVal >> 8);

            // Grade
            command[7] = (byte)(gradeVal & 0xFF);
            command[8] = (byte)(gradeVal >> 8);

            return command;
        }

        public byte[] CreateFtmsSimCommand(double gradePercent, double crr = 0.004, double cw = 0.51, double windSpeedMps = 0)
        {
            // FTMS OpCode 0x11: Set Indoor Bike Simulation Parameters
            // Payload: OpCode (1) + WindSpeed (2) + Grade (2) + Crr (1) + Cw (1)
            // Total: 7 Bytes
            
            byte opCode = 0x11;

            short windVal = (short)(Math.Clamp(windSpeedMps, -50, 50) * 1000);
            short gradeVal = (short)(Math.Clamp(gradePercent, -45.0, 45.0) * 100); // 0.01% resolution
            byte crrVal = (byte)(Math.Clamp(crr, 0, 0.0254) * 10000); // 0.0001 resolution
            byte cwVal = (byte)(Math.Clamp(cw, 0, 2.54) * 100); // 0.01 resolution (kg/m)

            byte[] command = new byte[7];
            command[0] = opCode;

            // Wind Speed (Int16, Little Endian)
            command[1] = (byte)(windVal & 0xFF);
            command[2] = (byte)(windVal >> 8);

            // Grade (Int16, Little Endian)
            command[3] = (byte)(gradeVal & 0xFF);
            command[4] = (byte)(gradeVal >> 8);

            // Crr (UInt8)
            command[5] = crrVal;

            // Cw (UInt8)
            command[6] = cwVal;

            return command;
        }

        public int ParsePower(byte[] data)
        {
            if (data == null || data.Length < 4) return 0;
            
            // Flags (16 bit) - Byte 0-1
            // Instantaneous Power (SInt16) - Byte 2-3
            
            // We assume standard format. Little Endian.
            short power = BitConverter.ToInt16(data, 2);
            return Math.Max(0, (int)power);
        }

        public (bool hasWheelData, uint wheelRevs, ushort lastWheelTime) ParseCscData(byte[] data)
        {
            if (data == null || data.Length == 0) return (false, 0, 0);

            byte flags = data[0];
            bool wheelRevPresent = (flags & 0x01) != 0;
            
            if (!wheelRevPresent) return (false, 0, 0);

            // Data index starts at 1
            int index = 1;

            if (data.Length < index + 6) return (false, 0, 0);

            uint wheelRevs = BitConverter.ToUInt32(data, index);
            ushort lastWheelTime = BitConverter.ToUInt16(data, index + 4);

            return (true, wheelRevs, lastWheelTime);
        }

        public (bool hasCrankData, ushort crankRevs, ushort lastCrankTime) ParseCscCrankData(byte[] data)
        {
            if (data == null || data.Length == 0) return (false, 0, 0);

            byte flags = data[0];
            bool wheelRevPresent = (flags & 0x01) != 0;
            bool crankRevPresent = (flags & 0x02) != 0;

            if (!crankRevPresent) return (false, 0, 0);

            int index = 1;
            if (wheelRevPresent)
            {
                index += 6; // Skip Wheel Data (4 + 2 bytes)
            }

            if (data.Length < index + 4) return (false, 0, 0);

            ushort crankRevs = BitConverter.ToUInt16(data, index);
            ushort lastCrankTime = BitConverter.ToUInt16(data, index + 2);

            return (true, crankRevs, lastCrankTime);
        }

        public double CalculateCadence(ushort prevRevs, ushort prevTime, ushort currRevs, ushort currTime)
        {
            if (currRevs < prevRevs) return 0; // Simple wrap-around handling: ignore negative diffs
            
            int revsDiff = currRevs - prevRevs;
            if (revsDiff == 0) return 0;

            // Time unit is 1/1024 seconds
            int timeDiff = currTime - prevTime;
            if (timeDiff < 0) timeDiff += 65536; // Wrap around adjustment for UInt16

            if (timeDiff == 0) return 0;

            double timeMinutes = (timeDiff / 1024.0) / 60.0;
            return revsDiff / timeMinutes;
        }

        public (bool hasCrankData, ushort crankRevs, ushort lastCrankTime) ParseCrankDataFromPower(byte[] data)
        {
            if (data == null || data.Length < 4) return (false, 0, 0);

            ushort flags = BitConverter.ToUInt16(data, 0);
            bool pedalBalancePresent = (flags & 0x01) != 0;
            bool torquePresent = (flags & 0x04) != 0;
            bool wheelRevPresent = (flags & 0x10) != 0;
            bool crankRevPresent = (flags & 0x20) != 0;

            if (!crankRevPresent) return (false, 0, 0);

            int index = 4; // Skip Flags(2) and Power(2)
            if (pedalBalancePresent) index += 1;
            if (torquePresent) index += 2;
            if (wheelRevPresent) index += 6;

            if (data.Length < index + 4) return (false, 0, 0);

            ushort crankRevs = BitConverter.ToUInt16(data, index);
            ushort lastCrankTime = BitConverter.ToUInt16(data, index + 2);

            return (true, crankRevs, lastCrankTime);
        }

        public (bool hasWheelData, uint wheelRevs, ushort lastWheelTime) ParseWheelDataFromPower(byte[] data)
        {
            if (data == null || data.Length < 4) return (false, 0, 0);

            ushort flags = BitConverter.ToUInt16(data, 0);
            bool pedalBalancePresent = (flags & 0x01) != 0;
            bool torquePresent = (flags & 0x04) != 0;
            bool wheelRevPresent = (flags & 0x10) != 0;

            if (!wheelRevPresent) return (false, 0, 0);

            int index = 4; // Skip Flags(2) and Power(2)
            if (pedalBalancePresent) index += 1;
            if (torquePresent) index += 2;

            if (data.Length < index + 6) return (false, 0, 0);

            uint wheelRevs = BitConverter.ToUInt32(data, index);
            ushort lastWheelTime = BitConverter.ToUInt16(data, index + 4);

            return (true, wheelRevs, lastWheelTime);
        }

        public double CalculateSpeed(uint prevRevs, ushort prevTime, uint currRevs, ushort currTime, double circumferenceMeters)
        {
            if (currRevs < prevRevs) return 0; // Handle wrap-around if needed, or just ignore simple case
            
            uint revsDiff = currRevs - prevRevs;
            if (revsDiff == 0) return 0;

            // Time unit is 1/1024 seconds
            // Handle wrap-around of time (UInt16)
            int timeDiff = currTime - prevTime;
            if (timeDiff < 0) timeDiff += 65536; // Wrap around adjustment for UInt16

            if (timeDiff == 0) return 0;

            double timeSeconds = timeDiff / 1024.0;
            double distanceMeters = revsDiff * circumferenceMeters;
            
            double speedMps = distanceMeters / timeSeconds;
            return speedMps * 3.6; // Convert to KPH
        }

        public double CalculateDistance(uint totalRevs, double circumferenceMeters)
        {
            return totalRevs * circumferenceMeters;
        }

        // "Fake" Simulation Mode: Maps Grade % to Resistance % (0.0 - 1.0)
        // Since the device rejects OpCode 0x42 (Sim Mode), we manually calculate brake force.
        public double CalculateResistanceFromGrade(double gradePercent)
        {
            // User Requirements (Feb 2026):
            // -10% Grade -> 0%   Res (Coasting)
            //  0%  Grade -> 0.5% Res (Flat)
            //  2%  Grade -> 3%   Res
            //  5%  Grade -> 9%   Res
            //  8%  Grade -> 15%  Res
            //  12% Grade -> 22%  Res
            //  15% Grade -> 28%  Res
            //  20% Grade -> 30%  Res (Max)

            var points = new (double Grade, double Res)[]
            {
                (-10.0, 0.000),
                (0.0,   0.005),
                (2.0,   0.030),
                (5.0,   0.090),
                (8.0,   0.150),
                (12.0,  0.220),
                (15.0,  0.280),
                (20.0,  0.300)
            };

            if (gradePercent <= points[0].Grade) return points[0].Res;
            if (gradePercent >= points[points.Length - 1].Grade) return points[points.Length - 1].Res;

            // Find the segment
            for (int i = 0; i < points.Length - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                if (gradePercent >= p1.Grade && gradePercent <= p2.Grade)
                {
                    // Linear Interpolation: y = y1 + (x - x1) * (y2 - y1) / (x2 - x1)
                    double ratio = (gradePercent - p1.Grade) / (p2.Grade - p1.Grade);
                    return p1.Res + ratio * (p2.Res - p1.Res);
                }
            }

            return 0; // Fallback
        }
    }
}
