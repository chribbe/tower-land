using System;
using System.Collections.Generic;

namespace TowerLand.Tower
{
    /// <summary>Geometry rules shared by level generation and the view for multi-layer (concentric) towers.</summary>
    public static class TowerLayout
    {
        public const int MinColumns = 6;

        /// <summary>
        /// Column count of layer <paramref name="layer"/> (0 = outermost). Each inner layer sits one cube spacing further
        /// in, so its circumference shrinks by 2π·spacing, i.e. it loses ~2π columns while keeping the same cube size.
        /// </summary>
        public static int ColumnsForLayer(int outerColumns, int layer)
        {
            if (layer < 0) throw new ArgumentOutOfRangeException(nameof(layer));
            return (int)Math.Round(outerColumns - 2.0 * Math.PI * layer);
        }

        /// <summary>Column of <paramref name="otherColumns"/>-column layer that sits at the same angle as <paramref name="column"/>.</summary>
        public static int ColumnAtSameAngle(int column, int columns, int otherColumns)
        {
            int mapped = (int)Math.Round((double)column * otherColumns / columns);
            return ((mapped % otherColumns) + otherColumns) % otherColumns;
        }

        /// <summary>
        /// An outer (or inner) column counts as overlapping a cube once it spans at least this share of the cube's
        /// angular width; thinner slivers don't hide it.
        /// </summary>
        public const float MinOverlapShare = 0.2f;

        /// <summary>
        /// True when an outer layer hides <paramref name="cell"/> of <paramref name="layer"/>: every cube of that layer
        /// overlapping its angular span (same floor) is there. Uncovering any of them reveals part of the cube, and a
        /// partly revealed cube counts as exposed. Covered cubes are hidden in shadow; exposed ones are lit and playable.
        /// </summary>
        public static bool IsCovered(IReadOnlyList<TowerGrid> layers, int layer, CellCoord cell)
        {
            int columns = layers[layer].Columns;
            for (int outer = layer - 1; outer >= 0; outer--)
                if (SpanFilled(layers[outer], cell.Column, columns, cell.Floor))
                    return true;
            return false;
        }

        /// <summary>
        /// Whether <paramref name="grid"/> (another layer) has a cube on <paramref name="floor"/> in every column overlapping
        /// the angular span of <paramref name="column"/> of a <paramref name="columns"/>-column layer.
        /// </summary>
        public static bool SpanFilled(TowerGrid grid, int column, int columns, int floor)
        {
            if (floor < 0 || floor >= grid.Floors) return false;
            float scale = (float)grid.Columns / columns;
            float from = (column - 0.5f) * scale, to = (column + 0.5f) * scale;
            float minOverlap = (to - from) * MinOverlapShare;
            for (int other = (int)Math.Floor(from + 0.5f); other <= (int)Math.Floor(to + 0.5f); other++)
            {
                float overlap = Math.Min(to, other + 0.5f) - Math.Max(from, other - 0.5f);
                if (overlap < minOverlap) continue;
                int wrapped = ((other % grid.Columns) + grid.Columns) % grid.Columns;
                if (grid[wrapped, floor] == TowerGrid.Empty)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Columns of an <paramref name="otherColumns"/>-column layer whose cubes touch <paramref name="column"/> of a
        /// <paramref name="columns"/>-column layer (adjacent layers, same floor): angular overlap of at least
        /// <see cref="MinOverlapShare"/> of the narrower of the two cubes. Symmetric, so links work both ways.
        /// </summary>
        public static IEnumerable<int> TouchingColumns(int column, int columns, int otherColumns)
        {
            float scale = (float)otherColumns / columns;
            float from = (column - 0.5f) * scale, to = (column + 0.5f) * scale;   // in the other layer's column units
            float minOverlap = Math.Min(1f, scale) * MinOverlapShare;
            for (int other = (int)Math.Floor(from); other <= (int)Math.Ceiling(to); other++)
            {
                float overlap = Math.Min(to, other + 0.5f) - Math.Max(from, other - 0.5f);
                if (overlap >= minOverlap)
                    yield return ((other % otherColumns) + otherColumns) % otherColumns;
            }
        }

        /// <summary>Whether column <paramref name="other"/> of an <paramref name="otherColumns"/>-column layer overlaps <paramref name="column"/> (see <see cref="SpanFilled"/>).</summary>
        public static bool Overlaps(int column, int columns, int other, int otherColumns)
        {
            float scale = (float)otherColumns / columns;
            float from = (column - 0.5f) * scale, to = (column + 0.5f) * scale;
            // Nearest copy of the other column around the circle.
            float centre = other + otherColumns * (float)Math.Round((column * scale - other) / otherColumns);
            float overlap = Math.Min(to, centre + 0.5f) - Math.Max(from, centre - 0.5f);
            return overlap >= (to - from) * MinOverlapShare;
        }

        /// <summary>How many of the requested layers fit before a layer would drop below <see cref="MinColumns"/>.</summary>
        public static int FittingLayers(int outerColumns, int requestedLayers)
        {
            int layers = 0;
            while (layers < requestedLayers && ColumnsForLayer(outerColumns, layers) >= MinColumns)
                layers++;
            return Math.Max(1, layers);
        }
    }
}
