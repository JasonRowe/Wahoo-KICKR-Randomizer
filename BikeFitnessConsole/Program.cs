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
        
        private static readonly Guid FTMS_SERVICE = new Guid("00001826-0000-1000-8000-00805f9b34fb");
        private static readonly Guid FTMS_CP = new Guid("00002ad9-0000-1000-8000-00805f9b34fb");

        private static readonly Guid POWER_SERVICE = new Guid("00001818-0000-1000-8000-00805f9b34fb");
        private static readonly Guid POWER_MEASUREMENT = new Guid("00002A63-0000-1000-8000-00805f9b34fb");
        
        private static readonly Guid SPEED_SERVICE = new Guid("00001816-0000-1000-8000-00805f9b34fb");
        private static readonly Guid CSC_MEASUREMENT = new Guid("00002A5B-0000-1000-8000-00805f9b34fb");

        private static BluetoothLEDevice? _device;
        private static GattCharacteristic? _controlPoint;
        private static bool _isFtms = false; // Track if we are using FTMS
        private static KickrLogic _logic = new KickrLogic();

        // Toggles
        private static bool _showPower = false;
        private static bool _showCadence = false;
        private static bool _showSpeed = false;
        private static bool _useResistanceMode = true; // Default to Resistance (0x41) as it matches Main App

        // State for calculations
        private static ushort _prevCrankRevs = 0;
        private static ushort _prevCrankTime = 0;
        private static bool _firstCrankData = true;

        private static uint _prevWheelRevs = 0;
        private static ushort _prevWheelTime = 0;
        private static uint _startWheelRevs = 0; // To calculate session distance
        private static bool _firstWheelData = true;
        private const double WheelCircumference = 2.1; // Meters

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
                string name = "Unknown";
                if (s.Uuid == WAHOO_SERVICE) name = "WAHOO CUSTOM";
                if (s.Uuid == POWER_SERVICE) name = "POWER (0x1818)";
                if (s.Uuid == SPEED_SERVICE) name = "CSC (0x1816)";
                if (s.Uuid == FTMS_SERVICE) name = "FTMS (0x1826)";
                Console.WriteLine($"Service: {s.Uuid} ({name})");
            }

            Console.WriteLine("\n--- DEEP SCAN: WAHOO SERVICE ---");
            var wahooService = allServices.FirstOrDefault(s => s.Uuid == WAHOO_SERVICE);
            if (wahooService != null)
            {
                var chars = await wahooService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                foreach (var c in chars.Characteristics)
                {
                    Console.WriteLine($"  Char: {c.Uuid} | Props: {c.CharacteristicProperties}");
                    if (c.Uuid == WAHOO_CP)
                    {
                        _controlPoint = c;
                        Console.WriteLine("  >>> FOUND WAHOO CONTROL POINT <<<");
                    }
                }
            }
            else
            {
                Console.WriteLine("  Wahoo Service NOT found.");
            }

            if (_controlPoint == null)
            {
                Console.WriteLine("\n--- DEEP SCAN: POWER SERVICE (Fallback) ---");
                var pwrService = allServices.FirstOrDefault(s => s.Uuid == POWER_SERVICE);
                if (pwrService != null)
                {
                    var chars = await pwrService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    foreach (var c in chars.Characteristics)
                    {
                        Console.WriteLine($"  Char: {c.Uuid} | Props: {c.CharacteristicProperties}");
                        if (c.Uuid == WAHOO_CP) // Checking if the Wahoo CP UUID exists inside Power Service (rare but possible)
                        {
                            _controlPoint = c;
                            Console.WriteLine("  >>> FOUND WAHOO CONTROL POINT (In Power Service) <<<");
                        }
                    }
                }
            }

            if (_controlPoint == null)
            {
                Console.WriteLine("\nCRITICAL: Could not find Control Point (a026e005) in Wahoo or Power services.");
            }
            else
            {
                Console.WriteLine($"\nSELECTED CONTROL POINT: {_controlPoint.Uuid}");
            }

            // 2. Setup Power Notifications
            await SetupPower(allServices);

            // 3. Setup Speed/Cadence Notifications
            await SetupSpeedAndCadence(allServices);

            // 4. Send Init/Unlock
            // Removed auto-init to match Main App behavior and isolate issues.
            // if (_controlPoint != null)
            // {
            //     Console.WriteLine("\nInitializing (Sending 0x00)...");
            //     await SendInit();
            // }

            Console.WriteLine("\n=== CONTROLS ===");
            Console.WriteLine("0-9: Send Resistance (0-90%) OR Level (0-9)");
            Console.WriteLine("M:   Toggle 0-9 Mode (Current: " + (_useResistanceMode ? "Resistance %" : "Level 0-9") + ")");
            Console.WriteLine("G:   Test Sim Mode (Grade) [Wahoo 0x43]");
            Console.WriteLine("I:   Send Init (0x00)");
            Console.WriteLine("U:   Send Unlock (0x20)");
            Console.WriteLine("P:   Toggle Power Display");
            Console.WriteLine("C:   Toggle Cadence Display");
            Console.WriteLine("V:   Toggle Speed/Dist Display");
            Console.WriteLine("S:   Manual Resistance % Input");
            Console.WriteLine("Q:   Quit");

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).KeyChar;
                    if (key == 'q' || key == 'Q') break;

                    if (char.IsDigit(key))
                    {
                        int digit = int.Parse(key.ToString());
                        if (_useResistanceMode)
                        {
                            int percent = digit * 10;
                            Console.WriteLine($"\n[Command] Resistance {percent}% (0x41)");
                            await SendMode41(percent);
                        }
                        else
                        {
                            Console.WriteLine($"\n[Command] Level {digit} (0x40)");
                            await SendLevel(digit);
                        }
                    }
                    else if (key == 'm' || key == 'M')
                    {
                        _useResistanceMode = !_useResistanceMode;
                        Console.WriteLine($"\n[Mode] 0-9 Keys now set to: {(_useResistanceMode ? "Resistance % (0x41)" : "Level 0-9 (0x40)")}");
                    }
                    else if (key == 'i' || key == 'I')
                    {
                        Console.WriteLine($"\n[Command] Init (0x00)");
                        await SendInit();
                    }
                    else if (key == 'u' || key == 'U')
                    {
                        Console.WriteLine($"\n[Command] Unlock (0x20)");
                        await Write(new byte[] { 0x20 });
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
                    else if (key == 'g' || key == 'G')
                    {
                        Console.Write("\nEnter Grade % (-15.0 to 20.0): ");
                        string? input = Console.ReadLine();
                        if (double.TryParse(input, out double grade))
                        {
                            Console.WriteLine($"\n[Command] Wahoo Sim Grade {grade}% (0x43)");
                            // Use default Weight (85kg), Crr (0.004), Cw (0.6)
                            var cmd = _logic.CreateWahooSimGradeCommand(grade);
                            await Write(cmd);
                        }
                    }
                    else if (key == 'p' || key == 'P')
                    {
                        _showPower = !_showPower;
                        Console.WriteLine($"\n[Display] Power: {(_showPower ? "ON" : "OFF")}");
                    }
                    else if (key == 'c' || key == 'C')
                    {
                        _showCadence = !_showCadence;
                        Console.WriteLine($"\n[Display] Cadence: {(_showCadence ? "ON" : "OFF")}");
                    }
                    else if (key == 'v' || key == 'V')
                    {
                        _showSpeed = !_showSpeed;
                        Console.WriteLine($"\n[Display] Speed/Dist: {(_showSpeed ? "ON" : "OFF")}");
                    }
                }
                await Task.Delay(50); // Small loop delay
            }
            
            _device.Dispose();
        }

        private static async Task DumpAllCharacteristics(IReadOnlyList<GattDeviceService> services)
        {
            // ... (omitted for brevity, not calling it in main loop anymore to keep clean)
        }

        // Removed old SetupControlPoint to avoid confusion
        // private static async Task SetupControlPoint...

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
                    if (!_showPower && !_showCadence && !_showSpeed) return;
                    
                    var reader = DataReader.FromBuffer(e.CharacteristicValue);
                    byte[] data = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(data);
                    
                    int watts = _logic.ParsePower(data);
                    
                    // Cadence from Power
                    var (hasCrank, crankRevs, crankTime) = _logic.ParseCrankDataFromPower(data);
                    double rpm = 0;
                    if (hasCrank)
                    {
                        if (!_firstCrankData) rpm = _logic.CalculateCadence(_prevCrankRevs, _prevCrankTime, crankRevs, crankTime);
                        _prevCrankRevs = crankRevs;
                        _prevCrankTime = crankTime;
                        _firstCrankData = false;
                    }

                    // Speed/Dist from Power (Wheel Data)
                    var (hasWheel, wheelRevs, wheelTime) = _logic.ParseWheelDataFromPower(data);
                    double kph = 0;
                    double distKm = 0;
                    if (hasWheel)
                    {
                        if (_firstWheelData)
                        {
                            _startWheelRevs = wheelRevs;
                            _firstWheelData = false;
                        }
                        else
                        {
                            kph = _logic.CalculateSpeed(_prevWheelRevs, _prevWheelTime, wheelRevs, wheelTime, WheelCircumference);
                        }
                        // Distance based on session start
                        distKm = _logic.CalculateDistance(wheelRevs - _startWheelRevs, WheelCircumference) / 1000.0;
                        
                        _prevWheelRevs = wheelRevs;
                        _prevWheelTime = wheelTime;
                    }

                    string output = "";
                    if (_showPower) output += $"POWER: {watts} W ";
                    if (_showCadence) output += hasCrank ? $"| CAD: {rpm:F0} RPM " : "| CAD: -- (No Flag) ";
                    if (_showSpeed) output += hasWheel ? $"| SPD: {kph:F1} kph DIST: {distKm:F2} km " : "| SPD: -- ";

                    if (output.Length > 0) Console.Write($"\r[{output.TrimStart('|', ' ')}]   ");
                };
                Console.WriteLine("OK: Subscribed to POWER.");
            }
            else { Console.WriteLine("FAIL: Power Measurement char missing."); }
        }

        private static async Task SetupSpeedAndCadence(IReadOnlyList<GattDeviceService> services)
        {
            var spdService = services.FirstOrDefault(s => s.Uuid == SPEED_SERVICE);
            if (spdService == null) { Console.WriteLine("INFO: Speed Service (1816) NOT found (Cadence might come from Power)."); return; }

            var chars = await spdService.GetCharacteristicsForUuidAsync(CSC_MEASUREMENT, BluetoothCacheMode.Uncached);
            if (chars.Status == GattCommunicationStatus.Success && chars.Characteristics.Count > 0)
            {
                var c = chars.Characteristics[0];
                await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                c.ValueChanged += (s, e) =>
                {
                    if (!_showCadence) return;
                    
                    var reader = DataReader.FromBuffer(e.CharacteristicValue);
                    byte[] data = new byte[reader.UnconsumedBufferLength];
                    reader.ReadBytes(data);
                    
                    string rawHex = BitConverter.ToString(data);
                    var (hasCrank, crankRevs, crankTime) = _logic.ParseCscCrankData(data);
                    
                    if (hasCrank)
                    {
                        double rpm = 0;
                        if (!_firstCrankData) rpm = _logic.CalculateCadence(_prevCrankRevs, _prevCrankTime, crankRevs, crankTime);
                        _prevCrankRevs = crankRevs;
                        _prevCrankTime = crankTime;
                        _firstCrankData = false;
                        Console.Write($"\r[CADENCE (CSC): {rpm:F0} RPM]   ");
                    }
                    else
                    {
                         // If we are here, we got data but no crank flag. 
                         Console.Write($"\r[CSC RAW]: {rawHex} (No Crank Flag)   ");
                    }
                };
                Console.WriteLine("OK: Subscribed to CSC (Speed/Cadence).");
            }
            else { Console.WriteLine("FAIL: CSC Measurement char missing."); }
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
                catch(Exception ex) { Console.WriteLine($"Write Exception {i+1}: {ex}"); }
                await Task.Delay(250);
            }
            Console.WriteLine("Write FAILED after retries.");
        }
    }
}
