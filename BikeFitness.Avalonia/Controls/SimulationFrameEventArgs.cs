using System;

namespace BikeFitness.Avalonia.Controls
{
    /// <summary>
    /// Per-frame state handed to <see cref="SimulationCanvas.FrameRendered"/> subscribers: the delta the
    /// engine just integrated, plus the rider's resulting distance and speed.
    /// <para>
    /// Deliberately the same shape as the WPF canvas's equivalent, so the pacer panel's wiring is identical in
    /// both apps and the two UIs stay in sync.
    /// </para>
    /// </summary>
    public sealed class SimulationFrameEventArgs : EventArgs
    {
        public SimulationFrameEventArgs(double deltaSeconds, double riderDistanceMeters, double riderSpeedKph)
        {
            DeltaSeconds = deltaSeconds;
            RiderDistanceMeters = riderDistanceMeters;
            RiderSpeedKph = riderSpeedKph;
        }

        /// <summary>Seconds since the previous frame (already clamped by the canvas).</summary>
        public double DeltaSeconds { get; }

        /// <summary>The player's cumulative distance after this frame's update.</summary>
        public double RiderDistanceMeters { get; }

        /// <summary>The player's speed for this frame.</summary>
        public double RiderSpeedKph { get; }
    }
}
