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

        public int CalculateResistance(double min, double max)
        {
            if (min > max) min = max;
            // Ensure we stay within 0-100 bounds
            min = Math.Clamp(min, 0, 100);
            max = Math.Clamp(max, 0, 100);
            
            return _random.Next((int)min, (int)max + 1);
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
    }
}