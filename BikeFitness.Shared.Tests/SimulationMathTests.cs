using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class SimulationMathTests
    {
        #region Clamp01 Tests

        [TestMethod]
        public void Clamp01_BelowZero_ReturnsZero()
        {
            Assert.AreEqual(0.0, SimulationMath.Clamp01(-0.5));
            Assert.AreEqual(0.0, SimulationMath.Clamp01(-100.0));
        }

        [TestMethod]
        public void Clamp01_AboveOne_ReturnsOne()
        {
            Assert.AreEqual(1.0, SimulationMath.Clamp01(1.5));
            Assert.AreEqual(1.0, SimulationMath.Clamp01(100.0));
        }

        [TestMethod]
        public void Clamp01_WithinRange_ReturnsValue()
        {
            Assert.AreEqual(0.0, SimulationMath.Clamp01(0.0));
            Assert.AreEqual(0.5, SimulationMath.Clamp01(0.5));
            Assert.AreEqual(1.0, SimulationMath.Clamp01(1.0));
            Assert.AreEqual(0.25, SimulationMath.Clamp01(0.25));
        }

        #endregion

        #region SmoothStep Tests

        [TestMethod]
        public void SmoothStep_AtZero_ReturnsZero()
        {
            Assert.AreEqual(0.0, SimulationMath.SmoothStep(0.0));
        }

        [TestMethod]
        public void SmoothStep_AtOne_ReturnsOne()
        {
            Assert.AreEqual(1.0, SimulationMath.SmoothStep(1.0));
        }

        [TestMethod]
        public void SmoothStep_AtHalf_ReturnsHalf()
        {
            // SmoothStep(0.5) = 0.5 * 0.5 * (3 - 2*0.5) = 0.25 * 2 = 0.5
            Assert.AreEqual(0.5, SimulationMath.SmoothStep(0.5), 0.0001);
        }

        [TestMethod]
        public void SmoothStep_NegativeInput_ClampedToZero()
        {
            Assert.AreEqual(0.0, SimulationMath.SmoothStep(-1.0));
        }

        [TestMethod]
        public void SmoothStep_AboveOne_ClampedToOne()
        {
            Assert.AreEqual(1.0, SimulationMath.SmoothStep(2.0));
        }

        [TestMethod]
        public void SmoothStep_InterpolatesSmooth()
        {
            // SmoothStep should have zero derivative at edges
            // At t=0.25: 0.25^2 * (3 - 0.5) = 0.0625 * 2.5 = 0.15625
            Assert.AreEqual(0.15625, SimulationMath.SmoothStep(0.25), 0.0001);
            
            // At t=0.75: 0.75^2 * (3 - 1.5) = 0.5625 * 1.5 = 0.84375
            Assert.AreEqual(0.84375, SimulationMath.SmoothStep(0.75), 0.0001);
        }

        #endregion

        #region GetBiomeLabelText Tests

        [TestMethod]
        public void GetBiomeLabelText_Mountain_ReturnsCorrectText()
        {
            Assert.AreEqual("Entering Mountains", SimulationMath.GetBiomeLabelText(BackgroundTheme.Mountain));
        }

        [TestMethod]
        public void GetBiomeLabelText_Plain_ReturnsCorrectText()
        {
            Assert.AreEqual("Entering Plains", SimulationMath.GetBiomeLabelText(BackgroundTheme.Plain));
        }

        [TestMethod]
        public void GetBiomeLabelText_Desert_ReturnsCorrectText()
        {
            Assert.AreEqual("Entering Desert", SimulationMath.GetBiomeLabelText(BackgroundTheme.Desert));
        }

        [TestMethod]
        public void GetBiomeLabelText_Ocean_ReturnsCorrectText()
        {
            Assert.AreEqual("Entering Ocean", SimulationMath.GetBiomeLabelText(BackgroundTheme.Ocean));
        }

        [TestMethod]
        public void GetBiomeLabelText_Transition_FallsBackToPlains()
        {
            Assert.AreEqual("Entering Plains", SimulationMath.GetBiomeLabelText(BackgroundTheme.Transition));
        }

        #endregion
    }
}
