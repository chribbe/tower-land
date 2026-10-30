using System;
using System.Collections.Generic;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Meta
{
    /// <summary>
    /// Order from chaos: a won level's cubes are sorted into colour bands before they are spat onto the construction, so it
    /// grows in layers like coloured sand in a bottle. Bands follow the rainbow (red -> orange -> ... -> pink); each haul
    /// runs the way that starts nearest to the colour on top already, so consecutive hauls ping-pong into soft gradients
    /// and a shared colour carries on as one thicker layer.
    /// </summary>
    public static class BrickSorting
    {
        private const float RedFirst = 0.05f;   // hue shift so red (hue ~0.98) starts the rainbow and pink ends it

        /// <summary>
        /// <paramref name="bricks"/> grouped by colour, groups in <paramref name="order"/> (ascending key per colour) or
        /// reversed - whichever starts closer to <paramref name="lastColour"/> (the top brick so far; -1 = none).
        /// </summary>
        public static List<int> Bands(IEnumerable<int> bricks, Func<int, float> order, int lastColour)
        {
            var counts = new SortedDictionary<int, int>();
            foreach (int colour in bricks)
                counts[colour] = counts.TryGetValue(colour, out int n) ? n + 1 : 1;

            var colours = new List<int>(counts.Keys);
            colours.Sort((a, b) => order(a) != order(b) ? order(a).CompareTo(order(b)) : a.CompareTo(b));
            if (lastColour >= 0 && colours.Count > 1)
            {
                float last = order(lastColour);
                if (Mathf.Abs(order(colours[colours.Count - 1]) - last) < Mathf.Abs(order(colours[0]) - last))
                    colours.Reverse();
            }

            var sorted = new List<int>();
            foreach (int colour in colours)
                for (int i = 0; i < counts[colour]; i++)
                    sorted.Add(colour);
            return sorted;
        }

        /// <summary><see cref="Bands(IEnumerable{int}, Func{int, float}, int)"/> in rainbow order of the palette's colours.</summary>
        public static List<int> Bands(IEnumerable<int> bricks, CubePalette palette, int lastColour) =>
            Bands(bricks, colour => RainbowKey(palette[colour % palette.Count]), lastColour);

        /// <summary>Position of a colour along the rainbow, 0..1 (red first).</summary>
        public static float RainbowKey(Color colour)
        {
            Color.RGBToHSV(colour, out float hue, out _, out _);
            return Mathf.Repeat(hue + RedFirst, 1f);
        }
    }
}
