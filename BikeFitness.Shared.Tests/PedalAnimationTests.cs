using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class PedalAnimationTests
    {
        private const double MetersPerRev = 6.5;

        #region GetCrankPhase

        [TestMethod]
        public void GetCrankPhase_ZeroDistance_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(0.0, MetersPerRev));
        }

        [TestMethod]
        public void GetCrankPhase_ExactRevolution_WrapsToZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(MetersPerRev, MetersPerRev), 1e-12);
        }

        [TestMethod]
        public void GetCrankPhase_HalfRevolution_ReturnsHalf()
        {
            Assert.AreEqual(0.5, PedalAnimation.GetCrankPhase(MetersPerRev * 0.5, MetersPerRev), 1e-12);
        }

        [TestMethod]
        public void GetCrankPhase_NegativeDistance_WrapsPositive()
        {
            // -0.1 rev -> 0.9
            double phase = PedalAnimation.GetCrankPhase(-MetersPerRev * 0.1, MetersPerRev);
            Assert.AreEqual(0.9, phase, 1e-12);
        }

        [TestMethod]
        public void GetCrankPhase_NonPositiveRevolution_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(10.0, 0.0));
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(10.0, -5.0));
        }

        #endregion

        #region GetFrameIndex

        [TestMethod]
        public void GetFrameIndex_ZeroPhase_ReturnsZero()
        {
            Assert.AreEqual(0, PedalAnimation.GetFrameIndex(0.0));
        }

        [TestMethod]
        public void GetFrameIndex_HalfPhase_ReturnsMiddleFrame()
        {
            Assert.AreEqual(6, PedalAnimation.GetFrameIndex(0.5));
        }

        [TestMethod]
        public void GetFrameIndex_NearEnd_ReturnsLastFrame()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(0.999));
        }

        [TestMethod]
        public void GetFrameIndex_PhaseOne_ClampsToLastFrame()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(1.0));
        }

        [TestMethod]
        public void GetFrameIndex_PhaseAboveOne_ClampsToLastFrame()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(2.5));
        }

        [TestMethod]
        public void GetFrameIndex_NegativePhase_ClampsToZero()
        {
            Assert.AreEqual(0, PedalAnimation.GetFrameIndex(-0.5));
        }

        [TestMethod]
        public void GetFrameIndex_DropLast_LoopsOverElevenFrames()
        {
            Assert.AreEqual(0, PedalAnimation.GetFrameIndex(0.0, SeamMode.DropLast));
            // 11-frame loop: phase near 1 -> index 10 (frame_10), never 11.
            Assert.AreEqual(10, PedalAnimation.GetFrameIndex(0.999, SeamMode.DropLast));
        }

        [TestMethod]
        public void GetFrameIndex_StraightAndCrossFade_UseAllTwelveFrames()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(0.999, SeamMode.Straight));
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(0.999, SeamMode.CrossFade));
        }

        #endregion

        #region GetEffectiveFrameCount

        [TestMethod]
        public void GetEffectiveFrameCount_MatchesSeamMode()
        {
            Assert.AreEqual(12, PedalAnimation.GetEffectiveFrameCount(SeamMode.Straight));
            Assert.AreEqual(12, PedalAnimation.GetEffectiveFrameCount(SeamMode.CrossFade));
            Assert.AreEqual(11, PedalAnimation.GetEffectiveFrameCount(SeamMode.DropLast));
        }

        #endregion

        #region GetSourceRect

        [TestMethod]
        public void GetSourceRect_FirstFrame_TopLeftCell()
        {
            var rect = PedalAnimation.GetSourceRect(0);
            Assert.AreEqual((0, 0, 290, 322), rect);
        }

        [TestMethod]
        public void GetSourceRect_LastColumnOfTopRow()
        {
            var rect = PedalAnimation.GetSourceRect(5);
            Assert.AreEqual(1450, rect.X);
            Assert.AreEqual(0, rect.Y);
        }

        [TestMethod]
        public void GetSourceRect_FirstColumnOfBottomRow()
        {
            var rect = PedalAnimation.GetSourceRect(6);
            Assert.AreEqual(0, rect.X);
            Assert.AreEqual(322, rect.Y);
        }

        [TestMethod]
        public void GetSourceRect_LastFrame_BottomRightCell()
        {
            var rect = PedalAnimation.GetSourceRect(11);
            Assert.AreEqual((1450, 322, 290, 322), rect);
        }

        [TestMethod]
        public void GetSourceRect_OutOfRange_Clamps()
        {
            Assert.AreEqual((0, 0, 290, 322), PedalAnimation.GetSourceRect(-1));
            Assert.AreEqual((1450, 322, 290, 322), PedalAnimation.GetSourceRect(12));
        }

        #endregion

        #region GetCrossFadeFactor

        [TestMethod]
        public void GetCrossFadeFactor_ZeroWindow_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeFactor(0.5, 0.0));
        }

        [TestMethod]
        public void GetCrossFadeFactor_BeforeWindow_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeFactor(0.8, 0.1));
        }

        [TestMethod]
        public void GetCrossFadeFactor_AtWindowStart_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeFactor(0.9, 0.1), 1e-12);
        }

        [TestMethod]
        public void GetCrossFadeFactor_AtWrap_ReturnsOne()
        {
            Assert.AreEqual(1.0, PedalAnimation.GetCrossFadeFactor(1.0, 0.1), 1e-12);
        }

        [TestMethod]
        public void GetCrossFadeFactor_MidWindow_Interpolates()
        {
            // window [0.9, 1.0], phase 0.95 -> t = 0.5
            Assert.AreEqual(0.5, PedalAnimation.GetCrossFadeFactor(0.95, 0.1), 1e-12);
        }

        #endregion

        #region GetCrossFadeWindowPhase

        [TestMethod]
        public void GetCrossFadeWindowPhase_ZeroSpeed_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeWindowPhase(0.0, MetersPerRev));
        }

        [TestMethod]
        public void GetCrossFadeWindowPhase_NonPositiveRevolution_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeWindowPhase(30.0, 0.0));
        }

        [TestMethod]
        public void GetCrossFadeWindowPhase_ScalesWithSpeed()
        {
            // At 36 kph = 10 m/s, 80 ms = 0.8 m; over 6.5 m/rev -> ~0.123
            double window = PedalAnimation.GetCrossFadeWindowPhase(36.0, MetersPerRev);
            Assert.AreEqual(0.8 / 6.5, window, 1e-9);
        }

        #endregion

        #region Frame progression over distance

        [TestMethod]
        public void GetFrameIndexForDistance_AdvancesThroughAllTwelveFrames()
        {
            // A full revolution traverses every frame index exactly once in order.
            int previous = -1;
            for (int i = 0; i < 12; i++)
            {
                double distance = (i + 0.5) * (MetersPerRev / 12.0);
                int frame = PedalAnimation.GetFrameIndexForDistance(distance, MetersPerRev);
                Assert.AreEqual(i, frame);
                Assert.IsTrue(frame > previous);
                previous = frame;
            }
        }

        #endregion
    }
}
