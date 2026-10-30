using TowerLand.Levels.Generation;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Levels
{
    /// <summary>Everything needed to deterministically generate one level's tower.</summary>
    [CreateAssetMenu(menuName = "TowerLand/Level Config", fileName = "LevelConfig")]
    public sealed class LevelConfig : ScriptableObject
    {
        [SerializeField, Min(3)] private int _columns = 24;
        [SerializeField, Min(1)] private int _floors = 14;
        [Tooltip("Concentric cube cylinders. Inner layers lose ~6 columns each; layers that would get too small are skipped.")]
        [SerializeField, Range(1, 4)] private int _layers = 1;
        [SerializeField, Range(1, 8)] private int _colourCount = 5;
        [SerializeField] private int _seed = 1;
        [SerializeField] private TowerPatternGenerator _pattern;
        [Tooltip("Preview of the level's tower (seed above) for level lists; baked by Tools/TowerLand/Build Level List.")]
        [SerializeField] private Texture2D _thumbnail;

        public int Columns => _columns;
        public int Floors => _floors;
        public int Layers => TowerLayout.FittingLayers(_columns, _layers);
        public int ColourCount => _colourCount;
        public int Seed => _seed;
        public TowerPatternGenerator Pattern => _pattern;
        public Texture2D Thumbnail => _thumbnail;

        public TowerGrid Generate() => Generate(_seed);

        /// <summary>All layers, outermost first. Each layer gets its own pattern from a seed derived from <paramref name="seed"/>.</summary>
        public TowerGrid[] GenerateLayers(int seed)
        {
            var layers = new TowerGrid[Layers];
            for (int i = 0; i < layers.Length; i++)
                layers[i] = Generate(TowerLayout.ColumnsForLayer(_columns, i), unchecked(seed + i * 7919));
            return layers;
        }

        public TowerGrid Generate(int seed) => Generate(_columns, seed);

        private TowerGrid Generate(int columns, int seed)
        {
            if (_pattern == null)
            {
                Debug.LogError($"[TowerLand] {name} has no pattern generator.", this);
                return new TowerGrid(columns, _floors);
            }

            return _pattern.Generate(columns, _floors, _colourCount, new System.Random(seed));
        }
    }
}
