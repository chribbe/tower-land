using System.Collections.Generic;
using NUnit.Framework;
using TowerLand.Levels.Generation;
using TowerLand.Tower;

namespace TowerLand.Tests
{
    public class VoronoiPatternTests
    {
        private static VoronoiResult Generate(int seed, int colours = 5, VoronoiMetric metric = VoronoiMetric.Manhattan)
        {
            var settings = new VoronoiSettings { RegionCount = 14, Metric = metric };
            return VoronoiPattern.Generate(24, 14, colours, settings, new System.Random(seed));
        }

        [Test]
        public void FillsEveryCell_WithValidColour([Values(1, 2, 3, 42)] int seed)
        {
            TowerGrid grid = Generate(seed).Grid;
            Assert.AreEqual(grid.CellCount, grid.CountFilled());
            for (int i = 0; i < grid.CellCount; i++)
            {
                int colour = grid[grid.CoordOf(i)];
                Assert.That(colour, Is.InRange(0, 4));
            }
        }

        [Test]
        public void SameSeed_GivesSameTower()
        {
            TowerGrid a = Generate(7).Grid;
            TowerGrid b = Generate(7).Grid;
            for (int i = 0; i < a.CellCount; i++)
                Assert.AreEqual(a[a.CoordOf(i)], b[b.CoordOf(i)]);
        }

        [Test]
        public void UsesEveryColour_WhenEnoughRegions([Range(1, 20)] int seed)
        {
            TowerGrid grid = Generate(seed).Grid;
            var used = new HashSet<int>();
            for (int i = 0; i < grid.CellCount; i++)
                used.Add(grid[grid.CoordOf(i)]);
            Assert.AreEqual(5, used.Count);
        }

        [Test]
        public void TouchingRegions_HaveDifferentColours(
            [Range(1, 20)] int seed,
            [Values(VoronoiMetric.Euclidean, VoronoiMetric.Manhattan, VoronoiMetric.Chebyshev)] VoronoiMetric metric)
        {
            VoronoiResult result = Generate(seed, metric: metric);
            TowerGrid grid = result.Grid;
            var neighbours = new List<CellCoord>();

            for (int i = 0; i < grid.CellCount; i++)
            {
                CellCoord cell = grid.CoordOf(i);
                neighbours.Clear();
                grid.GetNeighbours(cell, neighbours);
                foreach (CellCoord neighbour in neighbours)
                {
                    int j = grid.IndexOf(neighbour);
                    if (result.RegionOfCell[i] != result.RegionOfCell[j])
                        Assert.AreNotEqual(grid[cell], grid[neighbour], $"Regions touch with same colour at {cell}/{neighbour}");
                }
            }
        }
    }
}
