using System;
using System.Collections.Generic;

namespace BikeFitness.Shared
{
    /// <summary>
    /// Pure logic engine for the bike fitness simulation.
    /// Decoupled from any specific UI framework.
    /// </summary>
    public class SimulationEngine<TImage, TSprite>
    {
        // Simulation Parameters (Constants from SimulationCanvas)
        public const double PixelsPerMeter = 50.0;
        public const double BackgroundPixelsPerMeter = 5.0;
        public const double BiomeSegmentLengthMeters = 600.0;
        public const double TransitionSegmentLengthMeters = 140.0;
        public const double BackgroundFadeMeters = 25.0;
        public const double BackgroundTileOverlapPx = 1.0;
        public const bool UseMirroredBackgroundTiles = true;
        public const double BiomeFadeOutMeters = 40.0;
        public const double TransitionFadeInTailMeters = 40.0;
        public const double TransitionToBiomeBlendMeters = 40.0;
        public const double BiomeLabelDurationSeconds = 1.6;
        public const double BiomeLabelFadeSeconds = 0.35;
        public const double BiomeLabelTopRatio = 0.08;
        public const double TransitionParticleSpawnRate = 18.0;
        public const int TransitionParticleMaxCount = 70;
        public const double TransitionParticleMinLife = 0.6;
        public const double TransitionParticleMaxLife = 1.4;
        public const double TransitionParticleMinSize = 4.0;
        public const double TransitionParticleMaxSize = 12.0;
        public const double TransitionParticleMinSpeed = 10.0;
        public const double TransitionParticleMaxSpeed = 28.0;
        public const double TransitionParticleMinYRatio = 0.08;
        public const double TransitionParticleMaxYRatio = 0.55;

        public const double RoadsideSpawnAheadMeters = 180.0;
        public const double RoadsideDespawnBehindMeters = 40.0;
        public const double RoadsideMinSpacingMeters = 6.0;
        public const double RoadsideMaxSpacingMeters = 16.0;
        public const double ShrubGroundSinkFactor = 0.30;

        // State
        public double SpeedKph { get; set; }
        public double GradePercent { get; set; }
        public double TotalDistanceMeters { get; set; }
        public double CurrentSlopeAngle { get; private set; } // Degrees
        public double ActualWidth { get; set; }
        public double ActualHeight { get; set; }

        // Assets
        public TSprite? CyclistSprite { get; set; }
        public readonly List<TSprite> BushSprites = new List<TSprite>();

        // Background
        private readonly List<BackgroundSegment<TImage>> _backgroundSegments = new List<BackgroundSegment<TImage>>();
        private double _backgroundCycleLengthMeters;
        public double BackgroundFadeStartDistance { get; set; }

        // Terrain
        private readonly TerrainCalculator _terrainCalculator = new TerrainCalculator();

        // Roadside
        public enum RoadsideObjectType { Shrub, Tree, Rock }
        public struct RoadsideObject
        {
            public double Distance;
            public double SideOffsetMeters;
            public double HeightOffsetMeters;
            public double SizeMeters;
            public RoadsideObjectType Type;
            public int SpriteIndex;
        }
        private readonly List<RoadsideObject> _roadsideObjects = new List<RoadsideObject>();
        private readonly Random _rng = new Random();
        private double _nextRoadsideSpawnDistance = 0;

        // Transitions
        public struct TransitionParticle
        {
            public SimulationPoint Position;
            public SimulationVector Velocity;
            public double Life;
            public double MaxLife;
            public double Size;
            public BackgroundTheme Theme;
        }
        private readonly List<TransitionParticle> _transitionParticles = new List<TransitionParticle>();
        private double _transitionParticleSpawnAccumulator = 0.0;
        private double _transitionIntensity = 0.0;
        private BackgroundTheme _currentBiomeTheme = BackgroundTheme.Plain;
        private BackgroundTheme _lastAnnouncedBiome = BackgroundTheme.Plain;
        private BackgroundTheme _biomeLabelTheme = BackgroundTheme.Plain;
        private double _biomeLabelTimer = 0.0;
        private bool _wasInTransitionSegment = false;

        public struct BackgroundSegmentInfo
        {
            public BackgroundSegment<TImage>? Segment;
            public BackgroundSegment<TImage>? NextSegment;
            public BackgroundSegment<TImage>? PreviousSegment;
            public double BlendToNext;
            public double LocalDistance;
            public double SegmentLength;
        }

        // Exposed State for Rendering
        public IReadOnlyList<BackgroundSegment<TImage>> BackgroundSegments => _backgroundSegments;
        public IReadOnlyList<RoadsideObject> RoadsideObjects => _roadsideObjects;
        public IReadOnlyList<TransitionParticle> TransitionParticles => _transitionParticles;
        public BackgroundTheme CurrentBiomeTheme => _currentBiomeTheme;
        public BackgroundTheme BiomeLabelTheme => _biomeLabelTheme;
        public double BiomeLabelTimer => _biomeLabelTimer;
        public double TransitionIntensity => _transitionIntensity;
        public TerrainCalculator Terrain => _terrainCalculator;

        public SimulationEngine()
        {
        }

        public void Reset(double distance = 0)
        {
            TotalDistanceMeters = distance;
            BackgroundFadeStartDistance = distance;
            _nextRoadsideSpawnDistance = distance;
            _terrainCalculator.Reset(distance, 0, GradePercent);
            _roadsideObjects.Clear();
            _transitionParticles.Clear();
            ResetTransitionEffects();
        }

        public void AddBackgroundSegment(string name, BackgroundTheme theme, TImage image, double lengthMeters, bool mirrorTiles)
        {
            _backgroundSegments.Add(new BackgroundSegment<TImage>(name, theme, image, lengthMeters, mirrorTiles));
            _backgroundCycleLengthMeters += lengthMeters;
        }

        public void ClearBackgroundSegments()
        {
            _backgroundSegments.Clear();
            _backgroundCycleLengthMeters = 0;
        }

        public void Update(double deltaTime)
        {
            if (ActualWidth <= 0) return;

            double metersPerSecond = (SpeedKph * 1000.0) / 3600.0;
            TotalDistanceMeters += metersPerSecond * deltaTime;

            double currentGrade = _terrainCalculator.GetGradeAt(TotalDistanceMeters);
            CurrentSlopeAngle = Math.Atan(currentGrade / 100.0) * (180.0 / Math.PI);

            var bgInfo = GetBackgroundSegmentInfo(TotalDistanceMeters);
            UpdateTransitionEffects(deltaTime, bgInfo);
            UpdateRoadsideObjects();
        }

        public void RecordGradeChange(double newGrade)
        {
            GradePercent = newGrade;
            _terrainCalculator.RecordGradeChange(TotalDistanceMeters, newGrade);
        }

        private void ResetTransitionEffects()
        {
            _transitionParticles.Clear();
            _transitionParticleSpawnAccumulator = 0.0;
            _transitionIntensity = 0.0;
            _biomeLabelTimer = 0.0;

            var info = GetBackgroundSegmentInfo(TotalDistanceMeters);
            var theme = GetTransitionTargetTheme(info, BackgroundTheme.Plain);
            _currentBiomeTheme = theme;
            _lastAnnouncedBiome = theme;
            _biomeLabelTheme = theme;
            _wasInTransitionSegment = info.Segment?.Theme == BackgroundTheme.Transition;
        }

        private void UpdateTransitionEffects(double deltaTime, BackgroundSegmentInfo info)
        {
            UpdateBiomeLabel(deltaTime, info);
            UpdateTransitionParticles(deltaTime, info);
        }

        private void UpdateBiomeLabel(double deltaTime, BackgroundSegmentInfo info)
        {
            if (_biomeLabelTimer > 0)
            {
                _biomeLabelTimer = Math.Max(0, _biomeLabelTimer - deltaTime);
            }

            if (info.Segment == null)
            {
                _wasInTransitionSegment = false;
                return;
            }

            bool inTransition = info.Segment.Theme == BackgroundTheme.Transition;
            if (!inTransition)
            {
                _currentBiomeTheme = info.Segment.Theme;
            }

            if (TotalDistanceMeters - BackgroundFadeStartDistance <= BackgroundFadeMeters)
            {
                _wasInTransitionSegment = inTransition;
                return;
            }

            if (inTransition && !_wasInTransitionSegment)
            {
                var targetTheme = GetTransitionTargetTheme(info, _currentBiomeTheme);
                if (targetTheme != _lastAnnouncedBiome)
                {
                    _biomeLabelTheme = targetTheme;
                    _biomeLabelTimer = BiomeLabelDurationSeconds;
                    _lastAnnouncedBiome = targetTheme;
                }
            }

            _wasInTransitionSegment = inTransition;
        }

        private void UpdateTransitionParticles(double deltaTime, BackgroundSegmentInfo info)
        {
            for (int i = _transitionParticles.Count - 1; i >= 0; i--)
            {
                var particle = _transitionParticles[i];
                particle.Life -= deltaTime;
                if (particle.Life <= 0)
                {
                    _transitionParticles.RemoveAt(i);
                    continue;
                }

                particle.Position = new SimulationPoint(
                    particle.Position.X + (particle.Velocity.X * deltaTime),
                    particle.Position.Y + (particle.Velocity.Y * deltaTime));

                _transitionParticles[i] = particle;
            }

            if (TotalDistanceMeters - BackgroundFadeStartDistance <= BackgroundFadeMeters)
            {
                _transitionParticles.Clear();
                _transitionParticleSpawnAccumulator = 0.0;
                _transitionIntensity = 0.0;
                return;
            }

            _transitionIntensity = GetTransitionIntensity(info);
            if (_transitionIntensity <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                _transitionParticleSpawnAccumulator = 0.0;
                return;
            }

            double spawnRate = TransitionParticleSpawnRate * _transitionIntensity;
            _transitionParticleSpawnAccumulator += deltaTime * spawnRate;
            int spawnCount = (int)_transitionParticleSpawnAccumulator;
            if (spawnCount <= 0)
            {
                return;
            }

            _transitionParticleSpawnAccumulator -= spawnCount;
            var targetTheme = GetTransitionTargetTheme(info, _currentBiomeTheme);

            for (int i = 0; i < spawnCount && _transitionParticles.Count < TransitionParticleMaxCount; i++)
            {
                double x = _rng.NextDouble() * ActualWidth;
                double y = (TransitionParticleMinYRatio + (_rng.NextDouble() * (TransitionParticleMaxYRatio - TransitionParticleMinYRatio))) * ActualHeight;
                double size = TransitionParticleMinSize + (_rng.NextDouble() * (TransitionParticleMaxSize - TransitionParticleMinSize));
                double life = TransitionParticleMinLife + (_rng.NextDouble() * (TransitionParticleMaxLife - TransitionParticleMinLife));
                double speed = TransitionParticleMinSpeed + (_rng.NextDouble() * (TransitionParticleMaxSpeed - TransitionParticleMinSpeed));
                double drift = (_rng.NextDouble() - 0.5) * 6.0;

                var particle = new TransitionParticle
                {
                    Position = new SimulationPoint(x, y),
                    Velocity = new SimulationVector(-speed, drift),
                    Life = life,
                    MaxLife = life,
                    Size = size,
                    Theme = targetTheme
                };

                _transitionParticles.Add(particle);
            }
        }

        public BackgroundSegmentInfo GetBackgroundSegmentInfo(double distanceMeters)
        {
            if (_backgroundSegments.Count == 0 || _backgroundCycleLengthMeters <= 0)
            {
                return new BackgroundSegmentInfo();
            }

            double cycleDistance = distanceMeters % _backgroundCycleLengthMeters;
            if (cycleDistance < 0)
            {
                cycleDistance += _backgroundCycleLengthMeters;
            }

            double cursor = 0;
            for (int i = 0; i < _backgroundSegments.Count; i++)
            {
                var segment = _backgroundSegments[i];
                double nextCursor = cursor + segment.LengthMeters;
                if (cycleDistance <= nextCursor)
                {
                    double local = cycleDistance - cursor;

                    var nextSegment = _backgroundSegments[(i + 1) % _backgroundSegments.Count];
                    var previousSegment = _backgroundSegments[(i - 1 + _backgroundSegments.Count) % _backgroundSegments.Count];
                    return new BackgroundSegmentInfo
                    {
                        Segment = segment,
                        NextSegment = nextSegment,
                        PreviousSegment = previousSegment,
                        BlendToNext = 0,
                        LocalDistance = local,
                        SegmentLength = segment.LengthMeters
                    };
                }

                cursor = nextCursor;
            }

            return new BackgroundSegmentInfo
            {
                Segment = _backgroundSegments[0],
                NextSegment = _backgroundSegments.Count > 1 ? _backgroundSegments[1] : _backgroundSegments[0],
                PreviousSegment = _backgroundSegments.Count > 1 ? _backgroundSegments[_backgroundSegments.Count - 1] : _backgroundSegments[0],
                BlendToNext = 0,
                LocalDistance = 0,
                SegmentLength = _backgroundSegments[0].LengthMeters
            };
        }

        public double GetTransitionIntensity(BackgroundSegmentInfo info)
        {
            if (info.Segment == null)
            {
                return 0.0;
            }

            if (info.Segment.Theme == BackgroundTheme.Transition)
            {
                if (info.SegmentLength <= 0)
                {
                    return 0.0;
                }

                double progress = SimulationMath.Clamp01(info.LocalDistance / info.SegmentLength);
                return 0.4 + (0.6 * (1.0 - SimulationMath.SmoothStep(progress)));
            }

            if (info.PreviousSegment?.Theme == BackgroundTheme.Transition)
            {
                double tailMeters = Math.Max(1.0, TransitionFadeInTailMeters);
                double tailProgress = SimulationMath.Clamp01(info.LocalDistance / tailMeters);
                return 0.4 * (1.0 - SimulationMath.SmoothStep(tailProgress));
            }

            if (info.NextSegment?.Theme == BackgroundTheme.Transition && BiomeFadeOutMeters > 0)
            {
                double fadeOutMeters = Math.Min(BiomeFadeOutMeters, info.SegmentLength);
                if (fadeOutMeters <= 0)
                {
                    return 0.0;
                }

                double distanceToEnd = info.SegmentLength - info.LocalDistance;
                double progress = SimulationMath.Clamp01(1.0 - (distanceToEnd / fadeOutMeters));
                return SimulationMath.SmoothStep(progress);
            }

            return 0.0;
        }

        public BackgroundTheme GetTransitionTargetTheme(BackgroundSegmentInfo info, BackgroundTheme fallback)
        {
            if (info.Segment == null)
            {
                return fallback;
            }

            if (info.Segment.Theme == BackgroundTheme.Transition)
            {
                if (info.NextSegment != null && info.NextSegment.Theme != BackgroundTheme.Transition)
                {
                    return info.NextSegment.Theme;
                }

                return fallback;
            }

            return info.Segment.Theme;
        }

        private void UpdateRoadsideObjects()
        {
            _roadsideObjects.RemoveAll(o => o.Type == RoadsideObjectType.Rock);

            double spawnTarget = TotalDistanceMeters + RoadsideSpawnAheadMeters;
            if (_nextRoadsideSpawnDistance < TotalDistanceMeters)
            {
                _nextRoadsideSpawnDistance = TotalDistanceMeters;
            }

            while (_nextRoadsideSpawnDistance <= spawnTarget)
            {
                var type = PickRoadsideType();
                double sideOffsetMeters = (_rng.NextDouble() < 0.5 ? -1 : 1) * (0.5 + _rng.NextDouble() * 1.5);
                double heightOffsetMeters = 0.2 + _rng.NextDouble() * 0.6;
                double sizeMeters = 0.4 + _rng.NextDouble() * 0.9;

                if (type == RoadsideObjectType.Shrub)
                {
                    sideOffsetMeters = (_rng.NextDouble() - 0.5) * 0.2;
                    heightOffsetMeters = 0.0;
                    sizeMeters = 0.35 + _rng.NextDouble() * 0.5;
                }
                else if (type == RoadsideObjectType.Tree)
                {
                    heightOffsetMeters = 0.0;
                }

                var obj = new RoadsideObject
                {
                    Distance = _nextRoadsideSpawnDistance,
                    SideOffsetMeters = sideOffsetMeters,
                    HeightOffsetMeters = heightOffsetMeters,
                    SizeMeters = sizeMeters,
                    Type = type,
                    SpriteIndex = BushSprites.Count > 0 ? _rng.Next(BushSprites.Count) : 0
                };

                _roadsideObjects.Add(obj);
                _nextRoadsideSpawnDistance += RoadsideMinSpacingMeters + (_rng.NextDouble() * (RoadsideMaxSpacingMeters - RoadsideMinSpacingMeters));
            }

            double despawnCutoff = TotalDistanceMeters - RoadsideDespawnBehindMeters;
            _roadsideObjects.RemoveAll(o => o.Distance < despawnCutoff);
        }

        private RoadsideObjectType PickRoadsideType()
        {
            double roll = _rng.NextDouble();
            if (roll < 0.75)
            {
                return RoadsideObjectType.Shrub;
            }
            return RoadsideObjectType.Tree;
        }

        public double GetBackgroundOpacity(BackgroundSegmentInfo info)
        {
            if (info.Segment == null || info.SegmentLength <= 0 || BackgroundFadeMeters <= 0)
            {
                return 1.0;
            }

            if (TotalDistanceMeters - BackgroundFadeStartDistance <= BackgroundFadeMeters)
            {
                return 1.0;
            }

            bool isTransition = info.Segment.Theme == BackgroundTheme.Transition;
            bool fadeOut = !isTransition && info.NextSegment?.Theme == BackgroundTheme.Transition;
            bool fadeIn = isTransition || info.PreviousSegment?.Theme == BackgroundTheme.Transition;

            if (!fadeIn && !fadeOut)
            {
                return 1.0;
            }

            double opacity = 1.0;

            if (fadeIn)
            {
                double transitionLength = info.SegmentLength;
                double distanceSinceTransitionStart = info.LocalDistance;

                if (!isTransition && info.PreviousSegment != null)
                {
                    transitionLength = info.PreviousSegment.LengthMeters;
                    distanceSinceTransitionStart = transitionLength + info.LocalDistance;
                }

                double fadeInMeters = Math.Max(1.0, transitionLength + TransitionFadeInTailMeters);
                opacity = Math.Min(opacity, SimulationMath.Clamp01(distanceSinceTransitionStart / fadeInMeters));
            }

            if (fadeOut)
            {
                double fadeOutMeters = Math.Max(1.0, Math.Min(BiomeFadeOutMeters, info.SegmentLength));
                opacity = Math.Min(opacity, SimulationMath.Clamp01((info.SegmentLength - info.LocalDistance) / fadeOutMeters));
            }

            return opacity;
        }
    }
}
