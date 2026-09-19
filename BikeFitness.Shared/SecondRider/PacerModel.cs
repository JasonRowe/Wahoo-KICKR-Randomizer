using System;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>
    /// Tuning for the rubber-band pacer (POC #2). Every value is exposed as a live harness slider or
    /// checkbox, so nothing here should be treated as fixed.
    /// </summary>
    public sealed class PacerConfig
    {
        /// <summary>Gap the pacer tries to hold ahead of the rider, in metres.</summary>
        public double GapTargetMeters = 25.0;

        /// <summary>Half-width of the "contested" zone around the target gap, in metres.</summary>
        public double BandMeters = 15.0;

        /// <summary>How hard the pacer corrects, 0.1–0.8. Higher = twitchier rival.</summary>
        public double Elasticity = 0.35;

        /// <summary>Upper bound on pacer speed as a multiple of the rider's.</summary>
        public double MaxRelativeSpeed = 1.35;

        /// <summary>Lower bound on pacer speed as a multiple of the rider's.</summary>
        public double MinRelativeSpeed = 0.65;

        /// <summary>Seconds the gap must stay beyond target + band before <see cref="PacerState.Surging"/>.</summary>
        public double SurgeEnterSeconds = 1.5;

        /// <summary>Seconds the gap must stay beyond target − band before <see cref="PacerState.Easing"/>.</summary>
        public double EaseEnterSeconds = 2.0;

        /// <summary>Mercy triggers when the recent average speed drops below this fraction of the best 30 s average.</summary>
        public double MercyDropRatio = 0.70;

        /// <summary>Seconds the fade must persist before mercy is granted.</summary>
        public double MercySeconds = 8.0;

        /// <summary>How much mercy loosens the hold: band widened, effective gap target shortened.</summary>
        public double MercyBandBonus = 0.20;

        /// <summary>Pacer strength, W/kg of <see cref="RiderConstants.TotalMassKg"/> (≈ pseudo-power).</summary>
        public double PacerWPerKg = 2.5;

        /// <summary>Mercy rule on/off — the harness exposes this so the "ruthless bot" can be felt too.</summary>
        public bool MercyEnabled = true;

        /// <summary>Physics constants for the pacer's speed cap (shared with the power model).</summary>
        public RiderConstants Rider = new RiderConstants();

        public PacerConfig Clone()
        {
            return new PacerConfig
            {
                GapTargetMeters = GapTargetMeters,
                BandMeters = BandMeters,
                Elasticity = Elasticity,
                MaxRelativeSpeed = MaxRelativeSpeed,
                MinRelativeSpeed = MinRelativeSpeed,
                SurgeEnterSeconds = SurgeEnterSeconds,
                EaseEnterSeconds = EaseEnterSeconds,
                MercyDropRatio = MercyDropRatio,
                MercySeconds = MercySeconds,
                MercyBandBonus = MercyBandBonus,
                PacerWPerKg = PacerWPerKg,
                MercyEnabled = MercyEnabled,
                Rider = new RiderConstants
                {
                    TotalMassKg = Rider.TotalMassKg,
                    Crr = Rider.Crr,
                    CdA = Rider.CdA,
                    AirDensity = Rider.AirDensity,
                    DrivetrainEfficiency = Rider.DrivetrainEfficiency,
                },
            };
        }
    }

    /// <summary>What the pacer is currently doing. Transitions are hysteretic — see <see cref="PacerModel"/>.</summary>
    public enum PacerState
    {
        /// <summary>Holding the band. The normal state.</summary>
        Contested,

        /// <summary>The gap has been stretched past target + band: the pacer is running away from you.</summary>
        Surging,

        /// <summary>You have pulled the gap under target − band: the pacer is being dropped.</summary>
        Easing,

        /// <summary>You are fading against your own best — the pacer loosens its hold instead of disappearing.</summary>
        Mercy,
    }

    /// <summary>
    /// The rubber-band pacer: a virtual rival whose speed is derived from <b>your</b> speed and the current
    /// grade, holding a configurable gap. Pure and time-injected (<see cref="Advance"/> is the only clock),
    /// so the whole duel is unit-testable with no UI and no hardware.
    /// <para>
    /// <b>It only moves itself.</b> It never writes resistance, never touches <c>KickrLogic</c> or any
    /// Bluetooth service, and never changes the rider's effort — that is the POC's hard rule.
    /// </para>
    /// <para>
    /// <b>Sign correction vs the spec.</b> The spec's §5.1 correction term is
    /// <c>1 + Elasticity × (gap − GapTarget)/GapTarget</c>, which is positive feedback: with the documented
    /// gap convention (+ = pacer ahead) it makes a pacer that is already ahead accelerate further and a
    /// pacer that is behind slow down, so it can never re-enter the band. The spec's own convergence and
    /// no-oscillation tests require the opposite, so the implemented term is
    /// <c>1 + Elasticity × (GapTarget − gap)/GapTarget</c>: ahead ⇒ ease off, behind ⇒ push. That is what
    /// makes "holds a 10–40 m band" true, and it is what the harness FEELS like a rubber band.
    /// </para>
    /// </summary>
    public sealed class PacerModel
    {
        /// <summary>Bucket granularity for the rider's rolling speed history.</summary>
        public const double BucketSeconds = 0.5;

        /// <summary>Number of buckets kept (60 × 0.5 s = the 30 s "best effort" window).</summary>
        public const int BucketCount = 60;

        /// <summary>Buckets used for the "recent" average that mercy compares against the best 30 s.</summary>
        public const int RecentBucketCount = 10;

        /// <summary>How long a granted mercy period lasts, in seconds.</summary>
        public const double MercyDurationSeconds = 30.0;

        /// <summary>Minimum stopping deceleration, so a stopped rider brings the pacer to 0 within 2 s.</summary>
        public const double MaxStopDecelerationKphPerSecond = 45.0;

        private readonly PacerConfig _config;
        private readonly double[] _bucketDistances = new double[BucketCount];
        private readonly double[] _bucketDurations = new double[BucketCount];

        private int _bucketIndex;
        private int _primedBuckets;
        private double _currentBucketDistance;
        private double _currentBucketDuration;
        private double _bestThirtySecondAverageKph;

        private double _surgeTimer;
        private double _easeTimer;
        private double _fadeTimer;
        private double _mercyRemainingSeconds;

        private double _lastRiderDistanceMeters;
        private double _lastRiderSpeedKph;
        private double _lastRiderDeltaMeters;

        public PacerModel(PacerConfig? config = null)
        {
            _config = config ?? new PacerConfig();
            Reset(0.0);
        }

        public PacerConfig Config => _config;

        public PacerState State { get; private set; } = PacerState.Contested;

        /// <summary>Pacer's own cumulative distance, in metres.</summary>
        public double DistanceMeters { get; private set; }

        /// <summary>Pacer's own speed, in km/h.</summary>
        public double SpeedKph { get; private set; }

        /// <summary>The pacer's pseudo-power for the current frame (<c>≈</c>, not a measurement).</summary>
        public double PseudoWatts { get; private set; }

        /// <summary>Gap target actually in force (shortened while mercy is active).</summary>
        public double EffectiveGapTargetMeters { get; private set; }

        /// <summary>Hysteresis band actually in force (widened while mercy is active).</summary>
        public double EffectiveBandMeters { get; private set; }

        /// <summary>Best 30 s rolling average rider speed seen so far, in km/h.</summary>
        public double BestThirtySecondAverageKph => _bestThirtySecondAverageKph;

        /// <summary>Recent (5 s) rolling average rider speed, in km/h.</summary>
        public double RecentAverageKph => AverageKphOverBuckets(RecentBucketCount);

        /// <summary>Seconds of mercy remaining, 0 when not in mercy.</summary>
        public double MercyRemainingSeconds => Math.Max(0, _mercyRemainingSeconds);

        /// <summary>
        /// Finish-time delta for the end-of-run verdict, on the same sign convention as
        /// <see cref="DuelMath.DeltaSeconds"/>: positive = the pacer is ahead ("beat you by 12 s").
        /// </summary>
        public double ProjectedFinishDeltaSeconds =>
            DuelMath.DeltaSeconds(DistanceMeters, _lastRiderDistanceMeters, _lastRiderSpeedKph, SpeedKph);

        /// <summary>Gap in metres, positive when the pacer is ahead (identical convention to POC #1).</summary>
        public double GapMeters(double riderDistanceMeters)
        {
            return DuelMath.GapMeters(DistanceMeters, riderDistanceMeters);
        }

        /// <summary>Restarts the duel with the pacer holding <see cref="PacerConfig.GapTargetMeters"/>.</summary>
        public void Reset(double riderDistanceMeters)
        {
            Reset(riderDistanceMeters, _config.GapTargetMeters);
        }

        /// <summary>Restarts the duel with an explicit starting gap (tests use this to start at ±100 m).</summary>
        public void Reset(double riderDistanceMeters, double gapMeters)
        {
            DistanceMeters = Math.Max(0.0, riderDistanceMeters + gapMeters);
            SpeedKph = 0;
            PseudoWatts = 0;
            State = PacerState.Contested;
            EffectiveGapTargetMeters = _config.GapTargetMeters;
            EffectiveBandMeters = _config.BandMeters;

            Array.Clear(_bucketDistances);
            Array.Clear(_bucketDurations);
            _bucketIndex = 0;
            _primedBuckets = 0;
            _currentBucketDistance = 0;
            _currentBucketDuration = 0;
            _bestThirtySecondAverageKph = 0;

            _surgeTimer = 0;
            _easeTimer = 0;
            _fadeTimer = 0;
            _mercyRemainingSeconds = 0;

            _lastRiderDistanceMeters = riderDistanceMeters;
            _lastRiderSpeedKph = 0;
            _lastRiderDeltaMeters = 0;
        }

        /// <summary>
        /// One frame of the duel. Everything the pacer needs arrives as a parameter — no clocks, no state
        /// outside this object.
        /// </summary>
        public void Advance(double deltaTime, double riderDistanceMeters, double riderSpeedKph, double gradePercent)
        {
            if (!double.IsFinite(deltaTime) || deltaTime <= 0) return;

            double dt = Math.Min(deltaTime, 2.0);
            double riderSpeed = double.IsFinite(riderSpeedKph) ? Math.Max(0.0, riderSpeedKph) : 0.0;
            double riderDistance = double.IsFinite(riderDistanceMeters) ? riderDistanceMeters : _lastRiderDistanceMeters;

            _lastRiderDeltaMeters = Math.Max(0.0, riderDistance - _lastRiderDistanceMeters);
            _lastRiderDistanceMeters = riderDistance;
            _lastRiderSpeedKph = riderSpeed;

            RecordRiderHistory(dt, _lastRiderDeltaMeters);
            UpdateMercy(dt);

            double gap = GapMeters(riderDistance);
            UpdateState(dt, gap);

            if (riderSpeed <= 0.01)
            {
                // Stopped rider: coast down, never divide by zero, and hold the gap while both are still.
                double deceleration = Math.Max(MaxStopDecelerationKphPerSecond, SpeedKph / 2.0);
                SpeedKph = Math.Max(0.0, SpeedKph - (deceleration * dt));
                if (SpeedKph < 0.05) SpeedKph = 0.0;
            }
            else
            {
                double target = EffectiveGapTargetMeters > 0 ? EffectiveGapTargetMeters : 1.0;
                double relative = 1.0 + (_config.Elasticity * ((target - gap) / target));
                relative = Math.Clamp(relative, _config.MinRelativeSpeed, _config.MaxRelativeSpeed);

                double physicalCap = RiderPowerModel.SpeedFromPower(
                    _config.PacerWPerKg * _config.Rider.TotalMassKg, gradePercent, _config.Rider);

                SpeedKph = Math.Max(0.0, Math.Min(riderSpeed * relative, physicalCap));
            }

            PseudoWatts = RiderPowerModel.PowerFromSpeed(SpeedKph, gradePercent, _config.Rider);
            DistanceMeters += SpeedKph / 3.6 * dt;
        }

        private void UpdateState(double deltaTime, double gap)
        {
            if (_mercyRemainingSeconds > 0)
            {
                State = PacerState.Mercy;
                return;
            }

            bool stretched = gap > EffectiveGapTargetMeters + EffectiveBandMeters;
            bool dropped = gap < EffectiveGapTargetMeters - EffectiveBandMeters;

            // Hysteresis: a threshold implementation flickers at 60 Hz and reads as a bug, so entry needs
            // sustained evidence and exit needs the gap back on the other side of the target.
            _surgeTimer = stretched ? _surgeTimer + deltaTime : 0;
            _easeTimer = dropped ? _easeTimer + deltaTime : 0;

            switch (State)
            {
                case PacerState.Surging:
                    if (gap < EffectiveGapTargetMeters) State = PacerState.Contested;
                    break;
                case PacerState.Easing:
                    if (gap > EffectiveGapTargetMeters) State = PacerState.Contested;
                    break;
                default:
                    if (_surgeTimer >= _config.SurgeEnterSeconds) State = PacerState.Surging;
                    else if (_easeTimer >= _config.EaseEnterSeconds) State = PacerState.Easing;
                    break;
            }

            if (State == PacerState.Contested)
            {
                EffectiveGapTargetMeters = _config.GapTargetMeters;
                EffectiveBandMeters = _config.BandMeters;
            }
        }

        private void UpdateMercy(double deltaTime)
        {
            if (_mercyRemainingSeconds > 0)
            {
                _mercyRemainingSeconds -= deltaTime;
                if (_mercyRemainingSeconds <= 0)
                {
                    _mercyRemainingSeconds = 0;
                    _fadeTimer = 0;
                    State = PacerState.Contested;
                    EffectiveGapTargetMeters = _config.GapTargetMeters;
                    EffectiveBandMeters = _config.BandMeters;
                }

                return;
            }

            if (!_config.MercyEnabled || _bestThirtySecondAverageKph <= 0)
            {
                _fadeTimer = 0;
                return;
            }

            bool fading = RecentAverageKph < _bestThirtySecondAverageKph * _config.MercyDropRatio;
            _fadeTimer = fading ? _fadeTimer + deltaTime : 0;

            if (_fadeTimer >= _config.MercySeconds)
            {
                _mercyRemainingSeconds = MercyDurationSeconds;
                _fadeTimer = 0;
                State = PacerState.Mercy;
                EffectiveBandMeters = _config.BandMeters * (1.0 + _config.MercyBandBonus);
                EffectiveGapTargetMeters = _config.GapTargetMeters * (1.0 - _config.MercyBandBonus);
            }
        }

        private void RecordRiderHistory(double deltaTime, double riderDeltaMeters)
        {
            _currentBucketDistance += riderDeltaMeters;
            _currentBucketDuration += deltaTime;

            if (_currentBucketDuration < BucketSeconds) return;

            _bucketDistances[_bucketIndex] = _currentBucketDistance;
            _bucketDurations[_bucketIndex] = _currentBucketDuration;
            _bucketIndex = (_bucketIndex + 1) % BucketCount;
            if (_primedBuckets < BucketCount) _primedBuckets++;

            _currentBucketDistance = 0;
            _currentBucketDuration = 0;

            if (_primedBuckets >= BucketCount)
            {
                double average = AverageKphOverBuckets(BucketCount);
                if (average > _bestThirtySecondAverageKph) _bestThirtySecondAverageKph = average;
            }
        }

        private double AverageKphOverBuckets(int bucketCount)
        {
            int available = Math.Min(_primedBuckets, bucketCount);
            if (available <= 0) return 0;

            double distance = 0;
            double duration = 0;
            for (int i = 1; i <= available; i++)
            {
                int index = ((_bucketIndex - i) % BucketCount + BucketCount) % BucketCount;
                distance += _bucketDistances[index];
                duration += _bucketDurations[index];
            }

            return duration > 0 ? distance / duration * 3.6 : 0;
        }
    }
}
