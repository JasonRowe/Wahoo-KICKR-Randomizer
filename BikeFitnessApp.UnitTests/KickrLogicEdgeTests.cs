using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp;
using BikeFitness.Shared;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class KickrLogicEdgeTests
    {
        [TestMethod]
        public void KickrLogic_Constructor_WithRandomSeeded()
        {
            // Arrange
            var seededRandom = new Random(42);
            var logic = new KickrLogic(seededRandom);
            
            // Act
            double val = logic.CalculateResistance(WorkoutMode.Random, 0, 100, 0);
            
            // Assert: Seed 42 first NextDouble() is approx 0.66
            // 0.66 * 100 + 0 = 66
            Assert.AreEqual(66, val, 1.0);
        }

        [TestMethod]
        public void CreateWahooSimGradeCommand_SetsCorrectBytes()
        {
            // Arrange
            var logic = new KickrLogic();
            double grade = 5.0; // 5.0 * 100 = 500 = 0x01F4
            
            // Act
            byte[] command = logic.CreateWahooSimGradeCommand(grade);

            // Assert
            Assert.AreEqual(0x43, command[0]); // OpCode
            Assert.AreEqual(0xF4, command[7]); // Grade LE
            Assert.AreEqual(0x01, command[8]);
        }

        [TestMethod]
        public void CalculateDistance_ReturnsCorrectValue()
        {
            // Arrange
            var logic = new KickrLogic();
            uint revs = 1000;
            double circumference = 2.105; // 700x25c approx
            
            // Act
            double distance = logic.CalculateDistance(revs, circumference);

            // Assert
            Assert.AreEqual(2105.0, distance, 0.001);
        }

        [TestMethod]
        public void ParsePower_NullOrSmallData_ReturnsZero()
        {
            var logic = new KickrLogic();
            Assert.AreEqual(0, logic.ParsePower(null));
            Assert.AreEqual(0, logic.ParsePower(new byte[] { 0, 0, 0 }));
        }

        [TestMethod]
        public void ParseCscData_NullOrSmallData_ReturnsFalse()
        {
            var logic = new KickrLogic();
            Assert.IsFalse(logic.ParseCscData(null).hasWheelData);
            Assert.IsFalse(logic.ParseCscData(new byte[] { 0x01, 0, 0, 0, 0, 0 }).hasWheelData); // 1 + 5 bytes < 1 + 6
        }

        [TestMethod]
        public void ParseCscCrankData_NoCrankFlag_ReturnsFalse()
        {
            var logic = new KickrLogic();
            byte[] data = new byte[] { 0x01, 0, 0, 0, 0, 0, 0 }; // Only wheel flag
            Assert.IsFalse(logic.ParseCscCrankData(data).hasCrankData);
        }

        [TestMethod]
        public void ParseCrankDataFromPower_NoCrankFlag_ReturnsFalse()
        {
            var logic = new KickrLogic();
            byte[] data = new byte[] { 0x00, 0x00, 0, 0 }; // No flags
            Assert.IsFalse(logic.ParseCrankDataFromPower(data).hasCrankData);
        }

        [TestMethod]
        public void ParseWheelDataFromPower_NoWheelFlag_ReturnsFalse()
        {
            var logic = new KickrLogic();
            byte[] data = new byte[] { 0x00, 0x00, 0, 0 }; // No flags
            Assert.IsFalse(logic.ParseWheelDataFromPower(data).hasWheelData);
        }

        [TestMethod]
        public void CalculateResistanceFromGrade_Extremes()
        {
            var logic = new KickrLogic();
            // Capped at -10% -> 0% res
            Assert.AreEqual(0.0, logic.CalculateResistanceFromGrade(-15.0), 0.001);
            // Capped at 20% -> 30% res
            Assert.AreEqual(0.3, logic.CalculateResistanceFromGrade(25.0), 0.001);
        }
    }
}
