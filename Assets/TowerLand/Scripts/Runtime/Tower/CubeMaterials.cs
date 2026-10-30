using UnityEngine;

namespace TowerLand.Tower
{
    /// <summary>Per-colour/state overrides of the shared cube material (<c>Cube.mat</c>, TowerLand/Candy Cube).</summary>
    public static class CubeMaterials
    {
        /// <summary>Tower cube (covered 0..1: darkened behind an outer layer), highlighted (covered 2) or loose (covered -1: no seam/form shading).</summary>
        public static void ApplyVariant(Material material, Color colour, float covered)
        {
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Covered", covered > 1.5f ? 0f : Mathf.Max(0f, covered));
            if (covered > 1.5f)   // highlighted
                material.SetColor("_BaseColor", Color.Lerp(colour, Color.white, 0.28f));
            if (covered < 0f)   // loose cube
            {
                material.SetFloat("_CreviceStrength", 0f);
                material.SetFloat("_FormShadowStrength", 0f);
                material.SetFloat("_SideOutline", 0f);
            }
        }

        /// <summary>A runtime loose-cube material per palette colour (never saved).</summary>
        public static Material[] CreateLoose(Material source, CubePalette palette)
        {
            var materials = new Material[palette.Count];
            for (int i = 0; i < palette.Count; i++)
            {
                materials[i] = new Material(source) { name = $"{source.name} [{i} loose]", hideFlags = HideFlags.DontSave, enableInstancing = true };
                ApplyVariant(materials[i], palette[i], -1f);
            }
            return materials;
        }
    }
}
