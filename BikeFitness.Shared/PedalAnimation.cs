using System;

namespace BikeFitness.Shared
{
    /// <summary>
    /// How the pedal-cycle sprite sheet wraps from its last frame back to frame 0.
    /// </summary>
    public enum SeamMode
    {
        /// <summary>Straight 12-frame loop (hard switch frame_11 -&gt; frame_00).</summary>
        Straight = 0,

        /// <summary>12-frame loop with a cross-fade from frame_11 into frame_00 at the wrap.</summary>
        CrossFade = 1,

        /// <summary>11-frame loop (drop frame_11) so the wrap is frame_10 -&gt; frame_00.</summary>
        DropLast = 2,
    }

    /// <summary>
    /// Pure, UI-framework-agnostic math for the 12-frame pedal-cycle sprite sheet.
    /// Kept here (BikeFitness.Shared) so the WPF and Avalonia canvases can share one
    /// frame-indexing implementation and unit tests can exercise it on any platform.
    /// </summary>
    public static class PedalAnimation
    {
        /// <summary>Number of frames in one crank revolution.</summary>
        public const int FrameCount = 12;

        /// <summary>Sprite sheet grid layout (row-major: left→right, top row first).</summary>
        public const int SheetColumns = 6;
        public const int SheetRows = 2;

        /// <summary>Pixel size of a single cell in the sheet.</summary>
        public const int CellWidth = 290;
        public const int CellHeight = 322;

        /// <summary>Default metres travelled per crank revolution (~50/16 gearing on 700c).</summary>
        public const double DefaultMetersPerRevolution = 6.5;

        /// <summary>Nominal 700c wheel circumference in metres (used for wheel-spin overlay).</summary>
        public const double WheelCircumferenceMeters = 2.1;

        /// <summary>Cross-fade duration at the seam, in seconds.</summary>
        public const double CrossFadeSeconds = 0.08;

        /// <summary>
        /// Crank phase in [0, 1) for a given travelled distance.
        /// </summary>
        public static double GetCrankPhase(double distanceMeters, double metersPerRevolution)
        {
            if (metersPerRevolution <= 0) return 0.0;
            double phase = (distanceMeters % metersPerRevolution) / metersPerRevolution;
            if (phase < 0) phase += 1.0;
            return phase;
        }

        /// <summary>
        /// Frame index (0..FrameCount-1) for a crank phase, assuming a straight full loop.
        /// </summary>
        public static int GetFrameIndex(double phase)
        {
            return GetFrameIndex(phase, SeamMode.Straight);
        }

        /// <summary>
        /// Frame index for a crank phase under a chosen seam mode. <see cref="SeamMode.DropLast"/>
        /// loops over FrameCount-1 frames; the others loop over FrameCount.
        /// </summary>
        public static int GetFrameIndex(double phase, SeamMode seamMode)
        {
            int frameCount = GetEffectiveFrameCount(seamMode);
            int index = (int)Math.Floor(SimulationMath.Clamp01(phase) * frameCount);
            if (index >= frameCount) index = frameCount - 1;
            return index;
        }

        /// <summary>Frame index straight from travelled distance (straight loop).</summary>
        public static int GetFrameIndexForDistance(double distanceMeters, double metersPerRevolution)
        {
            return GetFrameIndex(GetCrankPhase(distanceMeters, metersPerRevolution));
        }

        /// <summary>Number of frames in the loop for a given seam mode.</summary>
        public static int GetEffectiveFrameCount(SeamMode seamMode)
        {
            return seamMode == SeamMode.DropLast ? FrameCount - 1 : FrameCount;
        }

        /// <summary>
        /// Source rectangle (x, y, width, height) in the sheet for a frame index.
        /// Row-major: column = index % 6, row = index / 6.
        /// </summary>
        public static (int X, int Y, int Width, int Height) GetSourceRect(int frameIndex)
        {
            int index = Math.Clamp(frameIndex, 0, FrameCount - 1);
            int col = index % SheetColumns;
            int row = index / SheetColumns;
            return (col * CellWidth, row * CellHeight, CellWidth, CellHeight);
        }

        /// <summary>
        /// Cross-fade blend factor t in [0, 1] for the wrap seam. <paramref name="fadeWindow"/>
        /// is the fraction of the crank cycle (0..1) over which the fade happens. Returns 0
        /// outside the fade window, rising to 1 as the phase reaches the wrap.
        /// </summary>
        public static double GetCrossFadeFactor(double phase, double fadeWindow)
        {
            if (fadeWindow <= 0) return 0.0;
            double clampedPhase = SimulationMath.Clamp01(phase);
            double start = 1.0 - fadeWindow;
            if (clampedPhase < start) return 0.0;
            return SimulationMath.Clamp01((clampedPhase - start) / fadeWindow);
        }

        /// <summary>
        /// Fraction of the crank cycle (0..1) to spend cross-fading, given the current speed
        /// and metres-per-revolution, so the fade lasts ~<see cref="CrossFadeSeconds"/> of travel.
        /// </summary>
        public static double GetCrossFadeWindowPhase(double speedKph, double metersPerRevolution)
        {
            if (metersPerRevolution <= 0) return 0.0;
            double metersPerSecond = (speedKph * 1000.0) / 3600.0;
            double metersInFade = metersPerSecond * CrossFadeSeconds;
            return SimulationMath.Clamp01(metersInFade / metersPerRevolution);
        }
    }
}
