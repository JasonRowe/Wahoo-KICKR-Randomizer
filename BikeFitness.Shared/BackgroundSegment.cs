using System.Windows.Media.Imaging;

namespace BikeFitness.Shared
{
    /// <summary>
    /// Represents a background segment (biome or transition) in the simulation.
    /// </summary>
    public sealed class BackgroundSegment
    {
        public string Name { get; }
        public BackgroundTheme Theme { get; }
        public BitmapSource Image { get; }
        public double LengthMeters { get; }
        public bool MirrorTiles { get; }

        public BackgroundSegment(string name, BackgroundTheme theme, BitmapSource image, double lengthMeters, bool mirrorTiles)
        {
            Name = name;
            Theme = theme;
            Image = image;
            LengthMeters = lengthMeters;
            MirrorTiles = mirrorTiles;
        }
    }
}
