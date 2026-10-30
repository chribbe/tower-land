using System.Collections;
using System.Collections.Generic;
using TowerLand.Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerLand.Gameplay
{
    /// <summary>
    /// Player shooting: press anywhere and drag to aim (the aim point sits a little above the finger), a dotted arc
    /// previews the throw (bright when the targeted cube matches the projectile's colour), release to throw.
    /// A smaller queued cube of another reachable colour waits bottom-left of it (Tower Buster playable): tap it to swap the
    /// two; after a throw it hops into the main slot and a new one is queued.
    /// </summary>
    public sealed class Shooter : MonoBehaviour
    {
        [SerializeField] private TowerView _tower;
        [SerializeField] private ShooterSettings _settings;
        [SerializeField] private Material _lineMaterial;

        private Camera _camera;
        private Projectile _current;
        private Vector3 _launchPosition;
        private float _projectileSize;
        private bool _aiming;
        private Vector2 _pressPosition;
        private bool _spawnPending;
        private AimPath _path;
        private Vector3 _aimTarget;
        private Quaternion _restRotation = Quaternion.identity;
        // Queued cube, and the hops of both cubes between the slots (1 = settled in its slot).
        private Projectile _next;
        private Vector3 _nextPosition;
        private float _nextSize;
        private float _currentHop = 1f, _nextHop = 1f;
        private Vector3 _currentHopFrom, _nextHopFrom;
        private float _currentHopScale, _nextHopScale;
        private SpriteRenderer _swapRing;
        private Texture2D _swapRingTexture;
        private float _swapRingAngle, _swapRingFade;
        private LineRenderer _line;
        private Vector3[] _linePoints;
        private MeshRenderer _cap;
        private Material _capMaterial;
        private MaterialPropertyBlock _capProperties;
        private readonly System.Random _rng = new System.Random();

        /// <summary>The cube waiting to be thrown (null while one is in flight / respawning).</summary>
        public Projectile CurrentProjectile => _current;
        public ShooterSettings Settings => _settings;

        private void Start()
        {
            _camera = Camera.main;
            CreateLine();
            CreateSwapRing();
        }

        /// <summary>The level is over: the waiting cube pops away and input stops.</summary>
        public void Stop()
        {
            _aiming = false;
            if (_line != null) _line.enabled = false;
            if (_cap != null) _cap.enabled = false;
            if (_current != null)
            {
                Destroy(_current.gameObject);
                _current = null;
            }
            RemoveNext();
            if (_swapRing != null) _swapRing.enabled = false;
            StopAllCoroutines();
            enabled = false;
        }

        private void Update()
        {
            if (_tower.Layers.Count == 0)
                return;

            if (_current == null && !_spawnPending)
                Spawn();
            else if (_current != null && !_aiming && Time.time >= _nextColourCheck && _currentHop >= 1f && _nextHop >= 1f)
                RevalidateColour();

            HandleInput();
            Hover();
            HoverNext();
            PlaceSwapRing();
            TurnTowardsAim();
        }

        /// <summary>
        /// While aimed, the waiting cube turns part of the way towards the aim point (limited, lagging slightly behind the
        /// finger); otherwise it settles back to its resting orientation.
        /// </summary>
        private void TurnTowardsAim()
        {
            if (_current == null)
                return;
            Transform cube = _current.transform;
            Quaternion want = _restRotation;
            if (_aiming)
            {
                Vector3 rest = _restRotation * Vector3.forward;
                Vector3 toTarget = (_aimTarget - cube.position).normalized;
                Quaternion full = Quaternion.FromToRotation(rest, toTarget);
                full.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                float turn = Mathf.Clamp(angle * _settings.AimTurn, -_settings.AimTurnMax, _settings.AimTurnMax);
                want = Quaternion.AngleAxis(turn, axis) * _restRotation;
            }
            cube.rotation = Quaternion.Slerp(cube.rotation, want, 1f - Mathf.Exp(-Time.deltaTime * _settings.AimTurnSpeed));
        }

        /// <summary>
        /// The waiting cube floats above the grass, bobbing gently (and holds still while aimed). Arriving from the queue it
        /// hops in: eased, arcing over the top, growing to full size.
        /// </summary>
        private void Hover()
        {
            if (_current == null || _aiming)
                return;
            float bob = Mathf.Sin(Time.time * 2f * Mathf.PI / _settings.HoverBobPeriod) * _settings.HoverBob * _projectileSize;
            Vector3 slot = _launchPosition + Vector3.up * bob;
            if (_currentHop < 1f)
            {
                _currentHop = Mathf.Min(1f, _currentHop + Time.deltaTime / _settings.HopDuration);
                float e = 1f - Mathf.Pow(1f - _currentHop, 3f);
                _current.transform.position = Vector3.Lerp(_currentHopFrom, slot, e) + Vector3.up * (Mathf.Sin(Mathf.PI * e) * _settings.HopArc * _projectileSize);
                _current.transform.localScale = Vector3.one * Mathf.Lerp(_currentHopScale, _projectileSize, e);
                if (_currentHop >= 1f && _next == null)
                    SpawnNext();
                return;
            }
            _current.transform.position = slot;
        }

        /// <summary>The queued cube bobs out of phase with the main one; swapped out, it dips under on its way to the queue slot.</summary>
        private void HoverNext()
        {
            if (_next == null)
                return;
            float bob = Mathf.Sin(Time.time * 2f * Mathf.PI / _settings.HoverBobPeriod + 2.2f) * _settings.HoverBob * _nextSize;
            Vector3 slot = _nextPosition + Vector3.up * bob;
            if (_nextHop < 1f)
            {
                _nextHop = Mathf.Min(1f, _nextHop + Time.deltaTime / _settings.HopDuration);
                float e = 1f - Mathf.Pow(1f - _nextHop, 3f);
                _next.transform.position = Vector3.Lerp(_nextHopFrom, slot, e) - Vector3.up * (Mathf.Sin(Mathf.PI * e) * _settings.HopDip * _projectileSize);
                _next.transform.localScale = Vector3.one * Mathf.Lerp(_nextHopScale, _nextSize, e);
                return;
            }
            _next.transform.position = slot;
        }

        /// <summary>
        /// Aiming starts: the cube grows to the aiming scale with a small overshoot (ease-out-back) and stays big
        /// while the finger is held. It goes back to normal size only on release.
        /// </summary>
        private IEnumerator AimPunch(Transform cube)
        {
            float time = 0f;
            float overshoot = _settings.AimPunch * 10f;   // ease-out-back "s" parameter
            while (time < _settings.AimPunchDuration && cube != null && _aiming)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / _settings.AimPunchDuration) - 1f;
                float ease = 1f + k * k * ((overshoot + 1f) * k + overshoot);   // 0 -> 1, overshooting slightly
                cube.localScale = Vector3.one * (_projectileSize * Mathf.LerpUnclamped(1f, _settings.AimingScale, ease));
                yield return null;
            }
            if (cube != null && _aiming) cube.localScale = Vector3.one * (_projectileSize * _settings.AimingScale);
        }

        private void HandleInput()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || _current == null)
                return;

            if (pointer.press.wasPressedThisFrame)
            {
                if (OverUI(pointer.position.ReadValue()))
                    return;   // a tap on UI (debug corner, prompts) is not a throw
                if (TapsNext(pointer.position.ReadValue()))
                {
                    Swap();
                    return;
                }
                if (_currentHop < 1f)
                    return;   // still hopping into the slot
                _aiming = true;
                _pressPosition = pointer.position.ReadValue();
                StartCoroutine(AimPunch(_current.transform));
            }

            if (!_aiming)
                return;

            if (pointer.press.isPressed)
            {
                UpdateAim(pointer.position.ReadValue());
            }
            else
            {
                _aiming = false;
                _line.enabled = false;
                _cap.enabled = false;
                _tower.SetHighlight(null);
                _current.transform.localScale = Vector3.one * _projectileSize;
                _current.Throw(_path, OnShotResolved);
                _current = null;
                StartCoroutine(SpawnAfterDelay());   // next cube shortly after the throw, as in Tower Buster
            }
        }

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        /// <summary>Whether a UI graphic (raycast target) sits under <paramref name="screenPosition"/>.</summary>
        private bool OverUI(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            _uiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screenPosition }, _uiHits);
            return _uiHits.Count > 0;
        }

        private void UpdateAim(Vector2 screenPosition)
        {
            // Like Tower Buster: the drag is amplified from where the finger went down (more when moving up), so the top
            // of the tower is reachable with a small thumb movement.
            Vector2 drag = screenPosition - _pressPosition;
            drag.x *= _settings.DragMultiplier.x;
            drag.y *= drag.y > 0f ? _settings.DragMultiplier.y : 1f;
            Vector2 aimScreen = _pressPosition + drag + Vector2.up * (_settings.FingerOffset * Screen.height);
            Ray ray = _camera.ScreenPointToRay(aimScreen);

            bool onTower = _tower.Raycast(ray.origin, ray.origin + ray.direction * 500f, out TowerHit hit);
            Vector3 target = onTower ? _tower.SpinRoot.TransformPoint(hit.LocalPoint) : ray.origin + ray.direction * _settings.MissDistance;
            _aimTarget = target;
            bool good = onTower && _tower.Layers[hit.Layer][hit.Cell] == _current.Colour;
            // what releasing now would break glows (playable's aim highlight)
            _tower.SetHighlight(good ? TowerRules.FindGroup(_tower.Layers, hit.Layer, hit.Cell) : null);

            // A patch on the far side, seen through a hole: the throw goes via the hole, then along the aim ray.
            _path = onTower && hit.Inside && _tower.EntryPoint(ray.origin, ray.direction, _settings.HoleEntryMargin, out Vector3 via)
                ? new AimPath(_current.transform.position, via, ray.direction, target, _settings.ArcHeight)
                : new AimPath(_current.transform.position, target, _settings.ArcHeight);
            PlaceLine(good);
            PlaceCap(onTower, target, good);
        }

        private void OnShotResolved(ShotOutcome outcome)
        {
            // Hook for moves / scoring / feedback (next steps).
        }

        private IEnumerator SpawnAfterDelay()
        {
            _spawnPending = true;
            yield return new WaitForSeconds(_settings.SpawnDelay);
            _spawnPending = false;
            if (_current == null)
                Spawn();
        }

        private void Spawn()
        {
            ComputeLaunchPose();
            if (_next != null)
            {
                // The queued cube hops into the main slot (its colour was kept reachable while it waited).
                _current = _next;
                _next = null;
                StartHop(ref _currentHop, ref _currentHopFrom, ref _currentHopScale, _current.transform);
                _restRotation = Quaternion.identity;
                return;
            }

            int colour = PickColour();
            if (colour < 0)
                return;   // tower cleared
            _current = Projectile.Create(_tower, _settings, colour, _launchPosition, _projectileSize);
            _restRotation = _current.transform.rotation;
            _currentHop = 1f;
            StartCoroutine(PopIn(_current, _projectileSize, false, SpawnNext));
        }

        // ---------------------------------------------------------------- queued cube (colour swap)
        /// <summary>Queues a smaller cube of another reachable colour; none when only one colour can be reached.</summary>
        private void SpawnNext()
        {
            if (_next != null || _current == null || !enabled)
                return;
            int colour = PickColour(_current.Colour);
            if (colour < 0)
                return;
            _next = Projectile.Create(_tower, _settings, colour, _nextPosition, _nextSize);
            _nextHop = 1f;
            StartCoroutine(PopIn(_next, _nextSize, true, null));
        }

        private void RemoveNext()
        {
            if (_next == null) return;
            Destroy(_next.gameObject);
            _next = null;
        }

        /// <summary>The two cubes trade places with a quick hop: the queued one arcs into the main slot, the other dips into the queue.</summary>
        private void Swap()
        {
            if (_current == null || _next == null || _aiming || _currentHop < 1f || _nextHop < 1f)
                return;
            (_current, _next) = (_next, _current);
            StartHop(ref _currentHop, ref _currentHopFrom, ref _currentHopScale, _current.transform);
            StartHop(ref _nextHop, ref _nextHopFrom, ref _nextHopScale, _next.transform);
            _restRotation = Quaternion.identity;
            _nextColourCheck = Time.time + 1f;
        }

        private static void StartHop(ref float hop, ref Vector3 from, ref float scale, Transform cube)
        {
            hop = 0f;
            from = cube.position;
            scale = cube.localScale.x;
        }

        /// <summary>Generous tap target around the queued cube (a multiple of its on-screen radius, at least a minimum in pixels).</summary>
        private bool TapsNext(Vector2 screenPosition)
        {
            if (_next == null)
                return false;
            Vector3 centre = _camera.WorldToScreenPoint(_next.transform.position);
            Vector3 edge = _camera.WorldToScreenPoint(_next.transform.position + _camera.transform.right * (_nextSize * 0.5f));
            if (centre.z <= 0f) return false;
            float radius = Mathf.Max(_settings.NextTapMinPixels * Screen.height / 1920f, Mathf.Abs(edge.x - centre.x) * _settings.NextTapRadius);
            return Vector2.Distance(screenPosition, centre) < radius;
        }

        // ---------------------------------------------------------------- colour choice
        private float _nextColourCheck;
        private float _reachAt = -1f;
        private int _reachCubes = -1;
        private readonly Dictionary<int, int> _reach = new Dictionary<int, int>();

        /// <summary>
        /// Colour of a new cube: only colours a throw can really reach right now (Tower Buster playable): aim rays over
        /// a grid of the aimable part of the screen (the finger offset makes the bottom unreachable), weighted by how
        /// much screen area each colour covers. Single-sample slivers don't count. Falls back to any exposed colour.
        /// </summary>
        private int PickColour(int exclude = -1)
        {
            Dictionary<int, int> reach = Reachable();
            int total = 0;
            foreach (KeyValuePair<int, int> pair in reach)
                if (pair.Key != exclude) total += pair.Value;
            if (total > 0)
            {
                int roll = _rng.Next(total);
                foreach (KeyValuePair<int, int> pair in reach)
                    if (pair.Key != exclude && (roll -= pair.Value) < 0) return pair.Key;
            }
            if (reach.Count > 0 && exclude >= 0)
                return -1;   // the only reachable colour is the excluded one
            int fallback = TowerRules.PickReachableColour(_tower.Layers, _rng);
            return fallback == exclude ? -1 : fallback;
        }

        /// <summary>Colour -> number of aim-grid rays landing on it (cached for a moment, recomputed when cubes change).</summary>
        private Dictionary<int, int> Reachable()
        {
            int cubes = 0;
            foreach (TowerGrid grid in _tower.Layers) cubes += grid.CountFilled();
            if (Time.time - _reachAt < 0.25f && cubes == _reachCubes)
                return _reach;
            _reachAt = Time.time;
            _reachCubes = cubes;
            _reach.Clear();

            const int columns = 15, rows = 14;
            float minY = Mathf.Clamp01(_settings.FingerOffset) + 0.01f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var viewport = new Vector3(0.02f + 0.96f * c / (columns - 1), minY + (0.98f - minY) * r / (rows - 1), 0f);
                    Ray ray = _camera.ViewportPointToRay(viewport);
                    if (!_tower.Raycast(ray.origin, ray.origin + ray.direction * 500f, out TowerHit hit))
                        continue;
                    int colour = _tower.Layers[hit.Layer][hit.Cell];
                    _reach.TryGetValue(colour, out int n);
                    _reach[colour] = n + 1;
                }
            }
            var slivers = new List<int>();
            foreach (KeyValuePair<int, int> pair in _reach)
                if (pair.Value < 2) slivers.Add(pair.Key);
            foreach (int colour in slivers) _reach.Remove(colour);
            return _reach;
        }

        /// <summary>
        /// A cube keeps the colour it was given (reachability only matters when a colour is picked). The one exception: a
        /// colour wiped off the tower entirely, which could never hit anything - that cube is re-dealt. The queued cube goes
        /// away when it would match the main one, and comes back once another colour can be offered.
        /// </summary>
        private void RevalidateColour()
        {
            _nextColourCheck = Time.time + 1f;
            if (!TowerRules.HasColour(_tower.Layers, _current.Colour))
            {
                int colour = PickColour(_next != null ? _next.Colour : -1);
                if (colour < 0) colour = PickColour();
                if (colour >= 0)   // none left: the tower is being cleared
                    _current.SetColour(colour);
            }
            if (_next != null && !TowerRules.HasColour(_tower.Layers, _next.Colour))
            {
                int colour = PickColour(_current.Colour);
                if (colour < 0) RemoveNext();
                else _next.SetColour(colour);
            }
            if (_next != null && _next.Colour == _current.Colour)
                RemoveNext();   // only that colour is left: nothing to swap to
            if (_next == null)
                SpawnNext();
        }

        /// <summary>Places the waiting cube (and the queued one) hovering above the ground at the configured screen spots, sized to the configured screen width.</summary>
        private void ComputeLaunchPose()
        {
            _launchPosition = HoverPose(_settings.LaunchViewport, 1f, out _projectileSize);
            _nextPosition = HoverPose(_settings.LaunchViewport + _settings.NextOffset, _settings.NextScale, out _nextSize);
        }

        private Vector3 HoverPose(Vector2 viewport, float scale, out float size)
        {
            Ray ray = _camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            float t = ray.direction.y < -1e-4f ? -ray.origin.y / ray.direction.y : 20f;
            Vector3 ground = ray.origin + ray.direction * t;
            float depth = Vector3.Dot(ground - _camera.transform.position, _camera.transform.forward);
            float frameWidth = 2f * depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * _camera.aspect;
            size = frameWidth * _settings.ProjectileScreenWidth * scale;
            return ground + Vector3.up * (size * (0.5f + _settings.HoverHeight));
        }

        /// <summary>
        /// A new cube grows in with a little bounce. It stops if the cube leaves its slot meanwhile (swapped, promoted,
        /// thrown) or, for the main cube, as soon as aiming takes over its scale.
        /// </summary>
        private IEnumerator PopIn(Projectile projectile, float size, bool queued, System.Action done)
        {
            bool InSlot() => projectile != null && (queued ? projectile == _next : projectile == _current && !_aiming);
            float time = 0f;
            while (time < _settings.SpawnPopDuration && InSlot())
            {
                time += Time.deltaTime;
                float k = time / _settings.SpawnPopDuration;
                float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.2f;   // little bounce on arrival
                projectile.transform.localScale = Vector3.one * (size * Mathf.Min(1f, k * 1.4f) * overshoot);
                yield return null;
            }
            if (InSlot()) projectile.transform.localScale = Vector3.one * size;
            done?.Invoke();
        }

        // ---------------------------------------------------------------- swap hint
        /// <summary>Camera-facing ring of two curved arrows around the queued cube, drawn once into a texture.</summary>
        private void CreateSwapRing()
        {
            const int size = 256;
            _swapRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "SwapRing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            _swapRingTexture.SetPixels32(SwapRingPixels(size));
            _swapRingTexture.Apply(true, true);
            var go = new GameObject("SwapRing");
            go.transform.SetParent(transform, false);
            _swapRing = go.AddComponent<SpriteRenderer>();
            _swapRing.sprite = Sprite.Create(_swapRingTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _swapRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _swapRing.receiveShadows = false;
            _swapRing.enabled = false;
        }

        /// <summary>
        /// The hint follows the queued cube, just behind it (the cube hides the middle), spinning slowly; it fades out
        /// while the cubes hop between slots and fades back in once the queued cube has settled.
        /// </summary>
        private void PlaceSwapRing()
        {
            if (_swapRing == null) return;
            bool show = _next != null && _nextHop >= 1f && _currentHop >= 1f;
            _swapRingFade = Mathf.MoveTowards(_swapRingFade, show ? 1f : 0f, Time.deltaTime * (show ? 4f : 12f));
            _swapRing.enabled = _next != null && _swapRingFade > 0.01f;
            if (!_swapRing.enabled) return;
            Transform cam = _camera.transform;
            float cubeSize = _next.transform.localScale.x;
            _swapRingAngle -= _settings.SwapRingSpin * Time.deltaTime;
            Transform ring = _swapRing.transform;
            ring.SetPositionAndRotation(_next.transform.position + cam.forward * (cubeSize * 0.75f), cam.rotation * Quaternion.Euler(0f, 0f, _swapRingAngle));
            float pop = Mathf.Lerp(0.85f, 1f, _swapRingFade);
            ring.localScale = Vector3.one * (cubeSize * _settings.SwapRingSize * pop);
            _swapRing.color = new Color(1f, 1f, 1f, _settings.SwapRingAlpha * _swapRingFade);
        }

        /// <summary>
        /// White icon with a soft drop shadow: two arcs of a ring, each ending in an arrowhead pointing clockwise
        /// (the "swap / cycle" symbol). Supersampled for clean edges.
        /// </summary>
        private static Color32[] SwapRingPixels(int size)
        {
            const float radius = 0.7f, halfWidth = 0.085f, arc = 125f * Mathf.Deg2Rad;
            const int samples = 3;
            var shadowOffset = new Vector2(0.025f, -0.035f);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float shape = 0f, shadow = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / samples) / size * 2f - 1f, (y + (sy + 0.5f) / samples) / size * 2f - 1f);
                            if (InSwapIcon(p, radius, halfWidth, arc)) shape += 1f;
                            if (InSwapIcon(p - shadowOffset, radius, halfWidth * 1.35f, arc)) shadow += 1f;
                        }
                    }
                    shape /= samples * samples;
                    shadow /= samples * samples;
                    float alpha = shape + shadow * 0.4f * (1f - shape);
                    float grey = alpha > 1e-4f ? shape / alpha : 1f;
                    var c = new Color(0.16f + 0.84f * grey, 0.1f + 0.9f * grey, 0.28f + 0.72f * grey, alpha);   // shadow tinted like the title outline
                    pixels[y * size + x] = c;
                }
            }
            return pixels;
        }

        private static bool InSwapIcon(Vector2 p, float radius, float halfWidth, float arc)
        {
            float r = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            for (int i = 0; i < 2; i++)
            {
                // Arc i runs counter-clockwise from its head (start) for `arc` radians; the head points clockwise.
                float start = Mathf.PI * (0.62f + i);
                float along = Mathf.Repeat(angle - start, 2f * Mathf.PI);
                if (along <= arc && Mathf.Abs(r - radius) <= halfWidth) return true;
                // Arrowhead at the start: a triangle whose base straddles the ring, tip ahead along the clockwise tangent.
                var centre = new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
                var tangent = new Vector2(Mathf.Sin(start), -Mathf.Cos(start));
                var outward = centre / radius;
                Vector2 tip = centre + tangent * (halfWidth * 3.4f);
                Vector2 a = centre + outward * (halfWidth * 2.6f), b = centre - outward * (halfWidth * 2.6f);
                if (InTriangle(p, a, b, tip)) return true;
            }
            return false;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        /// <summary>The aim trajectory: one camera-facing glowing ribbon along the throw arc.</summary>
        private void CreateLine()
        {
            var go = new GameObject("AimLine");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.sharedMaterial = _lineMaterial;
            _line.useWorldSpace = true;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCapVertices = 0;
            _line.numCornerVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _linePoints = new Vector3[_settings.LineSegments + 1];
            _line.positionCount = _linePoints.Length;
            _line.enabled = false;

            // Cap: a dot where the throw will land on the tower ("releasing now hits something").
            _capMaterial = new Material(_lineMaterial) { name = "AimCap" };
            _capMaterial.SetFloat("_Dot", 1f);
            _capProperties = new MaterialPropertyBlock();
            var cap = new GameObject("AimCap");
            cap.transform.SetParent(go.transform, false);
            cap.AddComponent<MeshFilter>().sharedMesh = CapQuad();
            _cap = cap.AddComponent<MeshRenderer>();
            _cap.sharedMaterial = _capMaterial;
            _cap.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cap.receiveShadows = false;
            _cap.enabled = false;
        }

        /// <summary>Unit quad with white vertex colours (the line shader multiplies by them).</summary>
        private static Mesh CapQuad()
        {
            var mesh = new Mesh { name = "AimCapQuad" };
            mesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void PlaceCap(bool onTower, Vector3 target, bool good)
        {
            _cap.enabled = onTower;
            if (!onTower) return;
            Transform t = _cap.transform;
            t.SetPositionAndRotation(target, _camera.transform.rotation);
            t.localScale = Vector3.one * (_settings.CapSize * (good ? 1f : _settings.LineWidthWrong / _settings.LineWidth));
            Color colour = _tower.PaletteColour(_current.Colour);
            colour.a = good ? 1f : _settings.LineAlphaWrong;
            _capProperties.SetColor("_BaseColor", colour);
            _cap.SetPropertyBlock(_capProperties);
        }

        private void OnDestroy()
        {
            if (_capMaterial != null) Destroy(_capMaterial);
            if (_swapRingTexture != null) Destroy(_swapRingTexture);
        }

        private void PlaceLine(bool good)
        {
            for (int i = 0; i < _linePoints.Length; i++)
            {
                float t = Mathf.Lerp(_settings.LineRange.x, _settings.LineRange.y, i / (float)(_linePoints.Length - 1));
                _linePoints[i] = _path.Evaluate(t);
            }
            _line.SetPositions(_linePoints);
            _line.widthMultiplier = good ? _settings.LineWidth : _settings.LineWidthWrong;
            Color colour = _tower.PaletteColour(_current.Colour);   // same colour as the cube being thrown
            colour.a = good ? 1f : _settings.LineAlphaWrong;
            _line.startColor = colour;
            _line.endColor = colour;
            _line.enabled = true;
        }
    }
}
