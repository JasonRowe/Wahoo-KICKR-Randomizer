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
    }
}
