using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;
using System.Windows.Media;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class RoadsidePaletteTests
    {
        [TestMethod]
        public void RoadsidePalette_Constructor_SetsProperties()
        {
            // Arrange
            Brush shrub = Brushes.Green;
            Brush tree = Brushes.DarkGreen;
            Brush rock = Brushes.Gray;
            Brush trunk = Brushes.Brown;

            // Act
            var palette = new RoadsidePalette(shrub, tree, rock, trunk);

            // Assert
            Assert.AreEqual(shrub, palette.Shrub);
            Assert.AreEqual(tree, palette.Tree);
            Assert.AreEqual(rock, palette.Rock);
            Assert.AreEqual(trunk, palette.Trunk);
        }
    }
}
