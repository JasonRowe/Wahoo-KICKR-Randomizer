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
2. **Animated Background (See `ANIMATION_PLAN.md`):**
   - [ ] Implement multi-biome transitions (Mountain -> Plain -> Desert -> Ocean).
   - [ ] Replace mirroring logic in `SimulationCanvas.cs` with modulo-based scrolling once truly seamless assets are available.
   - [ ] Add roadside objects (shrubs/trees) for speed perception.

## Image Generation Prompts (Nano Banana)
- **Biome Reference:** "2D side-scrolling game background, [BIOME] biome. Vibrant colors, digital art. Perfectly seamless horizontal tiling; left and right edges must match exactly. Consistent flat brown dirt road at base."