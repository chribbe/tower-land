using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Levels.Generation
{
    /// <summary>
    /// A tower colour-pattern algorithm with its tuning. Each generator type is its own asset so levels can
    /// mix and match patterns (Voronoi today; stripes, spirals, rings... later).
    /// </summary>
    public abstract class TowerPatternGenerator : ScriptableObject
    {
        public abstract TowerGrid Generate(int columns, int floors, int colourCount, System.Random rng);
    }
}
