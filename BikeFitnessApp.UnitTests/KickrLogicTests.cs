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
    }
}
