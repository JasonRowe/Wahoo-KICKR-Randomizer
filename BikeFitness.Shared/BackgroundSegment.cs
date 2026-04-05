namespace BikeFitness.Shared
{
    /// <summary>
    /// Represents a background segment (biome or transition) in the simulation.
    /// </summary>
    public sealed class BackgroundSegment<TImage>
    {
        public string Name { get; }
        public BackgroundTheme Theme { get; }
        public TImage Image { get; }
        public double LengthMeters { get; }
        public bool MirrorTiles { get; }

        public BackgroundSegment(string name, BackgroundTheme theme, TImage image, double lengthMeters, bool mirrorTiles)
        {
            Name = name;
            Theme = theme;
            Image = image;
            LengthMeters = lengthMeters;
            MirrorTiles = mirrorTiles;
        }
    }
}
