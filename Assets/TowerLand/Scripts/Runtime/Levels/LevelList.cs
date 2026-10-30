using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Levels
{
    /// <summary>The game's levels, in play order (level 1 first).</summary>
    [CreateAssetMenu(menuName = "TowerLand/Level List", fileName = "LevelList")]
    public sealed class LevelList : ScriptableObject
    {
        [SerializeField] private LevelConfig[] _levels = System.Array.Empty<LevelConfig>();

        public IReadOnlyList<LevelConfig> Levels => _levels;
        public int Count => _levels.Length;

        /// <summary>1-based level number of <paramref name="level"/>, or 0 when it isn't in the list.</summary>
        public int NumberOf(LevelConfig level) => System.Array.IndexOf(_levels, level) + 1;
    }
}
