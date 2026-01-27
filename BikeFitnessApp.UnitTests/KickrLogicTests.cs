using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class KickrLogicTests
    {
        [TestMethod]
        public void TestCreateCommandBytes_OpCodeOnly()
        {
            // Arrange
            var logic = new KickrLogic();
            byte opCode = 0x01;

            // Act
            byte[] command = logic.CreateCommandBytes(opCode);

            // Assert
            CollectionAssert.AreEqual(new byte[] { 0x01 }, command);
        }

        [TestMethod]
        public void TestCreateCommandBytes_OpCodeAndByteParameter()
        {
            // Arrange
            var logic = new KickrLogic();
            byte opCode = 0x04;
            byte parameter = 50;

            // Act
            byte[] command = logic.CreateCommandBytes(opCode, parameter);

            // Assert
            CollectionAssert.AreEqual(new byte[] { 0x04, 50 }, command);
        }

        [TestMethod]
        public void TestCreateCommandBytes_OpCodeAndUshortParameter()
        {
            // Arrange
            var logic = new KickrLogic();
            byte opCode = 0x05;
            ushort parameter = 1000; // 0x03E8

            // Act
            byte[] command = logic.CreateCommandBytes(opCode, parameter);

            // Assert
            CollectionAssert.AreEqual(new byte[] { 0x05, 0xE8, 0x03 }, command);
        }

        [TestMethod]
        public void TestCreateWahooResistanceCommand()
        {
            var logic = new KickrLogic();
            // 50% resistance
            byte[] bytes = logic.CreateWahooResistanceCommand(0.5);
            
            // 0x42, 50 (0x32), 0x00
            CollectionAssert.AreEqual(new byte[] { 0x42, 0x32, 0x00 }, bytes);
        }

        [TestMethod]
        public void TestCalculateResistance_Bounds()
        {
            var logic = new KickrLogic();
            double val = logic.CalculateResistance(0.3, 0.7);
            Assert.IsTrue(val >= 0.3 && val <= 0.7);
        }

        [TestMethod]
        public void TestCalculateResistance_Clamping()
        {
            var logic = new KickrLogic();
            // Both above 1.0, should be clamped to 1.0
            double val = logic.CalculateResistance(1.5, 2.0);
            Assert.AreEqual(1.0, val);
            
            // Both below 0.0, should be clamped to 0.0
            val = logic.CalculateResistance(-0.5, -0.1);
            Assert.AreEqual(0.0, val);
        }

        [TestMethod]
        public void TestCalculateResistance_MinGreaterThanMax()
        {
            var logic = new KickrLogic();
            // Min 0.8 > Max 0.2. Code sets min = max (0.2). Result should be 0.2.
            double val = logic.CalculateResistance(0.8, 0.2);
            Assert.AreEqual(0.2, val);
        }

        [TestMethod]
        public void TestCalculateResistance_Hilly_SineWave()
        {
            var logic = new KickrLogic();
            double min = 0.0;
            double max = 1.0;
            
            // Period is 20 steps.
            // Step 0: Sin(0) = 0. Midpoint (0.5) + 0 = 0.5
            double val0 = logic.CalculateResistance(WorkoutMode.Hilly, min, max, 0);
            Assert.AreEqual(0.5, val0, 0.001);

            // Step 5 (Quarter cycle): Sin(PI/2) = 1. Midpoint (0.5) + 0.5*1 = 1.0
            double val5 = logic.CalculateResistance(WorkoutMode.Hilly, min, max, 5);
            Assert.AreEqual(1.0, val5, 0.001);

            // Step 15 (Three-quarter cycle): Sin(3PI/2) = -1. Midpoint (0.5) + 0.5*(-1) = 0.0
            double val15 = logic.CalculateResistance(WorkoutMode.Hilly, min, max, 15);
            Assert.AreEqual(0.0, val15, 0.001);
        }

        [TestMethod]
        public void TestCalculateResistance_Mountain_TriangleWave()
        {
            var logic = new KickrLogic();
            double min = 0.0;
            double max = 1.0;

            // Period is 20 steps. Half cycle is 10.
            
            // Step 0: Start at Min
            double val0 = logic.CalculateResistance(WorkoutMode.Mountain, min, max, 0);
            Assert.AreEqual(0.0, val0, 0.001);

            // Step 5: Halfway up (0.5)
            double val5 = logic.CalculateResistance(WorkoutMode.Mountain, min, max, 5);
            Assert.AreEqual(0.5, val5, 0.001);

            // Step 10: Peak (Max) - Note: implementation might wrap slightly depending on exact index, 
            // logic is "cyclePosition < halfCycle" (0 to 9 goes up). Index 10 is >= 10, so it starts going down.
            // Let's check boundary.
            // Index 9: 9/10 = 0.9
            double val9 = logic.CalculateResistance(WorkoutMode.Mountain, min, max, 9);
            Assert.AreEqual(0.9, val9, 0.001);

            // Index 10: (10-10)/10 = 0 -> 1.0 - 0 = 1.0
            double val10 = logic.CalculateResistance(WorkoutMode.Mountain, min, max, 10);
            Assert.AreEqual(1.0, val10, 0.001);

            // Index 15: Downward. (15-10)/10 = 0.5 -> 1.0 - 0.5 = 0.5
            double val15 = logic.CalculateResistance(WorkoutMode.Mountain, min, max, 15);
            Assert.AreEqual(0.5, val15, 0.001);
        }
    }
}
