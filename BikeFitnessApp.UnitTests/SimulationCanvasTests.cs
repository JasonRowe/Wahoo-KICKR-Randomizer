using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;
using System.Collections.Generic;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class SimulationCanvasTests
    {
        #region GetGradeAt Tests

        [TestMethod]
        public void GetGradeAt_InitialState_ReturnsZero()
        {
            var terrain = new TerrainCalculator();
            
            // Initial state should have grade 0 at distance 0
            Assert.AreEqual(0.0, terrain.GetGradeAt(0));
        }

        [TestMethod]
        public void GetGradeAt_BeforeFirstVertex_ReturnsZero()
        {
            var terrain = new TerrainCalculator();
            
            // Before all recorded vertices, should return 0
            Assert.AreEqual(0.0, terrain.GetGradeAt(-10));
        }

        [TestMethod]
        public void GetGradeAt_AfterGradeChange_ReturnsNewGrade()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(100, 5.0); // 5% grade at 100m
            
            Assert.AreEqual(5.0, terrain.GetGradeAt(100));
            Assert.AreEqual(5.0, terrain.GetGradeAt(150)); // Beyond the change point
        }

        [TestMethod]
        public void GetGradeAt_MultipleGradeChanges_ReturnsCorrectGrade()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(50, 3.0);  // 3% at 50m
            terrain.RecordGradeChange(100, 8.0); // 8% at 100m
            terrain.RecordGradeChange(150, -2.0); // -2% at 150m
            
            Assert.AreEqual(0.0, terrain.GetGradeAt(25)); // Before first change
            Assert.AreEqual(3.0, terrain.GetGradeAt(75)); // Between 50-100
            Assert.AreEqual(8.0, terrain.GetGradeAt(125)); // Between 100-150
            Assert.AreEqual(-2.0, terrain.GetGradeAt(175)); // After 150
        }

        #endregion

        #region GetHeightAt Tests

        [TestMethod]
        public void GetHeightAt_InitialState_ReturnsZero()
        {
            var terrain = new TerrainCalculator();
            
            // Initial height at 0 should be 0
            Assert.AreEqual(0.0, terrain.GetHeightAt(0));
        }

        [TestMethod]
        public void GetHeightAt_BeforeFirstVertex_ReturnsZero()
        {
            var terrain = new TerrainCalculator();
            
            // Before any terrain, height should be 0
            Assert.AreEqual(0.0, terrain.GetHeightAt(-10));
        }

        [TestMethod]
        public void GetHeightAt_WithZeroGrade_StaysFlat()
        {
            var terrain = new TerrainCalculator();
            
            // At distance 100m with 0% grade, height should still be 0
            double height = terrain.GetHeightAt(100);
            Assert.AreEqual(0.0, height, 0.001);
        }

        [TestMethod]
        public void GetHeightAt_WithPositiveGrade_Increases()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(0, 10.0); // 10% grade from start
            
            // At 100m with 10% grade: height = 100 * 0.10 = 10m
            double height = terrain.GetHeightAt(100);
            Assert.AreEqual(10.0, height, 0.001);
        }

        [TestMethod]
        public void GetHeightAt_WithNegativeGrade_Decreases()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(0, -5.0); // -5% grade from start
            
            // At 100m with -5% grade: height = 100 * -0.05 = -5m
            double height = terrain.GetHeightAt(100);
            Assert.AreEqual(-5.0, height, 0.001);
        }

        [TestMethod]
        public void GetHeightAt_AccumulatesHeight()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(100, 10.0); // Flat until 100m, then 10%
            
            // Height at 100m = 0 (flat section)
            Assert.AreEqual(0.0, terrain.GetHeightAt(100), 0.001);
            
            // Height at 200m = 0 + (100m * 10%) = 10m
            Assert.AreEqual(10.0, terrain.GetHeightAt(200), 0.001);
        }

        #endregion

        #region RecordGradeChange Tests

        [TestMethod]
        public void RecordGradeChange_AtSameLocation_UpdatesGrade()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(0, 5.0);
            terrain.RecordGradeChange(0.05, 8.0); // Within 0.1 of last point
            
            Assert.AreEqual(8.0, terrain.GetGradeAt(0));
        }

        [TestMethod]
        public void RecordGradeChange_AddsToHistory()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(50, 5.0);
            terrain.RecordGradeChange(100, 10.0);
            
            Assert.AreEqual(3, terrain.History.Count); // Initial + 2 changes
        }

        #endregion

        #region Reset Tests

        [TestMethod]
        public void Reset_ClearsHistory()
        {
            var terrain = new TerrainCalculator();
            terrain.RecordGradeChange(100, 5.0);
            terrain.RecordGradeChange(200, 10.0);
            
            terrain.Reset();
            
            Assert.AreEqual(1, terrain.History.Count);
            Assert.AreEqual(0.0, terrain.GetGradeAt(0));
            Assert.AreEqual(0.0, terrain.GetHeightAt(0));
        }

        [TestMethod]
        public void Reset_WithValues_InitializesCorrectly()
        {
            var terrain = new TerrainCalculator();
            terrain.Reset(startDistance: 500, startHeight: 25, startGrade: 3.0);
            
            Assert.AreEqual(3.0, terrain.GetGradeAt(500));
            Assert.AreEqual(25.0, terrain.GetHeightAt(500));
        }

        #endregion

        #region BackgroundTheme Enum Tests

        [TestMethod]
        public void BackgroundTheme_HasExpectedValues()
        {
            // Verify all expected biomes exist
            Assert.AreEqual(0, (int)BackgroundTheme.Mountain);
            Assert.AreEqual(1, (int)BackgroundTheme.Plain);
            Assert.AreEqual(2, (int)BackgroundTheme.Desert);
            Assert.AreEqual(3, (int)BackgroundTheme.Ocean);
            Assert.AreEqual(4, (int)BackgroundTheme.Transition);
        }

        #endregion
    }
}
