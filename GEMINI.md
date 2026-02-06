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

### 3. Cadence Data (Hardware Limitation)
*   **Observation:** Attempted to implement cadence display via standard Power Measurement (`0x1818`) and CSC (`0x1816`) services.
*   **Result:** The hardware (KICKR SNAP) does not set the "Crank Data Present" flag (bit 5) in the Power Measurement characteristic (raw flags `0x14`). Additionally, the CSC service is either not broadcast or does not provide crank updates.
*   **Conclusion:** Real-time cadence is not supported by this specific trainer's current firmware/hardware configuration. Task removed to prevent redundant exploration.

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
1.  **Heart Rate Monitoring (Garmin Integration):**
    -   Create `IHeartRateService` and `HeartRateService` to handle connection to Heart Rate monitors (Standard BLE Service `0x180D`).
    -   Implement parsing for Heart Rate Measurement characteristic (`0x2A37`).
    -   Update `SetupViewModel` to allow connecting to a second device (HRM).
    -   Display BPM in `WorkoutViewModel` and `WorkoutView`.
    -   *Note:* Garmin watches must have "Broadcast Heart Rate" enabled.
3.  **Workout Profiles:**
    -   [x] Added "Pyramid" mode (Steady increase then decrease over 40 intervals).
    -   [ ] Expand "Hilly" and "Mountain" modes to use more complex Grade patterns now that the logic supports it.
4.  **Workout Reporting & AI Analysis:**
    -   Implement session data collection (Power, Speed, Distance, Grade, and Heart Rate once available) at 1-second intervals.
    -   Add a "Save Workout Report" feature using `SaveFileDialog`.
    -   Export format: **JSON** (structured for AI ingestion) and/or **CSV** (for spreadsheet analysis).
    -   Include session metadata: Date, Duration, Total Distance, Avg/Max Power, Avg/Max HR.
5.  **Animated Background:**
    -   Plan and implement an animated background that reacts to Grade changes (e.g., parallax scrolling or changing scenery).
    -   **TODO:** Replace explicit meter markers with environmental objects (shrubs, trees) to mask distance synchronization discrepancies while maintaining the sense of speed.
        - [ ] **TODO:** Create a custom seamless loop background image. The current asset still requires the mirroring technique (alternating flip) to hide seams, which creates a "V" shape artifact. We need a true seamless asset where the left and right edges blend naturally.
        - [ ] **TODO:** Implement dynamic biome transitions. Start with Mountain, transition to Plain, Desert, and Ocean, then loop back to Mountain. 
            - All new biome assets (and the transitions between them) should be generated as **inherently seamless loops** (left edge matches right edge) to avoid using the mirroring technique.
            - **Reference Prompt:** "2D side-scrolling game background, [BIOME NAME] biome. Top 60% is sky with [SCENERY DETAILS]. Bottom 40% is [FOREGROUND DETAILS] that transitions into a consistent flat brown dirt road at the very base. Vibrant colors, digital art style. Perfectly seamless horizontal tiling; left and right edges must match exactly. Road position must be identical across all biomes."
        - [ ] **TODO:** Once true seamless assets are integrated for all biomes, remove the "Mirroring" logic from `SimulationCanvas.cs` and use standard modulo-based infinite scrolling.

## Future Suggestions
*   **Automatic Archiving:** Add a setting to auto-save every completed workout to a `Reports` folder in `Documents`.
*   **FIT/TCX Export:** Implement export to industry-standard `.fit` or `.tcx` formats to allow users to upload their BikeFitness data to Strava or Garmin Connect.
*   **AI Insight Button:** A `Analyze Performance` button that summarizes the current session data into a prompt for AI tools to provide training advice.
*   **RPE Tracking:** Prompt the user for a `Rate of Perceived Exertion` (1-10) and session notes after a workout to include in the report.
*   **Live Charts:** Integrate a lightweight charting library (like LiveCharts2) to show a real-time power/HR graph during the workout.
