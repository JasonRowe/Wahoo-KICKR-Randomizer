# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure unit tests pass ("dotnet test")

### Changes should always be added to git and committed after every valid build / test phase.

App should have robust logging to ensure we can figure out why it crashes.

### TODOs
 - It turns out the resistance levels are very aggressive and biking at over 10% is tough. Might be worth figuring out how to use Simulation (Sim) mode you can modify the nominal grade in .5% increments and the app will provide  resistance accounting for your weight as entered into your profile to simulate riding that grade outdoors. THis would allow for more incremental resistance instead of 1-100. 
 - Would be nice to show cadence or distance also.

## Refactoring & Improvement Plan (2026-01-28)

### Phase 1: Reliability & Architecture (Highest Priority)
**Goal:** Decouple logic from the UI to prevent crashes and ensure robust connection handling.

1.  **Extract Bluetooth Service (`IBluetoothService`)**
    *   **Why:** Currently, connection logic is in `SetupView` and command logic is in `WorkoutView`. This split makes it hard to maintain a stable connection or recover from errors.
    *   **Action:** Create a dedicated `BluetoothService` class that handles scanning, connecting, writing commands, and receiving notifications. This service will be a "Singleton" that persists even if Views change.
    *   **Benefit:** Centralized error handling and reconnection logic (solving the "robust logging" requirement).

2.  **Implement MVVM (Model-View-ViewModel)**
    *   **Why:** To remove complex logic from `.xaml.cs` files. This is the standard for WPF/WinUI.
    *   **Action:**
        *   Add `CommunityToolkit.Mvvm` package.
        *   Create `MainViewModel`, `SetupViewModel`, and `WorkoutViewModel`.
        *   Move `DispatcherTimer` and workout logic from `WorkoutView.xaml.cs` into `WorkoutViewModel`.
    *   **Benefit:** Makes the workout logic testable without a physical device or running the UI.

3.  **Dependency Injection (DI) Setup**
    *   **Why:** To cleanly manage the `BluetoothService` and ViewModels.
    *   **Action:** Configure `Microsoft.Extensions.DependencyInjection` in `App.xaml.cs`.

### Phase 2: User Experience & Features (Medium Priority)
**Goal:** Improve usability and implement requested features on top of the stable base.

1.  **Navigation Improvements**
    *   **Why:** Manually clearing `MainContainer.Children` is brittle.
    *   **Action:** Use a `CurrentViewModel` property in `MainViewModel` and DataTemplates in `App.xaml` to switch views automatically.

2.  **Implement "Simulation Mode"**
    *   **Why:** To address the user feedback that percentage-based resistance is too aggressive.
    *   **Action:** Update `KickrLogic` to support a new `CalculateSimulationResistance` method that takes grade and rider weight, translating it to the appropriate resistance command.

3.  **Add Cadence & Distance Display**
    *   **Why:** Requested feature.
    *   **Action:** Update `KickrLogic` to parse the "CSC Measurement" (Cycling Speed & Cadence) characteristic if available, or derive cadence from power meter data if provided.