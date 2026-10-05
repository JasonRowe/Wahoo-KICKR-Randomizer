using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared;
using System;
using System.IO;

namespace BikeFitnessApp.Tests
{
    [TestClass]
    public class PedalAnimationTests
    {
        private const double MetersPerRev = 6.5;

        #region GetCrankPhase

        [TestMethod]
        public void GetCrankPhase_ZeroDistance_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(0.0, MetersPerRev));
        }

        [TestMethod]
        public void GetCrankPhase_ExactRevolution_WrapsToZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(MetersPerRev, MetersPerRev), 1e-12);
        }

        [TestMethod]
        public void GetCrankPhase_HalfRevolution_ReturnsHalf()
        {
            Assert.AreEqual(0.5, PedalAnimation.GetCrankPhase(MetersPerRev * 0.5, MetersPerRev), 1e-12);
        }

        [TestMethod]
        public void GetCrankPhase_NegativeDistance_WrapsPositive()
        {
            // -0.1 rev -> 0.9
            double phase = PedalAnimation.GetCrankPhase(-MetersPerRev * 0.1, MetersPerRev);
            Assert.AreEqual(0.9, phase, 1e-12);
        }

        [TestMethod]
        public void GetCrankPhase_NonPositiveRevolution_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(10.0, 0.0));
            Assert.AreEqual(0.0, PedalAnimation.GetCrankPhase(10.0, -5.0));
        }

        #endregion

        #region GetFrameIndex

        [TestMethod]
        public void GetFrameIndex_ZeroPhase_ReturnsZero()
        {
            Assert.AreEqual(0, PedalAnimation.GetFrameIndex(0.0));
        }

        [TestMethod]
        public void GetFrameIndex_HalfPhase_ReturnsMiddleFrame()
        {
            Assert.AreEqual(6, PedalAnimation.GetFrameIndex(0.5));
        }

        [TestMethod]
        public void GetFrameIndex_NearEnd_ReturnsLastFrame()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(0.999));
        }

        [TestMethod]
        public void GetFrameIndex_PhaseOne_ClampsToLastFrame()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(1.0));
        }

        [TestMethod]
        public void GetFrameIndex_PhaseAboveOne_ClampsToLastFrame()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(2.5));
        }

        [TestMethod]
        public void GetFrameIndex_NegativePhase_ClampsToZero()
        {
            Assert.AreEqual(0, PedalAnimation.GetFrameIndex(-0.5));
        }

        [TestMethod]
        public void GetFrameIndex_DropLast_LoopsOverElevenFrames()
        {
            Assert.AreEqual(0, PedalAnimation.GetFrameIndex(0.0, SeamMode.DropLast));
            // 11-frame loop: phase near 1 -> index 10 (frame_10), never 11.
            Assert.AreEqual(10, PedalAnimation.GetFrameIndex(0.999, SeamMode.DropLast));
        }

        [TestMethod]
        public void GetFrameIndex_StraightAndCrossFade_UseAllTwelveFrames()
        {
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(0.999, SeamMode.Straight));
            Assert.AreEqual(11, PedalAnimation.GetFrameIndex(0.999, SeamMode.CrossFade));
        }

        #endregion

        #region GetEffectiveFrameCount

        [TestMethod]
        public void GetEffectiveFrameCount_MatchesSeamMode()
        {
            Assert.AreEqual(12, PedalAnimation.GetEffectiveFrameCount(SeamMode.Straight));
            Assert.AreEqual(12, PedalAnimation.GetEffectiveFrameCount(SeamMode.CrossFade));
            Assert.AreEqual(11, PedalAnimation.GetEffectiveFrameCount(SeamMode.DropLast));
        }

        #endregion

        #region GetSourceRect

        [TestMethod]
        public void GetSourceRect_FirstFrame_TopLeftCell()
        {
            var rect = PedalAnimation.GetSourceRect(0);
            Assert.AreEqual((0, 0, 290, PedalAnimation.CellCropHeight), rect);
        }

        [TestMethod]
        public void GetSourceRect_LastColumnOfTopRow()
        {
            var rect = PedalAnimation.GetSourceRect(5);
            Assert.AreEqual(1450, rect.X);
            Assert.AreEqual(0, rect.Y);
        }

        [TestMethod]
        public void GetSourceRect_FirstColumnOfBottomRow()
        {
            var rect = PedalAnimation.GetSourceRect(6);
            Assert.AreEqual(0, rect.X);
            Assert.AreEqual(322, rect.Y);
        }

        [TestMethod]
        public void GetSourceRect_LastFrame_BottomRightCell()
        {
            var rect = PedalAnimation.GetSourceRect(11);
            Assert.AreEqual((1450, 322, 290, PedalAnimation.CellCropHeight), rect);
        }

        [TestMethod]
        public void GetSourceRect_OutOfRange_Clamps()
        {
            Assert.AreEqual((0, 0, 290, PedalAnimation.CellCropHeight), PedalAnimation.GetSourceRect(-1));
            Assert.AreEqual((1450, 322, 290, PedalAnimation.CellCropHeight), PedalAnimation.GetSourceRect(12));
        }

        #endregion

        #region Cell crop & ground alignment

        [TestMethod]
        public void CellCropHeight_IsLessThanCellHeight_ToExcludeCaptionBand()
        {
            // Both values are constants of the code under test, so the pin is the point of the test; the
            // analyzer cannot see that (it only sees that they are constants).
#pragma warning disable MSTEST0032
            Assert.IsTrue(PedalAnimation.CellCropHeight < PedalAnimation.CellHeight);
            Assert.AreEqual(280, PedalAnimation.CellCropHeight);
#pragma warning restore MSTEST0032
        }

        [TestMethod]
        public void GetWheelBottomY_ReturnsValueWithinCell()
        {
            for (int i = 0; i < PedalAnimation.FrameCount; i++)
            {
                int bottom = PedalAnimation.GetWheelBottomY(i);
                Assert.IsTrue(bottom > 0 && bottom <= PedalAnimation.CellCropHeight,
                    $"frame {i} wheel bottom {bottom} out of crop range");
            }
        }

        [TestMethod]
        public void GetWheelBottomY_ClampsToValidFrame()
        {
            Assert.AreEqual(PedalAnimation.GetWheelBottomY(0), PedalAnimation.GetWheelBottomY(-1));
            Assert.AreEqual(PedalAnimation.GetWheelBottomY(11), PedalAnimation.GetWheelBottomY(12));
        }

        #endregion

        #region GetDrawScale / GetFrameDestRect

        [TestMethod]
        public void GetDrawScale_FullCellWidth_ReturnsOne()
        {
            Assert.AreEqual(1.0, PedalAnimation.GetDrawScale(PedalAnimation.CellWidth), 1e-12);
        }

        [TestMethod]
        public void GetDrawScale_NonPositiveWidth_FallsBackToNativeScale()
        {
            Assert.AreEqual(1.0, PedalAnimation.GetDrawScale(0.0), 1e-12);
            Assert.AreEqual(1.0, PedalAnimation.GetDrawScale(-10.0), 1e-12);
        }

        [TestMethod]
        public void GetDrawScale_DefaultInAppWidth_ScalesCellToThatWidth()
        {
            double scale = PedalAnimation.GetDrawScale(PedalAnimation.DefaultDrawWidthPx);
            Assert.AreEqual(PedalAnimation.DefaultDrawWidthPx / PedalAnimation.CellWidth, scale, 1e-12);
        }

        [TestMethod]
        public void GetFrameDestRect_IsHorizontallyCentredOnTheBikeAnchor()
        {
            var rect = PedalAnimation.GetFrameDestRect(0, PedalAnimation.DefaultDrawWidthPx);
            Assert.AreEqual(-rect.Width / 2.0, rect.X, 1e-12);
            Assert.AreEqual(PedalAnimation.DefaultDrawWidthPx, rect.Width, 1e-12);
        }

        [TestMethod]
        public void GetFrameDestRect_HeightKeepsTheCroppedCellAspect()
        {
            double scale = PedalAnimation.GetDrawScale(PedalAnimation.DefaultDrawWidthPx);
            var rect = PedalAnimation.GetFrameDestRect(3, PedalAnimation.DefaultDrawWidthPx);
            Assert.AreEqual(PedalAnimation.CellCropHeight * scale, rect.Height, 1e-12);
        }

        [TestMethod]
        public void GetFrameDestRect_AnchorsEachFrameByItsOwnWheelBottom()
        {
            double scale = PedalAnimation.GetDrawScale(PedalAnimation.DefaultDrawWidthPx);
            for (int i = 0; i < PedalAnimation.FrameCount; i++)
            {
                var rect = PedalAnimation.GetFrameDestRect(i, PedalAnimation.DefaultDrawWidthPx);
                double expectedBottom = (PedalAnimation.CellCropHeight - PedalAnimation.GetWheelBottomY(i)) * scale;
                Assert.AreEqual(expectedBottom, rect.Y + rect.Height, 1e-12, $"frame {i}");
            }
        }

        [TestMethod]
        public void GetFrameDestRect_BottomGridRow_SitsLowerToShareOneGroundLine()
        {
            // The bottom row's wheels bottom out ~5px higher inside their cells (273 vs 278), so
            // those frames must be pushed down by the same amount or the two rows bob.
            double scale = PedalAnimation.GetDrawScale(PedalAnimation.DefaultDrawWidthPx);
            var topRow = PedalAnimation.GetFrameDestRect(0, PedalAnimation.DefaultDrawWidthPx);
            var bottomRow = PedalAnimation.GetFrameDestRect(6, PedalAnimation.DefaultDrawWidthPx);

            double expectedDelta = (PedalAnimation.GetWheelBottomY(0) - PedalAnimation.GetWheelBottomY(6)) * scale;
            Assert.IsTrue(expectedDelta > 0, "test assumes the bottom row bottoms out higher in its cell");
            Assert.AreEqual(expectedDelta, (bottomRow.Y + bottomRow.Height) - (topRow.Y + topRow.Height), 1e-12);
        }

        [TestMethod]
        public void GetFrameDestRect_OutOfRange_Clamps()
        {
            Assert.AreEqual(PedalAnimation.GetFrameDestRect(0, 150.0), PedalAnimation.GetFrameDestRect(-1, 150.0));
            Assert.AreEqual(PedalAnimation.GetFrameDestRect(11, 150.0), PedalAnimation.GetFrameDestRect(12, 150.0));
        }

        #endregion

        #region Shipped sheet asset

        [TestMethod]
        public void GetDefaultSheetPath_UsesTheSharedFileName()
        {
            string path = PedalAnimation.GetDefaultSheetPath(Path.Combine("some", "images"));

            Assert.AreEqual(Path.Combine("some", "images", "rider_pedal_sheet_12f.png"), path);
        }

        [TestMethod]
        public void ShippedSheet_MatchesTheGridGeometryTheCropMathAssumes()
        {
            string? imagesDir = FindRepoImagesDirectory();
            if (imagesDir is null)
            {
                Assert.Inconclusive("Images/ not found above the test output directory — asset geometry not checked.");
                return;
            }

            string sheetPath = PedalAnimation.GetDefaultSheetPath(imagesDir);
            Assert.IsTrue(File.Exists(sheetPath), $"expected the shipped sheet at {sheetPath}");

            var (width, height) = ReadPngSize(sheetPath);
            Assert.AreEqual(PedalAnimation.CellWidth * PedalAnimation.SheetColumns, width,
                "sheet width no longer matches SheetColumns x CellWidth");
            Assert.AreEqual(PedalAnimation.CellHeight * PedalAnimation.SheetRows, height,
                "sheet height no longer matches SheetRows x CellHeight");
        }

        /// <summary>Nearest ancestor of the test output directory that contains an Images/ folder.</summary>
        private static string? FindRepoImagesDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, "Images");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>Reads width/height straight from a PNG IHDR chunk (no imaging dependency).</summary>
        private static (int Width, int Height) ReadPngSize(string path)
        {
            var header = new byte[24];
            using (var stream = File.OpenRead(path))
            {
                int read = stream.Read(header, 0, header.Length);
                Assert.AreEqual(header.Length, read, "PNG header truncated");
            }

            string chunkType = System.Text.Encoding.ASCII.GetString(header, 12, 4);
            Assert.AreEqual("IHDR", chunkType, "first PNG chunk is not IHDR");

            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return (width, height);
        }

        #endregion

        #region GetCrossFadeFactor

        [TestMethod]
        public void GetCrossFadeFactor_ZeroWindow_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeFactor(0.5, 0.0));
        }

        [TestMethod]
        public void GetCrossFadeFactor_BeforeWindow_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeFactor(0.8, 0.1));
        }

        [TestMethod]
        public void GetCrossFadeFactor_AtWindowStart_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeFactor(0.9, 0.1), 1e-12);
        }

        [TestMethod]
        public void GetCrossFadeFactor_AtWrap_ReturnsOne()
        {
            Assert.AreEqual(1.0, PedalAnimation.GetCrossFadeFactor(1.0, 0.1), 1e-12);
        }

        [TestMethod]
        public void GetCrossFadeFactor_MidWindow_Interpolates()
        {
            // window [0.9, 1.0], phase 0.95 -> t = 0.5
            Assert.AreEqual(0.5, PedalAnimation.GetCrossFadeFactor(0.95, 0.1), 1e-12);
        }

        #endregion

        #region GetCrossFadeWindowPhase

        [TestMethod]
        public void GetCrossFadeWindowPhase_ZeroSpeed_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeWindowPhase(0.0, MetersPerRev));
        }

        [TestMethod]
        public void GetCrossFadeWindowPhase_NonPositiveRevolution_ReturnsZero()
        {
            Assert.AreEqual(0.0, PedalAnimation.GetCrossFadeWindowPhase(30.0, 0.0));
        }

        [TestMethod]
        public void GetCrossFadeWindowPhase_ScalesWithSpeed()
        {
            // At 36 kph = 10 m/s, 80 ms = 0.8 m; over 6.5 m/rev -> ~0.123
            double window = PedalAnimation.GetCrossFadeWindowPhase(36.0, MetersPerRev);
            Assert.AreEqual(0.8 / 6.5, window, 1e-9);
        }

        #endregion

        #region Frame progression over distance

        [TestMethod]
        public void GetFrameIndexForDistance_AdvancesThroughAllTwelveFrames()
        {
            // A full revolution traverses every frame index exactly once in order.
            int previous = -1;
            for (int i = 0; i < 12; i++)
            {
                double distance = (i + 0.5) * (MetersPerRev / 12.0);
                int frame = PedalAnimation.GetFrameIndexForDistance(distance, MetersPerRev);
                Assert.AreEqual(i, frame);
                Assert.IsTrue(frame > previous);
                previous = frame;
            }
        }

        #endregion
    }
}
