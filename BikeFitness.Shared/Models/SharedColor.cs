namespace BikeFitness.Shared.Models
{
    public struct SharedColor
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public SharedColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static SharedColor White => new SharedColor(255, 255, 255);
    }
}
