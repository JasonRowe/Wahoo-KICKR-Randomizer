using System.Collections.Generic;

namespace BikeFitnessApp
{
    public class TireSize
    {
        public string Name { get; set; } = "";
        public double Circumference { get; set; }

        public override string ToString() => Name;
    }

    public static class AppSettings
    {
        public static bool UseMetric { get; set; } = false;
        public static double WheelCircumference { get; set; } = 2.10; // Default 700c

        public static List<TireSize> StandardTireSizes { get; } = new List<TireSize>
        {
            new TireSize { Name = "700c (Road)", Circumference = 2.10 },
            new TireSize { Name = "26\" (MTB)", Circumference = 2.07 },
            new TireSize { Name = "27\" (Touring)", Circumference = 2.14 },
            new TireSize { Name = "29\" (MTB)", Circumference = 2.30 }
        };
    }
}
