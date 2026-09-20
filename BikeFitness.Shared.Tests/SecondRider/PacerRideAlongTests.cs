using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.SecondRider
{
    /// <summary>
    /// Ride-along pacer (POC #2b). Two things live here:
    /// <list type="bullet">
    /// <item>the regression lock: with <see cref="PacerConfig.RideAlongMode"/> off, every frame must match
    /// the behaviour captured from <c>6cca907</c> exactly — zero tolerance, no rounding;</item>
    /// <item>the ride-along behaviour: he can be caught, he attacks on schedule, you can get past him, and
    /// nothing about it breaks the physical model or the visible window.</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class PacerRideAlongTests
    {
        private const double Dt = 0.05;
        private const double SampleSeconds = 0.5;
        private const string GoldenFileName = "pacer-golden-6cca907.csv";

        // ------------------------------------------------------------------ the lock

        /// <summary>
        /// The important one. Flag off, same trace, same frame-for-frame numbers as <c>6cca907</c> — if this
        /// fails, the flag is not a flag and a normal ride has changed.
        /// </summary>
        [TestMethod]
        public void RideAlongMode_Off_IsByteIdenticalToCurrentBehaviour()
        {
            string path = GoldenPath();
            Assert.IsTrue(File.Exists(path), $"golden file missing: {path}");

            string[] golden = File.ReadAllLines(path);
            List<string> rows = RegressionRows(new PacerConfig());

            Assert.AreEqual(golden.Length - 1, rows.Count, "the run produced a different number of frames");

            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(
                    golden[i + 1], rows[i],
                    $"frame {i} does not match the 6cca907 baseline (states/speeds/gaps must be identical)");
            }
        }

        /// <summary>
        /// Writes the golden trace. Inert unless asked for, so CI never rewrites the baseline:
        /// <code>PACER_WRITE_GOLDEN=1 dotnet test --filter RegenerateGolden</code>
        /// Regenerate only when the flag-off behaviour changes on purpose.
        /// </summary>
        [TestMethod]
        public void RegenerateGolden()
        {
            if (Environment.GetEnvironmentVariable("PACER_WRITE_GOLDEN") != "1")
            {
                return;   // no-op in CI: the committed golden file is the source of truth
            }

            var sb = new StringBuilder();
            sb.AppendLine("Seconds;GapMeters;PacerSpeedKph;RiderSpeedKph;State");
            foreach (string row in RegressionRows(new PacerConfig()))
            {
                sb.AppendLine(row);
            }

            string path = Path.Combine(Path.GetTempPath(), GoldenFileName);
            File.WriteAllText(path, sb.ToString());
            Console.WriteLine($"golden written: {path}");
        }

        // ------------------------------------------------------- caught, attacked, passed

        /// <summary>He has to be catchable: from 60 m up the road, level inside a minute.</summary>
        [TestMethod]
        public void RideAlong_RiderCanCloseToLevel_Within60Seconds()
        {
            var config = RideAlongConfig();
            var trace = PacerSim.ConstantEffortTrace(180, speedKph: 25.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt, initialGapMeters: 60.0);

            double intoBand = result.SecondsToReachBand(config.AlongsideGapMeters, 4.0);
            Assert.IsTrue(double.IsFinite(intoBand), "he never came back to the band");
            Assert.IsTrue(intoBand <= 60.0, $"took {intoBand:F1} s to get back to the band");

            // Genuinely alongside, not merely inside a loose tolerance.
            double level = result.SecondsToReachBand(config.AlongsideGapMeters, 2.0);
            Assert.IsTrue(double.IsFinite(level), "he never got properly alongside");
            Assert.IsTrue(level <= 120.0, $"took {level:F1} s to get properly alongside");

            Assert.AreEqual(0, result.SpeedCapViolations, "the pacer broke a speed cap");
        }

        /// <summary>
        /// The bug that came off the trainer: a standing start. He used to arrive at station with a lag
        /// smoothed from zero, which cost him 50 m inside the first 100 m — and then tripped the overtake
        /// line on the way, so he conceded too and was gone for a minute. The lag is spent as road now, so
        /// off the line he is a couple of metres back at worst, and he is never handed over as "caught".
        /// </summary>
        [TestMethod]
        public void RideAlong_StandingStart_HeStaysWithYouAndNeverConcedes()
        {
            var config = RideAlongConfig();
            // 100 s of hold: the first attack is due at 120 s, and this test is about the start, not the attack.
            var trace = StandingStartTrace(topSpeedKph: 27.0, rampSeconds: 3.0, holdSeconds: 100.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            double closest = result.GapsMeters.Min();
            Assert.IsTrue(
                closest > -config.CatchOvertakeMeters,
                $"a standing start left him {-closest:F1} m behind (the overtake line is "
                + $"{config.CatchOvertakeMeters:F0} m past him) — he should never be \"caught\" off the line");

            Assert.AreEqual(0, result.Events.Count(e => e.Event == PacerEvent.Caught), "he conceded off a standing start");
            Assert.AreEqual(0, result.CountState(PacerState.Conceding), "he never left the alongside station");

            // Back inside a couple of metres of the station inside 45 s of the start, and still there.
            double recovered = double.NaN;
            for (int i = 0; i < result.TimesSeconds.Count; i++)
            {
                if (result.TimesSeconds[i] < 5.0) continue;
                if (result.GapsMeters[i] >= config.AlongsideGapMeters - 2.0) { recovered = result.TimesSeconds[i]; break; }
            }

            Assert.IsTrue(double.IsFinite(recovered), "he never got back to the station after the start");
            Assert.IsTrue(recovered <= 45.0, $"took {recovered:F1} s to get back to the station off the line");
            Assert.AreEqual(
                config.AlongsideGapMeters, result.FinalGapMeters, 2.0,
                $"he never settled on the station (finished at {result.FinalGapMeters:F2} m)");
            Assert.AreEqual(0, result.SpeedCapViolations, "the pacer broke a speed cap");
        }

        /// <summary>
        /// A surge still bites — he is on your old pace for a moment and you take ground — but it is a
        /// bounded allowance, not a free pass. Dropping him is a bigger effort than one acceleration
        /// (see <see cref="RideAlong_RiderWhoOutRidesHimGetsPastAndHeComesBack"/>): the alternative is a
        /// rival who vanishes for the next minute every time the rider stands on the pedals.
        /// </summary>
        [TestMethod]
        public void RideAlong_HardSurgeTakesGroundButDoesNotHandOverTheRoad()
        {
            var config = RideAlongConfig();
            var trace = StageTrace(
                ("settle", 30.0, 25.0, 0.0),
                ("surge", 30.0, 33.0, 0.0),
                ("ease", 90.0, 25.0, 0.0));

            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            double closest = result.GapsMeters.Min();
            double groundTaken = config.AlongsideGapMeters - closest;

            Assert.IsTrue(
                groundTaken >= 2.5,
                $"a 33 kph surge only took {groundTaken:F2} m off him — the surge has to be felt");
            Assert.IsTrue(
                closest > -config.CatchOvertakeMeters,
                $"a 33 kph surge handed the rider the road ({closest:F2} m past him)");
            Assert.AreEqual(0, result.Events.Count(e => e.Event == PacerEvent.Caught), "a plain surge registered as a pass");
            Assert.AreEqual(0, result.CountState(PacerState.Conceding), "he conceded to a surge he could answer");
        }

        /// <summary>
        /// The pass, properly earned: ride above what he can hold (his <see cref="PacerConfig.PacerWPerKg"/>
        /// cap) and he concedes — briefly, then he is back on your wheel. The old concession ran 20 s at
        /// 0.80 x, which at these speeds puts him 50 m back and off the end of the gap strip.
        /// </summary>
        [TestMethod]
        public void RideAlong_RiderWhoOutRidesHimGetsPastAndHeComesBack()
        {
            var config = RideAlongConfig();
            var trace = StageTrace(
                ("settle", 30.0, 25.0, 0.0),
                ("out-ride", 40.0, 37.0, 0.0),
                ("ease", 120.0, 25.0, 0.0));

            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            double closest = result.GapsMeters.Min();
            Assert.IsTrue(
                closest < -config.CatchOvertakeMeters,
                $"out-riding him at 37 kph only got {closest:F2} m past him (catch line {-config.CatchOvertakeMeters} m)");
            Assert.IsTrue(
                result.Events.Count(e => e.Event == PacerEvent.Caught) >= 1,
                "getting past him never registered");
            Assert.IsTrue(
                result.CountState(PacerState.Conceding) > 0,
                "he never conceded after being passed");

            // And he has to come back: alongside again inside 45 s of the effort ending.
            const double surgeEnd = 70.0;
            double closedAt = double.NaN;
            for (int i = 0; i < result.TimesSeconds.Count; i++)
            {
                if (result.TimesSeconds[i] < surgeEnd) continue;
                if (result.GapsMeters[i] >= config.AlongsideGapMeters - 2.0)
                {
                    closedAt = result.TimesSeconds[i];
                    break;
                }
            }

            Assert.IsTrue(double.IsFinite(closedAt), "the gap never came back to the station");
            Assert.IsTrue(
                closedAt - surgeEnd <= 45.0,
                $"took {closedAt - surgeEnd:F1} s to get back alongside — the rider cannot see him behind");
        }

        /// <summary>Attacks land on their own clock, inside the jitter band.</summary>
        [TestMethod]
        public void RideAlong_PacerAttacksOnSchedule_WithinJitterBounds()
        {
            var config = RideAlongConfig();
            config.AttackIntervalSeconds = 120.0;

            PacerRunResult result = PacerSim.Run(
                config, PacerSim.ConstantEffortTrace(600, speedKph: 25.0), Dt);

            List<double> attacks = result.Events
                .Where(e => e.Event == PacerEvent.Attacked)
                .Select(e => e.T)
                .ToList();

            Assert.IsTrue(attacks.Count >= 3, $"only {attacks.Count} attacks in 600 s");

            double low = config.AttackIntervalSeconds * (1.0 - config.AttackJitterFraction);
            double high = config.AttackIntervalSeconds * (1.0 + config.AttackJitterFraction);

            for (int i = 1; i < attacks.Count; i++)
            {
                double gap = attacks[i] - attacks[i - 1];
                Assert.IsTrue(
                    gap >= low - 0.1 && gap <= high + 0.1,
                    $"attack {i} came {gap:F1} s after the previous one (jitter band {low:F0}-{high:F0} s)");
            }

            Assert.IsTrue(
                result.CountState(PacerState.Attacking) > 0,
                "he never actually attacked");
        }

        /// <summary>An attack is still a physical model, not a teleport.</summary>
        [TestMethod]
        public void RideAlong_AttackIsCappedByPhysicalModel()
        {
            var config = RideAlongConfig();
            var trace = PacerSim.ConstantEffortTrace(180, speedKph: 20.0, gradePercent: 15.0);

            PacerRunResult result = PacerSim.Run(config, trace, Dt, initialGapMeters: 0.0);

            Assert.AreEqual(0, result.SpeedCapViolations, "the pacer teleported up a 15 % climb");
            Assert.IsTrue(
                result.CountState(PacerState.Attacking) > 0,
                "he should still go, he just cannot go fast on a 15 % climb");
        }

        /// <summary>The attack has to stay on the canvas — an attack you cannot see is not an attack.</summary>
        [TestMethod]
        public void RideAlong_NeverLosesSightOfTheRival()
        {
            var config = RideAlongConfig();
            PacerRunResult result = PacerSim.Run(
                config, PacerSim.ConstantEffortTrace(600, speedKph: 25.0), Dt);

            double worst = result.GapsMeters.Max();

            bool onScreen = SecondRiderGeometry.TryGetScreenPosition(
                riderDistanceMeters: 0.0,
                secondRiderDistanceMeters: worst,
                canvasWidth: 900.0,
                canvasHeight: 600.0,
                bikeScreenRatio: SecondRiderGeometry.DefaultBikeScreenRatio,
                pixelsPerMeter: 50.0,
                out double screenX,
                out SecondRiderSide side);

            Assert.IsTrue(
                onScreen && side == SecondRiderSide.OnScreen,
                $"worst-case attack left him {worst:F2} m ahead (screen x {screenX:F0} px) — off the canvas");
        }

        /// <summary>Mercy outranks attacks: he does not press a rider who is fading.</summary>
        [TestMethod]
        public void RideAlong_MercyOutranksAttacks()
        {
            var config = RideAlongConfig();
            config.AttackIntervalSeconds = 6.0;   // would be attacking constantly if mercy did not outrank it
            config.AttackLengthSeconds = 5.0;
            config.AttackJitterFraction = 0.0;

            var model = new PacerModel(config);
            model.Reset(0.0, config.AlongsideGapMeters);

            double t = 0;
            double distance = 0;
            double mercySeconds = 0;
            int attacksDuringMercy = 0;
            PacerEvent previous = PacerEvent.None;

            while (t < 150.0)
            {
                double speed = t < 60.0 ? 30.0 : 12.0;   // sets the best 30 s, then fades hard
                t += Dt;
                distance += speed / 3.6 * Dt;
                model.Advance(Dt, distance, speed, 0.0);

                if (model.State == PacerState.Mercy)
                {
                    mercySeconds += Dt;

                    if (model.LastEvent == PacerEvent.Attacked && previous != PacerEvent.Attacked)
                    {
                        attacksDuringMercy++;
                    }
                }

                previous = model.LastEvent;
            }

            Assert.IsTrue(mercySeconds > 0, "mercy never triggered");
            Assert.AreEqual(0, attacksDuringMercy, "he attacked while granting mercy");
        }

        /// <summary>A rider who stops must not produce a NaN, a negative speed or a drifting gap.</summary>
        [TestMethod]
        public void RideAlong_StoppedRider_NoNaN()
        {
            var config = RideAlongConfig();
            var trace = StageTrace(("rolling", 45.0, 25.0, 0.0), ("stopped", 45.0, 0.0, 0.0));

            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            int stopIndex = result.TimesSeconds.FindIndex(t => t >= 45.0);
            Assert.IsTrue(stopIndex > 0);

            int twoSecondsLater = result.TimesSeconds.FindIndex(t => t >= 47.0);
            Assert.AreEqual(0.0, result.PacerSpeedsKph[twoSecondsLater], 1e-9, "pacer must stop within 2 s of the rider");

            PacerState[] rideAlongStates =
            {
                PacerState.Alongside, PacerState.Attacking, PacerState.Recovering,
                PacerState.Conceding, PacerState.Mercy,
            };

            for (int i = stopIndex; i < result.GapsMeters.Count; i++)
            {
                Assert.IsTrue(double.IsFinite(result.GapsMeters[i]), $"gap not finite at step {i}");
                Assert.IsTrue(double.IsFinite(result.PacerSpeedsKph[i]), $"speed not finite at step {i}");
                Assert.IsTrue(result.PacerSpeedsKph[i] >= 0, $"negative speed at step {i}");
                Assert.IsTrue(
                    rideAlongStates.Contains(result.States[i]),
                    $"the rubber band's {result.States[i]} leaked into a ride-along run");
            }

            double frozenGap = result.GapsMeters[twoSecondsLater];
            Assert.AreEqual(frozenGap, result.FinalGapMeters, 1e-9, "the gap drifted while both were stopped");
        }

        // ------------------------------------------------------------------ the knobs

        /// <summary>
        /// Recover speed has to do something. It was inert while the state reused the elastic correction,
        /// which is why recovering is now an explicit multiplier.
        /// </summary>
        [TestMethod]
        public void RideAlong_RecoverSpeedSetsHowFastTheGapCloses()
        {
            var slow = RideAlongConfig();
            slow.RecoverRelativeSpeed = 0.60;

            var fast = RideAlongConfig();
            fast.RecoverRelativeSpeed = 0.95;

            var trace = StageTrace(("settle", 20.0, 25.0, 0.0), ("go", 120.0, 25.0, 0.0));

            PacerRunResult slowRun = PacerSim.Run(slow, trace, Dt);
            PacerRunResult fastRun = PacerSim.Run(fast, trace, Dt);

            int slowFrames = slowRun.CountState(PacerState.Recovering);
            int fastFrames = fastRun.CountState(PacerState.Recovering);

            Assert.IsTrue(slowFrames > 0, "he never recovered from an attack");
            Assert.IsTrue(
                slowFrames < fastFrames,
                $"recovering at 0.60 x ({slowFrames} frames) must be quicker than at 0.95 x ({fastFrames} frames)");
        }

        /// <summary>
        /// The other knob that mattered: easing off must bring him back to you, never push him up the road.
        /// With the reference speed taken as the lower of current and lagged, a slow-down cannot launch him.
        /// </summary>
        [TestMethod]
        public void RideAlong_EasingOffNeverPushesHimUpTheRoad()
        {
            var config = RideAlongConfig();
            var trace = StageTrace(("fast", 60.0, 28.0, 0.0), ("ease off", 90.0, 17.0, 0.0));

            PacerRunResult result = PacerSim.Run(config, trace, Dt);

            int easeStart = result.TimesSeconds.FindIndex(t => t >= 60.0);
            double settled = result.GapsMeters.Skip(easeStart + (int)(30.0 / Dt)).Max();

            Assert.IsTrue(
                settled <= config.AlongsideGapMeters + config.AlongsideBandMeters,
                $"he was {settled:F1} m up the road 30 s after the rider eased off (band allows "
                + $"{config.AlongsideGapMeters + config.AlongsideBandMeters:F0} m)");
        }

        /// <summary>The attack has to be an event, not a rounding error: he gets properly up the road.</summary>
        [TestMethod]
        public void RideAlong_AttackIsFelt()
        {
            var config = RideAlongConfig();
            config.AttackIntervalSeconds = 30.0;
            config.AttackJitterFraction = 0.0;
            config.AttackLengthSeconds = 12.0;

            PacerRunResult result = PacerSim.Run(
                config, PacerSim.ConstantEffortTrace(120, speedKph: 25.0), Dt);

            double station = config.AlongsideGapMeters;
            double peak = result.GapsMeters.Max();

            Assert.IsTrue(
                peak >= station + 5.0,
                $"the attack only opened the gap to {peak:F2} m from a {station:F1} m station — not a chase");
        }

        // ------------------------------------------------------------------ helpers

        private static PacerConfig RideAlongConfig() => new PacerConfig
        {
            RideAlongMode = true,
            AttackJitterFraction = 0.0,   // deterministic unless a test is about jitter
        };

        private static string GoldenPath() =>
            Path.Combine(AppContext.BaseDirectory, "SecondRider", "Golden", GoldenFileName);

        private static List<string> RegressionRows(PacerConfig config)
        {
            PacerRunResult result = PacerSim.Run(config, RideAlongRegressionTrace(), Dt);
            int every = (int)Math.Round(SampleSeconds / Dt);
            var rows = new List<string>();

            for (int i = 0; i < result.TimesSeconds.Count; i += every)
            {
                rows.Add(string.Join(
                    ';',
                    result.TimesSeconds[i].ToString("R", CultureInfo.InvariantCulture),
                    result.GapsMeters[i].ToString("R", CultureInfo.InvariantCulture),
                    result.PacerSpeedsKph[i].ToString("R", CultureInfo.InvariantCulture),
                    result.RiderSpeedsKph[i].ToString("R", CultureInfo.InvariantCulture),
                    result.States[i].ToString()));
            }

            return rows;
        }

        /// <summary>
        /// The shared trace: mixed speeds, a climb, a stop, a sprint, a fade long enough to grant mercy and
        /// a thrown gap — so one golden file covers every branch the flag-off pacer has.
        /// </summary>
        private static List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> RideAlongRegressionTrace()
        {
            var trace = new List<(double, double, double, double)>();
            double t = 0;
            double distance = 0;

            AppendStages(trace, ref t, ref distance, ("warm-up", 30.0, 22.0, 0.0));

            // Thrown gap, early: Surging needs the gap past target + band, which a converging rider cannot
            // do on their own, and mercy (once armed) outranks it — so it has to happen while nothing is
            // hovering. Same shape as the existing chatter tests.
            int jump = 0;
            double jumpEnd = t + 24.0;
            while (t < jumpEnd - 1e-9)
            {
                t += 1.0;
                distance += 24.0 / 3.6;
                double offset = (jump / 4) % 2 == 0 ? 16.0 : -16.0;
                trace.Add((t, distance + offset, 24.0, 0.0));
                jump++;
            }

            AppendStages(
                trace, ref t, ref distance,
                ("climb", 90.0, 20.0, 8.0),
                ("fast flat", 60.0, 35.0, 0.0),
                ("stop", 20.0, 0.0, 0.0),
                ("sprint", 30.0, 35.0, -2.0),
                ("fade", 60.0, 14.0, 2.0),
                ("roll", 40.0, 24.0, 1.0));

            return trace;
        }

        private static void AppendStages(
            List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> trace,
            ref double t,
            ref double distance,
            params (string Name, double Seconds, double SpeedKph, double GradePercent)[] stages)
        {
            if (trace.Count == 0)
            {
                trace.Add((0, 0, stages[0].SpeedKph, stages[0].GradePercent));
            }

            foreach ((string _, double seconds, double speedKph, double grade) in stages)
            {
                double end = t + seconds;
                while (t < end - 1e-9)
                {
                    t += 1.0;
                    distance += speedKph / 3.6;
                    trace.Add((t, distance, speedKph, grade));
                }
            }
        }

        /// <summary>Rider trace with named constant-effort stages.</summary>
        private static List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> StageTrace(
            params (string Name, double Seconds, double SpeedKph, double GradePercent)[] stages)
        {
            var trace = new List<(double, double, double, double)>();
            double t = 0;
            double distance = 0;
            AppendStages(trace, ref t, ref distance, stages);

            return trace;
        }

        /// <summary>
        /// A real standing start: stationary at t=0, then a ramp to <paramref name="topSpeedKph"/> over
        /// <paramref name="rampSeconds"/>, then held. <see cref="StageTrace"/> cannot express this — it
        /// starts the rider already at the first stage's speed, which is how the standing-start bug hid.
        /// </summary>
        private static List<(double T, double DistanceMeters, double SpeedKph, double GradePercent)> StandingStartTrace(
            double topSpeedKph,
            double rampSeconds,
            double holdSeconds)
        {
            var trace = new List<(double, double, double, double)> { (0, 0, 0, 0) };
            double t = 0;
            double distance = 0;

            while (t < rampSeconds - 1e-9)
            {
                t += 1.0;
                double speed = topSpeedKph * Math.Min(1.0, t / rampSeconds);
                distance += speed / 3.6;
                trace.Add((t, distance, speed, 0.0));
            }

            double end = t + holdSeconds;
            while (t < end - 1e-9)
            {
                t += 1.0;
                distance += topSpeedKph / 3.6;
                trace.Add((t, distance, topSpeedKph, 0.0));
            }

            return trace;
        }
    }
}
