using NUnit.Framework;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Tests
{
    public class BeveledCubeMeshTests
    {
        [Test]
        public void Bounds_MatchSize()
        {
            Mesh mesh = BeveledCubeMesh.Create(1f, 0.14f, 4);
            Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(1f).Within(1e-4f));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Triangles_FaceOutward()
        {
            Mesh mesh = BeveledCubeMesh.Create(1f, 0.14f, 4);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                if (faceNormal.sqrMagnitude < 1e-12f) continue; // degenerate corner sliver
                Vector3 centroid = (a + b + c) / 3f;
                Assert.Greater(Vector3.Dot(faceNormal, centroid), 0f, $"Triangle {t / 3} faces inward");
            }

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Normals_AreUnitLength()
        {
            Mesh mesh = BeveledCubeMesh.Create(1f, 0.14f, 4);
            foreach (Vector3 normal in mesh.normals)
                Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Object.DestroyImmediate(mesh);
        }
    }
}
