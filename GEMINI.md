# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure all unit tests pass ("dotnet test")
*   **Refactoring Rule:** If modifying or creating Shared Projects/Libraries, ALWAYS run `dotnet clean` and build the **Entire Solution** to verify references and exclude duplicate files from the main project.

### Changes should always be added to git and committed after every valid build / test phase.

## Current Architecture (Grade Mode)
The application now operates primarily in "Grade Mode". 
- **UI:** Sliders and Status display Grade % (-10% to +20%).
- **Logic:** `KickrLogic.CalculateResistanceFromGrade` maps Grade % to a Target Resistance % (0.0 - 1.0) which is sent to the device via OpCode `0x41`.
- **Calibration (2026-01-30):**
    - **-10% Grade** -> **0% Resistance** (Full release/Coasting).
    - **0% Grade** -> **1% Resistance** (Minimal friction for flat road feel).
    - **20% Grade** -> **40% Resistance** (Capped max resistance to prevent "brick wall" feeling).
    - **Mapping:** Piecewise linear interpolation between these points.

## Environment & Tooling Notes
- **Shell:** Always use `;` as a statement separator instead of `&&` (e.g., `git add .; git commit ...`). This environment uses Windows PowerShell where `&&` is not supported in the current version.
- **Diagnostics:** Use the `BikeFitnessConsole` project to test raw Bluetooth commands and OpCodes before integrating into the main WPF app.

## Findings & Decisions

### 1. Simulation Mode (Physics)
*   **Attempt:** Tried enabling Wahoo Advanced Sim Mode via OpCode `0x42`.
*   **Result:** Device returned Error `0x40`. The specific hardware (KICKR SNAP) does not support the physics engine commands directly.
*   **Solution:** Implemented "Fake Simulation Mode". The app calculates the appropriate brake force based on the Grade and maps it to standard Resistance Mode (`0x41`), which is fully supported and granular.

### 2. Resistance Levels
*   **Observation:** Users reported that 100% resistance is impossible to pedal.
*   **Adjustment:** We capped the mapping so that a steep 20% climb equals only 40% actual brake force. This provides a realistic difficulty curve without locking the flywheel.

## Completed Tasks
- [x] **Architecture:** Extracted `BluetoothService` and implemented `IBluetoothService`.
- [x] **MVVM:** Refactored to ViewModels (`MainViewModel`, `WorkoutViewModel`) and Dependency Injection.
- [x] **Connection:** Robust scanning and connection logic.
- [x] **Telemetry:** Speed and Distance calculated from Wheel Revolutions (Power Service).
- [x] **Feature:** "Fake Sim Mode" (Grade Control).
    - [x] Grade-based UI (-10% to 20%).
    - [x] Logic to handle negative grades (downhill).
    - [x] Piecewise linear mapping for realistic feel.
    - [x] Unit tests for mapping logic.
- [x] **UX:** Prevent "-0.0%" display in UI.
- [x] **UX:** Added static background image to Workout View as a placeholder for future animation.

## TODOs / Next Steps
1.  **Cadence Display:**
    -   Investigate CSC Service (`0x1816`) or Crank Data from Power Service (`0x1818`) to display RPM.
2.  **Workout Profiles:**
    -   Expand "Hilly" and "Mountain" modes to use more complex Grade patterns now that the logic supports it.
3.  **Animated Background:**
    -   Plan and implement an animated background that reacts to Grade changes (e.g., parallax scrolling or changing scenery).
    -   **TODO:** Replace explicit meter markers with environmental objects (shrubs, trees) to mask distance synchronization discrepancies while maintaining the sense of speed.
    -   [x] **TODO:** Create a custom seamless loop background image. The current mirroring technique (alternating flip) works but creates a "V" shape artifact at the center. We need an asset where the left and right edges blend naturally to allow for true infinite scrolling without mirroring.
    -   **TODO:** Implement dynamic biome transitions. Start with Mountain (current), transition to Plain, Desert, and Ocean, then loop back to Mountain. This would require "Connector/Transition" background assets (e.g., Mountain-to-Plain) to bridge the seamless loops of each biome smoothly.
