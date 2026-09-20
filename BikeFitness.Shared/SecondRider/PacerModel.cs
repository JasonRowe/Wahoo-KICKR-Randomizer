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

        /// <summary>
        /// Ride-along (POC #2b): he rides with you at <see cref="AlongsideGapMeters"/> and attacks now and
        /// then, instead of holding a fixed gap up the road. Default <b>off</b> — with it off the pacer is
        /// bit-identical to the rubber-band pacer and every value below is ignored.
        /// </summary>
        public bool RideAlongMode = false;

        /// <summary>Gap he rides at in ride-along mode, in metres. 4 m ≈ 200 px at 50 px/m: actually on screen.</summary>
        public double AlongsideGapMeters = 4.0;

        /// <summary>Band around the alongside gap, in metres. He eases back inside it rather than snapping.</summary>
        public double AlongsideBandMeters = 6.0;

        /// <summary>
        /// Distance over which the alongside pull-back ramps from nothing to <see cref="MaxRelativeSpeed"/>.
        /// The station gap is only a few metres, so normalising the elasticity on it makes the smallest error
        /// a full-speed rocket — the rider could never hold station, let alone get past him. Ramping over a
        /// road-scale distance keeps the correction graded: nearly nothing beside you, full effort to close
        /// a real gap.
        /// </summary>
        public double AlongsideElasticScaleMeters = 50.0;

        /// <summary>
        /// The same ramp for the <b>behind</b> half of the station: how many metres short of the station he
        /// has to be before the pull-back is at full strength. Deliberately shorter than
        /// <see cref="AlongsideElasticScaleMeters"/>, because being dropped is the one recovery the rider
        /// cannot see — the camera shows ~12 m ahead and the gap strip saturates, so he has to get back on
        /// terms within a few seconds, not over 50 m of road.
        /// </summary>
        public double AlongsideCatchUpScaleMeters = 20.0;

        /// <summary>
        /// The most of the rider's speed the alongside lag may take off him — as road, not as a levy. The
        /// lag (he is still riding your old pace while you go) is what makes a surge bite, but it must be
        /// bounded: the smoothed speed starts at whatever the rider was doing when he was switched on, so
        /// from a standing start an unbounded lag strands him 60 m off the back inside 20 s. Once he is
        /// this far behind the station the lag is spent and he simply rides your pace again. Keep it inside
        /// <see cref="AlongsideGapMeters"/> + <see cref="CatchOvertakeMeters"/>, or a standing start starts
        /// reading as an overtake. Small values (a metre or so) make the lag inert.
        /// </summary>
        public double AlongsideLagBudgetMeters = 8.0;

        /// <summary>
        /// Seconds before he reacts to a change in your speed, as a smoothing time constant. This is what
        /// makes a hard surge take ground off him: he is still riding your old pace while you go. It is also
        /// one-sided — see <c>PacerModel.RideAlongReferenceSpeed</c> — so easing off can never push him ahead,
        /// and it is spent as road rather than granted without limit (see
        /// <see cref="AlongsideLagBudgetMeters"/>), so an acceleration can colour the gap without ever
        /// stranding him.
        /// </summary>
        public double AlongsideResponseSeconds = 10.0;

        /// <summary>How far up the road an attack takes him, in metres, added to <see cref="AlongsideGapMeters"/>.</summary>
        public double AttackPushMeters = 8.0;

        /// <summary>Seconds between attacks, before jitter.</summary>
        public double AttackIntervalSeconds = 120.0;

        /// <summary>Fraction of the interval the attack timer jitters by, ±, 0–0.5.</summary>
        public double AttackJitterFraction = 0.25;

        /// <summary>Seconds an attack lasts.</summary>
        public double AttackLengthSeconds = 12.0;

        /// <summary>Pacer speed while recovering, as a multiple of yours, 0.60–0.95.</summary>
        public double RecoverRelativeSpeed = 0.80;

        /// <summary>Pacer speed while conceding, as a multiple of yours, 0.60–0.95.</summary>
        public double ConcedeRelativeSpeed = 0.80;

        /// <summary>How far past him you must get for it to count as a pass, in metres.</summary>
        public double CatchOvertakeMeters = 5.0;

        /// <summary>Seconds you must hold the pass before he concedes, in seconds.</summary>
        public double CatchHoldSeconds = 3.0;

        /// <summary>
        /// Seconds he concedes for once you are past. Deliberately short: at 25–30 kph a long concession digs a
        /// 25–30 m hole the rider then has to watch him climb out of, and a rival who is off the back is a
        /// rival the camera cannot show. Long enough to read as "fair play, go on then".
        /// </summary>
        public double ConcedeSeconds = 8.0;

        /// <summary>
        /// Seed for the attack-jitter RNG. The model stays clock-free and reproducible: same seed and same
        /// trace give the same attacks.
        /// </summary>
        public int AttackJitterSeed = 20260919;

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
                RideAlongMode = RideAlongMode,
                AlongsideGapMeters = AlongsideGapMeters,
                AlongsideBandMeters = AlongsideBandMeters,
                AlongsideElasticScaleMeters = AlongsideElasticScaleMeters,
                AlongsideCatchUpScaleMeters = AlongsideCatchUpScaleMeters,
                AlongsideLagBudgetMeters = AlongsideLagBudgetMeters,
                AlongsideResponseSeconds = AlongsideResponseSeconds,
                AttackPushMeters = AttackPushMeters,
                AttackIntervalSeconds = AttackIntervalSeconds,
                AttackJitterFraction = AttackJitterFraction,
                AttackLengthSeconds = AttackLengthSeconds,
                RecoverRelativeSpeed = RecoverRelativeSpeed,
                ConcedeRelativeSpeed = ConcedeRelativeSpeed,
                CatchOvertakeMeters = CatchOvertakeMeters,
                CatchHoldSeconds = CatchHoldSeconds,
                ConcedeSeconds = ConcedeSeconds,
                AttackJitterSeed = AttackJitterSeed,
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

        /// <summary>Ride-along: riding with you at the alongside gap. The normal state.</summary>
        Alongside,

        /// <summary>Ride-along: pushing up the road to the attack gap, so you have to chase.</summary>
        Attacking,

        /// <summary>Ride-along: easing back to alongside after an attack.</summary>
        Recovering,

        /// <summary>Ride-along: you got past him and he is letting you go for now.</summary>
        Conceding,
    }

    /// <summary>What last happened between you and the pacer. Ride-along only; <see cref="None"/> otherwise.</summary>
    public enum PacerEvent
    {
        /// <summary>Nothing has happened yet.</summary>
        None,

        /// <summary>He has just gone up the road.</summary>
        Attacked,

        /// <summary>You got <see cref="PacerConfig.CatchOvertakeMeters"/> past him and held it.</summary>
        Caught,

        /// <summary>His concede period is up: he stopped letting you go.</summary>
        Conceded,

        /// <summary>He is back alongside after an attack.</summary>
        RecoveredToAlongside,
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

        /// <summary>
        /// Ride-along: an attack is over after this long regardless of the gap, so a rider who stops cannot
        /// leave him stuck in <see cref="PacerState.Recovering"/>. The gap normally closes well inside it.
        /// </summary>
        public const double RecoverTimeoutSeconds = 30.0;

        /// <summary>
        /// How close to the alongside gap he has to get before <see cref="PacerState.Recovering"/> is done.
        /// Tight on purpose: the whole point of that state is the closing rate, which
        /// <see cref="PacerConfig.RecoverRelativeSpeed"/> sets.
        /// </summary>
        public const double RecoverExitToleranceMeters = 1.0;

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

        // Ride-along (POC #2b). All timers are advanced by Advance's deltaTime — never a wall clock.
        private Random _jitter = new Random(1);
        private double _attackTimer;
        private double _nextAttackSeconds;
        private double _attackRemainingSeconds;
        private double _recoverRemainingSeconds;
        private double _concedeRemainingSeconds;
        private double _holdTimer;
        private bool _passLatched;
        private double _smoothedRiderSpeedKph;
        private bool _smoothedSpeedPrimed;

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

        /// <summary>Ride-along: the last thing that happened. <see cref="PacerEvent.None"/> when the flag is off.</summary>
        public PacerEvent LastEvent { get; private set; } = PacerEvent.None;

        /// <summary>Ride-along: seconds until the next attack, jitter included. 0 when not applicable.</summary>
        public double SecondsToNextAttack =>
            _config.RideAlongMode && _nextAttackSeconds > 0
                ? Math.Max(0.0, _nextAttackSeconds - _attackTimer)
                : 0.0;

        /// <summary>
        /// Ride-along: the gap he is working to right now — the alongside gap, pushed out while attacking.
        /// Equal to <see cref="EffectiveGapTargetMeters"/> when the flag is off.
        /// </summary>
        public double RideAlongTargetMeters =>
            _config.RideAlongMode
                ? EffectiveGapTargetMeters + (_attackRemainingSeconds > 0 ? _config.AttackPushMeters : 0.0)
                : EffectiveGapTargetMeters;

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
            Reset(riderDistanceMeters, DefaultStartGapMeters());
        }

        /// <summary>Gap the pacer starts at: the alongside gap in ride-along mode, the target gap otherwise.</summary>
        private double DefaultStartGapMeters() =>
            _config.RideAlongMode ? _config.AlongsideGapMeters : _config.GapTargetMeters;

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

            LastEvent = PacerEvent.None;
            _jitter = new Random(_config.AttackJitterSeed);
            _attackTimer = 0;
            _attackRemainingSeconds = 0;
            _recoverRemainingSeconds = 0;
            _concedeRemainingSeconds = 0;
            _holdTimer = 0;
            _passLatched = false;
            _nextAttackSeconds = 0;
            _smoothedRiderSpeedKph = 0;
            _smoothedSpeedPrimed = false;

            if (_config.RideAlongMode)
            {
                // Ride-along owns the state machine: the rubber band's Contested/Surging/Easing never apply.
                State = PacerState.Alongside;
                EffectiveGapTargetMeters = _config.AlongsideGapMeters;
                EffectiveBandMeters = _config.AlongsideBandMeters;
                _nextAttackSeconds = NextAttackIntervalSeconds();
            }

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

            UpdateSmoothedRiderSpeed(riderSpeed, dt);

            RecordRiderHistory(dt, _lastRiderDeltaMeters);
            UpdateMercy(dt);

            double gap = GapMeters(riderDistance);
            if (_config.RideAlongMode) UpdateRideAlongState(dt, gap);
            else UpdateState(dt, gap);

            if (riderSpeed <= 0.01)
            {
                // Stopped rider: coast down, never divide by zero, and hold the gap while both are still.
                double deceleration = Math.Max(MaxStopDecelerationKphPerSecond, SpeedKph / 2.0);
                SpeedKph = Math.Max(0.0, SpeedKph - (deceleration * dt));
                if (SpeedKph < 0.05) SpeedKph = 0.0;
            }
            else
            {
                double reference = _config.RideAlongMode ? RideAlongReferenceSpeed(riderSpeed, gap) : riderSpeed;
                double relative;

                if (_config.RideAlongMode)
                {
                    relative = RideAlongRelativeSpeed(Math.Max(1.0, RideAlongTargetMeters), gap);
                }
                else
                {
                    // Unchanged from 6cca907: the rubber band's own correction, on its own target.
                    double target = EffectiveGapTargetMeters > 0 ? EffectiveGapTargetMeters : 1.0;
                    relative = 1.0 + (_config.Elasticity * ((target - gap) / target));
                }

                relative = Math.Clamp(relative, _config.MinRelativeSpeed, _config.MaxRelativeSpeed);

                double physicalCap = RiderPowerModel.SpeedFromPower(
                    _config.PacerWPerKg * _config.Rider.TotalMassKg, gradePercent, _config.Rider);

                SpeedKph = Math.Max(0.0, Math.Min(reference * relative, physicalCap));
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

        /// <summary>
        /// Ride-along state machine. Mercy outranks everything; otherwise he holds station alongside, takes
        /// off on the attack timer, eases back once the attack is done, and lets you go when you have held a
        /// pass. The rubber band's Contested/Surging/Easing states never apply while this is in charge.
        /// </summary>
        private void UpdateRideAlongState(double deltaTime, double gap)
        {
            if (_mercyRemainingSeconds > 0)
            {
                State = PacerState.Mercy;
                EffectiveGapTargetMeters = BaseGapTargetMeters * (1.0 - _config.MercyBandBonus);
                EffectiveBandMeters = BaseBandMeters * (1.0 + _config.MercyBandBonus);
                return;
            }

            _attackTimer += deltaTime;
            double alongside = _config.AlongsideGapMeters;

            switch (State)
            {
                case PacerState.Alongside:
                    // One concede per pass: he has to get back up the road before another pass counts, or a
                    // sustained effort would re-trigger it every ConcedeSeconds and hand the rider the road.
                    if (_passLatched && gap > alongside + _config.AlongsideBandMeters)
                    {
                        _passLatched = false;
                    }

                    if (!_passLatched && gap < -_config.CatchOvertakeMeters)
                    {
                        _holdTimer += deltaTime;
                        if (_holdTimer >= _config.CatchHoldSeconds)
                        {
                            _holdTimer = 0.0;
                            _passLatched = true;
                            State = PacerState.Conceding;
                            _concedeRemainingSeconds = _config.ConcedeSeconds;
                            LastEvent = PacerEvent.Caught;
                            break;
                        }
                    }
                    else
                    {
                        _holdTimer = 0.0;
                    }

                    if (_attackTimer >= _nextAttackSeconds)
                    {
                        State = PacerState.Attacking;
                        _attackRemainingSeconds = _config.AttackLengthSeconds;
                        _attackTimer = 0.0;
                        _nextAttackSeconds = NextAttackIntervalSeconds();
                        LastEvent = PacerEvent.Attacked;
                    }

                    break;

                case PacerState.Attacking:
                    _attackRemainingSeconds -= deltaTime;
                    if (_attackRemainingSeconds <= 0.0 || gap > RideAlongTargetMeters + _config.AlongsideBandMeters)
                    {
                        _attackRemainingSeconds = 0.0;
                        State = PacerState.Recovering;
                        _recoverRemainingSeconds = RecoverTimeoutSeconds;
                    }

                    break;

                case PacerState.Recovering:
                    _recoverRemainingSeconds -= deltaTime;
                    if (_recoverRemainingSeconds <= 0.0 || gap <= alongside + RecoverExitToleranceMeters)
                    {
                        _recoverRemainingSeconds = 0.0;
                        EnterAlongside();
                        LastEvent = PacerEvent.RecoveredToAlongside;
                    }

                    break;

                case PacerState.Conceding:
                    _concedeRemainingSeconds -= deltaTime;
                    if (_concedeRemainingSeconds <= 0.0)
                    {
                        _concedeRemainingSeconds = 0.0;
                        EnterAlongside();
                        LastEvent = PacerEvent.Conceded;
                    }

                    break;

                default:
                    // Contested / Surging / Easing cannot be current under ride-along: mercy expiry and the
                    // first frame after a flag change both land here.
                    EnterAlongside();
                    LastEvent = PacerEvent.None;
                    break;
            }

            EffectiveGapTargetMeters = BaseGapTargetMeters;
            EffectiveBandMeters = BaseBandMeters;
        }

        /// <summary>
        /// Back to riding alongside. The attack timer deliberately keeps running: the interval is a clock,
        /// not a countdown that restarts every time something interesting happens.
        /// </summary>
        private void EnterAlongside()
        {
            State = PacerState.Alongside;
            _holdTimer = 0.0;
        }

        /// <summary>Seconds until the next attack: the interval, jittered by ±<see cref="PacerConfig.AttackJitterFraction"/>.</summary>
        private double NextAttackIntervalSeconds()
        {
            double interval = Math.Max(1.0, _config.AttackIntervalSeconds);
            double jitter = Math.Clamp(_config.AttackJitterFraction, 0.0, 0.5);
            if (jitter <= 0.0) return interval;

            return interval * (1.0 + (jitter * ((2.0 * _jitter.NextDouble()) - 1.0)));
        }

        /// <summary>
        /// The speed he rides at when he is not attacking or conceding: yours, with a graded correction
        /// toward the station he is holding. The lag is the point — it is what lets a hard effort take
        /// ground off him, while easing off can never push him up the road. It is spent as road rather than
        /// granted without limit (see <see cref="PacerConfig.AlongsideLagBudgetMeters"/>), so a hard effort
        /// takes a few metres off him and a standing start cannot strand him.
        /// </summary>
        private double RideAlongReferenceSpeed(double riderSpeed, double gap)
        {
            if (!_smoothedSpeedPrimed) return riderSpeed;

            // The lower of the two, on purpose: he never rides faster than you are going right now, so
            // slowing down can only ever bring him back to you.
            double lagged = Math.Min(riderSpeed, _smoothedRiderSpeedKph);
            double allowance = LagAllowance(gap);

            return riderSpeed + ((lagged - riderSpeed) * allowance);
        }

        /// <summary>
        /// How much of the alongside lag is still in force at this gap: 1 at or ahead of the station, 0 once
        /// the budget is spent. <see cref="RideAlongReferenceSpeed"/> blends the lag in with it, which is
        /// what turns "he is still on your old pace" from a levy on every acceleration into a bounded
        /// allowance — and keeps the drift from ever reaching the pass line, so a standing start cannot
        /// register as an overtake.
        /// </summary>
        private double LagAllowance(double gap)
        {
            double budget = Math.Max(1.0, _config.AlongsideLagBudgetMeters);
            double behindStation = _config.AlongsideGapMeters - gap;
            return Math.Clamp(1.0 - (behindStation / budget), 0.0, 1.0);
        }

        /// <summary>One-sided lag on your speed: he has to notice an acceleration before he answers it.</summary>
        private void UpdateSmoothedRiderSpeed(double riderSpeed, double deltaTime)
        {
            if (!_smoothedSpeedPrimed)
            {
                _smoothedRiderSpeedKph = riderSpeed;
                _smoothedSpeedPrimed = true;
                return;
            }

            double tau = Math.Max(0.5, _config.AlongsideResponseSeconds);
            double alpha = Math.Min(1.0, deltaTime / tau);
            _smoothedRiderSpeedKph += (riderSpeed - _smoothedRiderSpeedKph) * alpha;
        }

        /// <summary>
        /// Ride-along speed as a multiple of the reference speed. Attacks and the alongside hold both pull
        /// toward <paramref name="target"/>, but the alongside hold is ramped — over
        /// <see cref="PacerConfig.AlongsideElasticScaleMeters"/> ahead of the station and the shorter
        /// <see cref="PacerConfig.AlongsideCatchUpScaleMeters"/> behind it; recovering and conceding are
        /// explicit multipliers so those sliders do exactly what they say.
        /// </summary>
        private double RideAlongRelativeSpeed(double target, double gap)
        {
            if (State == PacerState.Recovering) return _config.RecoverRelativeSpeed;
            if (State == PacerState.Conceding) return _config.ConcedeRelativeSpeed;

            if (State == PacerState.Attacking)
            {
                // Going for it: the pull is on the attack station itself, so the push reads as acceleration
                // rather than as a slow asymptote.
                double attackTarget = Math.Max(1.0, target);
                return 1.0 + (_config.Elasticity * ((attackTarget - gap) / attackTarget));
            }

            // Two ramps: a long one ahead of the station (he must not rocket past you for a 5 m error) and a
            // short one behind it (a rider cannot see a rival who is off the back, so that one has to close
            // in seconds).
            double scale = gap < target
                ? Math.Max(1.0, _config.AlongsideCatchUpScaleMeters)
                : Math.Max(1.0, _config.AlongsideElasticScaleMeters);

            return 1.0 + (_config.Elasticity * ((target - gap) / scale));
        }

        /// <summary>Gap he rides at when nothing special is happening, on either side of the flag.</summary>
        private double BaseGapTargetMeters =>
            _config.RideAlongMode ? _config.AlongsideGapMeters : _config.GapTargetMeters;

        /// <summary>Hysteresis band in force when nothing special is happening, on either side of the flag.</summary>
        private double BaseBandMeters =>
            _config.RideAlongMode ? _config.AlongsideBandMeters : _config.BandMeters;

        private void UpdateMercy(double deltaTime)
        {
            if (_mercyRemainingSeconds > 0)
            {
                _mercyRemainingSeconds -= deltaTime;
                if (_mercyRemainingSeconds <= 0)
                {
                    _mercyRemainingSeconds = 0;
                    _fadeTimer = 0;
                    State = _config.RideAlongMode ? PacerState.Alongside : PacerState.Contested;
                    EffectiveGapTargetMeters = BaseGapTargetMeters;
                    EffectiveBandMeters = BaseBandMeters;

                    if (_config.RideAlongMode)
                    {
                        // A mercy period is a breather, not a launch pad: he does not attack the moment it lifts.
                        _attackTimer = 0;
                        _nextAttackSeconds = NextAttackIntervalSeconds();
                    }
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
                EffectiveBandMeters = BaseBandMeters * (1.0 + _config.MercyBandBonus);
                EffectiveGapTargetMeters = BaseGapTargetMeters * (1.0 - _config.MercyBandBonus);
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
