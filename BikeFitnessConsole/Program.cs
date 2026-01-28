using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using BikeFitnessApp;

namespace BikeFitnessConsole
{
    class Program
    {
        // UUIDs
        private static readonly Guid WAHOO_SERVICE = new Guid("a026e001-0a7d-4ab3-97fa-f1500f9feb8b");
        private static readonly Guid WAHOO_CP = new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b");
        
        private static readonly Guid POWER_SERVICE = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        private static readonly Guid POWER_MEASUREMENT = new Guid("00002A63-0000-1000-8000-00805f9b34fb");
        
        private static readonly Guid SPEED_SERVICE = new Guid("00001816-0000-1000-8000-00805f9b34fb");
        private static readonly Guid CSC_MEASUREMENT = new Guid("00002A5B-0000-1000-8000-00805f9b34fb");

        private static BluetoothLEDevice? _device;
        private static GattCharacteristic? _controlPoint;
        private static KickrLogic _logic = new KickrLogic();

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== KICKR CONSOLE TESTER ===");
            Console.WriteLine("Scanning for KICKR or WAHOO devices...");

            var watcher = new BluetoothLEAdvertisementWatcher();
            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            
            ulong foundAddress = 0;
            string foundName = "";
            var tcs = new TaskCompletionSource<bool>();

            watcher.Received += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Advertisement.LocalName) && 
                   (e.Advertisement.LocalName.Contains("KICKR") || e.Advertisement.LocalName.Contains("WAHOO")))
                {
                    Console.WriteLine($"Found: {e.Advertisement.LocalName} ({e.BluetoothAddress})");
                    foundAddress = e.BluetoothAddress;
                    foundName = e.Advertisement.LocalName;
                    watcher.Stop();
                    tcs.TrySetResult(true);
                }
            };

            watcher.Start();
            await tcs.Task;

            Console.WriteLine($"Connecting to {foundName}...");
            _device = await BluetoothLEDevice.FromBluetoothAddressAsync(foundAddress);

            if (_device == null)
            {
                Console.WriteLine("Failed to connect.");
                return;
            }

            Console.WriteLine("Connected. Dumping Services...");
            await DumpDeviceDetails();

            Console.WriteLine("Searching for specific services...");

            // 1. Setup Control Point
            await SetupControlPoint();

            // 2. Setup Power Notifications
            await SetupPower();

            // 3. Setup Speed Notifications
            await SetupSpeed();

            // 4. Send Init/Unlock
            Console.WriteLine("\nInitializing (Sending 0x00)...");
            await SendInit();

            Console.WriteLine("\n=== CONTROLS ===");
            Console.WriteLine("0-9: Send Level 0-9 (OpCode 0x40, Val 0-9)");
            Console.WriteLine("R:   Send Level 0-100 (OpCode 0x40, Val 0-100)");
            Console.WriteLine("S:   Send Resistance Mode (OpCode 0x41, Val 0-100%)");
            Console.WriteLine("E:   Set ERG Mode 50 Watts (OpCode 0x42)");
            Console.WriteLine("F:   Set ERG Mode 100 Watts (OpCode 0x42)");
            Console.WriteLine("T:   Test Simulation (Hilly Mode loop)");
            Console.WriteLine("U:   Send Init/Unlock (OpCode 0x00)");
            Console.WriteLine("Q:   Quit");

            while (true)
            {
                var key = Console.ReadKey(true).KeyChar;
                if (key == 'q' || key == 'Q') break;

                if (char.IsDigit(key))
                {
                    int level = int.Parse(key.ToString());
                    Console.WriteLine($"\n[Command] Level {level}");
                    await SendLevel(level);
                }
                else if (key == 'r' || key == 'R')
                {
                    Console.Write("\nEnter Level 0-100: ");
                    string? input = Console.ReadLine();
                    if (int.TryParse(input, out int val))
                    {
                        await SendResistance(val);
                    }
                }
                else if (key == 's' || key == 'S')
                {
                    Console.Write("\nEnter Resistance % (0-100): ");
                    string? input = Console.ReadLine();
                    if (int.TryParse(input, out int val))
                    {
                        await SendMode41(val);
                    }
                }
                else if (key == 'e' || key == 'E')
                {
                    Console.WriteLine("\n[Command] ERG 50W");
                    await SendErg(50);
                }
                else if (key == 'f' || key == 'F')
                {
                    Console.WriteLine("\n[Command] ERG 100W");
                    await SendErg(100);
                }
                else if (key == 't' || key == 'T')
                {
                    Console.WriteLine("\n[Test] Starting Hilly Mode Simulation (Ctrl+C to stop)...");
                    await RunSimulation();
                }
                else if (key == 'u' || key == 'U')
                {
                    Console.WriteLine("\n[Command] Init/Unlock (0x00)");
                    await SendInit();
                }
            }
            
            _device.Dispose();
        }

        private static async Task DumpDeviceDetails()
        {
            try
            {
                var services = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (services.Status != GattCommunicationStatus.Success)
                {
                    Console.WriteLine($"Failed to get services: {services.Status}");
                    return;
                }

                foreach(var s in services.Services)
                {
                    Console.WriteLine($"Service: {s.Uuid}");
                    var chars = await s.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (chars.Status == GattCommunicationStatus.Success)
                    {
                        foreach(var c in chars.Characteristics)
                        {
                            Console.WriteLine($"  - Char: {c.Uuid}  Props: {c.CharacteristicProperties}");
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Dump Error: {ex.Message}");
            }
        }

        private static async Task RunSimulation()
        {
            Console.WriteLine("Press any key to stop...");
            int step = 0;
            // Loop until key press
            while (!Console.KeyAvailable)
            {
                // Simulate Hilly, Min 0.2, Max 0.8
                double r = _logic.CalculateResistance(WorkoutMode.Hilly, 0.2, 0.8, step);
                
                // Print visualization bar
                int barLen = (int)(r * 50);
                string bar = new string('#', barLen);
                Console.WriteLine($"Step {step:D3}: {r:F2} | {bar}");

                // Send actual command if connected
                if (_controlPoint != null)
                {
                    byte[] cmd = _logic.CreateWahooResistanceCommand(r);
                    await Write(cmd);
                }

                step++;
                await Task.Delay(1000); // 1 sec steps for testing
            }
            Console.ReadKey(true); // Consume key
            Console.WriteLine("Simulation Stopped.");
        }

        private static async Task SendMode41(int percent)
        {
            if (_controlPoint == null) return;
            // OpCode 0x41, Resistance % (0-100)
            byte[] cmd = new byte[] { 0x41, (byte)percent };
            await Write(cmd);
        }

        private static async Task SetupControlPoint()
        {
            // Try Wahoo Service First
            var wahoo = await _device.GetGattServicesForUuidAsync(WAHOO_SERVICE, BluetoothCacheMode.Uncached);
            if (wahoo.Status == GattCommunicationStatus.Success && wahoo.Services.Count > 0)
            {
                var cp = await wahoo.Services[0].GetCharacteristicsForUuidAsync(WAHOO_CP, BluetoothCacheMode.Uncached);
                if (cp.Status == GattCommunicationStatus.Success && cp.Characteristics.Count > 0)
                {
                    _controlPoint = cp.Characteristics[0];
                    Console.WriteLine("OK: Control Point found in WAHOO Service.");
                    return;
                }
            }

            // Try Power Service
            var power = await _device.GetGattServicesForUuidAsync(POWER_SERVICE, BluetoothCacheMode.Uncached);
            if (power.Status == GattCommunicationStatus.Success && power.Services.Count > 0)
            {
                var cp = await power.Services[0].GetCharacteristicsForUuidAsync(WAHOO_CP, BluetoothCacheMode.Uncached);
                if (cp.Status == GattCommunicationStatus.Success && cp.Characteristics.Count > 0)
                {
                    _controlPoint = cp.Characteristics[0];
                    Console.WriteLine("OK: Control Point found in POWER Service.");
                    return;
                }
            }

            Console.WriteLine("FAIL: Control Point NOT found.");
        }

        private static async Task SetupPower()
        {
            var services = await _device.GetGattServicesForUuidAsync(POWER_SERVICE, BluetoothCacheMode.Uncached);
            if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
            {
                var chars = await services.Services[0].GetCharacteristicsForUuidAsync(POWER_MEASUREMENT, BluetoothCacheMode.Uncached);
                if (chars.Status == GattCommunicationStatus.Success && chars.Characteristics.Count > 0)
                {
                    var c = chars.Characteristics[0];
                    await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    c.ValueChanged += (s, e) =>
                    {
                        var reader = DataReader.FromBuffer(e.CharacteristicValue);
                        byte[] data = new byte[reader.UnconsumedBufferLength];
                        reader.ReadBytes(data);
                        int watts = _logic.ParsePower(data);
                        Console.Write($"\r[POWER: {watts} W]   ");
                    };
                    Console.WriteLine("OK: Subscribed to POWER.");
                    return;
                }
            }
            Console.WriteLine("FAIL: Power Measurement NOT found.");
        }

        private static async Task SetupSpeed()
        {
            var services = await _device.GetGattServicesForUuidAsync(SPEED_SERVICE, BluetoothCacheMode.Uncached);
            if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
            {
                var chars = await services.Services[0].GetCharacteristicsForUuidAsync(CSC_MEASUREMENT, BluetoothCacheMode.Uncached);
                if (chars.Status == GattCommunicationStatus.Success && chars.Characteristics.Count > 0)
                {
                    var c = chars.Characteristics[0];
                    await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    c.ValueChanged += (s, e) =>
                    {
                        var reader = DataReader.FromBuffer(e.CharacteristicValue);
                        byte[] data = new byte[reader.UnconsumedBufferLength];
                        reader.ReadBytes(data);
                        
                        // Debug raw
                        string hex = BitConverter.ToString(data);
                        
                        var (hasWheel, revs, time) = _logic.ParseCscData(data);
                        if(hasWheel)
                        {
                            Console.Write($"\r[SPEED DATA: {hex} | Revs: {revs}]   ");
                        }
                    };
                    Console.WriteLine("OK: Subscribed to SPEED.");
                    return;
                }
            }
            Console.WriteLine("FAIL: Speed Service NOT found.");
        }

        private static async Task SendInit()
        {
            if (_controlPoint == null) return;
            await Write(new byte[] { 0x00 });
        }

        private static async Task SendLevel(int level)
        {
            if (_controlPoint == null) return;
            // OpCode 0x40, Level 0-9
            byte[] cmd = new byte[] { 0x40, (byte)level };
            await Write(cmd);
        }

        private static async Task SendResistance(int percent)
        {
            if (_controlPoint == null) return;
            // OpCode 0x40, but raw byte value 0-100
            byte[] cmd = new byte[] { 0x40, (byte)percent };
            await Write(cmd);
        }

        private static async Task SendErg(int watts)
        {
            if (_controlPoint == null) return;
            // OpCode 0x42, Watts (Low byte, High byte)
            byte[] cmd = new byte[3];
            cmd[0] = 0x42;
            cmd[1] = (byte)(watts & 0xFF);
            cmd[2] = (byte)(watts >> 8);
            await Write(cmd);
        }

        private static async Task Write(byte[] cmd)
        {
            const int MaxRetries = 3;
            for(int i=0; i<MaxRetries; i++)
            {
                try
                {
                    // string hex = BitConverter.ToString(cmd);
                    // Console.WriteLine($"Sending: {hex}");
                    
                    var writer = new DataWriter();
                    writer.WriteBytes(cmd);
                    var result = await _controlPoint.WriteValueAsync(writer.DetachBuffer());
                    
                    if (result == GattCommunicationStatus.Success) 
                    {
                        // Console.WriteLine("OK");
                        return;
                    }
                    Console.WriteLine($"Write Retry {i+1}: {result}");
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Write Exception {i+1}: {ex.Message}");
                }
                await Task.Delay(250);
            }
            Console.WriteLine("Write FAILED after retries.");
        }
    }
}
