using System;
using System.Collections.Generic;

namespace BikeFitness.Shared.Scoring
{
    /// <summary>
    /// Tuning for the combo/XP layer (POC #3). The spec pinned band/hysteresis/target; the rest live here so
    /// every rule is a number a test can pin and the harness can turn into a slider.
    /// </summary>
    public sealed class ZoneConfig
    {
        /// <summary>Half-width of the target-effort zone, in watts.</summary>
        public double BandWatts = 30.0;

        /// <summary>Schmitt-trigger width on both edges, in watts. Without this the combo resets constantly.</summary>
        public double HysteresisWatts = 8.0;

        /// <summary>Target effort (100 %, FTP-equivalent in the POC), in watts.</summary>
        public double TargetWatts = 200.0;

        /// <summary>Base accrual while in zone, points per second before multiplier and bonuses.</summary>
        public double PointsPerSecond = 10.0;

        /// <summary>Grace after leaving the zone before the combo actually breaks.</summary>
        public double BreakGraceSeconds = 3.0;

        /// <summary>Grade at or above which accrual pays the climb bonus.</summary>
        public double ClimbGradePercent = 3.0;

        /// <summary>Climb bonus as a fraction of accrual (+50 %).</summary>
        public double ClimbBonusFactor = 0.50;

        /// <summary>Sprint threshold as a multiple of the target watts.</summary>
        public double SprintFactor = 1.35;

        /// <summary>Seconds above the sprint threshold before the bonus pays.</summary>
        public double SprintSeconds = 5.0;

        /// <summary>Sprint bonus, points.</summary>
        public double SprintBonusPoints = 300.0;

        /// <summary>Cooldown after a sprint bonus before another can be earned.</summary>
        public double SprintCooldownSeconds = 30.0;

        /// <summary>Score milestone that emits an accrual popup ("+50 ZONE").</summary>
        public double PopupMilestonePoints = 50.0;

        /// <summary>Minimum interval between released popups — enforced here, not in the UI, so it is testable.</summary>
        public double PopupMinIntervalSeconds = 0.8;

        /// <summary>Maximum popups on screen at once.</summary>
        public int MaxAlivePopups = 3;

        /// <summary>How long a released popup stays on screen.</summary>
        public double PopupLifetimeSeconds = 0.9;
    }

    /// <summary>
    /// Combo tier. Values are the tier order (Tier2 = "×1.5"); use
    /// <see cref="ComboScoring.MultiplierFor"/> for the actual multiplier.
    /// </summary>
    public enum ComboTier
    {
        Base = 1,
        Tier2 = 2,
        Tier3 = 3,
        Tier4 = 4,
    }

    /// <summary>
    /// One floating popup / score event. <c>Kind</c> is a stable label: ZONE, CLIMB, SPRINT, COMBO or LOST.
    /// </summary>
    public sealed record ScoreEvent(string Kind, double Amount, string Label);

    /// <summary>
    /// In-ride scoring: hold the target power zone and the combo multiplier climbs; break it and the combo
    /// drops one tier. Pure and time-injected — <see cref="Advance"/> is the only clock.
    /// <para>
    /// <b>Effort arrives as pseudo-power.</b> The harness has no power meter, so callers pass watts derived
    /// from speed + grade by <c>RiderPowerModel</c>; anything shown to a human must carry a <c>≈</c>.
    /// </para>
    /// <para>
    /// The popup rate limit lives in here, not in the UI, so "at most one popup per 800 ms, at most three
    /// alive" is a unit test instead of a visual impression.
    /// </para>
    /// </summary>
    public sealed class ComboEngine
    {
        private readonly ZoneConfig _config;
        private readonly List<ScoreEvent> _pending = new List<ScoreEvent>();
        private readonly List<ScoreEvent> _released = new List<ScoreEvent>();
        private readonly List<ScoreEvent> _sessionEvents = new List<ScoreEvent>();
        private readonly Queue<double> _alivePopups = new Queue<double>();

        /// <summary>Frames of event log kept per session (the POC's score card is derived from this log).</summary>
        public const int MaxSessionEvents = 4000;

        private double _rawSinceMilestone;
        private double _scoreSinceMilestone;
        private double _outOfZoneSeconds;
        private double _sprintHighSeconds;
        private double _sprintCooldownSeconds;
        private double _popupIntervalSeconds;
        private double _sessionSeconds;
        private double _zoneSeconds;
        private double _bestComboSeconds;
        private ComboTier _tier = ComboTier.Base;
        private ComboTier _retainedTier = ComboTier.Base;

        public ComboEngine(ZoneConfig? config = null)
        {
            _config = config ?? new ZoneConfig();
            Reset();
        }

        public ZoneConfig Config => _config;

        public double Score { get; private set; }

        public bool InZone { get; private set; }

        /// <summary>True once the combo has been broken this session (latched until <see cref="Reset"/>).</summary>
        public bool ComboBroken { get; private set; }

        /// <summary>Seconds of continuous in-zone time; resets when the combo breaks.</summary>
        public double ComboSeconds { get; private set; }

        public ComboTier Tier => _tier;

        public double Multiplier => ComboScoring.MultiplierFor(_tier);

        public int CombosLost { get; private set; }

        public int AlivePopupCount { get; private set; }

        public double SessionSeconds => _sessionSeconds;

        public double TimeInZoneSeconds => _zoneSeconds;

        public double BestComboSeconds => _bestComboSeconds;

        /// <summary>XP earned this session: <c>floor(score / 100)</c>. Never decreases, because score never does.</summary>
        public double XpEarned => Math.Floor(Score / 100.0);

        public void Reset()
        {
            Score = 0;
            InZone = false;
            ComboBroken = false;
            ComboSeconds = 0;
            CombosLost = 0;
            AlivePopupCount = 0;
            _tier = ComboTier.Base;
            _retainedTier = ComboTier.Base;
            _rawSinceMilestone = 0;
            _scoreSinceMilestone = 0;
            _outOfZoneSeconds = 0;
            _sprintHighSeconds = 0;
            _sprintCooldownSeconds = 0;
            _popupIntervalSeconds = 0;
            _sessionSeconds = 0;
            _zoneSeconds = 0;
            _bestComboSeconds = 0;
            _pending.Clear();
            _released.Clear();
            _alivePopups.Clear();
            _sessionEvents.Clear();
        }

        /// <summary>
        /// One frame. <paramref name="pseudoWatts"/> is approximate power; <paramref name="sprintFlag"/> lets
        /// the caller declare a sprint, since the harness has no measured power to infer one from.
        /// </summary>
        public void Advance(double deltaTime, double pseudoWatts, double gradePercent = 0.0, bool sprintFlag = false)
        {
            if (!double.IsFinite(deltaTime) || deltaTime <= 0) return;

            double dt = Math.Min(deltaTime, 2.0);
            double watts = double.IsFinite(pseudoWatts) ? Math.Max(0.0, pseudoWatts) : 0.0;
            double grade = double.IsFinite(gradePercent) ? gradePercent : 0.0;

            _sessionSeconds += dt;
            UpdatePopups(dt);
            UpdateZone(dt, watts, grade);
            UpdateSprint(dt, watts, sprintFlag);
            ReleasePendingEvents();
        }

        /// <summary>
        /// Returns the popups that may be shown right now and clears them. Rate limiting has already been
        /// applied, so a caller never sees more than the configured interval or alive count.
        /// </summary>
        public IReadOnlyList<ScoreEvent> DrainEvents()
        {
            if (_released.Count == 0) return Array.Empty<ScoreEvent>();

            var drained = new List<ScoreEvent>(_released);
            _released.Clear();
            return drained;
        }

        /// <summary>Immutable snapshot for the end-of-ride score card.</summary>
        public ComboSession Snapshot(string workoutMode = "")
        {
            var events = new List<ScoreEvent>(_sessionEvents);

            // Whatever has been banked but not yet shown as a popup still counts, so the card reconciles.
            if (_scoreSinceMilestone > 1e-9)
            {
                events.Add(new ScoreEvent("ZONE", _scoreSinceMilestone, $"+{_scoreSinceMilestone:F0} ZONE"));
            }

            return new ComboSession
            {
                WorkoutMode = workoutMode,
                Score = Score,
                BestComboSeconds = _bestComboSeconds,
                TimeInZoneSeconds = _zoneSeconds,
                DurationSeconds = _sessionSeconds,
                CombosLost = CombosLost,
                XpEarned = XpEarned,
                Events = events,
            };
        }

        private void UpdateZone(double deltaTime, double watts, double gradePercent)
        {
            double target = _config.TargetWatts;
            double enterHigh = target + _config.BandWatts;
            double enterLow = Math.Max(0.0, target - _config.BandWatts);
            double exitHigh = enterHigh + _config.HysteresisWatts;
            double exitLow = Math.Max(0.0, enterLow - _config.HysteresisWatts);

            // Schmitt trigger on both edges: a rider sitting exactly on the boundary must not flicker in and
            // out of the zone — the single most likely "this feels broken" failure in the whole POC.
            bool inZone = InZone
                ? watts <= exitHigh && watts >= exitLow
                : watts <= enterHigh && watts >= enterLow;

            if (!inZone)
            {
                if (InZone)
                {
                    InZone = false;
                    _outOfZoneSeconds = 0;
                }

                _outOfZoneSeconds += deltaTime;

                // Only a live combo can break, so a rider parked outside the zone doesn't spam COMBO LOST.
                if (ComboSeconds > 0 && _outOfZoneSeconds >= _config.BreakGraceSeconds)
                {
                    BreakCombo();
                }

                return;
            }

            InZone = true;
            _outOfZoneSeconds = 0;
            ComboSeconds += deltaTime;
            _zoneSeconds += deltaTime;
            if (ComboSeconds > _bestComboSeconds) _bestComboSeconds = ComboSeconds;

            RecomputeTier();

            bool climbing = _config.ClimbBonusFactor > 0 && gradePercent >= _config.ClimbGradePercent;
            string kind = climbing ? "CLIMB" : "ZONE";
            double climbFactor = climbing ? 1.0 + _config.ClimbBonusFactor : 1.0;
            double points = _config.PointsPerSecond * Multiplier * climbFactor * deltaTime;

            Score += points;
            _scoreSinceMilestone += points;
            _rawSinceMilestone += _config.PointsPerSecond * deltaTime;

            if (_config.PopupMilestonePoints <= 0) return;

            // The popup carries the points actually banked since the last one (not a flat 50), so summing the
            // session's events reproduces the score exactly — which is what the score card relies on.
            while (_rawSinceMilestone >= _config.PopupMilestonePoints)
            {
                _rawSinceMilestone -= _config.PopupMilestonePoints;

                double amount = _scoreSinceMilestone;
                _scoreSinceMilestone = 0;

                Enqueue(new ScoreEvent(kind, amount, $"+{amount:F0} {kind}"));
            }
        }

        private void UpdateSprint(double deltaTime, double watts, bool sprintFlag)
        {
            if (_sprintCooldownSeconds > 0) _sprintCooldownSeconds = Math.Max(0, _sprintCooldownSeconds - deltaTime);

            bool sprinting = sprintFlag || watts > _config.TargetWatts * _config.SprintFactor;
            _sprintHighSeconds = sprinting ? _sprintHighSeconds + deltaTime : 0;

            if (_sprintHighSeconds < _config.SprintSeconds) return;
            if (_sprintCooldownSeconds > 0) return;

            Score += _config.SprintBonusPoints;
            _sprintCooldownSeconds = _config.SprintCooldownSeconds;
            Enqueue(new ScoreEvent("SPRINT", _config.SprintBonusPoints, $"+{_config.SprintBonusPoints:F0} SPRINT"));
        }

        private void BreakCombo()
        {
            ComboBroken = true;
            CombosLost++;
            ComboSeconds = 0;
            _outOfZoneSeconds = 0;

            // Bank whatever accrual has not been turned into a popup yet, otherwise those points would exist
            // in the score but not in the event log the score card is built from.
            FlushAccrualRemainder();

            // Drop ONE tier rather than back to base: harsh enough to sting, forgiving enough not to be a
            // rage-quit. The tier can only climb back through the documented time thresholds.
            _retainedTier = ComboScoring.OneTierDown(_tier);
            _tier = _retainedTier;

            Enqueue(new ScoreEvent("LOST", 0, "COMBO LOST"));
        }

        private void RecomputeTier()
        {
            ComboTier byTime = ComboScoring.TierFor(ComboSeconds);
            ComboTier next = byTime > _retainedTier ? byTime : _retainedTier;
            if (next == _tier) return;

            _tier = next;
            if (_tier > ComboTier.Base)
            {
                Enqueue(new ScoreEvent("COMBO", 0, $"x{ComboScoring.MultiplierFor(_tier):0.#} COMBO"));
            }
        }

        private void Enqueue(ScoreEvent scoreEvent)
        {
            _pending.Add(scoreEvent);

            if (_sessionEvents.Count < MaxSessionEvents) _sessionEvents.Add(scoreEvent);
        }

        /// <summary>
        /// Turns whatever accrual has not yet become a popup into one final ZONE event, so the sum of the
        /// session's events always equals the score exactly (the score card is built from that log).
        /// </summary>
        private void FlushAccrualRemainder()
        {
            if (_scoreSinceMilestone > 1e-9)
            {
                Enqueue(new ScoreEvent("ZONE", _scoreSinceMilestone, $"+{_scoreSinceMilestone:F0} ZONE"));
            }

            _rawSinceMilestone = 0;
            _scoreSinceMilestone = 0;
        }

        private void UpdatePopups(double deltaTime)
        {
            if (_popupIntervalSeconds > 0) _popupIntervalSeconds = Math.Max(0, _popupIntervalSeconds - deltaTime);

            int alive = _alivePopups.Count;
            for (int i = 0; i < alive; i++)
            {
                double remaining = _alivePopups.Dequeue() - deltaTime;
                if (remaining > 0) _alivePopups.Enqueue(remaining);
            }

            AlivePopupCount = _alivePopups.Count;
        }

        private void ReleasePendingEvents()
        {
            while (_pending.Count > 0
                && _popupIntervalSeconds <= 0
                && _alivePopups.Count < Math.Max(1, _config.MaxAlivePopups))
            {
                ScoreEvent next = _pending[0];
                _pending.RemoveAt(0);

                _released.Add(next);
                _alivePopups.Enqueue(_config.PopupLifetimeSeconds);
                AlivePopupCount = _alivePopups.Count;
                _popupIntervalSeconds = Math.Max(0, _config.PopupMinIntervalSeconds);
            }
        }
    }
}
