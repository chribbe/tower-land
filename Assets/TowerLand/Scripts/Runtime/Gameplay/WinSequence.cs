using System.Collections;
using System.Collections.Generic;
using TowerLand.Feel;
using TowerLand.Tower;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TowerLand.Gameplay
{
    /// <summary>
    /// Level won: once the tower is cleared and its debris has settled, a glass vacuum hose (<see cref="VacuumHose"/>) lowers in
    /// from above the screen, settles, spins its suction up, then sweeps once around the tower over every debris pile -
    /// pieces lift off the ground, curl into the mouth anywhere across its opening and flow up the hose as a stream - gulps
    /// the stragglers, yanks back out, and the win screen pops.
    /// Development: D breaks the whole tower at once to jump to it (editor and development builds).
    /// </summary>
    public sealed class WinSequence : MonoBehaviour
    {
        [SerializeField] private TowerView _tower;
        [SerializeField] private Shooter _shooter;
        [SerializeField] private VacuumSettings _settings;
        [SerializeField] private Material _glass;
        [SerializeField] private GameObject _nozzle;
        [SerializeField] private GameObject _jointRing;
        [Tooltip("Particle material for the suction streaks (TowerLand/Particle FX, glow shape).")]
        [SerializeField] private Material _streak;
        [SerializeField] private UI.TitleStyle _titleStyle;
        [Tooltip("Scene the win leads to (meta: the bricks build the player's construction). Missing from the build: the level reloads.")]
        [SerializeField] private string _buildScene = "Build";
        [Tooltip("Seconds the title shows (the scene fading to black behind it) before the build scene opens; a tap skips ahead.")]
        [SerializeField, Range(0.5f, 4f)] private float _titleTime = 1.4f;

        // Pieces still bouncing after this get swallowed anyway.
        private const float SettleTimeout = 1f;
        // Outro after the title pops: camera rise (metres) over this long, scene fading to black behind the title.
        private const float OutroDuration = 1.3f;
        private const float OutroRise = 22f;
        private const float OutroTilt = 20f;
        // Longest the hose lingers for the final gulp before leaving.
        private const float FinalGulp = 0.9f;
        private const int AngleBins = 18;

        private enum Stage { Waiting, Pulled, Hose, Done }

        private struct Piece
        {
            public Transform Transform;
            public Stage Stage;
            public float Start;                   // lifts off at this time (staggered)
            public Vector3 Velocity;              // while pulled: simulated on its own, world space
            public float Strength;                // how closely it follows the air (light / heavy feel)
            public float Seed;                    // flutter noise
            public float EntryAngle;              // its spot across the mouth opening, in the mouth frame
            public float Offset;                  // where it enters, fraction of the mouth / hose radius
            public float Speed;                   // up the hose, m/s
            public Vector3 SpinAxis;
            public float SpinRate;                // tumble, degrees per metre travelled
            public float Scale;
            public float Distance;                // up the hose, from the mouth
            public float HoseAngle;
        }

        private readonly List<Piece> _pieces = new List<Piece>();
        private bool _playing;
        private Camera _camera;
        private Vector3 _cameraHome;
        private float _shake;
        private VacuumHose _hose;
        private ParticleSystem _streaks;
        private int _swallowed;
        private float _suction;   // 0..1: suction strength (capture reach, streaks)
        // Nozzle swing: it hangs off the hose like a pendulum, leaning into its motion and swaying when it stops.
        private Vector3 _lastMouth, _tilt, _tiltVelocity;

        private void OnEnable() => _tower.Cleared += OnCleared;
        private void OnDisable() => _tower.Cleared -= OnCleared;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_playing && Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame && _tower.Layers.Count > 0)
            {
                Vector3 toCamera = Camera.main.transform.position - _tower.transform.position;
                Vector3 front = new Vector3(toCamera.x, 0f, toCamera.z).normalized;
                _tower.BreakAll(_tower.transform.position + Vector3.up * (_tower.Height * 0.5f) + front * _tower.Radius, 0.03f);
            }
#endif
            if (_camera != null && _playing)
            {
                _shake = Mathf.Max(0f, _shake - Time.deltaTime * 2.2f);
                float t = Time.time * 23f, s = _shake * _shake * 0.35f;
                _camera.transform.position = _cameraHome + _camera.transform.rotation
                    * new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * s;
            }
        }

        private void OnCleared()
        {
            if (!_playing)
                StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            _playing = true;
            if (_shooter != null) _shooter.Stop();
            _camera = Camera.main;
            _cameraHome = _camera.transform.position;

            // Let the last pieces land (falling cubes shatter, chunks bounce out of the no-go zone and lock).
            for (float t = 0f; t < SettleTimeout && !Settled(); t += Time.deltaTime)
                yield return null;
            yield return new WaitForSeconds(_settings.StartDelay);

            var cameraData = _camera.GetUniversalAdditionalCameraData();
            CameraOverrideOption opaque = cameraData.requiresColorOption;
            cameraData.requiresColorOption = CameraOverrideOption.On;   // frosted glass samples the scene behind it

            CollectPieces();
            List<Vector3> path = PlanSweep();
            Vector3 anchor = ScreenTopPoint(1.3f);
            Vector3 offscreen = ScreenTopPoint(1.12f);
            _hose = VacuumHose.Create(_settings, _glass, _nozzle, _jointRing, anchor, offscreen);
            CreateStreaks();

            // Enter: swings in from above and a little to the side, decelerating into place; the nozzle leans into the
            // motion and sways as it stops. No suction yet.
            Vector3 entryFrom = offscreen + _camera.transform.right * (path[0].x > _tower.transform.position.x ? -3f : 3f);
            Vector3 mouth = entryFrom;
            _lastMouth = mouth;
            _tilt = _tiltVelocity = Vector3.zero;
            _suction = 0f;
            for (float t = 0f; t < _settings.EnterDuration; t += Time.deltaTime)
            {
                float u = t / _settings.EnterDuration;
                float drop = EaseOutBack(u, 0.9f);
                float side = 1f - Mathf.Pow(1f - u, 3f);   // the sideways drift settles earlier than the drop: a curved arrival
                Vector3 straight = Vector3.LerpUnclamped(offscreen, path[0], drop);
                mouth = straight + (entryFrom - offscreen) * (1f - side);
                Drive(mouth, false, Time.deltaTime);
                yield return null;
            }

            // Gather: hovers while the nozzle's sway settles, rising a touch (a breath in) before it starts.
            Vector3 hover = path[0];
            for (float t = 0f; t < _settings.GatherDuration; t += Time.deltaTime)
            {
                float u = t / _settings.GatherDuration;
                mouth = hover + Vector3.up * (0.35f * u * u * (3f - 2f * u));
                Drive(mouth, false, Time.deltaTime);
                yield return null;
            }

            // Spin-up: the suction builds, the hose dips back down and starts to twist, nearest pieces go first.
            hover = mouth;
            for (float t = 0f; t < _settings.SpinUpDuration; t += Time.deltaTime)
            {
                float u = t / _settings.SpinUpDuration;
                _suction = u * u;
                mouth = Vector3.Lerp(hover, path[0], u * u * (3f - 2f * u));
                _hose.Twist = Mathf.MoveTowards(_hose.Twist, 1f, Time.deltaTime * 2f);
                Drive(mouth, true, Time.deltaTime);
                yield return null;
            }
            _suction = 1f;
            _shake = 0.3f;

            // Sweep once over all the piles.
            float length = PathLength(path);
            for (float t = 0f; t < _settings.SweepDuration; t += Time.deltaTime)
            {
                float u = t / _settings.SweepDuration;
                float eased = u - Mathf.Sin(2f * Mathf.PI * u) / (2f * Mathf.PI) * 0.4f;   // brisk, soft at both ends
                mouth = PathPoint(path, eased * length) + Vector3.up * (Mathf.Sin(t * 9f) * 0.18f);
                _hose.Twist = Mathf.MoveTowards(_hose.Twist, 1f, Time.deltaTime * 2f);
                Drive(mouth, true, Time.deltaTime);
                yield return null;
            }

            // Final gulp: everything left is pulled in from wherever it is. The hose doesn't wait for the last stragglers:
            // they chase the mouth as it leaves, and pieces still travelling up the hose ride along.
            _shake = 0.7f;
            CaptureAll();
            Vector3 gulpFrom = mouth, gulpTo = path[path.Count - 1] + Vector3.up * 1.2f;
            for (float t = 0f; Outside() > 0 && t < FinalGulp; t += Time.deltaTime)
            {
                mouth = Vector3.Lerp(gulpFrom, gulpTo, EaseOutBack(Mathf.Clamp01(t / 0.4f), 1.4f));
                Drive(mouth, false, Time.deltaTime);
                yield return null;
            }

            // Exit: a little anticipation dip, then yanked up out of the screen.
            Vector3 exitFrom = mouth, exitTo = ScreenTopPoint(1.35f);
            for (float t = 0f; t < _settings.ExitDuration; t += Time.deltaTime)
            {
                float u = t / _settings.ExitDuration;
                float dip = Mathf.Sin(Mathf.Clamp01(u / 0.18f) * Mathf.PI) * 0.6f;
                float rise = Mathf.Pow(Mathf.Clamp01((u - 0.1f) / 0.9f), 2.2f);   // yanked: accelerates all the way out
                mouth = Vector3.LerpUnclamped(exitFrom, exitTo, rise) + Vector3.down * dip;
                _hose.Twist = Mathf.MoveTowards(_hose.Twist, 0f, Time.deltaTime * 2f);
                Drive(mouth, false, Time.deltaTime);
                yield return null;
            }

            foreach (Piece piece in _pieces)
                if (piece.Stage != Stage.Done && piece.Transform != null)
                    Destroy(piece.Transform.gameObject);   // off screen by now
            Destroy(_hose.gameObject);
            if (_streaks != null) Destroy(_streaks.gameObject, 0.5f);
            cameraData.requiresColorOption = opaque;
            _camera.transform.position = _cameraHome;
            _shake = 0f;

            Celebrate();
            AsyncOperation next = PreloadBuildScene();
            UI.WinScreen.Show(_titleStyle, () => StartCoroutine(Leave(next)), autoContinue: _titleTime);

            // Outro: the camera takes off towards the sky, faster and faster, while the scene fades to black behind the title.
            Vector3 from = _camera.transform.position;
            Quaternion fromRotation = _camera.transform.rotation;
            Quaternion toRotation = Quaternion.AngleAxis(-OutroTilt, _camera.transform.right) * fromRotation;
            for (float t = 0f; t < OutroDuration; t += Time.deltaTime)
            {
                float u = t / OutroDuration;
                float e = 0.12f * u + 0.88f * u * u * u;   // takes off at once, then faster and faster as the scene fades out
                _camera.transform.SetPositionAndRotation(from + Vector3.up * (OutroRise * e), Quaternion.Slerp(fromRotation, toRotation, e));
                yield return null;
            }
        }

        /// <summary>Loads the build scene in the background while the title shows; it opens once the screen is black.</summary>
        private AsyncOperation PreloadBuildScene()
        {
            if (string.IsNullOrEmpty(_buildScene) || !Application.CanStreamedLevelBeLoaded(_buildScene)) return null;
            Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
            AsyncOperation load = SceneManager.LoadSceneAsync(_buildScene);
            load.allowSceneActivation = false;
            return load;
        }

        private IEnumerator Leave(AsyncOperation next)
        {
            if (next == null)
            {
                SceneManager.LoadScene(gameObject.scene.buildIndex);
                yield break;
            }
            Application.backgroundLoadingPriority = ThreadPriority.High;
            next.allowSceneActivation = true;
        }

        private bool Settled()
        {
            if (FindAnyObjectByType<FallingCube>() != null) return false;
            foreach (CubeChunk chunk in FindObjectsByType<CubeChunk>(FindObjectsSortMode.None))
                if (!chunk.IsResting) return false;
            return true;
        }

        private void CollectPieces()
        {
            _pieces.Clear();
            foreach (CubeChunk chunk in FindObjectsByType<CubeChunk>(FindObjectsSortMode.None))
                _pieces.Add(new Piece { Transform = chunk.transform, Stage = Stage.Waiting, Scale = chunk.transform.localScale.x });
        }

        /// <summary>
        /// Mouth waypoints over the debris: one per occupied angular sector around the tower, at the pieces' mean distance
        /// and above their highest one, in order round the tower starting just off the camera axis (so the sweep starts
        /// on one side in front, goes round the back and finishes on the other side in front).
        /// </summary>
        private List<Vector3> PlanSweep()
        {
            Vector3 centre = _tower.transform.position;
            Vector3 toCamera = _camera.transform.position - centre;
            var front = new Vector2(toCamera.x, toCamera.z).normalized;
            var count = new int[AngleBins];
            var radius = new float[AngleBins];
            var top = new float[AngleBins];
            foreach (Piece piece in _pieces)
            {
                Vector3 p = piece.Transform.position - centre;
                float angle = Vector2.SignedAngle(front, new Vector2(p.x, p.z));
                int bin = Mathf.Clamp((int)(((angle + 360f) % 360f) / (360f / AngleBins)), 0, AngleBins - 1);
                count[bin]++;
                radius[bin] += new Vector2(p.x, p.z).magnitude;
                top[bin] = Mathf.Max(top[bin], piece.Transform.position.y);
            }

            var path = new List<Vector3>();
            float floor = centre.y - 1.2f;
            for (int bin = 0; bin < AngleBins; bin++)
            {
                if (count[bin] < 3) continue;
                float angle = (bin + 0.5f) * 360f / AngleBins;
                Vector2 dir = Rotate(front, angle);
                float r = Mathf.Max(radius[bin] / count[bin], _tower.FaceRadius(0) * 0.6f);
                float y = Mathf.Max(top[bin], floor) + _settings.MouthHeight;
                path.Add(new Vector3(centre.x + dir.x * r, y, centre.z + dir.y * r));
            }
            if (path.Count == 0)
                path.Add(centre + Vector3.up * _settings.MouthHeight);
            if (path.Count == 1)
                path.Add(path[0] + Vector3.right * 0.5f);
            return path;
        }

        /// <summary>One frame of the hose: aim (with the nozzle's swing), capture what's under the mouth, move every piece, then let the hose catch up.</summary>
        private void Drive(Vector3 mouth, bool capture, float dt)
        {
            _hose.Aim(mouth, Swing(mouth, dt));
            if (capture)
                CaptureNear(mouth);
            _hose.Step(dt);
            MovePieces(dt);
            EmitStreaks(dt);
        }

        /// <summary>
        /// Which way the nozzle faces: it leans into its horizontal motion, through an under-damped spring, so it trails
        /// a little when setting off and sways past straight when it stops, like something hanging off a hose.
        /// </summary>
        private Vector3 Swing(Vector3 mouth, float dt)
        {
            Vector3 velocity = (mouth - _lastMouth) / Mathf.Max(dt, 1e-4f);
            _lastMouth = mouth;
            Vector3 want = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z) * 0.025f, 0.5f);
            _tiltVelocity += ((want - _tilt) * 55f - _tiltVelocity * 4.5f) * dt;
            _tilt += _tiltVelocity * dt;
            return (Vector3.down + _tilt).normalized;
        }

        private void CaptureNear(Vector3 mouth)
        {
            float reach = _settings.CaptureRadius * Mathf.Lerp(0.6f, 1f, _suction);
            float r2 = reach * reach;
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece piece = _pieces[i];
                if (piece.Stage != Stage.Waiting || piece.Transform == null) continue;
                Vector3 d = piece.Transform.position - mouth;
                float h2 = d.x * d.x + d.z * d.z;
                if (h2 > r2) continue;
                _pieces[i] = Capture(piece, Mathf.Sqrt(h2) / reach);
            }
        }

        private void CaptureAll()
        {
            for (int i = 0; i < _pieces.Count; i++)
                if (_pieces[i].Stage == Stage.Waiting && _pieces[i].Transform != null)
                {
                    // Last call: no waiting their turn - every straggler is ripped off at once, already flying at the mouth.
                    Piece piece = Capture(_pieces[i], 1f);
                    piece.Start = Time.time + Random.value * 0.06f;
                    Vector3 toMouth = (_hose.Mouth - piece.Transform.position).normalized;
                    piece.Velocity = (toMouth + Vector3.up * 0.3f).normalized * (_settings.AirMin * 2.5f);
                    _pieces[i] = piece;
                }
        }

        /// <summary>
        /// The suction grabs a piece: each gets its own lift-off delay (further ones later, plus chance), grip strength,
        /// kick, tumble and spot across the mouth, so neighbours peel off and fly in independently.
        /// </summary>
        private Piece Capture(Piece piece, float far)
        {
            CubeChunk chunk = piece.Transform.GetComponent<CubeChunk>();
            if (chunk != null) chunk.Detach();
            piece.Stage = Stage.Pulled;
            piece.Start = Time.time + _settings.PullDelay * (0.45f * Mathf.Clamp01(far) + 0.55f * Random.value);
            piece.Strength = Random.Range(0.7f, 1.3f);
            piece.Seed = Random.value * 100f;
            piece.Velocity = Vector3.up * Random.Range(_settings.LiftKick.x, _settings.LiftKick.y)
                + Vector3.Scale(Random.insideUnitSphere, new Vector3(1.5f, 0f, 1.5f));
            piece.EntryAngle = Random.value * Mathf.PI * 2f;
            piece.Offset = _settings.IntakeSpread * Mathf.Sqrt(Random.value);   // anywhere over the opening, evenly
            piece.SpinAxis = Random.onUnitSphere;
            piece.SpinRate = Random.Range(25f, 70f);
            return piece;
        }

        private void MovePieces(float dt)
        {
            _hose.ClearLoad();
            Vector3 mouth = _hose.Mouth;
            Vector3 axis = -_hose.MouthDirection;
            float mouthRadius = _hose.MouthRadius;
            float travel = _hose.TravelLength;
            for (int i = 0; i < _pieces.Count; i++)
            {
                Piece piece = _pieces[i];
                if (piece.Transform == null || piece.Stage == Stage.Waiting || piece.Stage == Stage.Done) continue;
                Transform t = piece.Transform;

                if (piece.Stage == Stage.Pulled)
                {
                    float age = Time.time - piece.Start;
                    if (age < 0f) continue;   // still waiting its turn on the ground
                    Vector3 p = t.position;

                    // Carried by the air rushing into the mouth: the flow heads straight for this piece's spot just in front
                    // of the opening, fast close to it, falling off with distance, picking up the longer the piece flies
                    // (nothing lingers). The piece is dragged towards the air's velocity - it never gets a sideways push of
                    // its own, so it cannot settle into an orbit - against its weight, with a little turbulence. Within the
                    // funnel distance it is committed: it heads straight in, momentum no longer carries it round the nose.
                    Vector3 toSpot = _hose.MouthPoint(-0.3f, piece.EntryAngle, piece.Offset * mouthRadius) - p;
                    float distance = toSpot.magnitude;
                    Vector3 toward = toSpot / Mathf.Max(distance, 1e-3f);
                    float falloff = distance / _settings.AirFalloff;
                    float speed = Mathf.Max(_settings.AirSpeed / (1f + falloff * falloff), _settings.AirMin) * (1f + _settings.AirRamp * age);
                    Vector3 air = toward * speed;
                    Vector3 fromAxis = p - mouth;
                    fromAxis -= axis * Vector3.Dot(fromAxis, axis);
                    float r = fromAxis.magnitude;
                    if (r > 1e-3f)
                        air += Vector3.Cross(axis, fromAxis / r) * (speed * _settings.Vortex * Mathf.Clamp01(1f - distance / 3f));
                    float n = Time.time * 3f;
                    Vector3 turbulence = new Vector3(Mathf.PerlinNoise(piece.Seed, n) - 0.5f, Mathf.PerlinNoise(piece.Seed + 7f, n) - 0.5f,
                        Mathf.PerlinNoise(piece.Seed + 13f, n) - 0.5f) * (2f * _settings.Flutter);
                    float grip = 1f - Mathf.Exp(-_settings.Grip * piece.Strength * dt);
                    piece.Velocity += (air - piece.Velocity) * grip + (Vector3.down * _settings.Weight + turbulence) * dt;
                    if (distance < _settings.Funnel)
                    {
                        // Committed: bend the velocity onto the straight line in, keeping (or gaining) speed.
                        float commit = 1f - distance / _settings.Funnel;
                        Vector3 straight = toward * Mathf.Max(piece.Velocity.magnitude, speed);
                        piece.Velocity = Vector3.Lerp(piece.Velocity, straight, Mathf.Clamp01(commit * 2f));
                    }
                    Vector3 step = piece.Velocity * dt;
                    p += step;

                    Vector3 frame = _hose.ToMouthFrame(p);   // (along the axis, angle, radius)
                    bool inside = frame.x > -0.35f && frame.z < mouthRadius;
                    if (inside || distance < 0.3f + step.magnitude)
                    {
                        piece.Stage = Stage.Hose;
                        piece.Distance = Mathf.Max(frame.x, 0f);
                        piece.HoseAngle = frame.y;
                        piece.Offset = Mathf.Clamp(frame.z / mouthRadius, 0f, 0.9f);
                        piece.Speed = Mathf.Max(_settings.IntakeSpeed, piece.Velocity.magnitude);   // no stall at the mouth
                        _hose.Gulp(0.012f);
                        if (++_swallowed % 12 == 0 && _tower.Effects != null)
                            _tower.Effects.PlayCubeBreak(mouth, Color.white, 0.3f);
                    }
                    else
                    {
                        t.position = p;
                        t.rotation = Quaternion.AngleAxis(piece.SpinRate * step.magnitude, piece.SpinAxis) * t.rotation;
                        t.localScale = Vector3.one * (piece.Scale * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01((distance - 0.5f) / 3f)));
                        _pieces[i] = piece;
                        continue;
                    }
                }

                // Up the hose as a stream: from its own spot in the mouth, speeding up, slowly corkscrewing round the axis.
                piece.Speed = Mathf.MoveTowards(piece.Speed, _settings.HoseSpeed, _settings.HoseAcceleration * dt);   // eases to the hose speed
                piece.Distance += piece.Speed * dt;
                piece.HoseAngle += _settings.HoseSpin * dt;
                if (piece.Distance >= travel)
                {
                    piece.Stage = Stage.Done;
                    Destroy(t.gameObject);
                    _pieces[i] = piece;
                    continue;
                }
                // Keeps its place across the flow; the outermost drift in a little so they stay clear of the glass.
                float offset = Mathf.Lerp(piece.Offset, Mathf.Min(piece.Offset, 0.5f), Mathf.Clamp01(piece.Distance / _hose.NozzleLength));
                t.position = _hose.PointInside(piece.Distance, piece.HoseAngle, offset);
                t.rotation = Quaternion.AngleAxis(dt * 240f, piece.SpinAxis) * t.rotation;
                t.localScale = Vector3.one * (piece.Scale * 0.8f);
                _hose.AddLoad(piece.Distance);
                _pieces[i] = piece;
            }
        }

        /// <summary>Pieces not yet swallowed (still on the ground or flying towards the mouth).</summary>
        private int Outside()
        {
            int n = 0;
            foreach (Piece piece in _pieces)
                if ((piece.Stage == Stage.Waiting || piece.Stage == Stage.Pulled) && piece.Transform != null) n++;
            return n;
        }

        // ---------------------------------------------------------------- suction streaks
        private void CreateStreaks()
        {
            var go = new GameObject("VacuumStreaks");
            _streaks = go.AddComponent<ParticleSystem>();
            _streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _streaks.main;
            main.playOnAwake = false;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            var emission = _streaks.emission; emission.enabled = false;
            var shape = _streaks.shape; shape.enabled = false;
            var colour = _streaks.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _streak;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _streaks.Play();
        }

        /// <summary>Air rushing into the mouth: short white streaks circling inwards and up.</summary>
        private void EmitStreaks(float dt)
        {
            if (_streaks == null) return;
            int count = Mathf.RoundToInt(dt * 90f * _suction + Random.value * _suction);
            Vector3 mouth = _hose.Mouth;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(1.4f, 3.2f);
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = new Vector3(-radial.z, 0f, radial.x);
                var e = new ParticleSystem.EmitParams
                {
                    position = mouth + radial * radius + Vector3.down * Random.Range(0.6f, 1.6f),
                    velocity = tangent * Random.Range(9f, 14f) - radial * Random.Range(6f, 10f) + Vector3.up * Random.Range(3f, 6f),
                    startSize = Random.Range(0.08f, 0.16f),
                    startLifetime = Random.Range(0.18f, 0.3f),
                    startColor = new Color(1f, 1f, 1f, 0.85f),
                    applyShapeToPosition = false,
                };
                _streaks.Emit(e, 1);
            }
        }

        /// <summary>Confetti-like bursts of sparkles around the plinth as the win screen pops.</summary>
        private void Celebrate()
        {
            if (_tower.Effects == null) return;
            Vector3 centre = _tower.transform.position + Vector3.up * 1.5f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = centre + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 3f + Vector3.up * Random.Range(0f, 3f);
                _tower.Effects.PlayHit(at, Vector3.up, _tower.PaletteColour(i), 0.8f);
            }
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>A point above the visible screen (viewport y &gt; 1), at the tower's depth.</summary>
        private Vector3 ScreenTopPoint(float viewportY)
        {
            float depth = Vector3.Dot(_tower.transform.position - _camera.transform.position, _camera.transform.forward);
            return _camera.ViewportToWorldPoint(new Vector3(0.5f, viewportY, depth));
        }

        private static float PathLength(List<Vector3> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        /// <summary>Point <paramref name="distance"/> metres along the waypoints (Catmull-Rom between them).</summary>
        private static Vector3 PathPoint(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (distance > segment && i < path.Count - 1) { distance -= segment; continue; }
                float t = Mathf.Clamp01(distance / Mathf.Max(segment, 1e-4f));
                Vector3 p0 = path[Mathf.Max(i - 2, 0)], p1 = path[i - 1], p2 = path[i], p3 = path[Mathf.Min(i + 1, path.Count - 1)];
                return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
            }
            return path[path.Count - 1];
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static float EaseOutBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

    }
}
