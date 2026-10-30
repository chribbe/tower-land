using NUnit.Framework;
using TowerLand.Feel;
using UnityEngine;

namespace TowerLand.Tests
{
    public class DebrisZoneTests
    {
        // Camera towards -z, like the Game scene; 6 m reach, 60° either side.
        private static readonly DebrisZone Zone = new DebrisZone(new Vector2(0f, -1f), 6f, 60f);

        [Test]
        public void Contains_PiecesInFrontWithinReach()
        {
            Assert.IsTrue(Zone.Contains(new Vector2(0f, -5f)));
            Assert.IsTrue(Zone.Contains(new Vector2(3f, -3f)), "45° off the camera axis");
            Assert.IsTrue(Zone.Contains(new Vector2(0f, -1f)), "on the base itself");
        }

        [Test]
        public void Excludes_SidesBackAndBeyondReach()
        {
            Assert.IsFalse(Zone.Contains(new Vector2(5f, -1f)), "~79° off axis");
            Assert.IsFalse(Zone.Contains(new Vector2(0f, 5f)), "behind the tower");
            Assert.IsFalse(Zone.Contains(new Vector2(0f, -7f)), "beyond reach");
        }

        [TestCase(0.01f, -5f)]
        [TestCase(2f, -4f)]
        [TestCase(-3.5f, -3f)]
        [TestCase(5f, 0.5f)]
        [TestCase(-4f, 2f)]
        public void EscapeDirection_LeadsRoundTowardsTheBack_NeverInwards(float x, float z)
        {
            var from = new Vector2(x, z);
            Vector2 push = Zone.EscapeDirection(from, 0.3f);
            Vector2 next = from + push * 0.1f;

            Assert.AreEqual(1f, push.magnitude, 1e-4f);
            Assert.Greater(Mathf.Abs(Zone.AngleOf(next)), Mathf.Abs(Zone.AngleOf(from)), "moves away from the camera axis");
            Assert.AreEqual(Mathf.Sign(Zone.AngleOf(from)), Mathf.Sign(Zone.AngleOf(next)), "stays on its side");
            Assert.Greater(next.magnitude, from.magnitude, "never pulled into the tower");
        }

        [Test]
        public void EscapeDirection_StraightOutBehindTheTower()
        {
            Vector2 push = Zone.EscapeDirection(new Vector2(0.001f, 5f), 0.3f);
            Assert.AreEqual(0f, push.x, 1e-3f);
            Assert.AreEqual(1f, push.y, 1e-3f);
        }

        [Test]
        public void Away_PointsStraightOutFromTheTowerAxis()
        {
            Vector2 away = Zone.Away(new Vector2(3f, -4f));
            Assert.AreEqual(0.6f, away.x, 1e-4f);
            Assert.AreEqual(-0.8f, away.y, 1e-4f);
            Assert.AreEqual(Zone.Front, Zone.Away(Vector2.zero), "on the axis: towards the camera");
        }
    }
}
