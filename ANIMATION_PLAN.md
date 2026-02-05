# Implementation Plan: High-Performance Animated Background

## 1. Executive Summary
This document outlines the technical approach for replacing the static background in `WorkoutView` with a dynamic, scrolling terrain animation. The animation will feature a cyclist traversing hills that visually represent the current grade of the workout.

## 2. Core Technology: `DrawingVisual`

### Why `DrawingVisual`?
Standard WPF controls (`Path`, `Rectangle`, `Image`) carry significant overhead due to the framework's layout system ("Measure" and "Arrange" passes) and event handling. For a game-like loop with constant movement, this overhead causes frame drops.

**`DrawingVisual`** is a lightweight visual object used to render shapes, images, or text.
*   **Performance:** It bypasses the layout engine. It is the most efficient way to render 2D graphics in WPF while remaining inside the standard visual tree.
*   **Compatibility:** Unlike `Win2D` or `OpenGL` interop, `DrawingVisual` does not suffer from "Airspace" issues. We can overlay standard WPF controls (HUD, buttons, gauges) directly on top of it using a standard `Grid`.

## 3. Architecture & Components

### 3.1. The Host Control: `SimulationCanvas`
We will create a custom control inheriting from `FrameworkElement`. This control acts as the container for our visual layer.

**Responsibilities:**
*   Manage the collection of `DrawingVisual` objects (Background, Terrain, Bike).
*   Override `GetVisualChild` and `VisualChildrenCount` to bridge the visuals to the WPF core.
*   Expose Dependency Properties for MVVM binding (`CurrentGrade`, `SpeedKph`, `DistanceTraveled`).

### 3.2. Rendering Strategy

#### A. The Game Loop
We will use `CompositionTarget.Rendering`. This event fires once per frame (synced with the monitor's refresh rate), providing a consistent "heartbeat" for physics and animation updates.

#### B. Discrete Grade Transitions: "Hard Geometry, Soft Actor"
Since the app can jump instantly from a 5% grade to a 1% grade, we need to visualize this without jarring the user.

*   **The Terrain (Hard Geometry):** When a grade change occurs, the geometry logic immediately starts drawing the new slope from the current "World X" position forward. This creates a visible "bend" or "vertex" in the road appearing ahead of the rider.
*   **The Bike (Soft Actor):** To prevent the bike from "snapping" to the new angle, we apply **Linear Interpolation (Lerp)** or Damping to the bike's rotation and vertical position.
    *   **Visual Effect:** The rider sees the road change shape ahead, and the bike appears to smoothly "crest" or "dip" into the new slope over several frames (approx. 200-300ms), simulating suspension and mass.

#### C. The Terrain (Hills)
*   **Geometry:** A single `StreamGeometry` or `PathGeometry` will represent the ground.
*   **Culling (Optimization):** We will implement viewport culling. Although the "world" might be kilometers long, we only generate drawing instructions for the segment currently visible within the control's width, plus a small buffer.
*   **Styling:** A `LinearGradientBrush` (Frozen) will fill the area under the curve.

#### C. The Bike
*   **Representation:** A bitmap image (Sprite).
*   **Movement:**
    *   **X-Axis:** The bike stays relatively centered (or at a fixed X), and the *terrain moves* left to simulate travel.
    *   **Y-Axis:** Calculated based on the terrain height at the bike's current "World X" position.
    *   **Rotation:** A `RotateTransform` is applied to the visual based on the slope (Grade) of the terrain at the bike's position.
*   **Transforms:** We will use `RenderTransform` inside the `DrawingVisual` context to move/rotate the bike efficiently without triggering layout recalculations.

## 4. Implementation Steps

### Step 1: Scaffold the Host
Create `SimulationCanvas.cs` extending `FrameworkElement`.
```csharp
public class SimulationCanvas : FrameworkElement
{
    private readonly VisualCollection _children;
    // Dependency Properties for Grade, Speed, etc.
}
```

### Step 2: The Terrain System
*   Implement a logic class to generate Y-coordinates based on distance and grade history.
*   Since "Random" and "Hilly" modes generate grades dynamically, we need a buffer that stores the "Grade Profile" ahead of the rider.

### Step 3: The Rendering Loop
```csharp
void OnRendering(object sender, EventArgs e)
{
    // 1. Update World Position (Distance += Speed * DeltaTime)
    // 2. Clear previous terrain visual
    // 3. Draw visible terrain segment
    // 4. Update Bike Y and Rotation
}
```

### Step 4: Integration
Update `WorkoutView.xaml` to use a `Grid` layout for layering.

```xml
<Grid>
    <!-- Layer 1: The Animation -->
    <local:SimulationCanvas CurrentGrade="{Binding CurrentGrade}" ... />

    <!-- Layer 2: The HUD / Background Image overlay -->
    <!-- Note: We can keep the existing background image with low opacity 
         to texture the sky if desired -->
    <materialDesign:Card ... />
</Grid>
```

## 5. Performance Checklist (The "Golden Rules")

1.  **Freeze Brushes:** Always call `brush.Freeze()` on pens and brushes used in the render loop. This makes them thread-safe and performant.
2.  **Bitmap Scaling:** Set `RenderOptions.BitmapScalingMode="LowQuality"` (Nearest Neighbor) if using pixel art, or `HighQuality` for smooth sprites, but ensure consistency.
3.  **Opacity:** Prefer `Brush.Opacity` over `Visual.Opacity`.
4.  **Hardware Acceleration:** Avoid software-rendering triggers (like `DropShadowEffect` on the moving bike). If shadows are needed, draw them as simple semi-transparent ellipses under the bike.
5.  **Hit Testing:** Set `IsHitTestVisible="False"` on the `SimulationCanvas` so it doesn't interfere with mouse interaction for the UI controls layered on top.
6.  **Memory Management:** Monitor memory usage during window resizing and repeated Start/Stop cycles. Ensure `DrawingVisual` objects are properly cleared or reused, and unhook the `CompositionTarget.Rendering` event when the View is unloaded to prevent "zombie" render loops consuming CPU.

## 6. Acceptance Criteria (User Stories)

We will use the following scenarios to verify the animation's behavior:

### Scenario 1: Pre-Ride (Static)
*   **Given** the user has opened the workout view but NOT clicked "Start".
*   **Then** the terrain is visible as a flat line.
*   **And** the bike is positioned at the center-left of the screen, stationary (not bobbing or rotating).

### Scenario 2: Movement & Speed
*   **Given** the user has clicked "Start".
*   **When** detected speed is > 0 kph.
*   **Then** the terrain scrolls left to right proportional to speed.
*   **But** if speed is 0 (even if "Start" is active), the terrain remains static.

### Scenario 3: Grade Change (The Bend)
*   **Given** the grade changes from a steady state (e.g., 0%) to a new value (e.g., 5%).
*   **Then** a visible "bend" or vertex appears at the bike's rear wheel position.
*   **And** the terrain ahead draws at the new 5% upward angle.
*   **And** the bike **smoothly** rotates upward to match the new slope over ~250ms (no instant snapping).

### Scenario 4: The "Big Jump Up" (Sudden Incline)
*   **Given** the grade jumps instantly from 0% to 15%.
*   **Then** the road geometry sharply angles up immediately ahead of the bike.
*   **And** the bike rotates steeply upward, climbing the "wall" without clipping through the ground or flying into the sky.

### Scenario 5: The "Big Jump Down" (Sudden Drop)
*   **Given** the grade drops instantly from 10% to -10%.
*   **Then** the road geometry sharply angles down.
*   **And** the bike rotates downward, visually "diving" into the descent smoothly.

### Scenario 6: End of Ride
*   **Given** the user clicks "Stop".
*   **Then** the scrolling slows to a halt.
*   **And** the bike returns to a neutral, stationary posture.

### Scenario 7: Grade Change while Stationary
*   **Given** the bike is stopped (Speed = 0).
*   **When** the Grade changes (e.g., manual slider adjustment).
*   **Then** the terrain geometry immediately updates to reflect the new slope directly under the bike.
*   **And** the bike smoothly rotates in place to match the new incline, simulating the platform tilting.

## 7. Proposed Milestone: "Moving Hills"
The first implementation goal should be:
1.  Draw a flat line.
2.  Move the line to the left based on speed.
3.  Modulate the line height based on the current Grade.
4.  Place a static red rectangle (placeholder bike) that rides the line.

## 8. Development Strategy: Rapid Iteration Harness

To decouple the visual logic from the physical Bluetooth trainer (and the need to pedal), we will create a lightweight "Harness" project.

### 8.1. The `BikeFitness.Harness` App
A simple WPF project containing:
*   The `SimulationCanvas` control (shared via a Shared Project or linked file).
*   Sliders to manually inject:
    *   **Speed (0 - 60 kph):** To test scrolling smoothness and parallax.
    *   **Grade (-10% to 20%):** To test slope transitions and bike rotation interpolation.
    *   **Zoom/Scale:** To test rendering at different viewport sizes.
*   **"Auto-Drive" Toggle:** A simple timer that simulates a sinusoidal speed/grade pattern to verify long-running stability without user input.

**Benefit:** This allows us to refine the physics (Lerp, transitions) and rendering performance instantly without setting up the bike trainer or wearing workout gear.

## 9. Visual Polish: Sprites & Backgrounds
To match the aesthetic of `Images\displaybackground.png` (a clean, modern, flat or semi-realistic style), we will implement the following visual layers:

### 9.1. Parallax Background
*   **Static Layer:** The sky/distant mountains from `displaybackground.png` will serve as the furthest layer.
*   **Parallax Strategy:**
    *   **Sky:** Static or moves extremely slowly (1% of bike speed).
    *   **Mid-Ground (Trees/Hills):** Moves at ~20-30% of bike speed.
    *   **Foreground (Road/Terrain):** Moves at 100% speed.
*   **Implementation:** Use `DrawImage` in the `DrawingVisual` loop. We will calculate the source rectangle (texture coordinates) to scroll the image seamlessly (wrapping).

### 9.2. The Bike Sprite
*   **Asset:** Replace the red rectangle with a high-quality sprite of a cyclist.
*   **Animation:**
    *   **Wheel Spin:** If possible, separate the wheels as child sprites and rotate them based on distance traveled.
    *   **Rider Bob:** Apply a slight vertical sine wave oscillation to the rider's body relative to the bike frame to simulate pedaling effort, faster at higher speeds.

### 9.3. The HUD (Heads-Up Display)
*   **Overlay:** Render gauges (Speed, Power, RPM) directly on top of the canvas or using WPF controls in the Grid above.
*   **Style:** Semi-transparent dark backgrounds with bright text (matching the mockup's likely dark-mode or high-contrast fitness app aesthetic).

### 9.4. Environmental Objects (Shrubs/Trees)
*   **Goal:** Replace the explicit "meter markers" (which expose sync discrepancies) with natural roadside objects.
*   **Implementation:**
    *   Load sprites for shrubs, rocks, or trees.
    *   Place them at random or fixed intervals along the track.
    *   This provides the "sense of speed" (parallax) without implying a precise odometer reading, making any minor distance drift unnoticeable to the user.

## 10. Phase 8: Advanced Rider Animation (Future)

To increase realism, we will explore replacing the static "sliding" bike with articulated animations.

### 10.1. Wheel Rotation (High Priority)
*   **Concept:** Separate the bike into three visual components: `Frame + Rider` (Static), `Front Wheel`, `Rear Wheel`.
*   **Implementation:**
    *   Load separate `BitmapImage` assets for the frame and wheels.
    *   In the `DrawFrame` loop, calculate the rotation angle:
        `Angle = (TotalDistance / WheelCircumference) * 360`
    *   Draw the wheels first with a `RotateTransform` at their respective hub coordinates relative to the frame.
    *   Draw the frame on top.
*   **Feasibility:** High. Requires minimal asset editing (cropping the current sprite) and basic math. Adds significant "road feel" at low cost.

### 10.2. Pedaling Animation (Medium Priority)
*   **Concept:** A rider that pedals in sync with speed.
*   **Challenge:** Requires a **Sprite Sheet** (a grid of images showing one full pedal stroke, e.g., 12 frames).
*   **Implementation:**
    *   Load the sprite sheet as a single large `BitmapImage`.
    *   Calculate `CrankPosition` (0.0 to 1.0) based on distance (fixed gear assumption) or eventually cadence.
    *   Map `CrankPosition` to a specific frame index (e.g., Frame 0 to 11).
    *   Use `CroppedBitmap` or source-rect `DrawImage` to render only that frame.
*   **Feasibility:** Medium. The coding is straightforward, but acquiring or creating a smooth 12-frame cycling sprite sheet that matches our art style is the blocker. Without quality assets, this looks "janky" and is worse than a static rider.
*   **Recommendation:** Defer until a high-quality sprite sheet is available. Stick to "Rider Bob" (vertical sine wave) for now.

