using BikeFitness.Shared;
using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.SecondRider
{
    /// <summary>
    /// L2 tests pinning the on-screen geometry — including the case that makes the off-screen marker
    /// mandatory: at 50 px/m, only ~5.3 m behind and ~12.3 m ahead of the rider are visible on the
    /// 900-wide harness canvas, so a 20 m rival is literally off screen.
    /// </summary>
    [TestClass]
    public class SecondRiderGeometryTests
    {
        private const double PixelsPerMeter = SimulationEngine<object, object>.PixelsPerMeter;

        private const double CanvasWidth = 880.0;
        private const double CanvasHeight = 920.0;

        [TestMethod]
        public void OnScreen_WhenGapWithinVisibleWindow()
        {
            // ~5.3 m behind and ~12.3 m ahead are visible; both sides must be reported as on screen.
            bool behind = SecondRiderGeometry.TryGetScreenPosition(
                1000, 995, CanvasWidth, CanvasHeight,
                SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                out double behindX, out SecondRiderSide behindSide);

            Assert.IsTrue(behind, "a rival 5 m behind is inside the visible window");
            Assert.AreEqual(SecondRiderSide.OnScreen, behindSide);
            Assert.AreEqual(14.0, behindX, 0.01);

            bool ahead = SecondRiderGeometry.TryGetScreenPosition(
                1000, 1012, CanvasWidth, CanvasHeight,
                SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                out double aheadX, out SecondRiderSide aheadSide);

            Assert.IsTrue(ahead, "a rival 12 m ahead is inside the visible window");
            Assert.AreEqual(SecondRiderSide.OnScreen, aheadSide);
            Assert.AreEqual(864.0, aheadX, 0.01);
        }

        [TestMethod]
        public void OffScreenAhead_AtGap20m_ReportsAheadOffScreen()
        {
            bool visible = SecondRiderGeometry.TryGetScreenPosition(
                1000, 1020, CanvasWidth, CanvasHeight,
                SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                out double x, out SecondRiderSide side);

            Assert.IsFalse(visible, "at 50 px/m a 20 m gap is off screen — the marker is mandatory");
            Assert.AreEqual(SecondRiderSide.AheadOffScreen, side);
            Assert.IsTrue(x > CanvasWidth);
        }

        [TestMethod]
        public void OffScreenBehind_AtGapMinus6m_ReportsBehindOffScreen()
        {
            bool visible = SecondRiderGeometry.TryGetScreenPosition(
                1000, 994, CanvasWidth, CanvasHeight,
                SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                out double x, out SecondRiderSide side);

            Assert.IsFalse(visible);
            Assert.AreEqual(SecondRiderSide.BehindOffScreen, side);
            Assert.IsTrue(x < 0);
        }

        [TestMethod]
        public void ScreenX_MatchesWorldToScreen_Formula()
        {
            foreach (double gap in new[] { 0.0, 4.0, -4.0, 10.0, -5.0 })
            {
                SecondRiderGeometry.TryGetScreenPosition(
                    500, 500 + gap, CanvasWidth, CanvasHeight,
                    SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                    out double x, out _);

                Assert.AreEqual((0.3 * CanvasWidth) + (gap * PixelsPerMeter), x, 1e-9, $"gap {gap}");
            }
        }

        [TestMethod]
        public void ZeroWidthCanvas_ReportsOffScreen_NoThrow()
        {
            bool visible = SecondRiderGeometry.TryGetScreenPosition(
                100, 110, 0, 0,
                SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                out double x, out SecondRiderSide side);

            Assert.IsFalse(visible);
            Assert.AreEqual(SecondRiderSide.AheadOffScreen, side);
            Assert.AreEqual(0.0, x, 1e-9);

            // And behind, on a zero-width canvas, must not report "ahead".
            SecondRiderGeometry.TryGetScreenPosition(
                100, 90, 0, 0,
                SecondRiderGeometry.DefaultBikeScreenRatio, PixelsPerMeter,
                out _, out SecondRiderSide behindSide);

            Assert.AreEqual(SecondRiderSide.BehindOffScreen, behindSide);
        }

        [TestMethod]
        public void GetMarkerX_PinsChipInsideCanvasEdge()
        {
            // Far ahead pins to the right edge, far behind to the left edge; inside stays put.
            Assert.AreEqual(CanvasWidth - 26.0, SecondRiderGeometry.GetMarkerX(2000, CanvasWidth), 1e-9);
            Assert.AreEqual(26.0, SecondRiderGeometry.GetMarkerX(-2000, CanvasWidth), 1e-9);
            Assert.AreEqual(400.0, SecondRiderGeometry.GetMarkerX(400, CanvasWidth), 1e-9);
        }

        [TestMethod]
        public void GetMarkerX_DegenerateCanvas_ReturnsCentre()
        {
            Assert.AreEqual(0.0, SecondRiderGeometry.GetMarkerX(50, 0), 1e-9);
            Assert.AreEqual(20.0, SecondRiderGeometry.GetMarkerX(50, 40, marginPx: 26), 1e-9);
        }
    }
}
