using System;

namespace BikeFitness.Shared.Services
{
    /// <summary>
    /// Implemented by bluetooth services that try to recover a dropped link instead of
    /// surfacing it straight away. <see cref="IBluetoothService.ConnectionLost"/> is then
    /// only raised once recovery has failed, so a view model can keep the ride alive while
    /// the link is being retried.
    /// </summary>
    public interface IConnectionRecovery
    {
        /// <summary>Raised before each retry, with the attempt number (1-based).</summary>
        event Action<int>? ReconnectAttempting;

        /// <summary>Raised once a retry has restored the link and the trainer is ready again.</summary>
        event Action? Reconnected;

        /// <summary>True while a reconnect loop is running.</summary>
        bool IsReconnecting { get; }
    }
}
