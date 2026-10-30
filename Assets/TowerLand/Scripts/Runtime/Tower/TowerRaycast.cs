using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Tower
{
    /// <summary>Where a segment first touches an occupied cube of the tower.</summary>
    public readonly struct TowerHit
    {
        public readonly int Layer;
        public readonly CellCoord Cell;
        /// <summary>Impact point on the cube's face (front, or back when <see cref="Inside"/>), in tower (spin root) space.</summary>
        public readonly Vector3 LocalPoint;
        /// <summary>Position along the tested segment, 0..1.</summary>
        public readonly float T;
        /// <summary>Impact offset from the cube's centre, in cells (x = column, y = floor), each in [-0.5, 0.5].</summary>
        public readonly Vector2 Offset;
        /// <summary>Hit on the cube's back, from inside the tower (through a hole: a patch on the far side).</summary>
        public readonly bool Inside;

        public TowerHit(int layer, CellCoord cell, Vector3 localPoint, float t, Vector2 offset, bool inside = false)
        {
            Layer = layer;
            Cell = cell;
            LocalPoint = localPoint;
            T = t;
            Offset = offset;
            Inside = inside;
        }
    }

    /// <summary>
    /// Exact hit test of a segment against the cube tower, using the grid only (no colliders). Each layer's cubes fill a
    /// band between two cylinders: their fronts (outer) and backs (inner). The segment can reach a cube from outside,
    /// crossing a front cylinder inwards, or - once it has gone through a hole into the hollow core - from inside,
    /// crossing a back cylinder outwards (aiming at the far side's patches through the hole). Crossings are taken in
    /// order along the segment and the first one landing on an occupied cell wins.
    /// Works in tower space: column 0 faces +Z, angles grow towards +X, floor f spans y in [f, f+1) * floorHeight.
    /// </summary>
    public static class TowerRaycast
    {
        private const int MaxCrossings = 16;

        /// <summary>
        /// Sweeps a sphere of <paramref name="radius"/> (0 = a ray) from <paramref name="from"/> to <paramref name="to"/>.
        /// The sphere touches a layer when it reaches its cube-front cylinder (or, from inside, its cube-back cylinder,
        /// <paramref name="cubeDepth"/> further in; 0 = fronts only); the touched cube is the closest occupied cell
        /// within reach of the contact point (the cell under it, or a neighbour whose edge the sphere grazes).
        /// </summary>
        public static bool Cast(IReadOnlyList<TowerGrid> layers, Func<int, float> faceRadius, float floorHeight,
            Vector3 from, Vector3 to, out TowerHit hit, float radius = 0f, float cubeDepth = 0f)
        {
            hit = default;
            Vector3 d = to - from;
            float a = d.x * d.x + d.z * d.z;
            if (a < 1e-9f)
                return false;
            float b = 2f * (from.x * d.x + from.z * d.z);
            float fromSq = from.x * from.x + from.z * from.z;

            // Crossings along the segment: (t, layer, from inside).
            Span<(float t, int layer, bool inside)> crossings = stackalloc (float, int, bool)[MaxCrossings];
            int count = 0;
            for (int layer = 0; layer < layers.Count && count < MaxCrossings - 1; layer++)
            {
                float face = faceRadius(layer);
                // Front: entering the cylinder from outside (first root; skipped when the segment starts inside it).
                float r = face + radius;
                float c = fromSq - r * r;
                float disc = b * b - 4f * a * c;
                if (c >= 0f && disc >= 0f)
                {
                    float t = (-b - Mathf.Sqrt(disc)) / (2f * a);
                    if (t >= 0f && t <= 1f) crossings[count++] = (t, layer, false);
                }
                // Back: leaving the hollow inside the layer, outwards (second root).
                if (cubeDepth <= 0f) continue;
                r = face - cubeDepth - radius;
                if (r <= 0.01f) continue;
                c = fromSq - r * r;
                disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float t = (-b + Mathf.Sqrt(disc)) / (2f * a);
                    if (t >= 0f && t <= 1f) crossings[count++] = (t, layer, true);
                }
            }

            // In order along the segment (a handful of entries: insertion sort).
            for (int i = 1; i < count; i++)
                for (int k = i; k > 0 && crossings[k].t < crossings[k - 1].t; k--)
                    (crossings[k], crossings[k - 1]) = (crossings[k - 1], crossings[k]);

            bool found = false;
            for (int i = 0; i < count && !found; i++)
            {
                (float t, int layer, bool inside) = crossings[i];
                Vector3 p = from + d * t;
                TowerGrid grid = layers[layer];
                float columnF = Mathf.Atan2(p.x, p.z) / (2f * Mathf.PI) * grid.Columns;
                float floorF = p.y / floorHeight;
                if (!ClosestOccupied(grid, columnF, floorF, radius / floorHeight, out CellCoord cell, out Vector2 offset))
                    continue;

                // Contact on the cube face: the sphere centre sits `radius` further out (front) or further in (back).
                Vector3 radial = new Vector3(p.x, 0f, p.z).normalized;
                Vector3 contact = radius > 0f ? p + radial * (inside ? radius : -radius) : p;
                hit = new TowerHit(layer, cell, contact, t, offset, inside);
                found = true;
            }

            // A path can also run inside a layer's band without crossing either of its cylinders (through a hole, off the
            // axis: it slides into the far cubes from the side) or enter a cube through its top / bottom: march the part
            // of the segment inside the tower and test the cube volumes. The earliest hit of both tests wins.
            if (cubeDepth > 0f && layers.Count > 0 && March(layers, faceRadius, floorHeight, from, d, a, b, fromSq, radius, cubeDepth,
                    found ? hit.T : 1f, found, out TowerHit marched))
            {
                hit = marched;
                found = true;
            }
            return found;
        }

        /// <summary>Spacing of the volume samples along the segment, metres (horizontal).</summary>
        private const float MarchStep = 0.2f;

        /// <summary>
        /// First sample point whose sphere overlaps an occupied cube of some layer, up to <paramref name="maxT"/>
        /// (strictly before it when <paramref name="strict"/>: a face crossing already hit there, and it is exact).
        /// </summary>
        private static bool March(IReadOnlyList<TowerGrid> layers, Func<int, float> faceRadius, float floorHeight,
            Vector3 from, Vector3 d, float a, float b, float fromSq, float radius, float cubeDepth, float maxT, bool strict,
            out TowerHit hit)
        {
            hit = default;
            // Only the stretch inside the outermost cube fronts (+ the sphere) can touch a cube.
            float outer = faceRadius(0) + radius;
            float c = fromSq - outer * outer;
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float root = Mathf.Sqrt(disc);
            float t0 = Mathf.Max(0f, (-b - root) / (2f * a)), t1 = Mathf.Min(maxT, (-b + root) / (2f * a));
            if (t1 <= t0) return false;

            float length = Mathf.Sqrt(a);   // horizontal length of the whole segment
            int steps = Mathf.Max(1, Mathf.CeilToInt((t1 - t0) * length / MarchStep));
            float reach = radius / floorHeight;
            for (int s = 0; s <= steps; s++)
            {
                float t = Mathf.Lerp(t0, t1, s / (float)steps);
                if (strict && t >= maxT - 1e-4f) break;
                Vector3 p = from + d * t;
                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                for (int layer = 0; layer < layers.Count; layer++)
                {
                    float face = faceRadius(layer);
                    if (r > face + radius || r < face - cubeDepth - radius) continue;   // outside this layer's band
                    TowerGrid grid = layers[layer];
                    float columnF = Mathf.Atan2(p.x, p.z) / (2f * Mathf.PI) * grid.Columns;
                    if (!ClosestOccupied(grid, columnF, p.y / floorHeight, reach, out CellCoord cell, out Vector2 offset))
                        continue;
                    bool inside = p.x * d.x + p.z * d.z > 0f;   // moving outwards: came from the hollow core
                    // Contact: the sphere's point towards the cube band (outward from the hollow, inward from outside).
                    Vector3 radial = r > 1e-4f ? new Vector3(p.x / r, 0f, p.z / r) : Vector3.zero;
                    hit = new TowerHit(layer, cell, p + radial * (inside ? radius : -radius), t, offset, inside);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Occupied cell nearest to (columnF, floorF) - in cell units - within <paramref name="reach"/> of its edges.</summary>
        private static bool ClosestOccupied(TowerGrid grid, float columnF, float floorF, float reach,
            out CellCoord cell, out Vector2 offset)
        {
            cell = default;
            offset = default;
            int centreColumn = Mathf.RoundToInt(columnF);
            int centreFloor = Mathf.FloorToInt(floorF);
            int span = reach > 0f ? 1 : 0;
            float best = float.MaxValue;
            for (int dc = -span; dc <= span; dc++)
            {
                for (int df = -span; df <= span; df++)
                {
                    int column = centreColumn + dc, floor = centreFloor + df;
                    if (floor < 0 || floor >= grid.Floors || grid[column, floor] == TowerGrid.Empty)
                        continue;
                    float ox = columnF - column, oy = floorF - floor - 0.5f;
                    float ex = Mathf.Max(0f, Mathf.Abs(ox) - 0.5f), ey = Mathf.Max(0f, Mathf.Abs(oy) - 0.5f);
                    float distance = Mathf.Sqrt(ex * ex + ey * ey);
                    if (distance > reach || distance >= best)
                        continue;
                    best = distance;
                    cell = new CellCoord(grid.WrapColumn(column), floor);
                    offset = new Vector2(Mathf.Clamp(ox, -0.5f, 0.5f), Mathf.Clamp(oy, -0.5f, 0.5f));
                }
            }
            return best < float.MaxValue;
        }
    }
}
