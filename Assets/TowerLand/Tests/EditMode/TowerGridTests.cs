using System.Collections.Generic;
using NUnit.Framework;
using TowerLand.Tower;

namespace TowerLand.Tests
{
    public class TowerGridTests
    {
        [Test]
        public void NewGrid_IsEmpty()
        {
            var grid = new TowerGrid(8, 4);
            Assert.AreEqual(32, grid.CellCount);
            Assert.AreEqual(0, grid.CountFilled());
        }

        [Test]
        public void Columns_WrapAround()
        {
            var grid = new TowerGrid(8, 4);
            grid[-1, 2] = 3;
            Assert.AreEqual(3, grid[7, 2]);
            Assert.AreEqual(3, grid[15, 2]);
        }

        [Test]
        public void Neighbours_WrapHorizontally_AndClampVertically()
        {
            var grid = new TowerGrid(8, 4);
            var neighbours = new List<CellCoord>();

            grid.GetNeighbours(new CellCoord(0, 0), neighbours);

            CollectionAssert.AreEquivalent(
                new[] { new CellCoord(7, 0), new CellCoord(1, 0), new CellCoord(0, 1) },
                neighbours);
        }

        [Test]
        public void IndexAndCoord_RoundTrip()
        {
            var grid = new TowerGrid(8, 4);
            for (int index = 0; index < grid.CellCount; index++)
                Assert.AreEqual(index, grid.IndexOf(grid.CoordOf(index)));
        }
    }
}
