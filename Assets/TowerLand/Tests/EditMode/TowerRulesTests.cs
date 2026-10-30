using System.Collections.Generic;
using NUnit.Framework;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Tests
{
    public class TowerRulesTests
    {
        private static TowerGrid Grid(int columns, params string[] rowsTopDown)
        {
            var grid = new TowerGrid(columns, rowsTopDown.Length);
            for (int r = 0; r < rowsTopDown.Length; r++)
                for (int c = 0; c < columns; c++)
                    grid[c, rowsTopDown.Length - 1 - r] = rowsTopDown[r][c] == '.' ? TowerGrid.Empty : rowsTopDown[r][c] - '0';
            return grid;
        }

        [Test]
        public void FindGroup_FloodsSameColourOnly_WithBfsDepth()
        {
            var g = Grid(6, "110022", "100022", "111000");
            List<RemovedCube> group = TowerRules.FindGroup(new[] { g }, 0, new CellCoord(0, 0));
            Assert.AreEqual(6, group.Count);
            Assert.AreEqual(0, group[0].Depth);
            Assert.AreEqual(2, group.Find(x => x.Cell.Equals(new CellCoord(2, 0))).Depth);
        }

        [Test]
        public void FindGroup_WrapsAroundTheCylinder()
        {
            var g = Grid(6, "300003");
            Assert.AreEqual(2, TowerRules.FindGroup(new[] { g }, 0, new CellCoord(0, 0)).Count);
        }

        [Test]
        public void ResolveShot_SameColour_Succeeds()
        {
            var g = Grid(6, "120000");
            var hit = new TowerHit(0, new CellCoord(0, 0), Vector3.zero, 0f, Vector2.zero);
            Assert.IsTrue(TowerRules.ResolveShot(new[] { g }, hit, 1, 0.25f, out _, out CellCoord cell));
            Assert.AreEqual(new CellCoord(0, 0), cell);
        }

        [Test]
        public void ResolveShot_NearMissOnMatchingNeighbour_IsForgiven()
        {
            var g = Grid(6, "120000");
            var edgeHit = new TowerHit(0, new CellCoord(0, 0), Vector3.zero, 0f, new Vector2(0.4f, 0.1f));
            Assert.IsTrue(TowerRules.ResolveShot(new[] { g }, edgeHit, 2, 0.25f, out _, out CellCoord cell));
            Assert.AreEqual(new CellCoord(1, 0), cell);

            var centreHit = new TowerHit(0, new CellCoord(0, 0), Vector3.zero, 0f, new Vector2(0.1f, 0f));
            Assert.IsFalse(TowerRules.ResolveShot(new[] { g }, centreHit, 2, 0.25f, out _, out _));
        }

        [Test]
        public void PickReachableColour_IgnoresCoveredInnerCubes_AndEndsWhenCleared()
        {
            var outer = new TowerGrid(8, 1);
            var inner = new TowerGrid(6, 1);
            for (int c = 0; c < 8; c++) outer[c, 0] = 1;
            for (int c = 0; c < 6; c++) inner[c, 0] = 4;
            var layers = new[] { outer, inner };
            var rng = new System.Random(1);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(1, TowerRules.PickReachableColour(layers, rng));

            for (int c = 0; c < 8; c++) outer[c, 0] = TowerGrid.Empty;
            Assert.AreEqual(4, TowerRules.PickReachableColour(layers, rng));
            for (int c = 0; c < 6; c++) inner[c, 0] = TowerGrid.Empty;
            Assert.AreEqual(-1, TowerRules.PickReachableColour(layers, rng));
        }

        [Test]
        public void FindUnsupported_PatchCutOffFromBaseAndPoleFalls_GroundedStays()
        {
            // rows top-down: outer column 0 is a grounded pillar; the 11 patch at columns 4-5 floats two floors up, and
            // the inner layer (on the pole) is empty, so nothing holds it
            var outer = Grid(8, "1...11..", "1...11..", "1.......", "1.......");
            var inner = Grid(6, "......", "......", "......", "......");
            var falling = TowerRules.FindUnsupported(new[] { outer, inner });
            Assert.AreEqual(4, falling.Count);
            Assert.IsTrue(falling.TrueForAll(x => x.layer == 0 && x.cell.Column >= 4));
        }

        [Test]
        public void FindUnsupported_BrokenBottom_TopHangsOnThePole()
        {
            // the outer layer's bottom floor is gone; its top ring touches one inner cube, which is fixed to the pole
            var outer = Grid(8, "11111111", "........");
            var inner = Grid(6, "..1...", "......");
            Assert.AreEqual(0, TowerRules.FindUnsupported(new[] { outer, inner }).Count);
        }

        [Test]
        public void FindUnsupported_AnyChainHolds_EvenHangingDown()
        {
            // outer column 0 hangs down three floors from a top cube that touches the pole layer: no support from below
            // and no sideways limit - connected is enough
            var outer = Grid(8, "1.......", "1.......", "1.......", "........");
            var inner = Grid(6, "1.....", "......", "......", "......");
            Assert.AreEqual(0, TowerRules.FindUnsupported(new[] { outer, inner }).Count);

            inner[0, 3] = TowerGrid.Empty;   // cut the link: the column falls (nothing on the base under it)
            Assert.AreEqual(3, TowerRules.FindUnsupported(new[] { outer, inner }).Count);
        }

        [Test]
        public void FindUnsupported_InnermostLayerNeverFalls()
        {
            var inner = Grid(6, "..1...", "......", "......");   // floating, but fixed to the pole
            Assert.AreEqual(0, TowerRules.FindUnsupported(new[] { Grid(8, "........", "........", "........"), inner }).Count);
            Assert.AreEqual(0, TowerRules.FindUnsupported(new[] { Grid(8, "...1....", "........") }).Count, "single layer = all on the pole");
        }

        [Test]
        public void TouchingColumns_IsSymmetric()
        {
            for (int outer = 0; outer < 26; outer++)
                foreach (int inner in TowerLayout.TouchingColumns(outer, 26, 20))
                    CollectionAssert.Contains(new List<int>(TowerLayout.TouchingColumns(inner, 20, 26)), outer);
            for (int inner = 0; inner < 20; inner++)
                foreach (int outer in TowerLayout.TouchingColumns(inner, 20, 26))
                    CollectionAssert.Contains(new List<int>(TowerLayout.TouchingColumns(outer, 26, 20)), inner);
        }

        [Test]
        public void Rescue_FindsNearbyExposedSameColour_NeverDeeper()
        {
            var outer = new TowerGrid(8, 3);
            var inner = new TowerGrid(6, 3);
            outer[0, 1] = 1;
            outer[1, 1] = 2;            // matching colour next to the touched cube
            inner[0, 1] = 2;            // matching colour behind it (deeper): never picked from layer 0
            var layers = new[] { outer, inner };
            Vector3 Centre(int l, CellCoord c) => new Vector3(c.Column, c.Floor, -l);

            Assert.IsTrue(TowerRules.Rescue(layers, 0, 2, new Vector3(0.6f, 1f, 0f), 1.2f, Centre, out int layer, out CellCoord cell));
            Assert.AreEqual(0, layer);
            Assert.AreEqual(new CellCoord(1, 1), cell);

            outer[1, 1] = TowerGrid.Empty;
            Assert.IsFalse(TowerRules.Rescue(layers, 0, 2, new Vector3(0.6f, 1f, 0f), 1.2f, Centre, out _, out _));
        }
    }
}
