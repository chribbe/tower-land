using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Levels.Generation
{
    [CreateAssetMenu(menuName = "TowerLand/Patterns/Voronoi", fileName = "VoronoiPattern")]
    public sealed class VoronoiPatternGenerator : TowerPatternGenerator
    {
        [SerializeField] private VoronoiSettings _settings = new VoronoiSettings();

        public VoronoiSettings Settings => _settings;

        public override TowerGrid Generate(int columns, int floors, int colourCount, System.Random rng)
            => VoronoiPattern.Generate(columns, floors, colourCount, _settings, rng).Grid;
    }
}
