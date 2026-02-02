# BikeFitness.Harness Development Plan

## Goal
Create a lightweight WPF application to develop and stress-test the `SimulationCanvas` graphics without Bluetooth hardware dependencies.

## Phases

### Phase 1: Skeleton & Basic Rendering
*   **Objective:** Verify project setup and `DrawingVisual` pipeline.
*   **Status:** [x] Complete
*   **Tasks:**
    - [x] Create `BikeFitness.Harness` (WPF App).
    - [x] Add to Solution.
    - [x] Create a basic `SimulationCanvas` class inheriting `FrameworkElement`.
    - [x] Implement basic `DrawingVisual` logic to render a static "Red Circle".
    - [x] Run and verify the circle appears.

### Phase 2: Input Controls & Data Flow
*   **Objective:** Verify we can drive the canvas with external data.
*   **Status:** [x] Complete
*   **Tasks:**
    - [x] Add `Slider` controls to `MainWindow` for **Speed (0-60)** and **Grade (-10 to 20)**.
    - [x] Add Dependency Properties to `SimulationCanvas` for these values.
    - [x] Bind Sliders to the Canvas.
    - [x] Update the rendering to display the current Speed/Grade values as text on the canvas (Debug Overlay).

### Phase 3: The Game Loop
*   **Objective:** Implement the animation heartbeat.
*   **Status:** [x] Complete
*   **Tasks:**
    - [x] Hook into `CompositionTarget.Rendering`.
    - [x] Implement a simple update loop: `X += Speed * DeltaTime`.
    - [x] Animate the "Red Circle" moving across the screen.
    - [x] Verify smooth motion and frame stability.

### Phase 4: Shared Integration
*   **Objective:** Move `SimulationCanvas` to a location shared with the main app.
*   **Status:** [x] Complete
*   **Tasks:**
    - [x] Create a "Shared Project" (or link files).
    - [x] Move `SimulationCanvas` code there.
    - [x] Reference it in both `BikeFitnessApp` and `BikeFitness.Harness`.

### Phase 5: Real Drawing (Milestone 7)
*   **Objective:** Implement terrain that responds to speed and grade.
*   **Status:** [~] In Progress
*   **Tasks:**
    - [x] Implement distance tracking based on Speed.
    - [x] Implement angle calculation based on Grade.
    - [x] Draw scrolling ground line.
    - [x] Draw rotating bike placeholder.
    - [ ] Implement infinite terrain generation (Step 2).

## Architecture Decisions & Notes (ADR)
*   **2026-02-01: Rendering Engine:** Selected `DrawingVisual` over standard WPF shapes for performance. It bypasses the layout system (Measure/Arrange) which is critical for a 60fps game loop.
*   **2026-02-01: Loop Timing:** Using `Stopwatch` and `CompositionTarget.Rendering`. We calculate `DeltaTime` to ensure movement is consistent regardless of frame rate variability.
*   **2026-02-01: Resource Management:** Explicitly hooking/unhooking `CompositionTarget.Rendering` on `Loaded`/`Unloaded` events to prevent memory leaks ("zombie" render loops) when the control is removed from the visual tree.