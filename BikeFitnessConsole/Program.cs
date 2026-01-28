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
        // Updated WAHOO_SERVICE based on device dump (ee01)
        private static readonly Guid WAHOO_SERVICE = new Guid("a026ee01-0a7d-4ab3-97fa-f1500f9feb8b");
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

            Console.WriteLine("Connected. discovering services...");
            
            // Get all services once to be robust
            var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                Console.WriteLine($"Failed to get services: {servicesResult.Status}");
                return;
            }
            var allServices = servicesResult.Services;
            Console.WriteLine($"Found {allServices.Count} services.");

            // Dump for debug
            foreach(var s in allServices)
            {
                Console.WriteLine($"Service: {s.Uuid}");
            }

            // 1. Setup Control Point
            await SetupControlPoint(allServices);

            // 2. Setup Power Notifications
            await SetupPower(allServices);

            // 3. Setup Speed Notifications
            await SetupSpeed(allServices);

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
            Console.WriteLine("D:   Dump Characteristic Details");
            Console.WriteLine("Q:   Quit");

            while (true)
            {
                if (Console.KeyAvailable)
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
                    else if (key == 'd' || key == 'D')
                    {
                        Console.WriteLine("\n[Debug] Dumping Characteristic Details...");
                        await DumpAllCharacteristics(allServices);
                    }
                }
                await Task.Delay(50); // Small loop delay
            }
            
            _device.Dispose();
        }

        private static async Task DumpAllCharacteristics(IReadOnlyList<GattDeviceService> services)
        {
            foreach(var s in services)
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

        private static async Task SetupControlPoint(IReadOnlyList<GattDeviceService> services)
        {
            var candidates = new[] { WAHOO_SERVICE, POWER_SERVICE };
            foreach (var uuid in candidates)
            {
                var service = services.FirstOrDefault(s => s.Uuid == uuid);
                if (service != null)
                {
                    var charsResult = await service.GetCharacteristicsForUuidAsync(WAHOO_CP, BluetoothCacheMode.Uncached);
                    if (charsResult.Status == GattCommunicationStatus.Success && charsResult.Characteristics.Count > 0)
                    {
                        _controlPoint = charsResult.Characteristics[0];
                        Console.WriteLine($"OK: Control Point found in Service {service.Uuid}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"Info: Service {service.Uuid} found, but Control Point missing inside.");
                    }
                }
            }
            Console.WriteLine("FAIL: Control Point characteristic NOT found.");
        }

        private static async Task SetupPower(IReadOnlyList<GattDeviceService> services)
        {
            var pwrService = services.FirstOrDefault(s => s.Uuid == POWER_SERVICE); 
            if (pwrService == null) { Console.WriteLine("FAIL: Power Service (1818) NOT found."); return; }

            var chars = await pwrService.GetCharacteristicsForUuidAsync(POWER_MEASUREMENT, BluetoothCacheMode.Uncached);
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
                    //Console.Write($"\r[POWER: {watts} W]   ");
                };
                Console.WriteLine("OK: Subscribed to POWER.");
            }
            else { Console.WriteLine("FAIL: Power Measurement char missing."); }
        }

        private static async Task SetupSpeed(IReadOnlyList<GattDeviceService> services)
        {
            var spdService = services.FirstOrDefault(s => s.Uuid == SPEED_SERVICE);
            if (spdService == null) { Console.WriteLine("FAIL: Speed Service (1816) NOT found."); return; }

            var chars = await spdService.GetCharacteristicsForUuidAsync(CSC_MEASUREMENT, BluetoothCacheMode.Uncached);
            if (chars.Status == GattCommunicationStatus.Success && chars.Characteristics.Count > 0)
            {
                var c = chars.Characteristics[0];
                await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                c.ValueChanged += (s, e) =>
                {
                    var reader = DataReader.FromBuffer(e.CharacteristicValue);
                    byte[] data = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(data);
                    var (hasWheel, revs, time) = _logic.ParseCscData(data);
                    if(hasWheel) Console.Write($"\r[SPEED DATA: {BitConverter.ToString(data)}]   ");
                };
                Console.WriteLine("OK: Subscribed to SPEED.");
            }
            else { Console.WriteLine("FAIL: Speed Measurement char missing."); }
        }

        private static async Task RunSimulation()
        {
            Console.WriteLine("Press any key to stop...");
            int step = 0;
            while (!Console.KeyAvailable)
            {
                double r = _logic.CalculateResistance(WorkoutMode.Hilly, 0.2, 0.8, step);
                int barLen = (int)(r * 50);
                Console.WriteLine($"Step {step:D3}: {r:F2} | {new string('#', barLen)}");
                await Write(_logic.CreateWahooResistanceCommand(r));
                step++;
                await Task.Delay(1000);
            }
            Console.ReadKey(true);
            Console.WriteLine("Simulation Stopped.");
        }

        private static async Task SendMode41(int percent) => await Write(new byte[] { 0x41, (byte)percent });
        private static async Task SendInit() => await Write(new byte[] { 0x00 });
        private static async Task SendLevel(int level) => await Write(new byte[] { 0x40, (byte)level });
        private static async Task SendResistance(int percent) => await Write(new byte[] { 0x40, (byte)percent });
        private static async Task SendErg(int watts) => await Write(new byte[] { 0x42, (byte)(watts & 0xFF), (byte)(watts >> 8) });

        private static async Task Write(byte[] cmd)
        {
            if (_controlPoint == null)
            {
                Console.WriteLine("ERROR: Cannot write. Control Point is NULL.");
                return;
            }

            const int MaxRetries = 3;
            for(int i=0; i<MaxRetries; i++)
            {
                try
                {
                    Console.WriteLine($"[TX] {BitConverter.ToString(cmd)}");
                    var writer = new DataWriter();
                    writer.WriteBytes(cmd);
                    var result = await _controlPoint.WriteValueAsync(writer.DetachBuffer());
                    if (result == GattCommunicationStatus.Success) return;
                    Console.WriteLine($"Write Retry {i+1}: {result}");
                }
                catch(Exception ex) { Console.WriteLine($"Write Exception {i+1}: {ex.Message}"); }
                await Task.Delay(250);
            }
            Console.WriteLine("Write FAILED after retries.");
        }
    }
}
