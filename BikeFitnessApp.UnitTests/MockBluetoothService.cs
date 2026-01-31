using System;
using System.Threading.Tasks;
using BikeFitnessApp.Services;

namespace BikeFitnessApp.UnitTests
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
        public string CurrentStatus { get; private set; } = "Ready";

        public bool StartScanningCalled { get; private set; }
        public bool ConnectAsyncCalled { get; private set; }

        public void FireDeviceDiscovered(DeviceDisplay device)
        {
            DeviceDiscovered?.Invoke(device);
        }

        public void FirePowerReceived(int watts)
        {
            PowerReceived?.Invoke(watts);
        }
        
        public void FireSpeedValuesUpdated(double kph, double meters)
        {
            SpeedValuesUpdated?.Invoke(kph, meters);
        }

        public void StartScanning()
        {
            StartScanningCalled = true;
            IsScanning = true;
        }

        public void StopScanning()
        {
            IsScanning = false;
        }

        public Task ConnectAsync(ulong address)
        {
            ConnectAsyncCalled = true;
            IsConnected = true;
            StatusChanged?.Invoke("Connected");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            ConnectionLost?.Invoke();
            return Task.CompletedTask;
        }

        public void QueueResistance(double resistance) { }

        public void QueueGrade(double gradePercent) { }

        public Task<bool> SendInitCommand()
        {
            return Task.FromResult(true);
        }
    }
}
