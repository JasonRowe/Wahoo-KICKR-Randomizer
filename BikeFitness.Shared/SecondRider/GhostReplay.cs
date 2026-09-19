using System;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>
    /// Replays a <see cref="RideProfile"/> as a second rider (POC #1 "Ghost Racer").
    /// <para>
    /// Pure and time-injected: the only clock input is <see cref="Advance"/>, matching
    /// <c>SimulationEngine.Update(double deltaTime)</c>. No <c>DateTime.Now</c>, no
    /// <c>Stopwatch</c>, no timers — that is what makes it unit-testable and lets the harness drive it
    /// from the same auto-drive clock as the scenery.
    /// </para>
    /// <para>
    /// Two deliberate design choices, both driven by the 1 Hz telemetry:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Distance comes from the profile, not from integrating speed.</b> Virtual profile time
    /// advances by <c>deltaTime × EffortFactor</c> and the ghost's distance is interpolated straight out
    /// of the recording, so a dropped frame (or a long pause) can never drift the ghost away from its
    /// own trace.
    /// </description></item>
    /// <item><description>
    /// <b>Speed is slew-limited.</b> Distance is piecewise-linear, so the raw segment speed is a step
    /// function: it would jump 1–5 kph on every 1 Hz sample boundary and read as stair-stepping.
    /// <see cref="SpeedKph"/> therefore slews toward the segment speed at
    /// <see cref="MaxSpeedSlewKphPerSecond"/>, which is continuous frame-to-frame while distance (the
    /// quantity the gap badge and pedal phase use) stays exact.
    /// </description></item>
    /// </list>
    /// </summary>
    public sealed class GhostReplay
    {
        /// <summary>Largest <c>deltaTime</c> the replay will apply in one call (pause / GC hiccup guard).</summary>
        public const double MaxDeltaSeconds = 2.0;

        public const double MinEffortFactor = 0.5;
        public const double MaxEffortFactor = 1.5;

        /// <summary>Speed readout ceiling, so a corrupt profile cannot produce a nonsense number.</summary>
        public const double MaxSpeedKph = 80.0;

        /// <summary>How fast the reported speed may change, in kph per second of real time.</summary>
        public const double MaxSpeedSlewKphPerSecond = 8.0;

        private readonly RideProfile _profile;
        private double _profileTime;
        private double _effortFactor = 1.0;
        private int _segment;

        public GhostReplay(RideProfile? profile = null)
        {
            _profile = profile ?? new RideProfile();
            _profile.Normalise();
            Reset();
        }

        /// <summary>The profile being replayed.</summary>
        public RideProfile Profile => _profile;

        /// <summary>
        /// Replay effort as a fraction of the recorded ride (0.9 = "ride the same trace at 90 % speed",
        /// so the ghost finishes later). Clamped to <see cref="MinEffortFactor"/>…<see cref="MaxEffortFactor"/>.
        /// </summary>
        public double EffortFactor
        {
            get => _effortFactor;
            set => _effortFactor = Math.Clamp(double.IsFinite(value) ? value : 1.0, MinEffortFactor, MaxEffortFactor);
        }

        /// <summary>Seconds into the profile that the ghost has replayed so far.</summary>
        public double ProfileTime => _profileTime;

        /// <summary>Cumulative distance covered, from the profile's own trace.</summary>
        public double DistanceMeters { get; private set; }

        /// <summary>Slew-limited speed readout.</summary>
        public double SpeedKph { get; private set; }

        /// <summary>Interpolated grade at the ghost's current profile time.</summary>
        public double GradePercent { get; private set; }

        /// <summary>True once the ghost has replayed the whole profile.</summary>
        public bool Finished { get; private set; }

        /// <summary>True when there is nothing to replay.</summary>
        public bool IsEmpty => _profile.IsEmpty;

        /// <summary>Returns the ghost to the start of the profile, preserving <see cref="EffortFactor"/>.</summary>
        public void Reset()
        {
            _profileTime = 0;
            _segment = 0;

            if (_profile.IsEmpty)
            {
                DistanceMeters = 0;
                SpeedKph = 0;
                GradePercent = 0;
                Finished = false;
                return;
            }

            RideProfileSample first = _profile.Samples[0];
            DistanceMeters = first.DistanceMeters;
            GradePercent = first.GradePercent;
            SpeedKph = ClampSpeed(RawSegmentSpeedKph(0) * _effortFactor);
            Finished = false;
        }

        /// <summary>Advanced by <paramref name="deltaTime"/> seconds of real time.</summary>
        public void Advance(double deltaTime)
        {
            if (_profile.IsEmpty) return;
            if (!double.IsFinite(deltaTime) || deltaTime <= 0) return;

            double dt = Math.Min(deltaTime, MaxDeltaSeconds);
            double duration = _profile.DurationSeconds;
            double previousDistance = DistanceMeters;

            _profileTime = Math.Min(_profileTime + (dt * _effortFactor), duration);
            if (_profileTime >= duration) Finished = true;

            // Distance is read from the trace (never integrated), and can only move forwards.
            DistanceMeters = Math.Max(previousDistance, InterpolatedDistance(_profileTime));
            GradePercent = InterpolatedGrade(_profileTime);

            double target = Finished ? 0.0 : ClampSpeed(RawSegmentSpeedKph(_profileTime) * _effortFactor);
            double maxStep = MaxSpeedSlewKphPerSecond * dt;
            SpeedKph = ClampSpeed(SpeedKph + Math.Clamp(target - SpeedKph, -maxStep, maxStep));
        }

        /// <summary>Piecewise-linear distance at a profile time.</summary>
        private double InterpolatedDistance(double t)
        {
            var samples = _profile.Samples;
            if (samples.Count == 0) return 0;
            if (t <= samples[0].T) return samples[0].DistanceMeters;
            if (t >= samples[samples.Count - 1].T) return samples[samples.Count - 1].DistanceMeters;

            int i = SegmentIndexForTime(t);
            double t0 = samples[i].T;
            double t1 = samples[i + 1].T;
            if (t1 <= t0) return samples[i + 1].DistanceMeters;

            double f = (t - t0) / (t1 - t0);
            return samples[i].DistanceMeters + ((samples[i + 1].DistanceMeters - samples[i].DistanceMeters) * f);
        }

        /// <summary>Piecewise-linear grade at a profile time, clamped to a sane terrain range.</summary>
        private double InterpolatedGrade(double t)
        {
            var samples = _profile.Samples;
            if (samples.Count == 0) return 0;
            if (t <= samples[0].T) return Clamp(samples[0].GradePercent, -30.0, 30.0);
            if (t >= samples[samples.Count - 1].T) return Clamp(samples[samples.Count - 1].GradePercent, -30.0, 30.0);

            int i = SegmentIndexForTime(t);
            double t0 = samples[i].T;
            double t1 = samples[i + 1].T;
            if (t1 <= t0) return Clamp(samples[i].GradePercent, -30.0, 30.0);

            double f = (t - t0) / (t1 - t0);
            double grade = samples[i].GradePercent + ((samples[i + 1].GradePercent - samples[i].GradePercent) * f);
            return Clamp(grade, -30.0, 30.0);
        }

        /// <summary>
        /// Speed of the profile segment containing <paramref name="t"/>, derived from distance over
        /// time (per the POC spec: distance is the monotonic quantity, so speed follows from it).
        /// </summary>
        private double RawSegmentSpeedKph(double t)
        {
            var samples = _profile.Samples;
            if (samples.Count == 0) return 0;
            if (samples.Count == 1) return ClampSpeed(samples[0].SpeedKph);

            int i = SegmentIndexForTime(t);
            double dt = samples[i + 1].T - samples[i].T;
            if (dt <= 0) return ClampSpeed(samples[i].SpeedKph);

            double metersPerSecond = (samples[i + 1].DistanceMeters - samples[i].DistanceMeters) / dt;
            return ClampSpeed(Math.Max(0, metersPerSecond) * 3.6);
        }

        /// <summary>
        /// Segment index for a time, using a cached cursor. The cursor only ever walks forwards during
        /// a ride, so this is O(1) amortised per frame instead of a binary search every call.
        /// </summary>
        private int SegmentIndexForTime(double t)
        {
            var samples = _profile.Samples;
            int count = samples.Count;
            if (count < 2) return 0;

            int last = count - 2;
            if (_segment > last) _segment = last;
            if (_segment < 0) _segment = 0;

            while (_segment > 0 && t < samples[_segment].T) _segment--;
            while (_segment < last && t >= samples[_segment + 1].T) _segment++;

            return _segment;
        }

        private static double ClampSpeed(double speedKph)
        {
            if (!double.IsFinite(speedKph)) return 0;
            if (speedKph < 0) return 0;
            if (speedKph > MaxSpeedKph) return MaxSpeedKph;
            return speedKph;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (!double.IsFinite(value)) return 0;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
