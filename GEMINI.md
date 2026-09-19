# BikeFitnessApp — Core Context & Constraints

## Build & Test Workflow
- **Linux:**
  - Build: `dotnet build BikeFitness.Avalonia/BikeFitness.Avalonia.csproj`
  - Test: `dotnet test BikeFitness.Shared.Tests/BikeFitness.Shared.Tests.csproj`
- **Windows:** `dotnet build` / `dotnet test` (builds solution including WPF).
- **Shared Projects:** When modifying `BikeFitness.Shared`, run `dotnet clean` then rebuild.

## Hardware: Wahoo KICKR SNAP (BLE)
- **Control Mode:** "Fake Sim Mode" using Resistance OpCode `0x41` (OpCode `0x42` is NOT supported).
- **Speed Calculation:** Wheel/crank event times come from the `0x2A63` power packet, so the divisor is **2048.0** (1/2048 s units). The old note calling for 1024.0 was wrong: it halved every reported speed — confirmed against a counted hand-spin (20 wheel revs in 34 s = 2.7 mph; the app read 1.5 mph) and against the Wahoo app reading ~3 mph for the same spin. Distance was never affected (revolution-based).
- **Cadence:** Not supported via standard BLE (Bit 5 of flags is 0). Do not parse CSC cadence.

## Logic & Calibration
- **Mode:** Grade Mode (-10% to +20%).
- **Grade to Resistance Mapping:**
  - -10% Grade -> 0% Resistance
  - 0% Grade -> 1% Resistance (Flat road feel)
  - 20% Grade -> 40% Resistance (Capped for realism)
- **Calculation:** `KickrLogic.CalculateResistanceFromGrade` (piecewise linear interpolation).

## Architecture
- **UI:** Avalonia UI (`net10.0`, cross-platform) and legacy WPF (`net10.0-windows`).
- **State & ViewModels:** `BikeFitness.Shared` using MVVM (`CommunityToolkit.Mvvm`) and DI.
- **Bluetooth:** `IBluetoothService` abstracted in `.Shared`.
  - Linux: `LinuxBluetoothService` (BlueZ / DBus).
  - Windows: `WindowsBluetoothService`.
  - Testing: `MockBluetoothService`.
- **Animation & Canvas:** `SimulationEngine` and `PedalAnimation` in `.Shared`.
  - Rendering: `SimulationCanvas` in Avalonia (`DrawingContext`) and WPF (`DrawingVisual`).
  - Assets: `Images/rider_pedal_sheet_12f.png` for 12-frame pedaling animation. 1-pixel overlap (`+1` width) prevents seams between tiles.
- **Gamification & Duel:** Ghost replay, pacer model (`SecondRider/`), and XP combo engine (`Scoring/`) in `.Shared`.

## Roadmap / Future Work
1. **Heart Rate (BLE 0x180D):** Implement `IHeartRateService` and display BPM in `WorkoutView`.
2. **Server-Side Strava OAuth:** Relay token exchange via backend to eliminate client secret in app.
3. **MacOS Support:** Implement `MacOSBluetoothService` (pending hardware).
4. **AI Assistant Mode:** Voice/AI workout adjustments and dynamic grade commands.

## Assets Reference
- **Biome Prompt:** "2D side-scrolling game background, [BIOME] biome. Vibrant colors, digital art. Perfectly seamless horizontal tiling; left and right edges must match exactly. Consistent flat brown dirt road at base."
