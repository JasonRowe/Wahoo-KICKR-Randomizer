using System.Windows.Media;

namespace BikeFitness.Shared
{
    /// <summary>
    /// Color palette for roadside objects in different biomes.
    /// </summary>
    public sealed class RoadsidePalette
    {
        public Brush Shrub { get; }
        public Brush Tree { get; }
        public Brush Rock { get; }
        public Brush Trunk { get; }

        public RoadsidePalette(Brush shrub, Brush tree, Brush rock, Brush trunk)
        {
            Shrub = shrub;
            Tree = tree;
            Rock = rock;
            Trunk = trunk;
        }
    }
}
