using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Tower
{
    /// <summary>A cube removed by a shot; <see cref="Depth"/> = steps from the impact cube (drives the ripple).</summary>
    public readonly struct RemovedCube
    {
        public readonly int Layer;
        public readonly CellCoord Cell;
        public readonly int Depth;

        public RemovedCube(int layer, CellCoord cell, int depth)
        {
            Layer = layer;
            Cell = cell;
            Depth = depth;
        }
    }

    /// <summary>Gameplay rules on the tower grids (no Unity scene dependency, unit tested).</summary>
    public static class TowerRules
    {
        /// <summary>
        /// The cube a shot of <paramref name="colour"/> destroys when it lands on <paramref name="hit"/>, or false for a
        /// miss. A wrong-colour hit close to the edge of a same-colour neighbour still counts (forgiving aim, as in
        /// Tower Buster): <paramref name="nearMiss"/> is how far from the centre (0..0.5 cells) the impact must be.
        /// </summary>
        public static bool ResolveShot(IReadOnlyList<TowerGrid> layers, TowerHit hit, int colour, float nearMiss,
            out int layer, out CellCoord cell)
        {
            layer = hit.Layer;
            cell = hit.Cell;
            TowerGrid grid = layers[layer];
            if (grid[cell] == colour)
                return true;

            Vector2 o = hit.Offset;
            bool sideways = Mathf.Abs(o.x) >= Mathf.Abs(o.y);
            float amount = sideways ? Mathf.Abs(o.x) : Mathf.Abs(o.y);
            if (amount < nearMiss)
                return false;

            CellCoord neighbour = sideways
                ? new CellCoord(grid.WrapColumn(cell.Column + (o.x > 0 ? 1 : -1)), cell.Floor)
                : new CellCoord(cell.Column, cell.Floor + (o.y > 0 ? 1 : -1));
            if (neighbour.Floor < 0 || neighbour.Floor >= grid.Floors || grid[neighbour] != colour
                || TowerLayout.IsCovered(layers, layer, neighbour))
                return false;

            cell = neighbour;
            return true;
        }

        /// <summary>Same-colour cubes connected to <paramref name="start"/> within its layer (breadth-first, with depth).</summary>
        public static List<RemovedCube> FindGroup(IReadOnlyList<TowerGrid> layers, int layer, CellCoord start)
        {
            var result = new List<RemovedCube>();
            TowerGrid grid = layers[layer];
            int colour = grid[start];
            if (colour == TowerGrid.Empty)
                return result;

            var visited = new HashSet<CellCoord> { start };
            var queue = new Queue<RemovedCube>();
            queue.Enqueue(new RemovedCube(layer, start, 0));
            var neighbours = new List<CellCoord>(4);
            while (queue.Count > 0)
            {
                RemovedCube current = queue.Dequeue();
                result.Add(current);
                neighbours.Clear();
                grid.GetNeighbours(current.Cell, neighbours);
                foreach (CellCoord n in neighbours)
                    if (grid[n] == colour && visited.Add(n))
                        queue.Enqueue(new RemovedCube(layer, n, current.Depth + 1));
            }

            return result;
        }

        /// <summary>Cubes the player can currently see/hit: occupied and not covered by an outer layer.</summary>
        public static void GetExposedCubes(IReadOnlyList<TowerGrid> layers, List<(int layer, CellCoord cell)> results)
        {
            results.Clear();
            for (int layer = 0; layer < layers.Count; layer++)
            {
                TowerGrid grid = layers[layer];
                for (int index = 0; index < grid.CellCount; index++)
                {
                    CellCoord cell = grid.CoordOf(index);
                    if (grid[cell] != TowerGrid.Empty && !TowerLayout.IsCovered(layers, layer, cell))
                        results.Add((layer, cell));
                }
            }
        }

        /// <summary>
        /// Colour for the next projectile: that of a random exposed cube, so it is always playable and common colours
        /// come up more often. -1 when the tower is cleared.
        /// </summary>
        public static int PickReachableColour(IReadOnlyList<TowerGrid> layers, System.Random rng)
        {
            var exposed = new List<(int layer, CellCoord cell)>();
            GetExposedCubes(layers, exposed);
            if (exposed.Count == 0)
                return -1;
            (int layer, CellCoord cell) pick = exposed[rng.Next(exposed.Count)];
            return layers[pick.layer][pick.cell];
        }

        public static bool HasColour(IReadOnlyList<TowerGrid> layers, int colour)
        {
            foreach (TowerGrid grid in layers)
                for (int index = 0; index < grid.CellCount; index++)
                    if (grid[grid.CoordOf(index)] == colour)
                        return true;
            return false;
        }

        /// <summary>
        /// Wrong-colour "second chance" (Tower Buster playable): the closest exposed cube of <paramref name="colour"/>
        /// whose centre lies within <paramref name="radius"/> of <paramref name="probe"/> (a point a little ahead of the
        /// projectile, tower space). Never a deeper layer than the one touched - breaking the layer underneath reads as
        /// a bug - and never a covered cube.
        /// </summary>
        public static bool Rescue(IReadOnlyList<TowerGrid> layers, int touchedLayer, int colour, Vector3 probe, float radius,
            Func<int, CellCoord, Vector3> cellCentre, out int layer, out CellCoord cell)
        {
            layer = -1;
            cell = default;
            float best = radius * radius;
            for (int l = 0; l <= touchedLayer && l < layers.Count; l++)
            {
                TowerGrid grid = layers[l];
                for (int index = 0; index < grid.CellCount; index++)
                {
                    CellCoord c = grid.CoordOf(index);
                    if (grid[c] != colour) continue;
                    float d = (cellCentre(l, c) - probe).sqrMagnitude;
                    if (d > best || TowerLayout.IsCovered(layers, l, c)) continue;
                    best = d;
                    layer = l;
                    cell = c;
                }
            }
            return layer >= 0;
        }

        /// <summary>
        /// Cubes no longer attached to the tower (Tower Land rule). A central pole runs up the tower's axis: every cube of
        /// the innermost layer is fixed to it, at any height, and every bottom-floor cube rests on the base. Anything
        /// connected to those anchors through touching cubes stays - left / right / up / down within a layer, and in front
        /// of / behind it in the adjacent layers (same floor, overlapping angle) - however long or thin the chain. So a
        /// broken bottom doesn't bring the top down while it still hangs off the pole. Everything else falls.
        /// (A single-layer tower is all innermost layer: nothing ever falls there.)
        /// </summary>
        public static List<(int layer, CellCoord cell)> FindUnsupported(IReadOnlyList<TowerGrid> layers)
        {
            var held = new HashSet<(int, CellCoord)>();
            var queue = new Queue<(int, CellCoord)>();
            for (int l = 0; l < layers.Count; l++)
            {
                TowerGrid grid = layers[l];
                bool pole = l == layers.Count - 1;
                for (int index = 0; index < grid.CellCount; index++)
                {
                    CellCoord c = grid.CoordOf(index);
                    if (grid[c] != TowerGrid.Empty && (pole || c.Floor == 0) && held.Add((l, c)))
                        queue.Enqueue((l, c));
                }
            }

            while (queue.Count > 0)
            {
                (int l, CellCoord c) = queue.Dequeue();
                foreach ((int nl, CellCoord nc) in Touching(layers, l, c))
                    if (layers[nl][nc] != TowerGrid.Empty && held.Add((nl, nc)))
                        queue.Enqueue((nl, nc));
            }

            var result = new List<(int, CellCoord)>();
            for (int l = 0; l < layers.Count; l++)
            {
                TowerGrid grid = layers[l];
                for (int index = 0; index < grid.CellCount; index++)
                {
                    CellCoord c = grid.CoordOf(index);
                    if (grid[c] != TowerGrid.Empty && !held.Contains((l, c)))
                        result.Add((l, c));
                }
            }
            return result;
        }

        /// <summary>Cells touching <paramref name="cell"/>: its 4 neighbours in the layer, and the overlapping cells of the adjacent layers on the same floor.</summary>
        private static IEnumerable<(int, CellCoord)> Touching(IReadOnlyList<TowerGrid> layers, int layer, CellCoord cell)
        {
            TowerGrid grid = layers[layer];
            yield return (layer, new CellCoord(grid.WrapColumn(cell.Column - 1), cell.Floor));
            yield return (layer, new CellCoord(grid.WrapColumn(cell.Column + 1), cell.Floor));
            if (cell.Floor + 1 < grid.Floors) yield return (layer, new CellCoord(cell.Column, cell.Floor + 1));
            if (cell.Floor > 0) yield return (layer, new CellCoord(cell.Column, cell.Floor - 1));
            foreach (int l in new[] { layer - 1, layer + 1 })
            {
                if (l < 0 || l >= layers.Count || cell.Floor >= layers[l].Floors) continue;
                foreach (int column in TowerLayout.TouchingColumns(cell.Column, grid.Columns, layers[l].Columns))
                    yield return (l, new CellCoord(column, cell.Floor));
            }
        }
    }
}
