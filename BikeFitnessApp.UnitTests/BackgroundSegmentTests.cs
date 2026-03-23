using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;
using System.Windows.Media.Imaging;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class BackgroundSegmentTests
    {
        [TestMethod]
        public void BackgroundSegment_Constructor_SetsProperties()
        {
            // Arrange
            string name = "Test Segment";
            BackgroundTheme theme = BackgroundTheme.Mountain;
            double length = 1000.0;
            bool mirror = true;
            
            // Create a dummy bitmap since it's a required parameter
            // We use PixelFormats.Bgr32 and a 1x1 size
            var bitmap = BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null, new byte[] { 0, 0, 0, 0 }, 4);

            // Act
            var segment = new BackgroundSegment(name, theme, bitmap, length, mirror);

            // Assert
            Assert.AreEqual(name, segment.Name);
            Assert.AreEqual(theme, segment.Theme);
            Assert.AreEqual(bitmap, segment.Image);
            Assert.AreEqual(length, segment.LengthMeters);
            Assert.AreEqual(mirror, segment.MirrorTiles);
        }
    }
}
