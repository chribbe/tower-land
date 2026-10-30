using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Meta
{
    /// <summary>A construction the player builds in the build scene from the bricks won in levels.</summary>
    [CreateAssetMenu(menuName = "TowerLand/Tower Blueprint", fileName = "TowerBlueprint")]
    public sealed class TowerBlueprint : ScriptableObject
    {
        [SerializeField] private string _title = "Tower";
        [SerializeField] private TowerShape _shape = new TowerShape();

        [System.NonSerialized] private List<BrickSlot> _slots;

        public string Title => _title;
        public TowerShape Shape => _shape;
        public IReadOnlyList<BrickSlot> Slots => _slots ??= _shape.Generate();
        public int Size => Slots.Count;

        private void OnValidate() => _slots = null;

        /// <summary>Editor setup (Tools/TowerLand/Build Meta Scene).</summary>
        public void Configure(string title, TowerShape shape)
        {
            _title = title;
            _shape = shape;
            _slots = null;
        }
    }
}
