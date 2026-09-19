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

        [TestMethod]
        public void SecondRiderGapStrip_DefaultsOff()
        {
            // The strip is a POC 2 instrument; with it off (and no rival enabled) the frame is unchanged.
            Assert.AreEqual(false, DefaultOf(SimulationCanvas.SecondRiderGapStripProperty));
        }

        [TestMethod]
        public void SecondRiderDefaults_MatchTheDocumentedPanelDefaults()
        {
            Assert.AreEqual("ghost", DefaultOf(SimulationCanvas.SecondRiderLabelProperty));
            Assert.AreEqual(25.0, (double)DefaultOf(SimulationCanvas.SecondRiderGapTargetMetersProperty), 1e-9);
            Assert.AreEqual(15.0, (double)DefaultOf(SimulationCanvas.SecondRiderGapBandMetersProperty), 1e-9);
        }

        /// <summary>
        /// Reads a dependency property's default straight from its metadata — no element construction, so no
        /// STA thread and no WPF Application instance are needed.
        /// </summary>
        private static object DefaultOf(DependencyProperty property)
        {
            return property.GetMetadata(typeof(SimulationCanvas)).DefaultValue;
        }
    }
}
