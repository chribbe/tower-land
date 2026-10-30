using System.Collections.Generic;
using NUnit.Framework;
using TowerLand.Core;
using TowerLand.UI;

namespace TowerLand.Tests
{
    public class DebugAccessTests
    {
        [Test]
        public void TapSequence_FiveQuickTaps_Complete()
        {
            var taps = new TapSequence(0.5f);
            for (int i = 0; i < 4; i++)
                Assert.IsFalse(taps.Tap(i * 0.3f, 5));
            Assert.IsTrue(taps.Tap(1.2f, 5));
            Assert.AreEqual(0, taps.Count, "the run restarts after completing");
        }

        [Test]
        public void TapSequence_SlowTap_StartsANewRun()
        {
            var taps = new TapSequence(0.5f);
            taps.Tap(0f, 5);
            taps.Tap(0.3f, 5);
            taps.Tap(0.6f, 5);
            taps.Tap(2f, 5);   // too slow: this is tap 1 of a new run
            Assert.AreEqual(1, taps.Count);
            Assert.IsTrue(taps.Tap(2.3f, 2), "the next quick tap continues the new run");
        }

        [Test]
        public void TapSequence_TwoTapsForDebuggers()
        {
            var taps = new TapSequence(0.5f);
            Assert.IsFalse(taps.Tap(10f, 2));
            Assert.IsTrue(taps.Tap(10.4f, 2));
        }

        [Test]
        public void Progress_DebuggerFlag_IsSavedAndReadBack()
        {
            var store = new MemoryStore();
            var progress = new PlayerProgress(store);
            Assert.IsFalse(progress.IsDebugger);
            progress.IsDebugger = true;
            Assert.AreEqual(1, store.Saves);
            Assert.IsTrue(new PlayerProgress(store).IsDebugger, "persists across instances (app restarts)");
        }
    }
}
