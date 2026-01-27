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
            // Wahoo Resistance Mode: OpCode 0x42
            // Range: 0-100% (User verified 0-99 works)
            byte opCode = 0x42;
            int value = (int)Math.Clamp(resistancePercent * 100, 0, 99);
            return CreateCommandBytes(opCode, (ushort)value);
        }
    }
}
