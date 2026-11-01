using System;
using System.Threading.Tasks;
using BikeFitness.Shared.Models;

namespace BikeFitness.Shared.Services
{
    /// <summary>
    /// Wraps a platform bluetooth service and retries a dropped trainer link with
    /// exponential backoff before reporting it as lost.
    ///
    /// Rationale: trainer dropouts happen across firmware revisions on every vendor, and
    /// almost all of them recover within a few seconds. The old behaviour tore the ride down
    /// on the first <see cref="IBluetoothService.ConnectionLost"/>, throwing away a live
    /// session for a blip. Here the loss is retried (1s, 2s, 4s, 8s, 16s by default) and
    /// <see cref="IBluetoothService.ConnectionLost"/> is only raised when every attempt has
    /// failed — so the view model's existing teardown path still runs, just later.
    ///
    /// The retry loop lives here rather than in each platform service so WinRT and BlueZ
    /// share one implementation; the platform services only had to stop holding stale link
    /// state after a drop so a fresh connect starts clean.
    /// </summary>
    public sealed class ReconnectingBluetoothService : IBluetoothService, IConnectionRecovery
    {
        public const int DefaultMaxAttempts = 5;
        public static readonly TimeSpan DefaultInitialBackoff = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

        private readonly IBluetoothService _inner;
        private readonly int _maxAttempts;
        private readonly Func<int, TimeSpan> _backoff;
        private readonly Func<TimeSpan, Task> _delay;

        private ulong? _lastAddress;
        private int _generation;
        private bool _stopped;
        private volatile bool _reconnecting;

        public event Action<DeviceDisplay>? DeviceDiscovered;
        public event Action<string>? StatusChanged;
        public event Action<int>? PowerReceived;
        public event Action<double, double>? SpeedValuesUpdated;
        public event Action? ConnectionLost;
        public event Action<int>? ReconnectAttempting;
        public event Action? Reconnected;

        /// <param name="inner">The platform service doing the actual BLE work.</param>
        /// <param name="maxAttempts">How many reconnect attempts before the loss is reported.</param>
        /// <param name="initialBackoff">Delay before the first retry; doubles each attempt (capped at 30s).</param>
        /// <param name="delay">Delay implementation, injectable so tests do not sleep.</param>
        public ReconnectingBluetoothService(
            IBluetoothService inner,
            int maxAttempts = DefaultMaxAttempts,
            TimeSpan? initialBackoff = null,
            Func<TimeSpan, Task>? delay = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            _maxAttempts = maxAttempts;
            var initial = initialBackoff ?? DefaultInitialBackoff;
            _backoff = attempt =>
            {
                double ms = initial.TotalMilliseconds * Math.Pow(2, attempt - 1);
                return TimeSpan.FromMilliseconds(Math.Min(ms, MaxBackoff.TotalMilliseconds));
            };
            _delay = delay ?? (d => Task.Delay(d));

            _inner.DeviceDiscovered += OnDeviceDiscovered;
            _inner.StatusChanged += OnInnerStatusChanged;
            _inner.PowerReceived += OnInnerPowerReceived;
            _inner.SpeedValuesUpdated += OnInnerSpeedValuesUpdated;
            _inner.ConnectionLost += OnInnerConnectionLost;
        }

        public bool IsScanning => _inner.IsScanning;
        public bool IsConnected => _inner.IsConnected;
        public bool IsReconnecting => _reconnecting;
        public string CurrentStatus { get; private set; } = "Ready";

        public void StartScanning() => _inner.StartScanning();

        public void StopScanning() => _inner.StopScanning();

        public async Task ConnectAsync(ulong address)
        {
            // An explicit connect (scan screen) supersedes any retry that was in flight.
            _generation++;
            _stopped = false;
            _reconnecting = false;
            _lastAddress = address;

            await _inner.ConnectAsync(address);
        }

        public async Task DisconnectAsync()
        {
            // Cancel any pending retry: the user asked for this.
            _stopped = true;
            _generation++;
            _reconnecting = false;
            _lastAddress = null;

            await _inner.DisconnectAsync();
        }

        public void QueueResistance(double resistance) => _inner.QueueResistance(resistance);

        public void QueueGrade(double gradePercent) => _inner.QueueGrade(gradePercent);

        public Task<bool> SendInitCommand() => _inner.SendInitCommand();

        private void OnDeviceDiscovered(DeviceDisplay device) => DeviceDiscovered?.Invoke(device);

        private void OnInnerPowerReceived(int watts) => PowerReceived?.Invoke(watts);

        private void OnInnerSpeedValuesUpdated(double kph, double meters) => SpeedValuesUpdated?.Invoke(kph, meters);

        private void OnInnerStatusChanged(string status)
        {
            // While retrying, our own "attempt n/m" text owns the status line.
            if (_reconnecting) return;

            UpdateStatus(status);
        }

        private void OnInnerConnectionLost()
        {
            if (_stopped) return;

            // A retry loop is already running: this is the same drop being reported again.
            if (_reconnecting) return;

            var address = _lastAddress;
            if (address == null)
            {
                // Never connected through us, so there is nothing to resume.
                ConnectionLost?.Invoke();
                return;
            }

            _ = ReconnectLoopAsync(address.Value, ++_generation);
        }

        private async Task ReconnectLoopAsync(ulong address, int generation)
        {
            _reconnecting = true;
            Logger.Log($"[Reconnect] Link lost to {address}; retrying up to {_maxAttempts} times.");

            try
            {
                for (int attempt = 1; attempt <= _maxAttempts; attempt++)
                {
                    if (generation != _generation) return;

                    UpdateStatus($"Trainer lost — reconnecting (attempt {attempt}/{_maxAttempts})...");
                    ReconnectAttempting?.Invoke(attempt);

                    await _delay(_backoff(attempt));
                    if (generation != _generation) return;

                    try
                    {
                        await _inner.ConnectAsync(address);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[Reconnect] Attempt {attempt} threw {ex.GetType().Name}: {ex.Message}");
                    }

                    if (generation != _generation) return;

                    if (_inner.IsConnected)
                    {
                        // Re-arm the trainer: the fresh link has none of the old session's
                        // commands, and the ride is about to resume sending resistance.
                        await _inner.SendInitCommand();

                        _reconnecting = false;
                        Logger.Log($"[Reconnect] Reconnected on attempt {attempt}.");
                        UpdateStatus("Trainer reconnected");
                        Reconnected?.Invoke();
                        return;
                    }

                    Logger.Log($"[Reconnect] Attempt {attempt} did not restore the link.");
                }

                if (generation != _generation) return;

                _reconnecting = false;
                Logger.Log("[Reconnect] All attempts failed; reporting the connection as lost.");
                UpdateStatus("Device Disconnected");
                ConnectionLost?.Invoke();
            }
            finally
            {
                if (generation == _generation) _reconnecting = false;
            }
        }

        private void UpdateStatus(string status)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(status);
        }
    }
}
