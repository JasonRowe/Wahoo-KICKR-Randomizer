# BikeFitnessApp: Hack Your Ride

**Turn your smart trainer into a mountain simulator. No subscriptions. Just code and sweat.**

![Connect Screen](Images/readme_image_connect.PNG)
![Workout Screen](Images/readme_image.PNG)

## What is this?
Most smart trainers (like the Wahoo KICKR SNAP) lock simulated hill climbing behind paid subscription apps or lack native grade physics. **BikeFitnessApp** bypasses that with a custom physics engine that translates road gradient directly into realistic trainer resistance (-10% downhills to +20% mountain ascents).

## Features at a Glance

* **Realistic Hill Physics ("Fake Sim Mode"):** True grade-based resistance mapping. Downhills let you coast (-10% grade = 0% resistance), while steep climbs make you earn every meter (up to +20%).
* **Animated Rider & Terrain:** A 12-frame pedaling cyclist that syncs to your cadence/speed, climbing dynamic terrain that tilts with the slope across parallax biomes.
* **Pacer & Ghost Duels:** Race side-by-side against an adaptive virtual pacer or challenge your own ghost replays from previous rides.
* **XP & Combo Scoring:** Gamified combo engine with streaks and multipliers that reward holding target power on tough grades.
* **Live Telemetry & Strava Sync:** Real-time speed, power, and distance telemetry with 1-click **FIT** export to Strava (plus CSV/JSON logs for analysis).
* **Workout Modes:** Cruise on the **Flat**, ride rolling **Hills**, conquer **Mountain** peaks, or brave **Random** terrain chaos.

## Platform Support

| Platform | Status | UI Framework | Bluetooth |
| :--- | :--- | :--- | :--- |
| **Linux** | ✅ Functional | Avalonia UI | BlueZ (DBus) |
| **Windows** | ✅ Stable | WPF & Avalonia UI | WinRT BLE |
| **macOS** | 🚧 Planned | Avalonia UI | Pending |

## Quick Start

### One-Line Install (Nightly Builds)
Grab the latest build directly on your training laptop:

**Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/JasonRowe/Wahoo-KICKR-Randomizer/main/scripts/update-and-ride.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/JasonRowe/Wahoo-KICKR-Randomizer/main/scripts/update-and-ride.ps1 | iex
```

### Run From Source
Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Linux / Cross-Platform
dotnet run --project BikeFitness.Avalonia

# Windows (WPF)
dotnet run --project BikeFitnessApp
```

## Testing & Verification

```bash
# Cross-platform logic, physics, and gamification tests (runs on Linux & Windows)
dotnet test BikeFitness.Shared.Tests/BikeFitness.Shared.Tests.csproj

# Windows WPF tests (Windows only)
dotnet test BikeFitnessApp.UnitTests
```

## Repository Structure

| Project | Purpose |
| :--- | :--- |
| `BikeFitness.Avalonia` | Modern cross-platform UI (Linux & Windows). |
| `BikeFitnessApp` | Original Windows WPF application. |
| `BikeFitness.Shared` | Core physics engine, BLE abstractions, telemetry, animations, and scoring. |
| `BikeFitness.Shared.Tests` | Comprehensive cross-platform unit test suite (240+ tests). |
| `BikeFitness.Harness` | Lightweight visual harness for tuning animations and physics. |
| `BikeFitnessConsole` | Diagnostic CLI tool for BLE scanning and telemetry debugging. |

---
*Built with .NET 10, C#, and a lot of sweat.*
