using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Meta
{
    /// <summary>The constructions in build order (each takes more levels than the last). Past the end, the last one repeats.</summary>
    [CreateAssetMenu(menuName = "TowerLand/Construction List", fileName = "ConstructionList")]
    public sealed class ConstructionList : ScriptableObject
    {
        [SerializeField] private TowerBlueprint[] _constructions = System.Array.Empty<TowerBlueprint>();

        public IReadOnlyList<TowerBlueprint> Constructions => _constructions;

        public TowerBlueprint For(int index) =>
            _constructions.Length == 0 ? null : _constructions[Mathf.Clamp(index, 0, _constructions.Length - 1)];
    }
}
