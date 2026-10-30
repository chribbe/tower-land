using System.Collections;
using System.Collections.Generic;
using TowerLand.Core;
using TowerLand.Feel;
using TowerLand.Levels;
using TowerLand.Tower;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TowerLand.Meta
{
    /// <summary>
    /// Build scene (meta game, composition root): the construction stands on the platform as far as it is built; the glass
    /// vacuum hose from the win sequence (stiffer here) drops in high above it and fires out the bricks won since the last
    /// visit in quick shots - each brick shoots down the glass and out of the mouth onto its slot, row by row round the
    /// tower - then yanks back out. Holding a
    /// finger down speeds it all up. A finished construction gets a title and the next one starts on the following visit.
    /// Tap to continue: the next level (preloaded) opens out of black.
    /// Development: B (once the show is over) adds a level's worth of random bricks and pours them.
    /// </summary>
    public sealed class BuildSequence : MonoBehaviour
    {
        [SerializeField] private LevelSession _session;
        [SerializeField] private string _gameScene = "Game";
        [SerializeField] private ConstructionList _constructions;
        [SerializeField] private BuildSettings _settings;
        [SerializeField] private CubePalette _palette;
        [Tooltip("Shared cube material (Cube.mat): bricks use its loose-cube variant per colour.")]
        [SerializeField] private Material _cubeMaterial;
        [SerializeField] private Mesh _brickMesh;
        [Tooltip("Base of the construction: centre of the platform top.")]
        [SerializeField] private Transform _site;
        [SerializeField] private ImpactEffects _effects;
        [SerializeField] private UI.TitleStyle _titleStyle;

        [Header("Vacuum hose (same as the win sequence)")]
        [SerializeField] private VacuumSettings _vacuum;
        [SerializeField] private Material _glass;
        [SerializeField] private GameObject _nozzle;
        [SerializeField] private GameObject _jointRing;

        private const float MaxStep = 1f / 60f;   // the hose springs are stepped at most this long (speed-up stays stable)
        private const float RecoilStiffness = 900f, RecoilDamping = 38f;   // nozzle recoil: stiff, settles within a shot

        private struct Flight
        {
            public int Slot;
            public int Colour;
            public bool Out;              // left the mouth: ballistic arc to the slot
            public float Distance;        // in the hose: metres up from the mouth
            public float Angle, Offset;   // place across the hose
            public Vector3 From, Velocity;
            public float Time, Duration;
            public Quaternion Tumble;
            public Vector3 SpinAxis;
            public int Shot;              // bricks fired together
        }

        private struct Landing
        {
            public int Slot, Colour, Index;
            public float Time;
        }

        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Landing> _landings = new List<Landing>();
        private PlayerProgress _progress;
        private TowerBlueprint _blueprint;
        private IReadOnlyList<BrickSlot> _slots;
        private List<int> _bricks;
        private BrickRenderer _renderer;
        private BuildHud _hud;
        private Camera _camera;
        private Vector3 _cameraHome;
        private float _cameraLift;
        private VacuumHose _hose;
        private AsyncOperation _next;
        private float _top;     // height of the highest landed brick's top, construction space
        private int _placed;    // bricks standing (slots 0.._placed-1, landed in order or not)
        private int _landed;
        private bool _leaving;
        private bool _waiting;   // show over, waiting for the tap
        private float _recoil, _recoilVelocity;   // nozzle kicked up by the shots, metres
        private Vector3 _aim = Vector3.down;      // nozzle direction: turns towards where it is shooting
        private int _lastFired = -1;

        private float Speed => Pointer.current != null && Pointer.current.press.isPressed ? _settings.HoldSpeedUp : 1f;

        private void Start()
        {
            UI.ScreenFade.In(_settings.FadeIn);
            if (_session != null) _session.FadeIn = false;
            _camera = Camera.main;
            _cameraHome = _camera.transform.position;

            _progress = PlayerProgress.Local();
            _blueprint = _constructions.For(_progress.Construction);
            _slots = _blueprint.Slots;
            _bricks = _progress.Bricks();
            int size = _slots.Count;
            float height = _blueprint.Shape.Height;
            float reach = _blueprint.Shape.OuterRadius + 1f;
            _renderer = gameObject.AddComponent<BrickRenderer>();
            _renderer.Init(_brickMesh, CubeMaterials.CreateLoose(_cubeMaterial, _palette),
                new Bounds(_site.position + Vector3.up * (height * 0.5f), new Vector3(reach * 2f, height + 20f, reach * 2f)));

            int shown = Mathf.Min(_progress.BricksShown, size);
            int target = Mathf.Min(_bricks.Count, size);
            for (int i = 0; i < shown; i++)
                Place(i);
            _placed = _landed = shown;

            _hud = BuildHud.Create(_titleStyle, $"{_blueprint.Title} {_progress.Construction + 1}");
            _hud.SetProgress(shown / (float)size, true);
            FrameCamera(0f, true);
            _next = Preload();
            StartCoroutine(Play(shown, target, size));
        }

        private AsyncOperation Preload()
        {
            if (!Application.CanStreamedLevelBeLoaded(_gameScene)) return null;
            Application.backgroundLoadingPriority = ThreadPriority.Low;   // in the background of the show
            AsyncOperation load = SceneManager.LoadSceneAsync(_gameScene);
            load.allowSceneActivation = false;
            return load;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // Only once the show is over (waiting for the tap): replays the pour with the new bricks.
            if (!_waiting || _leaving || Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame) return;
            // A level's worth: 700 cubes of 5 random colours, sorted into bands like a real win.
            var colours = new List<int>();
            while (colours.Count < Mathf.Min(5, _palette.Count))
            {
                int colour = Random.Range(0, _palette.Count);
                if (!colours.Contains(colour)) colours.Add(colour);
            }
            var bricks = new List<int>();
            for (int i = 0; i < 700; i++) bricks.Add(colours[Random.Range(0, colours.Count)]);
            _progress.Deposit(BrickSorting.Bands(bricks, _palette, _bricks.Count > 0 ? _bricks[_bricks.Count - 1] : -1));
            _bricks = _progress.Bricks();
            StopAllCoroutines();
            _hud.HideContinue();
            StartCoroutine(Play(_landed, Mathf.Min(_bricks.Count, _slots.Count), _slots.Count));
        }
#endif

        private IEnumerator Play(int shown, int target, int size)
        {
            _waiting = false;
            if (target > shown)
                yield return Spit(shown, target);
            _progress.BricksShown = target;

            if (_bricks.Count >= size)
            {
                // Finished: celebrate; the next visit starts the next construction with the leftover bricks.
                _progress.CompleteConstruction(size);
                Celebrate();
                Destroy(_hud.gameObject);   // the title takes its place at the top
                UI.WinScreen.Show(_titleStyle, Leave, "TOWER", "BUILT!", fadeToBlack: false, atTop: true);
                yield break;
            }

            _hud.ShowContinue();
            _waiting = true;
            // A finger still held from speeding up must lift first: only a fresh tap continues.
            while (Pointer.current != null && Pointer.current.press.isPressed) yield return null;
            while (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) yield return null;
            Leave();
        }

        private void Leave()
        {
            _waiting = false;
            if (_leaving) return;
            _leaving = true;
            UI.ScreenFade.Out(_settings.FadeOut, () =>
            {
                if (_session != null) _session.FadeIn = true;
                if (_next == null)
                {
                    SceneManager.LoadScene(_gameScene);
                    return;
                }
                Application.backgroundLoadingPriority = ThreadPriority.High;
                _next.allowSceneActivation = true;
            });
        }

        // ---------------------------------------------------------------- the hose spits the bricks
        // A stiff, mechanical feel (unlike the sucking): the hose drops in and locks in place high over the construction,
        // straight down, and fires the bricks in quick shots - each kicks the nozzle back and squashes it - and the bricks
        // shoot out fast and short onto their slots, snapping in.
        private IEnumerator Spit(int from, int to)
        {
            var cameraData = _camera.GetUniversalAdditionalCameraData();
            CameraOverrideOption opaque = cameraData.requiresColorOption;
            cameraData.requiresColorOption = CameraOverrideOption.On;   // frosted glass samples the scene behind it

            Vector3 anchor = ScreenTopPoint(1.3f);
            Vector3 offscreen = ScreenTopPoint(1.12f);
            _hose = VacuumHose.Create(_vacuum, _glass, _nozzle, _jointRing, anchor, offscreen);
            _recoil = _recoilVelocity = 0f;
            _aim = Vector3.down;

            // Enter: drops in fast and locks into place (barely any overshoot).
            Vector3 mouth = offscreen;
            for (float t = 0f; t < _settings.EnterDuration;)
            {
                float dt = Time.deltaTime * Speed;
                t += dt;
                mouth = Vector3.LerpUnclamped(offscreen, Hover(), EaseOutBack(t / _settings.EnterDuration, 0.5f));
                Drive(mouth, Vector3.down, dt);
                yield return null;
            }

            // Fire: shots at a steady beat, each sized so any haul takes about the same time.
            float rate = Mathf.Clamp((to - from) / _settings.SpitDuration, _settings.SpitRate.x, _settings.SpitRate.y);
            int perShot = Mathf.Max(1, Mathf.RoundToInt(rate * _settings.ShotInterval));
            float sinceShot = _settings.ShotInterval;
            int next = from, shot = 0;
            while (next < to || _flights.Count > 0)
            {
                float dt = Time.deltaTime * Speed;
                sinceShot += dt;
                for (; sinceShot >= _settings.ShotInterval && next < to; sinceShot -= _settings.ShotInterval, shot++)
                    for (int i = 0; i < perShot && next < to; i++)
                        Launch(next++, shot);
                mouth = Vector3.Lerp(mouth, Hover(), 1f - Mathf.Exp(-8f * dt));   // follows the rising top
                Drive(mouth, AimAt(mouth, next - 1), dt);
                yield return null;
            }

            // Exit: a short anticipation dip, then yanked straight up out of the screen.
            Vector3 exitFrom = mouth;
            Vector3 exitTo = exitFrom + Vector3.up * Vector3.Distance(ScreenTopPoint(1.35f), ScreenTopPoint(0.5f));
            for (float t = 0f; t < _settings.ExitDuration;)
            {
                float dt = Time.deltaTime * Speed;
                t += dt;
                float u = Mathf.Clamp01(t / _settings.ExitDuration);
                float dip = Mathf.Sin(Mathf.Clamp01(u / 0.2f) * Mathf.PI) * 0.35f;
                float rise = Mathf.Pow(Mathf.Clamp01((u - 0.12f) / 0.88f), 2f);
                Drive(Vector3.LerpUnclamped(exitFrom, exitTo, rise) + Vector3.down * dip, Vector3.down, dt);
                yield return null;
            }

            Destroy(_hose.gameObject);
            _hose = null;
            cameraData.requiresColorOption = opaque;
        }

        /// <summary>Where the mouth sits: centred high over the construction - above its top, never below the minimum height.</summary>
        private Vector3 Hover() =>
            _site.position + _site.rotation * Vector3.up * Mathf.Max(_top + _settings.MouthHeight, _settings.MinMouthHeight);

        /// <summary>
        /// Direction for the nozzle while laying <paramref name="slot"/>: part of the way from straight down towards that
        /// brick (<c>AimShare</c>), so it swivels round the tower as the rows go up.
        /// </summary>
        private Vector3 AimAt(Vector3 mouth, int slot)
        {
            Vector3 toSlot = (SlotPosition(Mathf.Clamp(slot, 0, _slots.Count - 1)) - mouth).normalized;
            return Vector3.Slerp(Vector3.down, toSlot, _settings.AimShare);
        }

        /// <summary>
        /// One frame: the nozzle turns towards <paramref name="aim"/> (quickly and without overshoot - rigid, no swing),
        /// recoils from the shots, bricks move, the camera follows.
        /// </summary>
        private void Drive(Vector3 mouth, Vector3 aim, float dt)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / MaxStep));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                _aim = Vector3.Slerp(_aim, aim, 1f - Mathf.Exp(-_settings.AimSpeed * step));
                // Recoil: a stiff spring kicking the nozzle back (against its aim) after each shot, then home.
                _recoilVelocity += (-_recoil * RecoilStiffness - _recoilVelocity * RecoilDamping) * step;
                _recoil += _recoilVelocity * step;
                _hose.Aim(mouth - _aim * _recoil, _aim);
                _hose.ClearLoad();
                MoveFlights(step, i == steps - 1);
                _hose.Step(step);
            }
            UpdateLandings(dt);
            FrameCamera(dt, false);
        }

        private void Launch(int slot, int shot)
        {
            _flights.Add(new Flight
            {
                Slot = slot,
                Colour = _bricks[slot],
                Shot = shot,
                // A shot travels as a clump, a little spread out along the hose.
                Distance = Mathf.Min(_settings.HoseRun + _hose.NozzleLength + Random.value * 0.8f, _hose.TravelLength),
                Angle = Random.value * Mathf.PI * 2f,
                Offset = Random.value * 0.6f,
                Tumble = Random.rotation,
                SpinAxis = Random.onUnitSphere,
            });
        }

        private void MoveFlights(float dt, bool draw)
        {
            var gravity = Vector3.down * _settings.Gravity;
            float size = _blueprint.Shape.BrickSize * _settings.BrickScale;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                Flight f = _flights[i];
                Vector3 position;
                Quaternion rotation;
                if (!f.Out)
                {
                    f.Distance -= _settings.HoseSpeed * dt;
                    if (f.Distance <= 0f)
                    {
                        // Fired out of the mouth: a fast, short arc that lands exactly on the slot after its flight time.
                        f.Out = true;
                        f.From = _hose.MouthPoint(0f, f.Angle, f.Offset * _hose.MouthRadius * 0.5f);
                        f.Duration = Random.Range(_settings.FlightTime.x, _settings.FlightTime.y);
                        f.Velocity = (SlotPosition(f.Slot) - f.From - 0.5f * f.Duration * f.Duration * gravity) / f.Duration;
                        f.Time = 0f;
                        if (f.Shot != _lastFired)
                        {
                            // First brick of a shot out: the nozzle kicks back and squashes.
                            _lastFired = f.Shot;
                            _recoilVelocity += _settings.Recoil;
                            _hose.Gulp(_settings.ShotSquash);
                        }
                    }
                    else
                    {
                        _hose.AddLoad(f.Distance);
                        position = _hose.PointInside(f.Distance, f.Angle, f.Offset);
                        _flights[i] = f;
                        if (draw) _renderer.AddFlying(f.Colour, Matrix4x4.TRS(position, f.Tumble, Vector3.one * size));
                        continue;
                    }
                }

                f.Time += dt;
                if (f.Time >= f.Duration)
                {
                    Land(f);
                    _flights[i] = _flights[_flights.Count - 1];
                    _flights.RemoveAt(_flights.Count - 1);
                    continue;
                }
                float u = f.Time / f.Duration;
                position = f.From + f.Velocity * f.Time + 0.5f * f.Time * f.Time * gravity;
                f.Tumble = Quaternion.AngleAxis(_settings.FlightSpin * dt, f.SpinAxis) * f.Tumble;
                rotation = Quaternion.Slerp(f.Tumble, SlotRotation(f.Slot), u * u);   // squares up to its slot as it arrives
                _flights[i] = f;
                if (draw) _renderer.AddFlying(f.Colour, Matrix4x4.TRS(position, rotation, Vector3.one * size));
            }
        }

        private void Land(Flight f)
        {
            int index = _renderer.AddPlaced(f.Colour, SlotMatrix(f.Slot, 1f + _settings.LandPop));
            _landings.Add(new Landing { Slot = f.Slot, Colour = f.Colour, Index = index });
            _top = Mathf.Max(_top, _slots[f.Slot].Position.y + _blueprint.Shape.BrickSize * 0.5f);
            _landed++;
            _hud.SetProgress(_landed / (float)_slots.Count);
            if (_effects != null && _landed % 25 == 0)
                _effects.PlayCubeBreak(SlotPosition(f.Slot), _palette[f.Colour % _palette.Count], _blueprint.Shape.BrickSize * 1.5f);
        }

        /// <summary>Freshly landed bricks squash from a little bigger down to their size.</summary>
        private void UpdateLandings(float dt)
        {
            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                Landing landing = _landings[i];
                landing.Time += dt;
                float u = Mathf.Clamp01(landing.Time / _settings.LandPopDuration);
                _renderer.SetPlaced(landing.Colour, landing.Index, SlotMatrix(landing.Slot, 1f + _settings.LandPop * (1f - u) * (1f - u)));
                if (u >= 1f)
                {
                    _landings[i] = _landings[_landings.Count - 1];
                    _landings.RemoveAt(_landings.Count - 1);
                }
                else _landings[i] = landing;
            }
        }

        private void Place(int slot)
        {
            _renderer.AddPlaced(_bricks[slot], SlotMatrix(slot, 1f));
            _top = Mathf.Max(_top, _slots[slot].Position.y + _blueprint.Shape.BrickSize * 0.5f);
        }

        private Vector3 SlotPosition(int slot) => _site.TransformPoint(_slots[slot].Position);
        private Quaternion SlotRotation(int slot) => _site.rotation * Quaternion.Euler(0f, _slots[slot].Yaw, 0f);

        private Matrix4x4 SlotMatrix(int slot, float scale) =>
            Matrix4x4.TRS(SlotPosition(slot), SlotRotation(slot), Vector3.one * (_blueprint.Shape.BrickSize * _settings.BrickScale * scale));

        // ---------------------------------------------------------------- camera
        /// <summary>
        /// The camera rises (straight up, so the painted background stays put) to keep the build top below the frame top
        /// by the margin; it never goes below its home position.
        /// </summary>
        private void FrameCamera(float dt, bool immediate)
        {
            Transform cam = _camera.transform;
            Vector3 top = _site.position + Vector3.up * (_top + 0.5f);
            Vector3 local = Quaternion.Inverse(cam.rotation) * (top - _cameraHome);
            float halfHeight = local.z * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float viewportY = 0.5f + local.y / (2f * halfHeight);
            float pitch = Mathf.Max(0.3f, new Vector2(cam.forward.x, cam.forward.z).magnitude);
            float lift = Mathf.Max(0f, (viewportY - (1f - _settings.TopMargin)) * 2f * halfHeight / pitch);
            _cameraLift = immediate ? lift : Mathf.Lerp(_cameraLift, lift, 1f - Mathf.Exp(-3f * dt));
            cam.position = _cameraHome + Vector3.up * _cameraLift;
        }

        /// <summary>A point above the visible screen (viewport y &gt; 1), at the construction's depth.</summary>
        private Vector3 ScreenTopPoint(float viewportY)
        {
            float depth = Vector3.Dot(_site.position - _camera.transform.position, _camera.transform.forward);
            return _camera.ViewportToWorldPoint(new Vector3(0.5f, viewportY, depth));
        }

        private void Celebrate()
        {
            if (_effects == null) return;
            Vector3 centre = _site.position + Vector3.up * (_top * 0.6f);
            for (int i = 0; i < 8; i++)
            {
                Vector3 at = centre + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (_blueprint.Shape.OuterRadius + 0.5f) + Vector3.up * Random.Range(-2f, 4f);
                _effects.PlayHit(at, Vector3.up, _palette[i % _palette.Count], 1f);
            }
        }

        private static float EaseOutBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        private void OnDestroy()
        {
            if (_hose != null) Destroy(_hose.gameObject);
        }
    }
}
