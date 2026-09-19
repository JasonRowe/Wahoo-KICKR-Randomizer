using System;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>Where the second rider is relative to the visible road window.</summary>
    public enum SecondRiderSide
    {
        /// <summary>Inside the canvas — draw the sprite.</summary>
        OnScreen,

        /// <summary>Past the right edge — draw the off-screen marker pointing forwards.</summary>
        AheadOffScreen,

        /// <summary>Past the left edge — draw the off-screen marker pointing backwards.</summary>
        BehindOffScreen,
    }

    /// <summary>
    /// Pure on-screen geometry for the second rider, mirroring <c>SimulationCanvas.DrawFrame</c>'s
    /// anchors (<c>bikeScreenX = ActualWidth × 0.3</c>, <c>PixelsPerMeter = 50</c>) so the HUD and the
    /// canvas cannot disagree about where a rival is.
    /// <para>
    /// This exists because of a hard constraint: at 50 px/m with the bike at 30 % of a 900 px canvas,
    /// only ~5.3 m behind and ~12.3 m ahead of the rider are visible. A rival 20 m up the road is
    /// <i>off screen</i>, so the off-screen marker is a requirement of the POC, not a nicety — and this
    /// class is what pins that geometry with tests.
    /// </para>
    /// </summary>
    public static class SecondRiderGeometry
    {
        /// <summary>Fraction of the canvas width the player's bike is anchored at (matches the canvas).</summary>
        public const double DefaultBikeScreenRatio = 0.3;

        /// <summary>Inset used when pinning an off-screen marker to the canvas edge.</summary>
        public const double DefaultMarkerMarginPx = 26.0;

        /// <summary>
        /// Screen X of the second rider, plus which side of the visible window it is on.
        /// <para>
        /// <paramref name="canvasHeight"/> is part of the signature for the vertical anchor the renderer
        /// applies (terrain height at the rival's own distance); the horizontal maths does not need it.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> when the rider is inside the canvas and should be drawn as a sprite.</returns>
        public static bool TryGetScreenPosition(
            double riderDistanceMeters,
            double secondRiderDistanceMeters,
            double canvasWidth,
            double canvasHeight,
            double bikeScreenRatio,
            double pixelsPerMeter,
            out double screenX,
            out SecondRiderSide side)
        {
            double gapMeters = secondRiderDistanceMeters - riderDistanceMeters;

            if (!IsUsable(canvasWidth, bikeScreenRatio, pixelsPerMeter) || !double.IsFinite(gapMeters))
            {
                screenX = 0;
                side = gapMeters >= 0 ? SecondRiderSide.AheadOffScreen : SecondRiderSide.BehindOffScreen;
                return false;
            }

            screenX = (bikeScreenRatio * canvasWidth) + (gapMeters * pixelsPerMeter);

            if (screenX < 0)
            {
                side = SecondRiderSide.BehindOffScreen;
            }
            else if (screenX > canvasWidth)
            {
                side = SecondRiderSide.AheadOffScreen;
            }
            else
            {
                side = SecondRiderSide.OnScreen;
            }

            return side == SecondRiderSide.OnScreen;
        }

        /// <summary>
        /// Horizontal position for the off-screen marker chip, pinned inside the canvas edge. Returns
        /// the canvas centre when the canvas is narrower than the two margins.
        /// </summary>
        public static double GetMarkerX(double secondRiderScreenX, double canvasWidth, double marginPx = DefaultMarkerMarginPx)
        {
            if (!double.IsFinite(canvasWidth) || canvasWidth <= 0) return 0;
            if (!double.IsFinite(secondRiderScreenX)) return canvasWidth / 2.0;

            double margin = Math.Max(0, marginPx);
            if (canvasWidth <= margin * 2.0) return canvasWidth / 2.0;

            return Math.Clamp(secondRiderScreenX, margin, canvasWidth - margin);
        }

        private static bool IsUsable(double canvasWidth, double bikeScreenRatio, double pixelsPerMeter)
        {
            return double.IsFinite(canvasWidth) && canvasWidth > 0
                && double.IsFinite(bikeScreenRatio)
                && double.IsFinite(pixelsPerMeter) && pixelsPerMeter > 0;
        }
    }
}
