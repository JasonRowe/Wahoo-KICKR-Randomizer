using System;
using System.Threading.Tasks;
using BikeFitness.Shared;
using BikeFitness.Shared.Services;
using Tmds.DBus;

namespace BikeFitness.Avalonia.Services
{
    /// <summary>
    /// Linux implementation that inhibits screen blanking/suspend for the duration of a
    /// workout via the freedesktop ScreenSaver D-Bus interface. Failures are logged and
    /// swallowed so a missing/incompatible D-Bus service never breaks a workout.
    /// </summary>
    public sealed class LinuxPowerManagementService : IPowerManagementService
    {
        private uint? _inhibitCookie;

        public void PreventSleep()
        {
            try
            {
                var screensaver = Connection.Session.CreateProxy<IScreenSaver>(
                    "org.freedesktop.ScreenSaver",
                    "/org/freedesktop/ScreenSaver");
                _inhibitCookie = screensaver.InhibitAsync("BikeFitnessApp", "Workout in progress").GetAwaiter().GetResult();
                Logger.Log($"Power Management: sleep inhibited (cookie {_inhibitCookie}).");
            }
            catch (Exception ex)
            {
                _inhibitCookie = null;
                Logger.Log($"Power Management: Error preventing sleep: {ex.Message}");
            }
        }

        public void AllowSleep()
        {
            try
            {
                if (_inhibitCookie is uint cookie)
                {
                    var screensaver = Connection.Session.CreateProxy<IScreenSaver>(
                        "org.freedesktop.ScreenSaver",
                        "/org/freedesktop/ScreenSaver");
                    screensaver.UnInhibitAsync(cookie).GetAwaiter().GetResult();
                    _inhibitCookie = null;
                    Logger.Log("Power Management: sleep inhibition released.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Power Management: Error allowing sleep: {ex.Message}");
            }
        }
    }

    [DBusInterface("org.freedesktop.ScreenSaver")]
    public interface IScreenSaver : IDBusObject
    {
        Task<uint> InhibitAsync(string applicationName, string reasonForInhibit);
        Task UnInhibitAsync(uint cookie);
    }
}
