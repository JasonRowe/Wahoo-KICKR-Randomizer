# BikeFitness.Harness Development Plan

## Goal
Create a lightweight WPF application to develop and stress-test the `SimulationCanvas` graphics without Bluetooth hardware dependencies.

## Phases

### Phase 1: Skeleton & Basic Rendering
*   **Objective:** Verify project setup and `DrawingVisual` pipeline.
*   **Tasks:**
    1.  Create `BikeFitness.Harness` (WPF App).
    2.  Add to Solution.
    3.  Create a basic `SimulationCanvas` class inheriting `FrameworkElement`.
    4.  Implement basic `DrawingVisual` logic to render a static "Red Circle".
    5.  Run and verify the circle appears.

### Phase 2: Input Controls & Data Flow
*   **Objective:** Verify we can drive the canvas with external data.
*   **Tasks:**
    1.  Add `Slider` controls to `MainWindow` for **Speed (0-60)** and **Grade (-10 to 20)**.
    2.  Add Dependency Properties to `SimulationCanvas` for these values.
    3.  Bind Sliders to the Canvas.
    4.  Update the rendering to display the current Speed/Grade values as text on the canvas (Debug Overlay).

### Phase 3: The Game Loop
*   **Objective:** Implement the animation heartbeat.
*   **Tasks:**
    1.  Hook into `CompositionTarget.Rendering`.
    2.  Implement a simple update loop: `X += Speed * DeltaTime`.
    3.  Animate the "Red Circle" moving across the screen.
    4.  Verify smooth motion and frame stability.

### Phase 4: Shared Integration (Future)
*   **Objective:** Move `SimulationCanvas` to a location shared with the main app.
*   **Tasks:**
    1.  Create a "Shared Project" (or link files).
    2.  Move `SimulationCanvas` code there.
    3.  Reference it in both `BikeFitnessApp` and `BikeFitness.Harness`.
