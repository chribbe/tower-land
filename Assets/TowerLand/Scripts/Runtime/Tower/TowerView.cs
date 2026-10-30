using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Tower
{
    /// <summary>
    /// Renders a tower as concentric cylinders of cubes (one <see cref="TowerGrid"/> per layer, outermost first) and spins it.
    /// Works in edit mode too (for level previews); edit-mode objects are never saved into the scene.
    /// In the editor, edits to the cube material or palette are pushed live to every cube (also during Play mode).
    /// </summary>
    [ExecuteAlways]
    public sealed class TowerView : MonoBehaviour
    {
        [SerializeField] private Mesh _cubeMesh;
        [SerializeField] private Material _cubeMaterial;
        [SerializeField] private CubePalette _palette;
        [SerializeField] private TowerVisualSettings _settings;
        [SerializeField] private Feel.ImpactEffects _effects;
        [SerializeField] private Feel.DebrisSweeper _debris;
        [Tooltip("Material of the central pole the innermost layer hangs on.")]
        [SerializeField] private Material _poleMaterial;
        [Tooltip("Twisted pole piece (radius 1, tiles vertically), stacked up the tower; scaled by PoleRadius. Empty = plain cylinder.")]
        [SerializeField] private Mesh _poleSegment;
        [Tooltip("Collar + ball on top of the pole (radius-1 units, origin = top of the pole).")]
        [SerializeField] private Mesh _poleCap;
        [SerializeField] private Material _poleCapMaterial;

        private const string SpinRootName = "Cubes";

        private Transform _spinRoot;
        private Material[] _colourMaterials;   // exposed variants
        private Material[] _coveredMaterials;  // same colours, darkened: cubes hidden behind an outer layer
        private Material[] _looseMaterials;    // cubes outside the tower (projectile, shards): no crevice/form shading
        private Material[] _highlightMaterials; // aim preview: the patch a release would break, brighter
        private readonly Dictionary<(int layer, CellCoord cell), GameObject> _cubes = new Dictionary<(int, CellCoord), GameObject>();
        private TowerGrid[] _layers = System.Array.Empty<TowerGrid>();
        private readonly List<int> _builtColours = new List<int>();

        public IReadOnlyList<TowerGrid> Layers => _layers;
        /// <summary>Colour of every cube the tower was built with (the level's haul once it is cleared).</summary>
        public IReadOnlyList<int> BuiltColours => _builtColours;
        /// <summary>Raised once (play mode) when the last cube of the tower has broken off or fallen.</summary>
        public event System.Action Cleared;
        private bool _clearedRaised;
        /// <summary>Outermost layer.</summary>
        public TowerGrid Grid => _layers.Length > 0 ? _layers[0] : null;
        public float Radius => RadiusOf(0);
        public float Height => Grid == null ? 0f : Grid.Floors * _settings.CubeSpacing;

        public float RadiusOf(int layer) => layer < _layers.Length ? _settings.RadiusFor(_layers[layer].Columns) : 0f;

        public void Build(TowerGrid grid) => Build(new[] { grid });

        public void Build(IReadOnlyList<TowerGrid> layers)
        {
            Clear();
            _clearedRaised = false;
            _builtColours.Clear();
            _layers = new TowerGrid[layers.Count];
            for (int i = 0; i < layers.Count; i++) _layers[i] = layers[i];
            EnsureMaterials();

            _spinRoot = new GameObject(SpinRootName).transform;
            _spinRoot.SetParent(transform, false);
            if (!Application.isPlaying)
                _spinRoot.gameObject.hideFlags = HideFlags.DontSave;

            for (int layer = 0; layer < _layers.Length; layer++)
            {
                TowerGrid grid = _layers[layer];
                for (int floor = 0; floor < grid.Floors; floor++)
                {
                    for (int column = 0; column < grid.Columns; column++)
                    {
                        int colour = grid[column, floor];
                        if (colour == TowerGrid.Empty)
                            continue;

                        var cell = new CellCoord(column, floor);
                        _cubes[(layer, cell)] = CreateCube(layer, cell, colour);
                        _builtColours.Add(colour);
                    }
                }
            }

            BuildExposureMeshes();
            foreach ((int layer, CellCoord cell) key in _cubes.Keys)
                RefreshExposure(key);
            RefreshCoverage();
            CreatePole();
        }

        /// <summary>
        /// The pole up the tower's axis, from the base to just above the top floor: the innermost layer hangs on it
        /// (<see cref="TowerRules.FindUnsupported"/>), so it is what holds the tower up once its bottom is broken.
        /// </summary>
        private void CreatePole()
        {
            float height = 0f;
            foreach (TowerGrid grid in _layers) height = Mathf.Max(height, grid.Floors * _settings.CubeSpacing);
            if (height <= 0f) return;
            height += _settings.CubeSpacing * 0.3f;
            float radius = _settings.PoleRadius;
            if (_poleSegment == null)
            {
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                DestroySafe(pole.GetComponent<Collider>());
                pole.transform.SetParent(_spinRoot, false);
                pole.transform.localPosition = Vector3.up * (height * 0.5f);
                pole.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);   // the primitive is 2 m tall
                if (_poleMaterial != null) pole.GetComponent<MeshRenderer>().sharedMaterial = _poleMaterial;
                return;
            }

            // Whole segments only (the twist tiles seamlessly), stretched a little to land exactly on the top.
            float segment = _poleSegment.bounds.size.y * radius;
            int count = Mathf.Max(1, Mathf.CeilToInt(height / segment - 0.25f));
            float stretch = height / (count * segment);
            for (int i = 0; i < count; i++)
                CreatePolePiece($"Pole_{i}", _poleSegment, _poleMaterial, i * segment * stretch, new Vector3(radius, radius * stretch, radius));
            if (_poleCap != null)
                CreatePolePiece("PoleCap", _poleCap, _poleCapMaterial != null ? _poleCapMaterial : _poleMaterial, height, Vector3.one * radius);
        }

        private void CreatePolePiece(string name, Mesh mesh, Material material, float y, Vector3 scale)
        {
            var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(_spinRoot, false);
            piece.transform.localPosition = Vector3.up * y;
            piece.transform.localScale = scale;
            piece.GetComponent<MeshFilter>().sharedMesh = mesh;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------------- exposed sides
        // The cube shader darkens a cube's sides as if they sat in the seam against a neighbour. Once a neighbour is gone
        // that side is exposed and must read as the cube's own colour: each cube uses one of 32 variants of the cube mesh
        // carrying its open-sides mask in UV1 (shared meshes keep SRP batching intact). The back (the face towards the
        // tower axis) is open when no cube of the next inner layer sits behind it: through a hole in the tower that face
        // is seen from inside, and must not keep the seam / covered / form shadows meant for the outside.
        private const int OpenLeft = 1, OpenRight = 2, OpenTop = 4, OpenBottom = 8, OpenBack = 16, ExposureVariants = 32;
        private Mesh[] _exposureMeshes;
        // Cubes already removed from the grid but still on screen, waiting for the ripple to break them: still neighbours.
        private readonly HashSet<(int, CellCoord)> _breaking = new HashSet<(int, CellCoord)>();

        private void BuildExposureMeshes()
        {
            DestroyExposureMeshes();
            if (_cubeMesh == null) return;
            _exposureMeshes = new Mesh[ExposureVariants];
            var uv = new Vector2[_cubeMesh.vertexCount];
            for (int mask = 0; mask < ExposureVariants; mask++)
            {
                Mesh mesh = Instantiate(_cubeMesh);
                mesh.name = $"{_cubeMesh.name} open{mask}";
                mesh.hideFlags = HideFlags.DontSave;
                for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(mask, 0f);
                mesh.uv2 = uv;
                _exposureMeshes[mask] = mesh;
            }
        }

        private void DestroyExposureMeshes()
        {
            if (_exposureMeshes == null) return;
            foreach (Mesh mesh in _exposureMeshes)
                if (mesh != null) DestroySafe(mesh);
            _exposureMeshes = null;
        }

        private bool HasNeighbour(int layer, CellCoord cell)
        {
            TowerGrid grid = _layers[layer];
            if (cell.Floor < 0) return true;              // the plinth
            if (cell.Floor >= grid.Floors) return false;  // open sky
            return grid[cell] != TowerGrid.Empty || _breaking.Contains((layer, cell));
        }

        /// <summary>
        /// Whether the next inner layer fills everything right behind <paramref name="cell"/> (same floor, every inner cube
        /// overlapping its angular span): a gap behind any part of it opens its back.
        /// </summary>
        private bool HasCubeBehind(int layer, CellCoord cell)
        {
            int inner = layer + 1;
            if (inner >= _layers.Length) return false;   // innermost layer: the hollow core
            int columns = _layers[layer].Columns, innerColumns = _layers[inner].Columns;
            for (int column = 0; column < innerColumns; column++)
                if (TowerLayout.Overlaps(cell.Column, columns, column, innerColumns) && !HasNeighbour(inner, new CellCoord(column, cell.Floor)))
                    return false;
            return true;
        }

        private void RefreshExposure((int layer, CellCoord cell) key)
        {
            if (_exposureMeshes == null || !_cubes.TryGetValue(key, out GameObject cube) || cube == null) return;
            TowerGrid grid = _layers[key.layer];
            CellCoord c = key.cell;
            int mask = 0;
            if (!HasNeighbour(key.layer, new CellCoord(grid.WrapColumn(c.Column - 1), c.Floor))) mask |= OpenLeft;
            if (!HasNeighbour(key.layer, new CellCoord(grid.WrapColumn(c.Column + 1), c.Floor))) mask |= OpenRight;
            if (!HasNeighbour(key.layer, new CellCoord(c.Column, c.Floor + 1))) mask |= OpenTop;
            if (!HasNeighbour(key.layer, new CellCoord(c.Column, c.Floor - 1))) mask |= OpenBottom;
            if (!HasCubeBehind(key.layer, c)) mask |= OpenBack;
            cube.GetComponent<MeshFilter>().sharedMesh = _exposureMeshes[mask];
        }

        /// <summary>
        /// A cube left the tower: its four neighbours may have gained an exposed side, and the outer-layer cubes in front
        /// of it (every outer cube whose span overlaps it) an exposed back.
        /// </summary>
        private void RefreshNeighbourExposure(int layer, CellCoord cell)
        {
            TowerGrid grid = _layers[layer];
            RefreshExposure((layer, new CellCoord(grid.WrapColumn(cell.Column - 1), cell.Floor)));
            RefreshExposure((layer, new CellCoord(grid.WrapColumn(cell.Column + 1), cell.Floor)));
            RefreshExposure((layer, new CellCoord(cell.Column, cell.Floor + 1)));
            RefreshExposure((layer, new CellCoord(cell.Column, cell.Floor - 1)));
            int outer = layer - 1;
            if (outer < 0) return;
            int columns = _layers[outer].Columns;
            for (int column = 0; column < columns; column++)
                if (TowerLayout.Overlaps(column, columns, cell.Column, grid.Columns))
                    RefreshExposure((outer, new CellCoord(column, cell.Floor)));
        }

        public void Clear()
        {
            _cubes.Clear();
            _homes.Clear();
            _highlight.Clear();
            _breaking.Clear();
            _layers = System.Array.Empty<TowerGrid>();
            // Destroy every spin root, not just the tracked one: edit-mode previews (DontSave) survive entering Play
            // mode while the _spinRoot reference resets, which used to leave frozen duplicate towers behind.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == SpinRootName)
                {
                    child.gameObject.SetActive(false);   // hide now; Destroy is deferred in Play mode
                    child.SetParent(null);
                    DestroySafe(child.gameObject);
                }
            }
            _spinRoot = null;
        }

        /// <summary>Re-evaluates which inner cubes are covered by an outer layer (call after cubes are removed).</summary>
        public void RefreshCoverage()
        {
            foreach (KeyValuePair<(int layer, CellCoord cell), GameObject> entry in _cubes)
            {
                if (entry.Value == null || _highlight.Contains(entry.Key)) continue;
                int colour = _layers[entry.Key.layer][entry.Key.cell];
                if (colour == TowerGrid.Empty) continue;
                bool covered = TowerLayout.IsCovered(_layers, entry.Key.layer, entry.Key.cell);
                Material[] set = covered ? _coveredMaterials : _colourMaterials;
                entry.Value.GetComponent<MeshRenderer>().sharedMaterial = set[colour % set.Length];
            }
        }

        public Transform SpinRoot => _spinRoot;
        public Mesh CubeMesh => _cubeMesh;
        public TowerVisualSettings Settings => _settings;
        public Feel.ImpactEffects Effects => _effects;
        /// <summary>Where broken pieces go to stay as debris; null when debris is not kept (pieces shrink away).</summary>
        public Feel.DebrisSweeper Debris => _settings.KeepDebris ? _debris : null;
        public CubePalette Palette => _palette;
        public Color PaletteColour(int colour) => _palette[colour % _palette.Count];

        /// <summary>Radius of a layer's cube front faces (what a projectile touches first).</summary>
        public float FaceRadius(int layer) => RadiusOf(layer) + 0.5f * _settings.CubeScale;

        /// <summary>
        /// Material for a cube of a palette colour that is NOT part of the tower (projectile, shards). The tower-only
        /// shading (seam crevices, per-cube form shadow) assumes cubes face outward from the tower axis, so it is off.
        /// </summary>
        public Material LooseMaterial(int colour)
        {
            EnsureMaterials();
            return _looseMaterials[colour % _looseMaterials.Length];
        }

        /// <summary>First occupied cube along a world-space segment.</summary>
        public bool Raycast(Vector3 worldFrom, Vector3 worldTo, out TowerHit hit)
        {
            hit = default;
            if (_spinRoot == null || _layers.Length == 0)
                return false;
            return TowerRaycast.Cast(_layers, FaceRadius, _settings.CubeSpacing,
                _spinRoot.InverseTransformPoint(worldFrom), _spinRoot.InverseTransformPoint(worldTo), out hit, 0f, _settings.CubeScale);
        }

        /// <summary>
        /// Sweeps a sphere through tower space. Passing the start point as it was in the tower's frame on the previous
        /// step (not re-projected now) includes the tower's own rotation, so nothing tunnels while the tower spins.
        /// </summary>
        /// <summary>
        /// Where a world-space ray first comes within <paramref name="margin"/> of the outer cube fronts (the cylinder
        /// around the whole tower): just in front of the hole a ray through the tower passes.
        /// </summary>
        public bool EntryPoint(Vector3 worldOrigin, Vector3 worldDirection, float margin, out Vector3 point)
        {
            point = default;
            if (_spinRoot == null || _layers.Length == 0) return false;
            Vector3 o = _spinRoot.InverseTransformPoint(worldOrigin), d = _spinRoot.InverseTransformDirection(worldDirection);
            float a = d.x * d.x + d.z * d.z, b = 2f * (o.x * d.x + o.z * d.z), r = FaceRadius(0) + margin;
            float c = o.x * o.x + o.z * o.z - r * r, disc = b * b - 4f * a * c;
            if (a < 1e-9f || c < 0f || disc < 0f) return false;
            float t = (-b - Mathf.Sqrt(disc)) / (2f * a);
            if (t < 0f) return false;
            point = _spinRoot.TransformPoint(o + d * t);
            return true;
        }

        public bool SweepLocal(Vector3 localFrom, Vector3 localTo, float radius, out TowerHit hit)
        {
            hit = default;
            if (_spinRoot == null || _layers.Length == 0)
                return false;
            return TowerRaycast.Cast(_layers, FaceRadius, _settings.CubeSpacing, localFrom, localTo, out hit, radius, _settings.CubeScale);
        }

        /// <summary>Centre of a cube in tower space.</summary>
        public Vector3 CellCentre(int layer, CellCoord cell) => LocalPositionOf(layer, cell, out _);

        // ---------------------------------------------------------------- aim highlight
        private readonly HashSet<(int, CellCoord)> _highlight = new HashSet<(int, CellCoord)>();
        private readonly List<(int, CellCoord)> _highlightScratch = new List<(int, CellCoord)>();

        /// <summary>The patch a release would break glows and swells slightly (null clears it).</summary>
        public void SetHighlight(IReadOnlyList<RemovedCube> cubes)
        {
            _highlightScratch.Clear();
            if (cubes != null)
                foreach (RemovedCube c in cubes) _highlightScratch.Add((c.Layer, c.Cell));
            if (_highlightScratch.Count == _highlight.Count && _highlight.SetEquals(_highlightScratch))
                return;

            foreach ((int, CellCoord) key in _highlight)
                ApplyCubeLook(key, false);
            _highlight.Clear();
            foreach ((int, CellCoord) key in _highlightScratch)
            {
                _highlight.Add(key);
                ApplyCubeLook(key, true);
            }
        }

        private void ApplyCubeLook((int layer, CellCoord cell) key, bool highlighted)
        {
            if (!_cubes.TryGetValue(key, out GameObject cube) || cube == null) return;
            int colour = _layers[key.layer][key.cell];
            if (colour == TowerGrid.Empty) return;
            bool covered = TowerLayout.IsCovered(_layers, key.layer, key.cell);
            Material[] set = highlighted ? _highlightMaterials : covered ? _coveredMaterials : _colourMaterials;
            cube.GetComponent<MeshRenderer>().sharedMaterial = set[colour % set.Length];
            cube.transform.localScale = Vector3.one * _settings.CubeScale;
        }

        /// <summary>Highlighted patch pulses at ~2 Hz: brighter (above 1, it glows) and swelling (Tower Buster playable).</summary>
        private void PulseHighlight()
        {
            if (_highlight.Count == 0 || _highlightMaterials == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            for (int i = 0; i < _highlightMaterials.Length && i < _palette.Count; i++)
            {
                Color colour = Color.Lerp(_palette[i], Color.white, 0.14f + 0.14f * pulse) * (1.15f + 0.15f * pulse);
                colour.a = 1f;
                _highlightMaterials[i].SetColor("_BaseColor", colour);
            }
            float scale = _settings.CubeScale * (1.05f + 0.035f * pulse);
            foreach ((int, CellCoord) key in _highlight)
                if (_cubes.TryGetValue(key, out GameObject cube) && cube != null)
                    cube.transform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// Breaks every cube left, as a ripple spreading from <paramref name="origin"/> (world space) at
        /// <paramref name="rippleStep"/> seconds per metre. Development shortcut to the win sequence.
        /// </summary>
        public void BreakAll(Vector3 origin, float rippleStep)
        {
            var all = new List<RemovedCube>();
            for (int layer = 0; layer < _layers.Length; layer++)
                for (int floor = 0; floor < _layers[layer].Floors; floor++)
                    for (int column = 0; column < _layers[layer].Columns; column++)
                        if (_layers[layer][column, floor] != TowerGrid.Empty)
                            all.Add(new RemovedCube(layer, new CellCoord(column, floor), 0));
            RemoveCubes(all, rippleStep, origin);
        }

        /// <summary>Removes cubes from the grid now and plays their destruction as a ripple (by BFS depth).</summary>
        public void RemoveCubes(IReadOnlyList<RemovedCube> cubes, float rippleStep) => RemoveCubes(cubes, rippleStep, null);

        /// <summary>
        /// Removes cubes from the grid now; each one breaks after a delay proportional to its straight-line distance from
        /// <paramref name="origin"/> (world space), so destruction spreads in a circle from the impact.
        /// Without an origin, the delay follows the group's BFS depth.
        /// </summary>
        public void RemoveCubes(IReadOnlyList<RemovedCube> cubes, float rippleStep, Vector3? origin)
        {
            // The ripple is timed from the cube(s) nearest the impact, so the touched cube breaks on the impact frame.
            float nearest = float.MaxValue;
            if (origin.HasValue)
                foreach (RemovedCube removed in cubes)
                    if (_cubes.TryGetValue((removed.Layer, removed.Cell), out GameObject cube) && cube != null)
                        nearest = Mathf.Min(nearest, Vector3.Distance(cube.transform.position, origin.Value));

            float maxDelay = 0f;
            foreach (RemovedCube removed in cubes)
            {
                int colour = _layers[removed.Layer][removed.Cell];
                _layers[removed.Layer][removed.Cell] = TowerGrid.Empty;
                if (!_cubes.TryGetValue((removed.Layer, removed.Cell), out GameObject cube) || cube == null)
                    continue;
                _cubes.Remove((removed.Layer, removed.Cell));
                _homes.Remove(cube.transform);
                _highlight.Remove((removed.Layer, removed.Cell));
                float delay = origin.HasValue
                    ? (Vector3.Distance(cube.transform.position, origin.Value) - nearest) * rippleStep
                    : removed.Depth * rippleStep;
                if (delay <= 0f)
                {
                    BreakCube(cube, colour);
                    RefreshNeighbourExposure(removed.Layer, removed.Cell);
                }
                else
                {
                    _breaking.Add((removed.Layer, removed.Cell));
                    StartCoroutine(PopCube(cube, colour, delay, removed.Layer, removed.Cell));
                }
                maxDelay = Mathf.Max(maxDelay, delay);
            }
            RefreshCoverage();
            ScheduleSupportCheck(maxDelay + 0.06f);
        }

        // ---------------------------------------------------------------- unsupported cubes fall
        private Coroutine _supportCheck;

        /// <summary>Once the current chain has finished breaking, cubes left without support start falling.</summary>
        public void ScheduleSupportCheck(float delay)
        {
            if (_supportCheck != null) StopCoroutine(_supportCheck);
            _supportCheck = StartCoroutine(SupportCheckAfter(delay));
        }

        private System.Collections.IEnumerator SupportCheckAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _supportCheck = null;
            DropUnsupported();
        }

        /// <summary>Returns how many cubes started falling.</summary>
        public int DropUnsupported()
        {
            List<(int layer, CellCoord cell)> falling = TowerRules.FindUnsupported(_layers);
            float spinRad = _settings.SpinDegreesPerSecond * Mathf.Deg2Rad;
            foreach ((int layer, CellCoord cell) in falling)
            {
                int colour = _layers[layer][cell];
                _layers[layer][cell] = TowerGrid.Empty;
                _highlight.Remove((layer, cell));
                if (!_cubes.TryGetValue((layer, cell), out GameObject cube) || cube == null) continue;
                _cubes.Remove((layer, cell));
                _homes.Remove(cube.transform);

                Transform t = cube.transform;
                t.SetParent(null, true);
                cube.GetComponent<MeshRenderer>().sharedMaterial = LooseMaterial(colour);
                Vector3 p = t.position - transform.position;
                Vector3 outward = new Vector3(p.x, 0f, p.z).normalized;
                Vector3 tangential = Vector3.Cross(Vector3.up * spinRad, new Vector3(p.x, 0f, p.z));   // carries the spin
                Vector3 velocity = outward * (Random.Range(2f, 3.5f) + p.y * 0.22f) + tangential + Vector3.up * Random.Range(0f, 1.5f);
                Feel.FallingCube.Launch(cube, velocity, _cubeMesh, LooseMaterial(colour), PaletteColour(colour), _effects, Debris);
            }
            foreach ((int layer, CellCoord cell) in falling)
                RefreshNeighbourExposure(layer, cell);
            if (falling.Count > 0)
                RefreshCoverage();
            return falling.Count;
        }

        /// <summary>Ripple cube: a tiny swell that ends exactly when it cracks, <paramref name="delay"/> after the impact.</summary>
        private System.Collections.IEnumerator PopCube(GameObject cube, int colour, float delay, int layer, CellCoord cell)
        {
            const float swell = 0.04f;
            if (delay > swell)
                yield return new WaitForSeconds(delay - swell);
            Transform t = cube.transform;
            Vector3 scale = t.localScale;
            float duration = Mathf.Min(swell, delay);
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                t.localScale = scale * Mathf.Lerp(1f, 1.12f, time / duration);
                yield return null;
            }
            t.localScale = scale;
            BreakCube(cube, colour);
            if (_breaking.Remove((layer, cell)))
                RefreshNeighbourExposure(layer, cell);
        }

        private void BreakCube(GameObject cube, int colour)
        {
            Transform t = cube.transform;
            float size = t.localScale.x;
            Feel.CubeChunk.Split(_cubeMesh, LooseMaterial(colour), t.position, t.rotation, size, t.rotation * Vector3.forward, Debris);
            if (_effects != null)
                _effects.PlayCubeBreak(t.position + t.rotation * Vector3.forward * (size * 0.5f), PaletteColour(colour), size);
            Destroy(cube);
        }

        /// <summary>
        /// A wrong-colour hit knocks the cubes around the impact away from it: a hard object takes one sharp push and
        /// settles, no oscillation. The wave front travels at 30 u/s; the push fades with distance (Tower Buster playable).
        /// </summary>
        public void Shockwave(Vector3 worldPoint, float radius, float strength)
        {
            foreach (GameObject cube in _cubes.Values)
            {
                if (cube == null) continue;
                Vector3 delta = cube.transform.position - worldPoint;
                float distance = delta.magnitude;
                if (distance > radius) continue;
                float push = strength / Mathf.Max(distance, 1f) * (1f - distance / radius);
                StartCoroutine(Knock(cube.transform, delta.normalized * push, distance / 30f));
            }
        }

        private readonly Dictionary<Transform, Vector3> _homes = new Dictionary<Transform, Vector3>();

        private System.Collections.IEnumerator Knock(Transform cube, Vector3 offset, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (cube == null || !_homes.TryGetValue(cube, out Vector3 home)) yield break;
            Vector3 local = cube.parent.InverseTransformVector(offset);
            const float tau = 0.045f, end = 0.32f;
            for (float time = 0f; time < end && cube != null && cube.parent == _spinRoot; time += Time.deltaTime)
            {
                float k = time / tau * Mathf.Exp(1f - time / tau);   // impulse response: peaks at tau, ~0 by the end
                cube.localPosition = home + local * k;
                yield return null;
            }
            if (cube != null && cube.parent == _spinRoot) cube.localPosition = home;
        }

        public Vector3 LocalPositionOf(int layer, CellCoord cell, out Quaternion rotation)
        {
            float angle = 2f * Mathf.PI * cell.Column / _layers[layer].Columns;
            var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            rotation = Quaternion.LookRotation(outward, Vector3.up);
            float y = (cell.Floor + 0.5f) * _settings.CubeSpacing;
            return outward * RadiusOf(layer) + Vector3.up * y;
        }

        private GameObject CreateCube(int layer, CellCoord cell, int colour)
        {
            var cube = new GameObject($"Cube L{layer} {cell.Column},{cell.Floor}");
            cube.transform.SetParent(_spinRoot, false);
            Vector3 position = LocalPositionOf(layer, cell, out Quaternion rotation);
            // Deterministic per-cell irregularity: stable across rebuilds, never flickers.
            float p = _settings.PositionJitter, r = _settings.RotationJitter;
            position += rotation * new Vector3(Jitter(layer, cell, 0) * p, Jitter(layer, cell, 1) * p, Jitter(layer, cell, 2) * p * 0.5f);
            rotation *= Quaternion.Euler(Jitter(layer, cell, 3) * r, Jitter(layer, cell, 4) * r, Jitter(layer, cell, 5) * r);
            cube.transform.localPosition = position;
            cube.transform.localRotation = rotation;
            _homes[cube.transform] = position;
            cube.transform.localScale = Vector3.one * _settings.CubeScale;
            cube.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
            cube.AddComponent<MeshRenderer>().sharedMaterial = _colourMaterials[colour % _colourMaterials.Length];
            return cube;
        }

        /// <summary>Hash-based value in [-1, 1] for a cell and channel.</summary>
        private static float Jitter(int layer, CellCoord cell, int channel)
        {
            unchecked
            {
                uint h = (uint)(layer * 73856093) ^ (uint)(cell.Column * 19349663) ^ (uint)(cell.Floor * 83492791) ^ (uint)(channel * 2654435761u);
                h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        private void EnsureMaterials()
        {
            if (_colourMaterials != null && _colourMaterials.Length == _palette.Count && _colourMaterials[0] != null && _looseMaterials != null && _highlightMaterials != null)
                return;

            DestroyMaterials();
            _colourMaterials = CreateMaterials(0f);
            _coveredMaterials = CreateMaterials(1f);
            _looseMaterials = CreateMaterials(-1f);
            _highlightMaterials = CreateMaterials(2f);
        }

        private Material[] CreateMaterials(float covered)
        {
            var materials = new Material[_palette.Count];
            for (int i = 0; i < _palette.Count; i++)
            {
                // One material per colour/state (not property blocks) keeps every cube on the SRP Batcher fast path.
                var material = new Material(_cubeMaterial) { name = $"{_cubeMaterial.name} [{i}{(covered > 0f ? " covered" : "")}]", hideFlags = HideFlags.DontSave };
                ApplyVariant(material, i, covered);
                materials[i] = material;
            }
            return materials;
        }

        /// <summary>Per-variant overrides on top of the shared source material.</summary>
        private void ApplyVariant(Material material, int colour, float covered) => CubeMaterials.ApplyVariant(material, _palette[colour], covered);

#if UNITY_EDITOR
        private int _materialDirtyCount = -1;
        private int _paletteDirtyCount = -1;

        /// <summary>
        /// Live tuning: when Cube.mat or the palette is edited in the Inspector, copy it onto every runtime variant.
        /// Note: material/palette edits made during Play mode are asset edits, so they are kept after Play stops.
        /// </summary>
        private void SyncLiveEdits()
        {
            if (_cubeMaterial == null || _palette == null || _colourMaterials == null || _coveredMaterials == null || _looseMaterials == null || _highlightMaterials == null)
                return;

            int materialDirty = UnityEditor.EditorUtility.GetDirtyCount(_cubeMaterial);
            int paletteDirty = UnityEditor.EditorUtility.GetDirtyCount(_palette);
            if (materialDirty == _materialDirtyCount && paletteDirty == _paletteDirtyCount)
                return;
            _materialDirtyCount = materialDirty;
            _paletteDirtyCount = paletteDirty;

            if (_colourMaterials.Length != _palette.Count)
            {
                if (_layers.Length > 0) Build(_layers);   // colours added/removed: rebuild variants and cubes
                return;
            }

            for (int i = 0; i < _palette.Count; i++)
            {
                foreach ((Material material, float covered) in new[] { (_colourMaterials[i], 0f), (_coveredMaterials[i], 1f), (_looseMaterials[i], -1f), (_highlightMaterials[i], 2f) })
                {
                    if (material == null) continue;
                    if (material.shader != _cubeMaterial.shader) material.shader = _cubeMaterial.shader;
                    material.CopyPropertiesFromMaterial(_cubeMaterial);
                    ApplyVariant(material, i, covered);
                }
            }
        }
#endif

        private void Update()
        {
#if UNITY_EDITOR
            SyncLiveEdits();
#endif
            if (Application.isPlaying && _spinRoot != null)
                _spinRoot.Rotate(0f, _settings.SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            PulseHighlight();

            if (Application.isPlaying && !_clearedRaised && _layers.Length > 0 && _cubes.Count == 0 && _breaking.Count == 0)
            {
                _clearedRaised = true;
                Cleared?.Invoke();
            }
        }

        private void OnDestroy()
        {
            Clear();
            DestroyMaterials();
            DestroyExposureMeshes();
        }

        private void DestroyMaterials()
        {
            foreach (Material[] set in new[] { _colourMaterials, _coveredMaterials, _looseMaterials, _highlightMaterials })
            {
                if (set == null) continue;
                foreach (Material material in set)
                    if (material != null) DestroySafe(material);
            }
            _colourMaterials = null;
            _coveredMaterials = null;
            _looseMaterials = null;
            _highlightMaterials = null;
        }

        private static void DestroySafe(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
