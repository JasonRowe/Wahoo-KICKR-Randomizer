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
            CurrentStatus = "Connecting...";
            StatusChanged?.Invoke(CurrentStatus);
            
            await Task.Delay(1500);
            
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
        public Task<bool> SendInitCommand() => Task.FromResult(true);
    }
}
