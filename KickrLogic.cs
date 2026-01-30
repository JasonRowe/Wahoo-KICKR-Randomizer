using System;

namespace BikeFitnessApp
{
    public enum WorkoutMode
    {
        Random,
        Hilly,     // Smooth Sine Wave
        Mountain   // Steep Triangle Wave
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
            // Bounds check
            if (min > max) min = max;
            min = Math.Clamp(min, 0, 1.0);
            max = Math.Clamp(max, 0, 1.0);

            switch (mode)
            {
                case WorkoutMode.Hilly:
                    return CalculateSineWave(min, max, stepIndex);
                case WorkoutMode.Mountain:
                    return CalculateTriangleWave(min, max, stepIndex);
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
    }
}
