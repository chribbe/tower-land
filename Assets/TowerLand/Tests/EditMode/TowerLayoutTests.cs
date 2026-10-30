using NUnit.Framework;
using TowerLand.Tower;

namespace TowerLand.Tests
{
    public class TowerLayoutTests
    {
        [Test]
        public void OuterLayer_KeepsColumnCount()
        {
            Assert.AreEqual(26, TowerLayout.ColumnsForLayer(26, 0));
        }

        [Test]
        public void InnerLayers_LoseAboutTwoPiColumns()
        {
            Assert.AreEqual(20, TowerLayout.ColumnsForLayer(26, 1));
            Assert.AreEqual(13, TowerLayout.ColumnsForLayer(26, 2));
            Assert.AreEqual(7, TowerLayout.ColumnsForLayer(26, 3));
        }

        [Test]
        public void ColumnAtSameAngle_MapsProportionallyAndWraps()
        {
            Assert.AreEqual(0, TowerLayout.ColumnAtSameAngle(0, 26, 20));
            Assert.AreEqual(10, TowerLayout.ColumnAtSameAngle(13, 26, 20));
            Assert.AreEqual(19, TowerLayout.ColumnAtSameAngle(25, 26, 20));
            Assert.AreEqual(0, TowerLayout.ColumnAtSameAngle(26, 26, 20), "wraps around");
        }

        [Test]
        public void InnerCube_IsCovered_OnlyWhileAnOuterLayerHidesItsWholeSpan()
        {
            var outer = new TowerGrid(26, 3);
            var middle = new TowerGrid(20, 3);
            var inner = new TowerGrid(13, 3);
            var layers = new[] { outer, middle, inner };
            var cell = new CellCoord(5, 1);

            Assert.IsFalse(TowerLayout.IsCovered(layers, 2, cell));

            // Inner column 5 of 13 spans middle columns 6.9 .. 8.5 of 20: 7 and 8 both cover a good part of it.
            middle[8, 1] = 2;
            Assert.IsFalse(TowerLayout.IsCovered(layers, 2, cell), "Column 7 still open: partly revealed");
            middle[7, 1] = 2;
            Assert.IsTrue(TowerLayout.IsCovered(layers, 2, cell));
            middle[9, 1] = 2;
            middle[8, 1] = TowerGrid.Empty;
            Assert.IsFalse(TowerLayout.IsCovered(layers, 2, cell), "Partly revealed counts as exposed");

            middle[7, 1] = middle[9, 1] = TowerGrid.Empty;
            for (int column = 0; column < 26; column++) outer[column, 1] = 0;
            Assert.IsTrue(TowerLayout.IsCovered(layers, 2, cell), "Any outer layer covers, not just the adjacent one");
            Assert.IsFalse(TowerLayout.IsCovered(layers, 0, new CellCoord(0, 0)), "Outermost layer is never covered");
        }

        [Test]
        public void SpanFilled_NeedsEveryOverlappingColumn_IgnoresSlivers_AndWraps()
        {
            // Column 0 of a 7-column layer spans -0.93 .. 0.93 of a 13-column one: 12 (wrapped), 0 and 1 all overlap enough.
            var grid = new TowerGrid(13, 1);
            grid[0, 0] = 1;
            Assert.IsFalse(TowerLayout.SpanFilled(grid, 0, 7, 0));
            grid[12, 0] = 1;
            grid[1, 0] = 1;
            Assert.IsTrue(TowerLayout.SpanFilled(grid, 0, 7, 0));
            Assert.IsTrue(TowerLayout.Overlaps(0, 7, 12, 13), "wraps around");
            Assert.IsFalse(TowerLayout.Overlaps(0, 7, 5, 13));

            // Column 0 of 13 spans -0.77 .. 0.77 of 20: columns 19 and 1 are slivers (< 20% of its width).
            var middle = new TowerGrid(20, 1);
            middle[0, 0] = 1;
            Assert.IsTrue(TowerLayout.SpanFilled(middle, 0, 13, 0));
            Assert.IsFalse(TowerLayout.Overlaps(0, 13, 19, 20));
        }

        [Test]
        public void FittingLayers_StopsBeforeTooFewColumns()
        {
            Assert.AreEqual(4, TowerLayout.FittingLayers(26, 6));
            Assert.AreEqual(3, TowerLayout.FittingLayers(26, 3));
            Assert.AreEqual(1, TowerLayout.FittingLayers(8, 3));
        }
    }
}
