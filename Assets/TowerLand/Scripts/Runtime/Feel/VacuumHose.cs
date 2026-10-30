using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>
    /// The win-sequence vacuum: a flexible, corrugated glass hose hanging from above the screen, ending in the nozzle
    /// (<c>Art/Models/Vacuum/Vacuum_Nozzle.fbx</c>, modelled in <c>ArtSource/Blender/Vacuum.blend</c>). The hose is a chain of
    /// spring-driven joints chasing a rest curve from the anchor to the nozzle, plus a corkscrew twist; a tube mesh is
    /// rebuilt through them every step, swelling where pieces travel inside. Driven by its owner via <see cref="Step"/>.
    /// </summary>
    public sealed class VacuumHose : MonoBehaviour
    {
        private const int Sides = 18;
        private const int RingsPerSegment = 8;
        private const int LoadBins = 64;
        // The Blender nozzle is modelled for a 0.55 m hose: 1.6 m from the hose connection (collar top) to the mouth plane.
        private const float ModelHoseRadius = 0.55f;
        private const float ModelNozzleLength = 1.6f;
        // Inner radius of the nozzle's glass at the mouth plane (outer 1.42).
        private const float ModelMouthRadius = 1.32f;

        private VacuumSettings _settings;
        private Transform _nozzle;
        private Transform[] _rings;
        private Mesh _mesh;

        private Vector3[] _joints, _velocities;
        private Vector3 _anchor, _mouth, _mouthDirection = Vector3.down;
        private float _phase;
        private float _gulp, _gulpVelocity;

        // Centreline (ring 0 at the anchor, last ring at the nozzle collar), rebuilt every step.
        private Vector3[] _centre, _normal, _binormal;
        private float[] _arc;
        private float[] _load = new float[LoadBins], _swell = new float[LoadBins];
        private Vector3[] _vertices, _vertexNormals;

        /// <summary>Centre of the mouth opening.</summary>
        public Vector3 Mouth => _mouth;
        /// <summary>Direction the mouth faces (out of the nozzle).</summary>
        public Vector3 MouthDirection => _mouthDirection;
        /// <summary>Distance from the mouth, up the nozzle and the hose, to the anchor (off screen).</summary>
        public float TravelLength => NozzleLength + (_arc != null ? _arc[_arc.Length - 1] : 0f);
        /// <summary>Nozzle and joint rings are scaled with the hose radius.</summary>
        private float ModelScale => _settings.HoseRadius / ModelHoseRadius;
        /// <summary>Distance from the hose connection (collar top) to the mouth plane, metres.</summary>
        public float NozzleLength => ModelNozzleLength * ModelScale;
        /// <summary>Inner radius of the mouth opening, metres.</summary>
        public float MouthRadius => ModelMouthRadius * ModelScale;
        /// <summary>0..1: how hard the hose corkscrews (it twists while sucking).</summary>
        public float Twist { get; set; }

        public static VacuumHose Create(VacuumSettings settings, Material glass, GameObject nozzlePrefab, GameObject ringPrefab,
            Vector3 anchor, Vector3 mouth)
        {
            var go = new GameObject("VacuumHose");
            var hose = go.AddComponent<VacuumHose>();
            hose._settings = settings;
            hose._anchor = anchor;
            hose._mouth = mouth;

            hose._mesh = new Mesh { name = "VacuumHose" };
            hose._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = hose._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glass;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (nozzlePrefab != null)
            {
                hose._nozzle = Instantiate(nozzlePrefab, go.transform).transform;
                foreach (Renderer r in hose._nozzle.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            int joints = settings.Joints;
            hose._rings = new Transform[ringPrefab != null ? joints - 2 : 0];
            for (int i = 0; i < hose._rings.Length; i++)
                hose._rings[i] = Instantiate(ringPrefab, go.transform).transform;

            hose._joints = new Vector3[joints];
            hose._velocities = new Vector3[joints];
            hose.RestCurve(hose._joints);
            hose.BuildTopology();
            hose.Rebuild();
            return hose;
        }

        /// <summary>Where the mouth should be, and which way it faces.</summary>
        public void Aim(Vector3 mouth, Vector3 direction)
        {
            _mouth = mouth;
            _mouthDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.down;
        }

        /// <summary>A piece went down the throat: the nozzle squashes and springs back.</summary>
        public void Gulp(float amount) => _gulpVelocity += amount;

        public void ClearLoad() => System.Array.Clear(_load, 0, LoadBins);

        /// <summary>A piece is travelling inside the hose, <paramref name="distance"/> metres from the mouth: it swells there.</summary>
        public void AddLoad(float distance)
        {
            float hose = distance - NozzleLength;
            if (hose < 0f || _arc == null) return;
            float length = _arc[_arc.Length - 1];
            int bin = Mathf.Clamp((int)((1f - hose / Mathf.Max(length, 0.01f)) * (LoadBins - 1)), 0, LoadBins - 1);
            _load[bin] += 1f;
        }

        /// <summary>
        /// A point inside the hose, <paramref name="distance"/> metres from the mouth (up the nozzle, then along the hose),
        /// offset from the centreline by <paramref name="offset"/> (0..1 of the radius) at <paramref name="angle"/> radians.
        /// </summary>
        public Vector3 PointInside(float distance, float angle, float offset)
        {
            float radius = _settings.HoseRadius * offset;
            if (distance <= NozzleLength)
            {
                // The nozzle funnels from the wide mouth down to the hose (roughly: it flares near the mouth).
                float funnel = 1f - Mathf.Pow(1f - distance / NozzleLength, 2f);
                return MouthPoint(distance, angle, Mathf.Lerp(MouthRadius, _settings.HoseRadius, funnel) * offset);
            }

            float fromAnchor = _arc[_arc.Length - 1] - (distance - NozzleLength);
            if (fromAnchor <= 0f) return _centre[0];
            int i = 1;
            while (i < _arc.Length - 1 && _arc[i] < fromAnchor) i++;
            float t = Mathf.InverseLerp(_arc[i - 1], _arc[i], fromAnchor);
            Vector3 c = Vector3.Lerp(_centre[i - 1], _centre[i], t);
            Vector3 n = Vector3.Lerp(_normal[i - 1], _normal[i], t);
            Vector3 b = Vector3.Lerp(_binormal[i - 1], _binormal[i], t);
            return c + (n * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * radius;
        }

        /// <summary>
        /// A point in the mouth's frame: <paramref name="along"/> metres up its axis (negative = out in front of it),
        /// <paramref name="radius"/> metres from the axis at <paramref name="angle"/> radians - same angles as <see cref="PointInside"/>.
        /// </summary>
        public Vector3 MouthPoint(float along, float angle, float radius)
        {
            Vector3 axis = -_mouthDirection;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, axis);
            return _mouth + axis * along + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
        }

        /// <summary>Inverse of <see cref="MouthPoint"/>: (along, angle, radius) of a world point.</summary>
        public Vector3 ToMouthFrame(Vector3 point)
        {
            Vector3 axis = -_mouthDirection;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, axis);
            Vector3 d = point - _mouth;
            float x = Vector3.Dot(d, side), y = Vector3.Dot(d, up);
            return new Vector3(Vector3.Dot(d, axis), Mathf.Atan2(y, x), Mathf.Sqrt(x * x + y * y));
        }

        /// <summary>Advances the joint springs, the twist and the gulp, then rebuilds the tube.</summary>
        public void Step(float deltaTime)
        {
            _phase += deltaTime * _settings.TwistSpeed;
            var rest = new Vector3[_joints.Length];
            RestCurve(rest);

            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / (1f / 120f)));
            float dt = deltaTime / steps;
            for (int s = 0; s < steps; s++)
            {
                for (int i = 1; i < _joints.Length - 1; i++)
                {
                    // Stiffer towards the anchor: the top hangs calm, the lower hose does the moving.
                    float stiffness = _settings.Stiffness * Mathf.Lerp(3f, 1f, i / (_joints.Length - 1f));
                    Vector3 acceleration = (rest[i] - _joints[i]) * stiffness - _velocities[i] * _settings.Damping;
                    _velocities[i] += acceleration * dt;
                    _joints[i] += _velocities[i] * dt;
                }
                _gulpVelocity += (-_gulp * 260f - _gulpVelocity * 14f) * dt;
                _gulp += _gulpVelocity * dt;
            }
            _joints[0] = rest[0];
            _joints[_joints.Length - 1] = rest[_joints.Length - 1];

            for (int i = 0; i < LoadBins; i++)
            {
                float around = _load[i] + 0.5f * (_load[Mathf.Max(i - 1, 0)] + _load[Mathf.Min(i + 1, LoadBins - 1)]);
                float target = _settings.Bulge * (1f - Mathf.Exp(-around * 0.35f));
                _swell[i] = Mathf.Lerp(_swell[i], target, 1f - Mathf.Exp(-deltaTime * 18f));
            }
            Rebuild();
        }

        /// <summary>
        /// The shape the hose relaxes towards: a cubic Bezier from the anchor (leaving straight down) to the collar (arriving
        /// along the mouth direction), plus a corkscrew that fades out at both ends.
        /// </summary>
        private void RestCurve(Vector3[] points)
        {
            Vector3 end = _mouth - _mouthDirection * NozzleLength;
            float span = Vector3.Distance(_anchor, end);
            Vector3 p1 = _anchor + Vector3.down * (span * 0.45f);
            Vector3 p2 = end - _mouthDirection * (span * 0.4f);
            Vector3 chord = (end - _anchor).normalized;
            Vector3 u = Vector3.Cross(chord, Mathf.Abs(chord.y) < 0.95f ? Vector3.up : Vector3.forward).normalized;
            Vector3 w = Vector3.Cross(chord, u);
            int n = points.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float k = 1f - t;
                Vector3 p = k * k * k * _anchor + 3f * k * k * t * p1 + 3f * k * t * t * p2 + t * t * t * end;
                // Twist grows towards the nozzle (nothing at the anchor), pinned again at the very end.
                float amplitude = Twist * _settings.TwistAmplitude * Mathf.Sin(Mathf.PI * t) * t * 1.5f;
                float angle = 2f * Mathf.PI * (_phase + t * 1.25f);
                points[i] = p + (u * Mathf.Cos(angle) + w * Mathf.Sin(angle)) * amplitude;
            }
        }

        private void BuildTopology()
        {
            int rings = (_joints.Length - 1) * RingsPerSegment + 1;
            _centre = new Vector3[rings];
            _normal = new Vector3[rings];
            _binormal = new Vector3[rings];
            _arc = new float[rings];
            _vertices = new Vector3[rings * Sides];
            _vertexNormals = new Vector3[rings * Sides];
            var triangles = new int[(rings - 1) * Sides * 6];
            int k = 0;
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * Sides + s, b = r * Sides + (s + 1) % Sides, c = a + Sides, d = b + Sides;
                    // Clockwise seen from outside (Unity front faces).
                    triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                    triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
                }
            _mesh.vertices = _vertices;
            _mesh.normals = _vertexNormals;
            _mesh.triangles = triangles;
        }

        private void Rebuild()
        {
            int rings = _centre.Length;
            // Centreline: Catmull-Rom through the joints.
            for (int r = 0; r < rings; r++)
            {
                int segment = Mathf.Min(r / RingsPerSegment, _joints.Length - 2);
                float t = (r - segment * RingsPerSegment) / (float)RingsPerSegment;
                Vector3 p0 = _joints[Mathf.Max(segment - 1, 0)], p1 = _joints[segment];
                Vector3 p2 = _joints[segment + 1], p3 = _joints[Mathf.Min(segment + 2, _joints.Length - 1)];
                _centre[r] = 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
                                     + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
                _arc[r] = r == 0 ? 0f : _arc[r - 1] + Vector3.Distance(_centre[r - 1], _centre[r]);
            }

            // Frames: parallel transport down the hose (no sudden flips while it twists).
            Vector3 tangent = (_centre[1] - _centre[0]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Vector3.forward);
            if (normal.sqrMagnitude < 1e-4f) normal = Vector3.Cross(tangent, Vector3.right);
            normal.Normalize();
            float length = _arc[rings - 1];
            float radius = _settings.HoseRadius;
            for (int r = 0; r < rings; r++)
            {
                Vector3 next = (_centre[Mathf.Min(r + 1, rings - 1)] - _centre[Mathf.Max(r - 1, 0)]).normalized;
                normal = (Quaternion.FromToRotation(tangent, next) * normal).normalized;
                tangent = next;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                _normal[r] = normal;
                _binormal[r] = binormal;

                float fromAnchor01 = _arc[r] / Mathf.Max(length, 0.01f);
                float swell = _swell[Mathf.Clamp((int)(fromAnchor01 * (LoadBins - 1)), 0, LoadBins - 1)];
                float rib = 1f + _settings.RibDepth * Mathf.Sin(_arc[r] * _settings.RibsPerMetre * 2f * Mathf.PI);
                float ringRadius = radius * rib * (1f + swell);
                for (int s = 0; s < Sides; s++)
                {
                    float angle = s * 2f * Mathf.PI / Sides;
                    Vector3 radial = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    _vertices[r * Sides + s] = _centre[r] + radial * ringRadius;
                    _vertexNormals[r * Sides + s] = radial;
                }
            }
            _mesh.vertices = _vertices;
            _mesh.normals = _vertexNormals;
            _mesh.RecalculateBounds();

            // Articulation rings on the inner joints.
            for (int i = 0; i < _rings.Length; i++)
            {
                int r = (i + 1) * RingsPerSegment;
                Vector3 along = (_centre[Mathf.Min(r + 1, rings - 1)] - _centre[r - 1]).normalized;
                float swell = _swell[Mathf.Clamp((int)(_arc[r] / Mathf.Max(length, 0.01f) * (LoadBins - 1)), 0, LoadBins - 1)];
                _rings[i].SetPositionAndRotation(_centre[r], Quaternion.FromToRotation(Vector3.up, along));
                _rings[i].localScale = Vector3.one * (ModelScale * (1f + swell));
            }

            if (_nozzle != null)
            {
                Vector3 end = _centre[rings - 1];
                _nozzle.SetPositionAndRotation(end, Quaternion.FromToRotation(Vector3.down, _mouthDirection));
                float g = _gulp;
                _nozzle.localScale = new Vector3(1f + g, 1f - g * 0.7f, 1f + g) * ModelScale;
            }
        }
    }
}
