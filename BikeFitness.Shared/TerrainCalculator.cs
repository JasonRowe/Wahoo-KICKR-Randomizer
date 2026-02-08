using System.Collections.Generic;

namespace BikeFitness.Shared
{
    /// <summary>
    /// Represents a point in terrain history with distance, height, and outgoing grade.
    /// </summary>
    public struct TerrainVertex
    {
        public double Distance;
        public double Height;
        public double GradeOut;
    }

    /// <summary>
    /// Calculates terrain height and grade from a list of terrain vertices.
    /// Extracted from SimulationCanvas for testability.
    /// </summary>
    public class TerrainCalculator
    {
        private readonly List<TerrainVertex> _terrainHistory;

        public TerrainCalculator()
        {
            _terrainHistory = new List<TerrainVertex>
            {
                new TerrainVertex { Distance = 0, Height = 0, GradeOut = 0 }
            };
        }

        public TerrainCalculator(List<TerrainVertex> history)
        {
            _terrainHistory = history ?? new List<TerrainVertex>();
            if (_terrainHistory.Count == 0)
            {
                _terrainHistory.Add(new TerrainVertex { Distance = 0, Height = 0, GradeOut = 0 });
            }
        }

        /// <summary>
        /// Gets the terrain height at the specified distance.
        /// </summary>
        public double GetHeightAt(double distance)
        {
            for (int i = _terrainHistory.Count - 1; i >= 0; i--)
            {
                var v = _terrainHistory[i];
                if (distance >= v.Distance)
                {
                    double d = distance - v.Distance;
                    return v.Height + (d * (v.GradeOut / 100.0));
                }
            }
            return 0;
        }

        /// <summary>
        /// Gets the grade at the specified distance.
        /// </summary>
        public double GetGradeAt(double distance)
        {
            for (int i = _terrainHistory.Count - 1; i >= 0; i--)
            {
                var v = _terrainHistory[i];
                if (distance >= v.Distance)
                {
                    return v.GradeOut;
                }
            }
            return 0;
        }

        /// <summary>
        /// Records a grade change at the specified distance.
        /// </summary>
        public void RecordGradeChange(double totalDistanceMeters, double newGrade)
        {
            var last = _terrainHistory[_terrainHistory.Count - 1];
            if (totalDistanceMeters <= last.Distance + 0.1)
            {
                _terrainHistory[_terrainHistory.Count - 1] = new TerrainVertex
                {
                    Distance = last.Distance,
                    Height = last.Height,
                    GradeOut = newGrade
                };
                return;
            }

            double distTraveled = totalDistanceMeters - last.Distance;
            double heightChange = distTraveled * (last.GradeOut / 100.0);
            double newHeight = last.Height + heightChange;

            _terrainHistory.Add(new TerrainVertex
            {
                Distance = totalDistanceMeters,
                Height = newHeight,
                GradeOut = newGrade
            });
        }

        /// <summary>
        /// Clears the terrain history and optionally initializes with a starting point.
        /// </summary>
        public void Reset(double startDistance = 0, double startHeight = 0, double startGrade = 0)
        {
            _terrainHistory.Clear();
            _terrainHistory.Add(new TerrainVertex
            {
                Distance = startDistance,
                Height = startHeight,
                GradeOut = startGrade
            });
        }

        /// <summary>
        /// Returns the terrain history for iteration (e.g., for rendering).
        /// </summary>
        public IReadOnlyList<TerrainVertex> History => _terrainHistory;
    }
}
