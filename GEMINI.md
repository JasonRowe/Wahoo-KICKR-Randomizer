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
- **UI:** WPF with Material Design.
- **Patterns:** MVVM (ViewModels in `/ViewModels`), Dependency Injection.
- **Services:** `IBluetoothService` handles scanning/connection.
- **Animation:** `SimulationCanvas` (in `.Shared`) uses `DrawingVisual` for high-performance rendering. 
  - **Mirroring:** Currently uses alternating mirrored tiles (flip) to hide seams in non-seamless assets.
  - **Overlap:** Uses a 1-pixel overlap (`+1` width) to prevent white lines between tiles.

## High-Priority TODOs
1. **Heart Rate (BLE 0x180D):**
   - Implement `IHeartRateService` for Garmin/standard HRM.
   - Display BPM in `WorkoutView`.

## Image Generation Prompts (Nano Banana)
- **Biome Reference:** "2D side-scrolling game background, [BIOME] biome. Vibrant colors, digital art. Perfectly seamless horizontal tiling; left and right edges must match exactly. Consistent flat brown dirt road at base."

---

## Linux Port Preparation (Avalonia UI)

The goal is to prepare the codebase for a complete switch to Avalonia UI, allowing native execution on Windows, Linux (Ubuntu), and macOS. This preparation must be done and tested on Windows first to ensure no regressions.

### Phase 1: Decoupling `BikeFitness.Shared` from WPF (COMPLETED)
Currently, `.Shared` depends on WPF's `System.Windows.*` and `System.Windows.Media.*` (specifically `DrawingVisual` and `BitmapSource` in `SimulationCanvas.cs`).
1. **Goal:** Make `.Shared` a pure UI-agnostic library (e.g., pure `net10.0` without `<UseWPF>true</UseWPF>`).
2. **Steps:**
   - [x] Extract the math, physics (`_totalDistanceMeters`, `SpeedKph`, `GradePercent`), and state logic out of `SimulationCanvas` into a pure logic class (e.g., `SimulationEngine`).
   - [x] Move all UI rendering (`DrawingVisual`, `DrawingContext`, `BitmapSource`, `Pen`, `Brush`) out of `.Shared` and into the main WPF project (`BikeFitnessApp`).
   - [x] Create an interface or event system so `SimulationEngine` can tell the front-end *what* to draw without knowing *how* to draw it.
3. **Validation:** Ensure the app still builds and runs, rendering the canvas on Windows exactly as it did before. 

### Phase 2: Abstracting Bluetooth (`IBluetoothService`)
Linux uses BlueZ (DBus) while Windows uses `Windows.Devices.Bluetooth`.
1. **Goal:** Create a clean abstraction so the UI doesn't know which OS's Bluetooth stack it's using.
2. **Steps:**
   - Verify `IBluetoothService` has no Windows-specific types in its method signatures.
   - Rename the existing `BluetoothService` to `WindowsBluetoothService`.
   - Consider integrating a cross-platform library like `Plugin.BLE` or `InTheHand.BluetoothLE` to replace the Windows-specific implementation entirely, OR plan to write a `LinuxBluetoothService` later. If using a cross-platform package, implement and test it on Windows first.
3. **Validation:** Connect to the Wahoo KICKR and verify power, speed, and resistance commands still work on Windows.

### Phase 3: The Avalonia Switch
Once Phases 1 & 2 are complete and tested on Windows, the codebase is structurally ready.
1. **Create the Project:** Add a new `BikeFitness.Avalonia` project to the solution (`dotnet new avalonia.app`).
2. **Migrate UI:** Copy `MainWindow.xaml`, `WorkoutView.xaml`, etc. Change namespaces (`xmlns="https://github.com/avaloniaui"`). Swap `MaterialDesignThemes` for `Material.Avalonia`.
3. **Reimplement Canvas:** Re-create `SimulationCanvas` in the Avalonia project, inheriting from `Avalonia.Controls.Control` and overriding `Render(DrawingContext)`. Tie it to the decoupled `SimulationEngine`.
4. **Validation:** Run the Avalonia project on Windows. If it works, copy the repository to Ubuntu and run it natively.