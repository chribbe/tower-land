using UnityEngine;

namespace TowerLand.Tower
{
    /// <summary>Cube colours, indexed by the colour values stored in a <see cref="TowerGrid"/>.</summary>
    [CreateAssetMenu(menuName = "TowerLand/Cube Palette", fileName = "CubePalette")]
    public sealed class CubePalette : ScriptableObject
    {
        [SerializeField] private Color[] _colours =
        {
            new Color(0.10f, 0.52f, 1.00f), // blue
            new Color(1.00f, 0.45f, 0.06f), // orange
            new Color(1.00f, 0.80f, 0.06f), // yellow
            new Color(0.64f, 0.24f, 0.95f), // purple
            new Color(1.00f, 0.20f, 0.33f), // red
            new Color(0.30f, 0.84f, 0.22f), // green
            new Color(0.15f, 0.85f, 0.90f), // cyan
            new Color(1.00f, 0.42f, 0.78f), // pink
        };

        public int Count => _colours.Length;
        public Color this[int index] => _colours[index];
    }
}
