using System;
using System.Runtime.InteropServices;

namespace BikeFitnessApp
{
    public static class PowerManagement
    {
        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_CONTINUOUS = 0x80000000,
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        /// <summary>
        /// Prevents the system from going to sleep and keeps the display on.
        /// </summary>
        public static void PreventSleep()
        {
            try
            {
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED);
                Logger.Log("Power Management: System sleep and display off prevented.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Power Management: Error preventing sleep: {ex.Message}");
            }
        }

        /// <summary>
        /// Allows the system to go to sleep and the display to turn off according to user settings.
        /// </summary>
        public static void AllowSleep()
        {
            try
            {
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                Logger.Log("Power Management: System sleep and display off allowed.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Power Management: Error allowing sleep: {ex.Message}");
            }
        }
    }
}
