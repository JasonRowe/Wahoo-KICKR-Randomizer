# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure unit tests pass ("dotnet test")

### Changes should always be added to git and committed after every valid build / test phase.

## When working with new bluetooth operations or devices we should first prove out the logic in test app BikeFitnessConsole.

App should have robust logging to ensure we can figure out why it crashes.

### TODOs
 - It turns out the resistance levels are very aggressive and biking at over 10% is tough. Might be worth figuring out how to use Simulation (Sim) mode you can modify the nominal grade in .5% increments and the app will provide  resistance accounting for your weight as entered into your profile to simulate riding that grade outdoors. THis would allow for more incremental resistance instead of 1-100. 
 - Would be nice to show cadence or distance also.
 - WheelCircumference should be set using standard bike tire sizes (26, 27, 29, 700c) and converted to meters internally via user settings menu in the workout page.
 - MPH vs KPH should be selected via settings menu in the workout page

## Refactoring & Improvement Plan (2026-01-28)

### Phase 1: Reliability & Architecture (Highest Priority) - **COMPLETED**
**Goal:** Decouple logic from the UI to prevent crashes and ensure robust connection handling.

1.  **Extract Bluetooth Service (`IBluetoothService`)** - **DONE**
    *   **Why:** Currently, connection logic is in `SetupView` and command logic is in `WorkoutView`. This split makes it hard to maintain a stable connection or recover from errors.
    *   **Action:** Create a dedicated `BluetoothService` class that handles scanning, connecting, writing commands, and receiving notifications. This service will be a "Singleton" that persists even if Views change.
    *   **Benefit:** Centralized error handling and reconnection logic (solving the "robust logging" requirement).

2.  **Implement MVVM (Model-View-ViewModel)** - **DONE** (Implemented "Zero-Dependency" approach)
    *   **Why:** To remove complex logic from `.xaml.cs` files. This is the standard for WPF/WinUI.
    *   **Action:**
        *   Create `ObservableObject` and `RelayCommand` (Zero-Dependency).
        *   Create `MainViewModel`, `SetupViewModel`, and `WorkoutViewModel`.
        *   Move `DispatcherTimer` and workout logic from `WorkoutView.xaml.cs` into `WorkoutViewModel`.
    *   **Benefit:** Makes the workout logic testable without a physical device or running the UI.

3.  **Dependency Injection (DI) Setup** - **DONE**
    *   **Why:** To cleanly manage the `BluetoothService` and ViewModels.
    *   **Action:** Configure `Microsoft.Extensions.DependencyInjection` in `App.xaml.cs`.

### Phase 2: User Experience & Features (Medium Priority)
**Goal:** Improve usability and implement requested features on top of the stable base.

1.  **Navigation Improvements**
    *   **Why:** Manually clearing `MainContainer.Children` is brittle.
    *   **Action:** Use a `CurrentViewModel` property in `MainViewModel` and DataTemplates in `App.xaml` to switch views automatically.
    *   *Status:* **Partially Completed** (Implemented as part of MVVM refactor in Phase 1).

2.  **Implement "Simulation Mode"**
    *   **Why:** To address the user feedback that percentage-based resistance is too aggressive.
    *   **Action:** Update `KickrLogic` to support a new `CalculateSimulationResistance` method that takes grade and rider weight, translating it to the appropriate resistance command.

3.  **Add Cadence & Distance Display**
    *   **Why:** Requested feature.
    *   **Action:** Update `KickrLogic` to parse the "CSC Measurement" (Cycling Speed & Cadence) characteristic if available, or derive cadence from power meter data if provided.

## Findings (2026-01-29) - Console App Testing

### 1. Resistance Control Modes
*   **Level Mode (OpCode 0x40):** Tested in Console App. User reported "no change" in resistance when sending levels 0-9. This mode appears unsupported or ineffective on the test device.
*   **Resistance Mode (OpCode 0x41):** Tested in Console App. User confirmed they "felt a change". This works as intended (0-100% resistance). 
    *   *Note:* The current implementation uses `0x41`. We should stick with this or investigate "Simulation Mode" for finer gradients (Sim mode uses OpCode 0x43 usually, taking weight/grade/Crr inputs).

### 2. Data Metrics
*   **Speed & Distance:** 
    *   Successfully implemented in Console App using **Wheel Revolution** data from the **Power Service (0x1818)**.
    *   Logic: `CalculateSpeed` and `CalculateDistance` methods added to `KickrLogic`.
    *   *Action:* Needs to be ported to the main WPF application.
*   **Cadence:**
    *   **Power Service:** The test device sends `0x1818` Power packets with the "Crank Data Present" flag (Bit 5) **UNSET**. This means we cannot derive cadence from the Power service on this device.
    *   **CSC Service (0x1816):** The device advertises this service. We need to ensure the main app subscribes to it as a fallback (or primary) source for Cadence when the Power service lacks it.