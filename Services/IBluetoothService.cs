using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;

namespace BikeFitnessApp.Services
{
    public interface IBluetoothService
    {
        event Action<DeviceDisplay> DeviceDiscovered;
        event Action<string>? StatusChanged;
        event Action<int>? PowerReceived;
        event Action<double, double>? SpeedValuesUpdated; // Speed (KPH), Distance (Meters)
        event Action? ConnectionLost;

        bool IsScanning { get; }
        bool IsConnected { get; }
        string CurrentStatus { get; }

        void StartScanning();
        void StopScanning();
        Task ConnectAsync(ulong address);
        Task DisconnectAsync();
        
        // Command Queueing
        void QueueResistance(double resistance);
        void QueueGrade(double gradePercent);
        Task<bool> SendInitCommand();
    }
}
