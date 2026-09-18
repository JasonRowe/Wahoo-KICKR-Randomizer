namespace BikeFitness.Shared.Services
{
    /// <summary>
    /// Keeps the machine awake during a workout and releases the hold when it ends.
    /// Implementations are platform-specific (Windows SetThreadExecutionState, Linux
    /// ScreenSaver D-Bus inhibit); the shared ViewModels depend only on this interface.
    /// </summary>
    public interface IPowerManagementService
    {
        void PreventSleep();
        void AllowSleep();
    }
}
