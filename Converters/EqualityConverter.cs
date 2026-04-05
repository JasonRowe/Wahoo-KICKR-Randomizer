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
            if (values == null || values.Length < 2) return false;
            
            if (values[0] == null && values[1] == null) return true;
            if (values[0] == null || values[1] == null) return false;
            
            return values[0].Equals(values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // For MultiBinding, we should return an array of Binding.DoNothing
            var results = new object[targetTypes.Length];
            for (int i = 0; i < targetTypes.Length; i++)
            {
                results[i] = Binding.DoNothing;
            }
            return results;
        }
    }
}
