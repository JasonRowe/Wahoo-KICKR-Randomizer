# BikeFitnessApp - Core Context & Constraints

## Critical Workflow Rules
- **Build & Test:** Run `dotnet build` and `dotnet test` before finishing.
- **Shared Projects:** When modifying `BikeFitness.Shared`, run `dotnet clean` then build the **Solution**.
- **PowerShell:** Use `;` for command chaining (e.g., `git add .; git commit`).

## Hardware: Wahoo KICKR SNAP (BLE)
- **Control Mode:** "Fake Sim Mode" using Resistance OpCode `0x41`. (OpCode `0x42` is NOT supported).
- **Speed Calculation:** Divisor MUST be **1024.0** (standard CSC). Using 2048.0 causes telemetry to freeze.
- **Cadence:** Not supported via standard BLE (Bit 5 of flags is 0). Do not attempt to parse CSC cadence.

## Logic & Calibration
- **Mode:** Grade Mode (-10% to +20%).
- **Mapping (Grade to Resistance %):**
  - -10% Grade -> 0% Resistance
  - 0% Grade -> 1% Resistance (Flat road feel)
  - 20% Grade -> 40% Resistance (Capped for realism)
- **Implementation:** `KickrLogic.CalculateResistanceFromGrade` (Piecewise linear interpolation).

## Current Architecture
- **UI:** Avalonia UI (Cross-platform) & WPF (Legacy Windows-only).
- **Patterns:** MVVM (Shared ViewModels in `BikeFitness.Shared/ViewModels`), Dependency Injection.
- **Services:** `IBluetoothService` abstracted in `.Shared`. Implementations: `WindowsBluetoothService` (WPF) and `MockBluetoothService` (Avalonia prototype).
- **Animation:** `SimulationEngine` (in `.Shared`) provides pure logic. 
  - **WPF Canvas:** Uses `DrawingVisual` for high-performance rendering.
  - **Avalonia Canvas:** Uses `Render` override with `DrawingContext` for cross-platform performance.
  - **Assets:** Uses 1-pixel overlap (`+1` width) to prevent white lines between tiles.

### Linux Port Status (Avalonia)
The Avalonia port is functional but requires significant polish to match the WPF version's visual quality.

#### Known Issues & Polish Tasks:
- **HUD Performance:** Real-time HUD updates (Power/Speed) in Avalonia may have slightly higher latency than the WPF version.
- **Theme Consistency:** Material Design styles in Avalonia (via `Material.Avalonia`) need further tuning to match the exact "look and feel" of the WPF `MaterialDesignThemes`.
- **Icon Support:** Some icons (using `PathIcon`) may not render with the same scaling/alignment as WPF's `PackIcon`.

---

## High-Priority TODOs
1. **Linux Hardware Support:** Implement `LinuxBluetoothService` using BlueZ/DBus.
2. **Heart Rate (BLE 0x180D):**
   - Implement `IHeartRateService` for Garmin/standard HRM.
   - Display BPM in `WorkoutView`.

## Image Generation Prompts (Nano Banana)
- **Biome Reference:** "2D side-scrolling game background, [BIOME] biome. Vibrant colors, digital art. Perfectly seamless horizontal tiling; left and right edges must match exactly. Consistent flat brown dirt road at base."

---

## Linux Port Progress (Avalonia UI)

### Phase 1: Decoupling `BikeFitness.Shared` from WPF (COMPLETED)
- [x] Extract math, physics, and state logic into `SimulationEngine`.
- [x] Move all UI rendering out of `.Shared`.
- [x] Create an interface system (`IUserInterfaceService`) for dialogs/UI thread calls.

### Phase 2: Abstracting Bluetooth (COMPLETED)
- [x] Verify `IBluetoothService` has no Windows-specific types.
- [x] Rename original implementation to `WindowsBluetoothService`.
- [x] Move models like `DeviceDisplay` to `.Shared.Models`.

### Phase 3: The Avalonia Switch (COMPLETED)
- [x] Create `BikeFitness.Avalonia` project (`net10.0`).
- [x] Port `MainWindow`, `SetupView`, and `WorkoutView` to AXAML.
- [x] Implement Avalonia-native `SimulationCanvas` using `SimulationEngine`.
- [x] Migrate all ViewModels to `BikeFitness.Shared` using `CommunityToolkit.Mvvm`.
- [x] Add `MockBluetoothService` for cross-platform UI testing.

---

## Final Step: Native Linux Support & Verification

### 1. Verification on Linux
- [ ] Push changes to GitHub and pull to Linux laptop.
- [ ] Install .NET 10 SDK on Linux.
- [ ] Run `dotnet run --project BikeFitness.Avalonia` and verify:
  - UI renders correctly (spacing, colors, icons).
  - Cyclist and background animation runs smoothly at ~60fps.
  - Mock workout flow (Connect -> Start -> Stop -> Save) works without crashes.

### 2. Linux Bluetooth Implementation
- [ ] Create `LinuxBluetoothService.cs` in a new project or folder.
- [ ] Integrate a library like `Tmds.DBus` or `DotNet.BlueZ` to talk to the Linux Bluetooth stack.
- [ ] Implement scanning and GATT characteristic communication for the Wahoo KICKR.
- [ ] Register the Linux service in `App.axaml.cs` when `OperatingSystem.IsLinux()` is true.
