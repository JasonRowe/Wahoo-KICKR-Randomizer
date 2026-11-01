using System;
using System.Threading.Tasks;
using BikeFitness.Shared.Models;

namespace BikeFitness.Shared.Services
{
    public class MockBluetoothService : IBluetoothService
    {
        public event Action<DeviceDisplay>? DeviceDiscovered;
        public event Action<string>? StatusChanged;
        public event Action<int>? PowerReceived;
        public event Action<double, double>? SpeedValuesUpdated;
        public event Action? ConnectionLost;

        public bool IsScanning { get; private set; }
        public bool IsConnected { get; private set; }
        public string CurrentStatus { get; set; } = "Ready";

        // For Testing
        public bool StartScanningCalled { get; private set; }
        public bool StopScanningCalled { get; private set; }
        public bool ConnectAsyncCalled { get; private set; }
        public int ConnectCalls { get; private set; }
        public int SendInitCommandCalls { get; private set; }

        /// <summary>How long <see cref="ConnectAsync"/> takes; 0 keeps tests fast.</summary>
        public int ConnectDelayMs { get; set; } = 1500;

        /// <summary>Number of upcoming connects that should fail, then connect normally.</summary>
        public int ConnectFailuresRemaining { get; set; }

        public void FireDeviceDiscovered(DeviceDisplay device) => DeviceDiscovered?.Invoke(device);
        public void FirePowerReceived(int watts) => PowerReceived?.Invoke(watts);
        public void FireSpeedValuesUpdated(double kph, double meters) => SpeedValuesUpdated?.Invoke(kph, meters);
        public void FireConnectionLost() => ConnectionLost?.Invoke();

        public void StartScanning()
        {
            StartScanningCalled = true;
            IsScanning = true;
            StatusChanged?.Invoke("Scanning...");
            
            // Auto-discover a mock device after a short delay (disabled for pure unit tests if needed, but fine for prototype)
            Task.Run(async () => {
                await Task.Delay(1000);
                if (IsScanning)
                {
                    DeviceDiscovered?.Invoke(new DeviceDisplay { Name = "Mock KICKR SNAP", Address = 123456789 });
                }
            });
        }

        public void StopScanning()
        {
            StopScanningCalled = true;
            IsScanning = false;
            StatusChanged?.Invoke("Scan Stopped");
        }

        public async Task ConnectAsync(ulong address)
        {
            ConnectAsyncCalled = true;
            ConnectCalls++;
            CurrentStatus = "Connecting...";
            StatusChanged?.Invoke(CurrentStatus);

            if (ConnectDelayMs > 0)
            {
                await Task.Delay(ConnectDelayMs);
            }

            if (ConnectFailuresRemaining > 0)
            {
                ConnectFailuresRemaining--;
                IsConnected = false;
                CurrentStatus = "Connect failed";
                StatusChanged?.Invoke(CurrentStatus);
                return;
            }

            IsConnected = true;
            CurrentStatus = "Connected";
            StatusChanged?.Invoke(CurrentStatus);

            // Start mock telemetry
            _ = Task.Run(async () => {
                double distance = 0;
                while (IsConnected)
                {
                    PowerReceived?.Invoke(150 + new Random().Next(-10, 10));
                    distance += 5.5; 
                    SpeedValuesUpdated?.Invoke(25.0, distance);
                    await Task.Delay(1000);
                }
            });
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            ConnectionLost?.Invoke();
            StatusChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        }

        public void QueueResistance(double resistance) { }
        public void QueueGrade(double gradePercent) { }
        public Task<bool> SendInitCommand()
        {
            SendInitCommandCalls++;
            return Task.FromResult(true);
        }
    }
}
