using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using BikeFitness.Shared.Models;

namespace BikeFitness.Avalonia.Converters
{
    public class SharedColorToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SharedColor color)
            {
                return new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            }
            return Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
