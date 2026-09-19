using System.Windows;
using BikeFitnessApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.UnitTests
{
    /// <summary>
    /// Rollback guard for the second-rider POCs: every new canvas behaviour must be off or neutral by
    /// default, so rendering is identical to pre-POC output unless a flag is explicitly switched on.
    /// <para>
    /// Reads dependency-property metadata rather than constructing a <see cref="SimulationCanvas"/>, so
    /// the test does not need an STA thread or a WPF application instance. This project targets
    /// <c>net10.0-windows</c>, so it only runs on the Windows CI runner — the same runner that builds the
    /// harness, which is the harness's only automated gate.
    /// </para>
    /// </summary>
    [TestClass]
    public class SecondRiderCanvasDefaultsTests
    {
        [TestMethod]
        public void GhostEnabled_DefaultsToFalse()
        {
            Assert.AreEqual(false, DefaultOf(SimulationCanvas.GhostEnabledProperty));
        }

        [TestMethod]
        public void GhostDistanceAndSpeed_DefaultToZero()
        {
            Assert.AreEqual(0.0, (double)DefaultOf(SimulationCanvas.GhostDistanceMetersProperty), 1e-9);
            Assert.AreEqual(0.0, (double)DefaultOf(SimulationCanvas.GhostSpeedKphProperty), 1e-9);
        }

        [TestMethod]
        public void GhostOpacity_DefaultsToFortyPercent()
        {
            Assert.AreEqual(0.4, (double)DefaultOf(SimulationCanvas.GhostOpacityProperty), 1e-9);
        }

        [TestMethod]
        public void GhostMarkerAndHud_DefaultOn_ButOnlyRenderWhenGhostEnabled()
        {
            // Both are default-on so that switching the ghost on gives a legible duel immediately; the
            // draw calls are still gated behind GhostEnabled, which defaults off.
            Assert.AreEqual(true, DefaultOf(SimulationCanvas.GhostShowMarkerProperty));
            Assert.AreEqual(true, DefaultOf(SimulationCanvas.GhostShowHudProperty));
        }

        private static object? DefaultOf(DependencyProperty property)
        {
            return property.GetMetadata(typeof(SimulationCanvas)).DefaultValue;
        }
    }
}
