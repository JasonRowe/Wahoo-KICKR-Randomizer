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
The Avalonia port is functional and matches the WPF version's visual quality on Linux.

#### Known Issues & Minor Tweaks:
- **HUD Performance:** Real-time HUD updates (Power/Speed) in Avalonia may have slightly higher latency than the WPF version.
- **Theme Consistency:** Material Design styles in Avalonia (via `Material.Avalonia`) need further tuning to match the exact "look and feel" of the WPF `MaterialDesignThemes`.

---

## High-Priority TODOs
1. **Heart Rate (BLE 0x180D):**
   - Implement `IHeartRateService` for Garmin/standard HRM.
   - Display BPM in `WorkoutView`.
2. **MacOS Support:**
   - Implement `MacOSBluetoothService` (Pending hardware purchase for testing).
3. **AI Assistant Mode:**
   - Integrate an AI service (e.g., OpenAI/Gemini) to allow voice-activated workout adjustments.
   - Design a "Bike Command" prompt structure to allow the AI to modify `KickrLogic` or switch `WorkoutMode` in real-time.
   - Requires extensive UI/UX and safety planning.

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

## Phase 4: Server-Side Strava Integration (Planned)

### 1. Server-Side Setup
- [ ] Create a script (PHP/Node.js) at `jasonrowe.com/misc/stravaconnect` to:
  - Securely store the `STRAVA_CLIENT_SECRET`.
  - Act as the `redirect_uri` for Strava OAuth.
  - Exchange the temporary `code` for `access_token` and `refresh_token`.
  - **(Optional) Database Integration:** Implement a small DB to store Refresh Tokens mapped to a unique UserID/DeviceID to allow multi-user "syncing" across devices.
  - Return tokens to the desktop app via a secure local callback or direct response.

### 2. Client-Side Refactoring
- [ ] Update `AppSettings.cs` to point to the new server-side Auth URL.
- [ ] Refactor `StravaService.cs` to:
  - Remove local `STRAVA_CLIENT_SECRET` dependencies.
  - Use the jasonrowe.com endpoint for token exchange and refresh operations.
  - Handle the new OAuth flow without needing local environment variables.
- [ ] Verify both WPF and Avalonia versions work with the new flow.

---

### Phase 5: Cross-Platform Testing & CI (Planned)
- [ ] Create `BikeFitness.Shared.UnitTests` project targeting `net10.0` (no Windows dependency).
- [ ] Migrate existing platform-agnostic tests from `BikeFitnessApp.UnitTests` to the shared project.
- [ ] Implement `LinuxBluetoothService` tests (potentially using mocks for BlueZ/DBus).
- [ ] Ensure `dotnet test` can run fully on Linux.
