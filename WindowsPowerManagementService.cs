using System;
using System.Runtime.InteropServices;
using BikeFitness.Shared;
using BikeFitness.Shared.Services;

namespace BikeFitnessApp
{
    /// <summary>
    /// Windows implementation using SetThreadExecutionState to keep the system awake
    /// (and the display on) while a workout is in progress.
    /// </summary>
    public sealed class WindowsPowerManagementService : IPowerManagementService
    {
        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_CONTINUOUS = 0x80000000,
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        public void PreventSleep()
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

        public void AllowSleep()
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
