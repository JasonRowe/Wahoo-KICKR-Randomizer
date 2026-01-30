using System;
using System.Globalization;
using System.Windows.Data;

namespace BikeFitnessApp.Converters
{
    public class EqualityConverter : IMultiValueConverter
    {
        public static EqualityConverter Instance { get; } = new EqualityConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return false;
            return Equals(values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing, Binding.DoNothing };
        }
    }
}
