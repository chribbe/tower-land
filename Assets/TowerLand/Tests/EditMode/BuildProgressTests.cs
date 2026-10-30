using System.Collections.Generic;
using NUnit.Framework;
using TowerLand.Core;
using TowerLand.Meta;
using UnityEngine;

namespace TowerLand.Tests
{
    public class BuildProgressTests
    {
        private static TowerShape Shape(int shaftRows = 10) => new TowerShape(0.3f, 3.7f, 2, 3, shaftRows, 2, 3, 3);

        [Test]
        public void Shape_SlotsGoUpRowByRow()
        {
            List<BrickSlot> slots = Shape().Generate();
            for (int i = 1; i < slots.Count; i++)
                Assert.GreaterOrEqual(slots[i].Position.y, slots[i - 1].Position.y - 1e-4f, $"slot {i} is below the one before it");
            Assert.AreEqual(0.15f, slots[0].Position.y, 1e-4f, "the first row sits on the platform");
            Assert.AreEqual(Shape().Height - 0.15f, slots[slots.Count - 1].Position.y, 1e-4f);
        }

        [Test]
        public void Shape_NoTwoBricksOverlap()
        {
            List<BrickSlot> slots = Shape(4).Generate();
            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                    Assert.Greater(Vector3.Distance(slots[i].Position, slots[j].Position), 0.25f, $"slots {i} and {j} overlap");
        }

        [Test]
        public void Shape_ShaftRowsSetTheSize()
        {
            int small = Shape(10).Generate().Count, big = Shape(11).Generate().Count;
            // One more shaft row = one more ring per wall layer (77 + 71 bricks at these radii).
            int ring = Mathf.RoundToInt(2f * Mathf.PI * 3.7f / 0.3f) + Mathf.RoundToInt(2f * Mathf.PI * 3.4f / 0.3f);
            Assert.AreEqual(ring, big - small, 6, "rows above shift their stagger, so merlon gaps may vary a little");
        }

        [Test]
        public void Shape_BattlementsLeaveGaps()
        {
            var flat = new TowerShape(0.3f, 3.7f, 2, 0, 0, 0, 1, 3).Generate().Count;
            int fullRings = Mathf.RoundToInt(2f * Mathf.PI * 4f / 0.3f) + Mathf.RoundToInt(2f * Mathf.PI * 3.7f / 0.3f);
            Assert.Less(flat, fullRings * 0.6f);
            Assert.Greater(flat, fullRings * 0.4f);
        }

        [Test]
        public void Bricks_DepositKeepsColoursInOrder()
        {
            var progress = new PlayerProgress(new MemoryStore());
            progress.Deposit(new[] { 3, 0, 7 });
            progress.Deposit(new[] { 1 });
            CollectionAssert.AreEqual(new[] { 3, 0, 7, 1 }, progress.Bricks());
            Assert.AreEqual(0, progress.BricksShown);
        }

        [Test]
        public void Bricks_CompletingCarriesTheRestOver()
        {
            var progress = new PlayerProgress(new MemoryStore());
            progress.Deposit(new[] { 0, 1, 2, 3, 4 });
            progress.BricksShown = 3;
            progress.CompleteConstruction(3);
            Assert.AreEqual(1, progress.Construction);
            CollectionAssert.AreEqual(new[] { 3, 4 }, progress.Bricks());
            Assert.AreEqual(0, progress.BricksShown);
        }

        [Test]
        public void Bricks_ShownNeverExceedsEarned()
        {
            var progress = new PlayerProgress(new MemoryStore());
            progress.Deposit(new[] { 2, 2 });
            progress.BricksShown = 10;
            Assert.AreEqual(2, progress.BricksShown);
        }

        [Test]
        public void Level_IsSaved()
        {
            var store = new MemoryStore();
            new PlayerProgress(store).Level = 4;
            Assert.AreEqual(4, new PlayerProgress(store).Level);
        }

        [Test]
        public void Sorting_GroupsColoursInOrder()
        {
            List<int> sorted = BrickSorting.Bands(new[] { 2, 0, 1, 2, 0, 2 }, c => c, -1);
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 2, 2, 2 }, sorted);
        }

        [Test]
        public void Sorting_StartsNearTheColourOnTop()
        {
            CollectionAssert.AreEqual(new[] { 3, 3, 1, 0 }, BrickSorting.Bands(new[] { 0, 3, 1, 3 }, c => c, 4));
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 3 }, BrickSorting.Bands(new[] { 0, 3, 1, 3 }, c => c, 0));
        }

        [Test]
        public void Sorting_RainbowStartsAtRedAndEndsAtPink()
        {
            float red = BrickSorting.RainbowKey(new Color(1f, 0.2f, 0.33f));
            float orange = BrickSorting.RainbowKey(new Color(1f, 0.45f, 0.06f));
            float blue = BrickSorting.RainbowKey(new Color(0.1f, 0.52f, 1f));
            float pink = BrickSorting.RainbowKey(new Color(1f, 0.42f, 0.78f));
            Assert.Less(red, orange);
            Assert.Less(orange, blue);
            Assert.Less(blue, pink);
        }
    }
}
