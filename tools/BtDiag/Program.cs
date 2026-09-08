using System;
using System.Linq;
using System.Threading.Tasks;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;

// BtDiag: GATT service-resolution diagnostic for the Linux.Bluetooth library.
//
// Answers the decisive question: while connected to the KICKR,
//   does GetServicesResolvedAsync() (poll) disagree with a fresh read
//   (GetPropertiesAsync / GetAllAsync) or with the ServicesResolved event?
//
// Phases:
//   A. Connect, then compare poll vs fresh property reads (10s).
//   B. Event-based wait: WaitForPropertyValueAsync("ServicesResolved", true, 10s).
//   C. Enumerate GATT services + characteristics via GetServicesAsync().
//   D. If the Power Measurement char (0x2A63) is present, subscribe and
//      count notifications for 8s to prove the end-to-end data path.

class Program
{
    const string POWER_UUID = "00002a63-0000-1000-8000-00805f9b34fb";
    const string WAHOO_CP_UUID = "a026e005-0a7d-4ab3-97fa-f1500f9feb8b";

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== BtDiag: GATT service-resolution diagnostic ===");

        var adapters = await BlueZManager.GetAdaptersAsync();
        var adapter = adapters.FirstOrDefault();
        if (adapter == null)
        {
            Console.WriteLine("FAIL: no Bluetooth adapter");
            return 1;
        }
        Console.WriteLine($"Adapter found.");

        string mac = null;
        var addrArg = args.FirstOrDefault(a => !a.StartsWith("--"));
        if (addrArg != null)
        {
            mac = NormalizeMac(addrArg);
            Console.WriteLine($"Using supplied address: {mac}");
        }
        else
        {
            mac = await ScanForKickrAsync(adapter);
        }

        if (mac == null)
        {
            Console.WriteLine("FAIL: no KICKR/WAHOO device found while scanning.");
            return 1;
        }

        var device = await adapter.GetDeviceAsync(mac);
        if (device == null)
        {
            Console.WriteLine("FAIL: adapter.GetDeviceAsync returned null.");
            return 1;
        }
        Console.WriteLine($"GetDeviceAsync -> {device.ObjectPath}");

        device.Connected += (d, e) => { Console.WriteLine($">>> EVENT: Connected"); return Task.CompletedTask; };
        device.Disconnected += (d, e) => { Console.WriteLine($">>> EVENT: Disconnected"); return Task.CompletedTask; };
        device.ServicesResolved += (d, e) => { Console.WriteLine($">>> EVENT: ServicesResolved FIRED at {DateTime.Now:HH:mm:ss.fff}"); return Task.CompletedTask; };

        Console.WriteLine("Connecting (ConnectAsync)...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await device.ConnectAsync();
        sw.Stop();
        Console.WriteLine($"ConnectAsync returned at {DateTime.Now:HH:mm:ss.fff} (took {sw.Elapsed.TotalSeconds:F2}s).");
        sw.Restart();

        // ---------- Phase A: poll vs fresh read ----------
        Console.WriteLine();
        Console.WriteLine("--- Phase A: poll GetServicesResolvedAsync vs fresh GetProperties/GetAll ---");
        bool anyResolved = false;
        for (int i = 0; i < 40 && !anyResolved; i++)
        {
            await Task.Delay(250);
            bool poll = await device.GetServicesResolvedAsync();
            var props = await device.GetPropertiesAsync();     // fresh D-Bus GetAll
            var all = await device.GetAllAsync();               // another fresh D-Bus GetAll

            string[] propUuids = props.UUIDs ?? Array.Empty<string>();
            string[] allUuids = all.UUIDs ?? Array.Empty<string>();

            Console.WriteLine(
                $"[{((i + 1) * 0.25):F2}s] " +
                $"poll={poll} | " +
                $"GetProperties.ServicesResolved={props.ServicesResolved} IsConnected={props.IsConnected} UUIDs={propUuids.Length} | " +
                $"GetAll.ServicesResolved={all.ServicesResolved} GetAll.UUIDs={allUuids.Length}");

            if (i == 0 && propUuids.Length > 0)
                Console.WriteLine($"    GetProperties.UUIDs: {string.Join(", ", propUuids)}");
            if (i == 0 && allUuids.Length > 0)
                Console.WriteLine($"    GetAll.UUIDs: {string.Join(", ", allUuids)}");

            anyResolved = poll || props.ServicesResolved || all.ServicesResolved;
        }
        Console.WriteLine($"Phase A done. anyResolved={anyResolved}");

        // ---------- Phase B: event-based wait ----------
        Console.WriteLine();
        Console.WriteLine($"--- Phase B: WaitForPropertyValueAsync(\"ServicesResolved\", true, 30s) [t={sw.Elapsed.TotalSeconds:F2}s] ---");
        try
        {
            var w = System.Diagnostics.Stopwatch.StartNew();
            await device.WaitForPropertyValueAsync<bool>("ServicesResolved", true, TimeSpan.FromSeconds(30));
            w.Stop();
            Console.WriteLine($"WaitForPropertyValueAsync returned at {DateTime.Now:HH:mm:ss.fff} after {w.Elapsed.TotalSeconds:F2}s (t={sw.Elapsed.TotalSeconds:F2}s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WaitForPropertyValueAsync THREW at {DateTime.Now:HH:mm:ss.fff} (t={sw.Elapsed.TotalSeconds:F2}s): {ex.GetType().Name}: {ex.Message}");
        }

        // ---------- Phase B2: re-poll AFTER the event fired ----------
        Console.WriteLine();
        Console.WriteLine("--- Phase B2: re-poll immediately after the event path resolved ---");
        bool pollNow = await device.GetServicesResolvedAsync();
        var propsNow = await device.GetPropertiesAsync();
        Console.WriteLine($"post-event: poll={pollNow}, GetProperties.ServicesResolved={propsNow.ServicesResolved}, UUIDs={propsNow.UUIDs?.Length ?? 0}");

        // ---------- Phase C: enumerate services ----------
        Console.WriteLine();
        Console.WriteLine("--- Phase C: GetServicesAsync enumeration ---");
        IGattCharacteristic1 powerChar = null;
        IGattCharacteristic1 cpChar = null;
        try
        {
            var services = await device.GetServicesAsync();
            Console.WriteLine($"GetServicesAsync -> {services?.Count ?? 0} services");
            if (services != null)
            {
                foreach (var svc in services)
                {
                    string su = await svc.GetUUIDAsync();
                    var chars = await svc.GetCharacteristicsAsync();
                    Console.WriteLine($"  Service {su}: {(chars?.Count ?? 0)} characteristics");
                    if (chars == null) continue;
                    foreach (var c in chars)
                    {
                        string cu = await c.GetUUIDAsync();
                        Console.WriteLine($"      {cu}");
                        if (string.Equals(cu, POWER_UUID, StringComparison.OrdinalIgnoreCase)) powerChar = c;
                        if (string.Equals(cu, WAHOO_CP_UUID, StringComparison.OrdinalIgnoreCase)) cpChar = c;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetServicesAsync threw: {ex.GetType().Name}: {ex.Message}");
        }
        Console.WriteLine($"Phase C done. powerChar={(powerChar != null ? "FOUND" : "missing")}, wahooCp={(cpChar != null ? "FOUND" : "missing")}");

        // ---------- Phase D: subscribe & count notifications ----------
        if (powerChar != null)
        {
            Console.WriteLine();
            Console.WriteLine("--- Phase D: subscribe to Power Measurement (0x2A63) ---");
            int count = 0;
            int lastWatts = -1;
            try
            {
                await powerChar.WatchPropertiesAsync(props =>
                {
                    foreach (var change in props.Changed)
                    {
                        if (change.Key == "Value" && change.Value is byte[] data)
                        {
                            count++;
                            lastWatts = RoughWatts(data);
                            Console.WriteLine($"    NOTIFY #{count} @ {DateTime.Now:HH:mm:ss.fff}: len={data.Length} hex={Convert.ToHexString(data)} watts~{lastWatts}");
                        }
                    }
                });
                await powerChar.StartNotifyAsync();
                Console.WriteLine("StartNotifyAsync returned; listening 8s...");
                await Task.Delay(8000);
                await powerChar.StopNotifyAsync();
                Console.WriteLine($"Phase D done. notifications={count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Phase D error: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Phase D SKIPPED: power char never found, cannot subscribe.");
        }

        Console.WriteLine();
        Console.WriteLine("Disconnecting...");
        try { await device.DisconnectAsync(); } catch (Exception ex) { Console.WriteLine($"Disconnect threw: {ex.Message}"); }
        Console.WriteLine("Done.");
        return 0;
    }

    static int RoughWatts(byte[] data)
    {
        if (data.Length < 4) return -1;
        bool pedaling = (data[0] & 0x01) != 0;
        int watts = data[2] | (data[3] << 8);
        return pedaling ? watts : -1;
    }

    static async Task<string> ScanForKickrAsync(Adapter adapter)
    {
        Console.WriteLine("Scanning for KICKR/WAHOO...");
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.DeviceFound += async (s, e) =>
        {
            try
            {
                string alias = await e.Device.GetAliasAsync();
                if (!string.IsNullOrEmpty(alias) &&
                    (alias.IndexOf("KICKR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     alias.IndexOf("WAHOO", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    string a = await e.Device.GetAddressAsync();
                    Console.WriteLine($"  discovered {alias} ({a})");
                    tcs.TrySetResult(a);
                }
            }
            catch { /* ignore scan noise */ }
        };
        try
        {
            await adapter.StartDiscoveryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StartDiscoveryAsync threw (maybe already scanning): {ex.Message}");
        }

        var done = await Task.WhenAny(tcs.Task, Task.Delay(25000));
        try { await adapter.StopDiscoveryAsync(); } catch { }

        if (done == tcs.Task) return await tcs.Task;

        // Fallback: pick from BlueZ's known devices.
        Console.WriteLine("Scan timeout; checking known devices...");
        try
        {
            var known = await adapter.GetDevicesAsync();
            foreach (var d in known)
            {
                string alias = await d.GetAliasAsync();
                string addr = await d.GetAddressAsync();
                Console.WriteLine($"  known: {alias} ({addr})");
                if (alias.IndexOf("KICKR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    alias.IndexOf("WAHOO", StringComparison.OrdinalIgnoreCase) >= 0)
                    return addr;
            }
        }
        catch { }
        return null;
    }

    static string NormalizeMac(string arg)
    {
        string s = arg.Replace(":", "").Replace("-", "").Trim().ToUpper();
        if (s.Length == 12 && s.All(c => "0123456789ABCDEF".Contains(c)))
            return MacFromHex(s);
        if (ulong.TryParse(arg.Trim(), out ulong dec) && dec < 0x1_0000_0000_0000)
            return MacFromHex(dec.ToString("X12"));
        return arg;
    }

    static string MacFromHex(string hex12) =>
        $"{hex12[0..2]}:{hex12[2..4]}:{hex12[4..6]}:{hex12[6..8]}:{hex12[8..10]}:{hex12[10..12]}";
}
