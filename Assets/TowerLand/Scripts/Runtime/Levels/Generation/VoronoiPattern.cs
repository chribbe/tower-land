using System;
using System.Collections.Generic;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Levels.Generation
{
    public enum VoronoiMetric
    {
        Euclidean,
        Manhattan,
        Chebyshev,
    }

    [Serializable]
    public sealed class VoronoiSettings
    {
        [Tooltip("Number of Voronoi sites, i.e. colour patches on the tower.")]
        [Min(1)] public int RegionCount = 14;

        [Tooltip("Distance metric. Manhattan/Chebyshev give blockier, more 'pixel' patches than Euclidean.")]
        public VoronoiMetric Metric = VoronoiMetric.Manhattan;

        [Tooltip(">1 makes patches taller than wide, <1 wider than tall.")]
        [Range(0.25f, 4f)] public float VerticalStretch = 1f;

        [Tooltip("Try to give touching patches different colours so every patch reads as its own group.")]
        public bool AvoidSameColourNeighbours = true;
    }

    public readonly struct VoronoiResult
    {
        public readonly TowerGrid Grid;
        /// <summary>Region index of every cell, indexed like <see cref="TowerGrid.IndexOf(CellCoord)"/>.</summary>
        public readonly int[] RegionOfCell;
        public readonly int[] RegionColour;

        public VoronoiResult(TowerGrid grid, int[] regionOfCell, int[] regionColour)
        {
            Grid = grid;
            RegionOfCell = regionOfCell;
            RegionColour = regionColour;
        }
    }

    /// <summary>
    /// Fills a cylindrical tower with Voronoi patches: random sites on the tower surface (wrapping around columns),
    /// every cell takes the colour of its nearest site.
    /// </summary>
    public static class VoronoiPattern
    {
        public static VoronoiResult Generate(int columns, int floors, int colourCount, VoronoiSettings settings, System.Random rng)
        {
            if (colourCount < 1) throw new ArgumentOutOfRangeException(nameof(colourCount));

            var grid = new TowerGrid(columns, floors);
            int regionCount = Mathf.Clamp(settings.RegionCount, 1, grid.CellCount);
            float stretch = Mathf.Max(0.01f, settings.VerticalStretch);

            var siteX = new float[regionCount];
            var siteY = new float[regionCount];
            for (int r = 0; r < regionCount; r++)
            {
                siteX[r] = (float)rng.NextDouble() * columns;
                siteY[r] = (float)rng.NextDouble() * floors;
            }

            var regionOfCell = new int[grid.CellCount];
            var regionSize = new int[regionCount];
            for (int index = 0; index < grid.CellCount; index++)
            {
                CellCoord cell = grid.CoordOf(index);
                float x = cell.Column + 0.5f;
                float y = cell.Floor + 0.5f;

                int best = 0;
                float bestDistance = float.MaxValue;
                for (int r = 0; r < regionCount; r++)
                {
                    float dx = Mathf.Abs(x - siteX[r]);
                    dx = Mathf.Min(dx, columns - dx);
                    float dy = Mathf.Abs(y - siteY[r]) / stretch;
                    float distance = Distance(dx, dy, settings.Metric);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = r;
                    }
                }

                regionOfCell[index] = best;
                regionSize[best]++;
            }

            HashSet<int>[] adjacency = BuildAdjacency(grid, regionOfCell, regionCount);
            int[] regionColour = AssignColours(regionSize, adjacency, colourCount, settings.AvoidSameColourNeighbours, rng);

            for (int index = 0; index < grid.CellCount; index++)
                grid[grid.CoordOf(index)] = regionColour[regionOfCell[index]];

            return new VoronoiResult(grid, regionOfCell, regionColour);
        }

        private static float Distance(float dx, float dy, VoronoiMetric metric)
        {
            switch (metric)
            {
                case VoronoiMetric.Manhattan: return dx + dy;
                case VoronoiMetric.Chebyshev: return Mathf.Max(dx, dy);
                default: return dx * dx + dy * dy; // squared: same ordering, cheaper
            }
        }

        private static HashSet<int>[] BuildAdjacency(TowerGrid grid, int[] regionOfCell, int regionCount)
        {
            var adjacency = new HashSet<int>[regionCount];
            for (int r = 0; r < regionCount; r++)
                adjacency[r] = new HashSet<int>();

            var neighbours = new List<CellCoord>(4);
            for (int index = 0; index < grid.CellCount; index++)
            {
                int region = regionOfCell[index];
                neighbours.Clear();
                grid.GetNeighbours(grid.CoordOf(index), neighbours);
                foreach (CellCoord neighbour in neighbours)
                {
                    int other = regionOfCell[grid.IndexOf(neighbour)];
                    if (other != region)
                    {
                        adjacency[region].Add(other);
                        adjacency[other].Add(region);
                    }
                }
            }

            return adjacency;
        }

        private const int MaxColouringSteps = 20000;

        /// <summary>
        /// Colours regions so touching regions differ, preferring the colour covering the fewest cells so far
        /// (random tie-break) to keep the palette balanced. Exact backtracking search ordered by DSatur (most
        /// constrained region first); a tower surface is planar so 4+ colours always succeed. If the search fails
        /// (fewer than 4 colours) or neighbour avoidance is off, falls back to a greedy min-conflict pass.
        /// </summary>
        private static int[] AssignColours(int[] regionSize, HashSet<int>[] adjacency, int colourCount, bool avoidNeighbours, System.Random rng)
        {
            var regionColour = new int[regionSize.Length];
            Array.Fill(regionColour, TowerGrid.Empty);
            var colourUsage = new int[colourCount];

            if (avoidNeighbours)
            {
                int steps = 0;
                if (TryColour(regionSize, adjacency, colourCount, regionColour, colourUsage, rng, ref steps))
                    return regionColour;

                Array.Fill(regionColour, TowerGrid.Empty);
                Array.Clear(colourUsage, 0, colourCount);
            }

            GreedyColour(regionSize, adjacency, colourCount, avoidNeighbours, regionColour, colourUsage, rng);
            return regionColour;
        }

        private static bool TryColour(int[] regionSize, HashSet<int>[] adjacency, int colourCount,
            int[] regionColour, int[] colourUsage, System.Random rng, ref int steps)
        {
            if (++steps > MaxColouringSteps)
                return false;

            int region = MostConstrainedUncoloured(regionSize, adjacency, regionColour);
            if (region < 0)
                return true;

            var blocked = new bool[colourCount];
            foreach (int neighbour in adjacency[region])
                if (regionColour[neighbour] != TowerGrid.Empty)
                    blocked[regionColour[neighbour]] = true;

            var candidates = new List<int>(colourCount);
            for (int c = 0; c < colourCount; c++)
                if (!blocked[c])
                    candidates.Add(c);

            Shuffle(candidates, rng);
            candidates.Sort((a, b) => colourUsage[a].CompareTo(colourUsage[b])); // List.Sort is unstable: still random among ties

            foreach (int colour in candidates)
            {
                regionColour[region] = colour;
                colourUsage[colour] += regionSize[region];
                if (TryColour(regionSize, adjacency, colourCount, regionColour, colourUsage, rng, ref steps))
                    return true;
                colourUsage[colour] -= regionSize[region];
                regionColour[region] = TowerGrid.Empty;
            }

            return false;
        }

        /// <summary>DSatur pick: uncoloured region with the most distinct neighbour colours, then the most cells.</summary>
        private static int MostConstrainedUncoloured(int[] regionSize, HashSet<int>[] adjacency, int[] regionColour)
        {
            int best = -1, bestSaturation = -1;
            var seen = new HashSet<int>();
            for (int r = 0; r < regionSize.Length; r++)
            {
                if (regionColour[r] != TowerGrid.Empty || regionSize[r] == 0)
                    continue;

                seen.Clear();
                foreach (int neighbour in adjacency[r])
                    if (regionColour[neighbour] != TowerGrid.Empty)
                        seen.Add(regionColour[neighbour]);

                if (seen.Count > bestSaturation || (seen.Count == bestSaturation && regionSize[r] > regionSize[best]))
                {
                    best = r;
                    bestSaturation = seen.Count;
                }
            }

            return best;
        }

        private static void GreedyColour(int[] regionSize, HashSet<int>[] adjacency, int colourCount, bool avoidNeighbours,
            int[] regionColour, int[] colourUsage, System.Random rng)
        {
            var order = new int[regionSize.Length];
            for (int r = 0; r < order.Length; r++) order[r] = r;
            Array.Sort(order, (a, b) => regionSize[b].CompareTo(regionSize[a]));

            var conflicts = new int[colourCount];
            var candidates = new List<int>(colourCount);
            foreach (int region in order)
            {
                if (regionSize[region] == 0)
                    continue;

                Array.Clear(conflicts, 0, colourCount);
                if (avoidNeighbours)
                {
                    foreach (int neighbour in adjacency[region])
                        if (regionColour[neighbour] != TowerGrid.Empty)
                            conflicts[regionColour[neighbour]]++;
                }

                int minConflicts = int.MaxValue;
                for (int c = 0; c < colourCount; c++)
                    minConflicts = Math.Min(minConflicts, conflicts[c]);

                candidates.Clear();
                for (int c = 0; c < colourCount; c++)
                    if (conflicts[c] == minConflicts)
                        candidates.Add(c);

                Shuffle(candidates, rng);
                int chosen = candidates[0];
                foreach (int c in candidates)
                    if (colourUsage[c] < colourUsage[chosen])
                        chosen = c;

                regionColour[region] = chosen;
                colourUsage[chosen] += regionSize[region];
            }
        }

        private static void Shuffle(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
