using NUnit.Framework;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Tests
{
    public class TowerRaycastTests
    {
        private static float Face(int layer) => layer == 0 ? 4f : 3f;

        [Test]
        public void FrontalRay_HitsColumnZero_AtTheRightFloor()
        {
            var outer = new TowerGrid(8, 4);
            outer[0, 1] = 2;
            var layers = new[] { outer };

            bool hit = TowerRaycast.Cast(layers, Face, 1f, new Vector3(0, 1.5f, 10), new Vector3(0, 1.5f, -10), out TowerHit h);

            Assert.IsTrue(hit);
            Assert.AreEqual(new CellCoord(0, 1), h.Cell);
            Assert.That(h.LocalPoint.z, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(h.Offset.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(h.Offset.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ThroughAHole_HitsTheFarSideFromInside()
        {
            // Column 0 faces +Z, column 4 (of 8) faces -Z. Front cube missing: the ray crosses the hollow core and meets
            // the far cube's back face (front radius 4, depth 1 -> back at radius 3).
            var outer = new TowerGrid(8, 4);
            outer[4, 1] = 3;
            var layers = new[] { outer };
            var from = new Vector3(0, 1.5f, 10);
            var to = new Vector3(0, 1.5f, -10);

            Assert.IsFalse(TowerRaycast.Cast(layers, Face, 1f, from, to, out _), "fronts only without a cube depth");
            Assert.IsTrue(TowerRaycast.Cast(layers, Face, 1f, from, to, out TowerHit h, 0f, 1f));
            Assert.AreEqual(new CellCoord(4, 1), h.Cell);
            Assert.IsTrue(h.Inside);
            Assert.That(h.LocalPoint.z, Is.EqualTo(-3f).Within(1e-4f));

            outer[0, 1] = 1;   // the near cube is back: it is hit first, from outside
            Assert.IsTrue(TowerRaycast.Cast(layers, Face, 1f, from, to, out h, 0f, 1f));
            Assert.AreEqual(new CellCoord(0, 1), h.Cell);
            Assert.IsFalse(h.Inside);
        }

        [Test]
        public void ThroughHolesInTwoLayers_HitsTheInnermostFarCubeFirst()
        {
            // Outer: front 4, back 3. Inner: front 3, back 2. Inner column 3 (of 6) and outer column 4 (of 8) face -Z.
            var outer = new TowerGrid(8, 4);
            var inner = new TowerGrid(6, 4);
            outer[4, 2] = 1;
            inner[3, 2] = 2;
            var layers = new[] { outer, inner };
            var from = new Vector3(0, 2.5f, 10);
            var to = new Vector3(0, 2.5f, -10);

            Assert.IsTrue(TowerRaycast.Cast(layers, Face, 1f, from, to, out TowerHit h, 0f, 1f));
            Assert.AreEqual(1, h.Layer);
            Assert.IsTrue(h.Inside);
            Assert.That(h.LocalPoint.z, Is.EqualTo(-2f).Within(1e-4f));

            inner[3, 2] = TowerGrid.Empty;
            Assert.IsTrue(TowerRaycast.Cast(layers, Face, 1f, from, to, out h, 0f, 1f));
            Assert.AreEqual(0, h.Layer);
            Assert.AreEqual(new CellCoord(4, 2), h.Cell);
        }

        [Test]
        public void OffAxisChord_InsideTheBand_HitsTheCubeItSlidesInto()
        {
            // A chord at x = 3.4 enters through an empty column (1) and never gets inside the back cylinder (radius 3) - it stays in
            // the band (3..4) and slides into the far cubes from the side. Fronts / backs alone miss it.
            var outer = new TowerGrid(8, 4);
            outer[3, 1] = 2;   // column 3 sits at 135 degrees: on the chord's way out
            var from = new Vector3(3.4f, 1.5f, 10f);
            var to = new Vector3(3.4f, 1.5f, -10f);
            Assert.IsTrue(TowerRaycast.Cast(new[] { outer }, Face, 1f, from, to, out TowerHit h, 0f, 1f));
            Assert.AreEqual(new CellCoord(3, 1), h.Cell);
        }

        [Test]
        public void ThroughAHolePath_PassesTheEntry_AlongTheAimRay()
        {
            var start = new Vector3(2f, 0.5f, -20f);
            var via = new Vector3(0f, 6f, -6f);
            var direction = new Vector3(0f, -0.1f, 1f).normalized;
            var target = via + direction * 8f;
            var path = new TowerLand.Gameplay.AimPath(start, via, direction, target, 0.8f);

            Assert.That(Vector3.Distance(path.Evaluate(0f), start), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(path.Evaluate(1f), target), Is.LessThan(1e-4f));
            // Straight along the ray after the entry: every point past it lies on the ray.
            for (float t = 0.7f; t <= 1f; t += 0.05f)
            {
                Vector3 p = path.Evaluate(t) - via;
                if (Vector3.Dot(p, direction) < 0f) continue;   // still on the curve
                Assert.That(Vector3.Cross(p, direction).magnitude, Is.LessThan(1e-3f));
            }
            // Arrives along the ray (smooth): the curve's tangent at the entry points along the aim direction.
            Vector3 before = path.Evaluate(0.999f * Mathf.Clamp01(Vector3.Distance(start, via) * 1.15f / (Vector3.Distance(start, via) * 1.15f + 8f)));
            Assert.That(Vector3.Angle(via - before, direction), Is.LessThan(5f));
        }

        [Test]
        public void FatProjectile_ThroughAHole_TouchesTheFarBackEarlier()
        {
            var outer = new TowerGrid(8, 4);
            outer[4, 1] = 3;
            Assert.IsTrue(TowerRaycast.Cast(new[] { outer }, Face, 1f, new Vector3(0, 1.5f, 10), new Vector3(0, 1.5f, -10), out TowerHit h, 0.4f, 1f));
            Assert.IsTrue(h.Inside);
            Assert.That(h.LocalPoint.z, Is.EqualTo(-3f).Within(1e-4f), "contact on the back face, sphere centre 0.4 inside");
        }

        [Test]
        public void EmptyOuterCell_LetsTheRayReachTheInnerLayer()
        {
            var outer = new TowerGrid(8, 4);
            var inner = new TowerGrid(6, 4);
            inner[0, 2] = 1;
            var layers = new[] { outer, inner };

            Assert.IsTrue(TowerRaycast.Cast(layers, Face, 1f, new Vector3(0, 2.5f, 10), new Vector3(0, 2.5f, -10), out TowerHit h));
            Assert.AreEqual(1, h.Layer);
            Assert.That(h.LocalPoint.z, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void RayAboveTheTower_Misses()
        {
            var outer = new TowerGrid(8, 4);
            for (int c = 0; c < 8; c++) outer[c, 3] = 0;
            Assert.IsFalse(TowerRaycast.Cast(new[] { outer }, Face, 1f, new Vector3(0, 6f, 10), new Vector3(0, 6f, -10), out _));
        }

        [Test]
        public void SideRay_HitsTheColumnFacingIt()
        {
            var outer = new TowerGrid(8, 4);
            outer[2, 0] = 3;   // column 2 of 8 = 90 degrees = facing +X
            Assert.IsTrue(TowerRaycast.Cast(new[] { outer }, Face, 1f, new Vector3(10, 0.5f, 0), new Vector3(-10, 0.5f, 0), out TowerHit h));
            Assert.AreEqual(new CellCoord(2, 0), h.Cell);
        }

        [Test]
        public void AimPathOvershoot_GoesThroughATargetOnTheSurface()
        {
            var outer = new TowerGrid(8, 4);
            outer[0, 1] = 2;
            var path = new TowerLand.Gameplay.AimPath(new Vector3(0, 0.5f, 20), new Vector3(0, 1.5f, 4f), 2f);
            Assert.IsFalse(TowerRaycast.Cast(new[] { outer }, Face, 1f, path.Evaluate(0.95f), path.Evaluate(1f) + Vector3.forward * 1e-3f, out _),
                "a segment ending just short of the face misses");
            Assert.IsTrue(TowerRaycast.Cast(new[] { outer }, Face, 1f, path.Evaluate(0.95f), path.Evaluate(1.1f), out TowerHit h));
            Assert.AreEqual(new CellCoord(0, 1), h.Cell);
        }

        [Test]
        public void SegmentStoppingShortOfTheTower_Misses()
        {
            var outer = new TowerGrid(8, 4);
            outer[0, 1] = 2;
            Assert.IsFalse(TowerRaycast.Cast(new[] { outer }, Face, 1f, new Vector3(0, 1.5f, 10), new Vector3(0, 1.5f, 6), out _));
        }

        [Test]
        public void FatProjectile_GrazingTheEdgeOfANeighbour_HitsIt()
        {
            var outer = new TowerGrid(8, 4);
            outer[0, 2] = 5;   // floor 2 spans y 2..3; a ray at y 1.8 passes just under it
            var layers = new[] { outer };
            Assert.IsFalse(TowerRaycast.Cast(layers, Face, 1f, new Vector3(0, 1.8f, 10), new Vector3(0, 1.8f, -10), out _));
            Assert.IsTrue(TowerRaycast.Cast(layers, Face, 1f, new Vector3(0, 1.8f, 10), new Vector3(0, 1.8f, -10), out TowerHit h, 0.3f));
            Assert.AreEqual(new CellCoord(0, 2), h.Cell);
        }
    }
}
