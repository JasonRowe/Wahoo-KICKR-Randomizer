using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp;
using System;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class KickrLogicGradeTests
    {
        private KickrLogic _logic;

        [TestInitialize]
        public void Setup()
        {
            _logic = new KickrLogic();
        }

        [TestMethod]
        public void CalculateResistanceFromGrade_LowestSliderValue_ShouldBeZeroResistance()
        {
            // User requirement: "lowest value on the slider -10 is 0"
            double grade = -10.0;
            double resistance = _logic.CalculateResistanceFromGrade(grade);
            
            // Allow small delta
            Assert.AreEqual(0.0, resistance, 0.001, "Grade -10% should result in 0% resistance.");
        }

        [TestMethod]
        public void CalculateResistanceFromGrade_ZeroGrade_ShouldBeLowResistance()
        {
            // User requirement: "maybe 0% grade is near 1% resistance"
            // Previous logic gave ~13-17%. User finds this too hard?
            // Let's assert it is within a "Low" range (e.g., 1-5%)
            
            double grade = 0.0;
            double resistance = _logic.CalculateResistanceFromGrade(grade);

            Console.WriteLine($"0% Grade maps to {resistance*100}% Resistance");

            // Assert it's low (<= 5%)
            Assert.IsTrue(resistance <= 0.05, $"0% Grade should be fairly easy (<= 5%), but was {resistance*100}%");
            Assert.IsTrue(resistance >= 0.00, "Resistance cannot be negative");
        }

        [TestMethod]
        public void CalculateResistanceFromGrade_MaxGrade_ShouldBeFortyPercent()
        {
            // User requirement: "make sure the max grade is correct 40%"
            // Max grade on slider is 20.
            
            double grade = 20.0;
            double resistance = _logic.CalculateResistanceFromGrade(grade);

            Assert.AreEqual(0.40, resistance, 0.001, "Grade 20% should result in 40% resistance.");
        }

        [TestMethod]
        public void CalculateResistanceFromGrade_NegativeValues_ShouldNotGetHarder()
        {
            // Regression check: "as it goes negative it gets harder"
            // Testing -5 vs -10. -10 should be Easier (or equal if both are bottomed out) than -5.
            
            double resAtMinus5 = _logic.CalculateResistanceFromGrade(-5.0);
            double resAtMinus10 = _logic.CalculateResistanceFromGrade(-10.0);

            Console.WriteLine($"-5% Grade: {resAtMinus5*100}% Res");
            Console.WriteLine($"-10% Grade: {resAtMinus10*100}% Res");

            Assert.IsTrue(resAtMinus10 <= resAtMinus5, "Going more negative (downhill) should lower or maintain resistance, not increase it.");
        }
    }
}
