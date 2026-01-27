using System;
using System.IO;

namespace BikeFitnessApp
{
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BikeFitnessApp.log");

        public static bool IsEnabled { get; set; } = false;

        public static void Log(string message)
        {
            if (!IsEnabled) return;

            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Ignore logging errors
            }
        }
    }
}
