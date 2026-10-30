using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Tower
{
    /// <summary>
    /// Builds a cube with rounded (filleted) edges and corners: each face is a grid whose outer rows are bent
    /// around an inner box of half-size (size/2 - bevel). Normals are analytic, so the bevels shade smoothly
    /// and catch the glossy highlight that sells the "candy cube" look.
    /// </summary>
    public static class BeveledCubeMesh
    {
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        private static readonly Vector3[] FaceTangents =
        {
            Vector3.forward, Vector3.back, Vector3.right, Vector3.right, Vector3.left, Vector3.right,
        };

        /// <param name="size">Edge length of the cube.</param>
        /// <param name="bevel">Fillet radius, clamped to [0, size/2].</param>
        /// <param name="segments">Subdivisions of each fillet (1 = chamfer, 3-4 = smooth).</param>
        /// <param name="pillow">0 = flat faces; &gt;0 tilts shading normals outward from the cube centre so faces read
        /// slightly inflated (soft gradient across each face, highlight drifting towards the lit corner).</param>
        public static Mesh Create(float size = 1f, float bevel = 0.14f, int segments = 4, float pillow = 0f)
        {
            var mesh = new Mesh { name = "BeveledCube" };
            Fill(mesh, size, bevel, segments, pillow);
            return mesh;
        }

        /// <summary>Rebuilds <paramref name="mesh"/> in place (keeps the asset/GUID; safe when the vertex count changes).</summary>
        public static void Fill(Mesh mesh, float size = 1f, float bevel = 0.14f, int segments = 4, float pillow = 0f)
        {
            float half = size * 0.5f;
            bevel = Mathf.Clamp(bevel, 0f, half);
            segments = Mathf.Max(1, segments);
            float inner = half - bevel;

            List<float> steps = AxisSteps(half, inner, bevel, segments);
            int n = steps.Count;

            var vertices = new List<Vector3>(6 * n * n);
            var normals = new List<Vector3>(6 * n * n);
            var uvs = new List<Vector2>(6 * n * n);
            var colours = new List<Color>(6 * n * n);
            var triangles = new List<int>(6 * (n - 1) * (n - 1) * 6);

            for (int face = 0; face < 6; face++)
            {
                Vector3 normal = FaceNormals[face];
                Vector3 u = FaceTangents[face];
                // Unity is left-handed with clockwise front faces: cross(u, v) must equal -normal.
                Vector3 v = Vector3.Cross(u, normal);
                int start = vertices.Count;

                for (int j = 0; j < n; j++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 onBox = normal * half + u * steps[i] + v * steps[j];
                        Vector3 core = new Vector3(
                            Mathf.Clamp(onBox.x, -inner, inner),
                            Mathf.Clamp(onBox.y, -inner, inner),
                            Mathf.Clamp(onBox.z, -inner, inner));
                        Vector3 offset = onBox - core;
                        Vector3 vertexNormal = offset.sqrMagnitude > 1e-12f ? offset.normalized : normal;

                        Vector3 position = core + vertexNormal * bevel;
                        vertices.Add(position);
                        normals.Add(Vector3.Lerp(vertexNormal, position.normalized, pillow).normalized);
                        uvs.Add(new Vector2((steps[i] + half) / size, (steps[j] + half) / size));
                        colours.Add(BevelMask(vertexNormal));
                    }
                }

                for (int j = 0; j < n - 1; j++)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        int a = start + j * n + i;
                        int b = a + n;     // (i, j+1)
                        int c = b + 1;     // (i+1, j+1)
                        int d = a + 1;     // (i+1, j)
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(a); triangles.Add(c); triangles.Add(d);
                    }
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Vertex colour read by the cube shader: R = how far onto a rounded edge (0 flat face, 1 at 45°),
        /// G = how close to a rounded corner (1 on the corner diagonal). Lets highlights and occlusion target the bevels.
        /// </summary>
        private static Color BevelMask(Vector3 normal)
        {
            float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
            float max = Mathf.Max(ax, Mathf.Max(ay, az));
            float min = Mathf.Min(ax, Mathf.Min(ay, az));
            const float edgeMax = 1f - 0.70710678f;   // 1 - cos 45°
            const float cornerMin = 0.57735027f;      // 1 / sqrt 3
            float edge = Mathf.Clamp01((1f - max) / edgeMax);
            float corner = Mathf.Clamp01(min / cornerMin);
            return new Color(edge, corner, 0f, 1f);
        }

        /// <summary>
        /// Face coordinates along one axis. Fillet samples are spaced by equal angle (tan mapping) so the
        /// curvature is evenly tessellated after projection onto the fillet.
        /// </summary>
        private static List<float> AxisSteps(float half, float inner, float bevel, int segments)
        {
            var steps = new List<float>();
            if (bevel <= 0f)
            {
                steps.Add(-half);
                steps.Add(half);
                return steps;
            }

            for (int s = segments; s >= 0; s--)
                steps.Add(-(inner + bevel * Mathf.Tan(Mathf.PI * 0.25f * s / segments)));
            for (int s = 0; s <= segments; s++)
                steps.Add(inner + bevel * Mathf.Tan(Mathf.PI * 0.25f * s / segments));
            return steps;
        }
    }
}
