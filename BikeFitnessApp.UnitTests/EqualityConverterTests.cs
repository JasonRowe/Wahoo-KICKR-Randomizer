using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.Converters;
using System.Windows.Data;
using System.Globalization;
using System;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class EqualityConverterTests
    {
        [TestMethod]
        public void Convert_WithEqualValues_ReturnsTrue()
        {
            var converter = new EqualityConverter();
            object[] values = new object[] { 5, 5 };
            
            var result = converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.IsTrue((bool)result);
        }

        [TestMethod]
        public void Convert_WithDifferentValues_ReturnsFalse()
        {
            var converter = new EqualityConverter();
            object[] values = new object[] { 5, 10 };

            var result = converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.IsFalse((bool)result);
        }

        [TestMethod]
        public void Convert_WithNullValues_ReturnsTrueIfBothNull()
        {
            var converter = new EqualityConverter();
            object[] values = new object[] { null, null };

            var result = converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.IsTrue((bool)result);
        }

        [TestMethod]
        public void Convert_WithOneNullValue_ReturnsFalse()
        {
            var converter = new EqualityConverter();
            object[] values = new object[] { 5, null };

            var result = converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.IsFalse((bool)result);
        }

        [TestMethod]
        public void Convert_WithFewerThanTwoValues_ReturnsFalse()
        {
            var converter = new EqualityConverter();
            object[] values = new object[] { 5 };

            var result = converter.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.IsFalse((bool)result);
        }

        [TestMethod]
        public void ConvertBack_ReturnsDoNothing()
        {
            var converter = new EqualityConverter();
            
            var result = converter.ConvertBack(true, new Type[] { typeof(object), typeof(object) }, null, CultureInfo.InvariantCulture);

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(Binding.DoNothing, result[0]);
            Assert.AreEqual(Binding.DoNothing, result[1]);
        }
    }
}
