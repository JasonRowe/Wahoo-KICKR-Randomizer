using System;

namespace BikeFitnessApp
{
    public class KickrLogic
    {
        private readonly Random _random;

        public KickrLogic()
        {
            _random = new Random();
        }

        // Constructor for dependency injection (useful for testing with a seeded random)
        public KickrLogic(Random random)
        {
            _random = random;
        }

        // Calculate a random resistance value between min and max, clamped to 0-1.0
        public double CalculateResistance(double min, double max)
        {
            if (min > max) min = max;
            // Ensure we stay within 0-1.0 bounds
            min = Math.Clamp(min, 0, 1.0);
            max = Math.Clamp(max, 0, 1.0);

            return _random.NextDouble() * (max - min) + min;
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
