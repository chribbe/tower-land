using System;
using System.Collections.Generic;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Gameplay
{
    public enum ShotOutcome
    {
        Destroyed,  // hit a matching colour: its group was removed
        Bounced,    // hit a wrong colour
        Missed,     // never touched the tower
    }

    /// <summary>
    /// The thrown cube (flight model from the Tower Buster playable):
    /// - follows the aim arc for a fixed duration while shrinking to a tower cube's size (ease-out);
    /// - collides as a sphere of 80% of its rendered size, swept in the tower's rotating frame (no tunnelling while the
    ///   tower spins), and against the static scenery (ground, plinth, rocks);
    /// - after the arc it keeps flying under gravity and can still hit;
    /// - wrong colour: a "second chance" looks for a matching cube just ahead, else it ricochets away (outward, up, toward
    ///   the camera) and ignores the tower from then on. Landing on the scenery first is a miss.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        private enum State { Waiting, Arc, Free, Dead }

        private State _state;
        private TowerView _tower;
        private ShooterSettings _settings;
        private AimPath _path;
        private float _time;
        private float _startSize;
        private float _endSize;
        private Vector3 _velocity;
        private Vector3 _lastLocal;   // position in tower space, as it was on the previous step
        private Vector3 _spin;
        private float _deadTime;
        private Action<ShotOutcome> _onResolved;
        private Rigidbody _body;

        public int Colour { get; private set; }

        private float Size => transform.localScale.x;
        private float ColliderRadius => Size * 0.5f * _settings.ColliderScale;

        public static Projectile Create(TowerView tower, ShooterSettings settings, int colour, Vector3 position, float size)
        {
            var go = new GameObject("Projectile");
            go.AddComponent<MeshFilter>().sharedMesh = tower.CubeMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = tower.LooseMaterial(colour);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // no drop shadow while it waits

            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var projectile = go.AddComponent<Projectile>();
            projectile._tower = tower;
            projectile._settings = settings;
            projectile.Colour = colour;
            return projectile;
        }

        public void SetColour(int colour)
        {
            Colour = colour;
            GetComponent<MeshRenderer>().sharedMaterial = _tower.LooseMaterial(colour);
        }

        public void Throw(AimPath path, Action<ShotOutcome> onResolved)
        {
            _path = path;
            _onResolved = onResolved;
            _time = 0f;
            _startSize = Size;
            _endSize = _tower.Settings.CubeScale;
            _lastLocal = _tower.SpinRoot.InverseTransformPoint(transform.position);
            _spin = new Vector3(UnityEngine.Random.Range(360f, 540f), UnityEngine.Random.Range(-180f, 180f), 0f);
            _state = State.Arc;
            GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;   // in flight it grounds itself
        }

        private void Update()
        {
            if (_state == State.Arc || _state == State.Free)
            {
                // two sub-steps per frame, like the playable: keeps the swept segments short on slow frames
                float h = Time.deltaTime * 0.5f;
                for (int i = 0; i < 2 && (_state == State.Arc || _state == State.Free); i++)
                    Step(h);
            }
            else if (_state == State.Dead)
            {
                _deadTime += Time.deltaTime;
                if (_deadTime > 1.1f)
                    transform.localScale *= Mathf.Max(0f, 1f - Time.deltaTime * 8f);
                if (_deadTime > 1.6f || transform.position.y < -5f)
                    Destroy(gameObject);
            }
        }

        private void Step(float h)
        {
            Vector3 from = transform.position;
            Vector3 to;
            _time += h;
            if (_state == State.Arc)
            {
                float k = Mathf.Min(1f, _time / _settings.ThrowDuration);
                float shrink = 1f - Mathf.Pow(1f - k, _settings.FlightShrinkPower);
                transform.localScale = Vector3.one * Mathf.Lerp(_startSize, _endSize, shrink);
                to = _path.Evaluate(k);
                _velocity = (to - from) / Mathf.Max(h, 1e-5f);
                if (k >= 1f)
                    _state = State.Free;   // keeps flying (and colliding), like Unity's rigidbody after the path
            }
            else
            {
                _velocity += Vector3.down * (_settings.Gravity * h);
                to = from + _velocity * h;
                if (_time > _settings.ThrowDuration + 2f)
                {
                    Destroy(gameObject);
                    Resolve(ShotOutcome.Missed);
                    return;
                }
            }
            transform.Rotate(_spin * h, Space.Self);

            Vector3 toLocal = _tower.SpinRoot.InverseTransformPoint(to);
            bool towerHit = _tower.SweepLocal(_lastLocal, toLocal, ColliderRadius, out TowerHit hit);
            float towerT = towerHit ? hit.T : float.MaxValue;
            bool sceneryHit = HitsScenery(from, to, out RaycastHit ground, out float groundT);

            if (sceneryHit && groundT <= towerT)
            {
                transform.position = from + (to - from) * groundT;
                OnHitScenery(ground);
                return;
            }
            if (towerHit)
            {
                Vector3 centre = Vector3.Lerp(_lastLocal, toLocal, towerT);
                transform.position = _tower.SpinRoot.TransformPoint(centre);
                OnHitTower(hit, centre);
                return;
            }

            transform.position = to;
            _lastLocal = toLocal;
        }

        /// <summary>Static environment colliders (ground, plinth, rocks) along a segment; ignores debris rigidbodies.</summary>
        private bool HitsScenery(Vector3 from, Vector3 to, out RaycastHit result, out float t)
        {
            result = default;
            t = float.MaxValue;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-5f)
                return false;
            foreach (RaycastHit h in Physics.SphereCastAll(from, ColliderRadius, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.rigidbody != null || h.distance <= 0f || h.distance / distance >= t) continue;
                t = h.distance / distance;
                result = h;
            }
            return t < float.MaxValue;
        }

        private void OnHitTower(TowerHit hit, Vector3 centreLocal)
        {
            IReadOnlyList<TowerGrid> layers = _tower.Layers;
            int layer = hit.Layer;
            CellCoord cell = hit.Cell;
            bool match = layers[layer][cell] == Colour;
            if (!match)
            {
                // Second chance: a matching cube in a sphere just ahead of the projectile along its flight.
                Vector3 velocityLocal = _tower.SpinRoot.InverseTransformDirection(_velocity);
                Vector3 probe = centreLocal + velocityLocal.normalized * _settings.SecondChanceAhead;
                match = TowerRules.Rescue(layers, hit.Layer, Colour, probe, _settings.SecondChanceRadius,
                    _tower.CellCentre, out layer, out cell);
            }

            if (match)
            {
                Vector3 position = transform.position;
                Vector3 back = -_velocity.normalized;
                _tower.RemoveCubes(TowerRules.FindGroup(layers, layer, cell), _settings.RippleStep, position);
                Feel.CubeChunk.Split(_tower.CubeMesh, _tower.LooseMaterial(Colour), position, transform.rotation, Size, back,
                    _tower.Debris);
                if (_tower.Effects != null)
                    _tower.Effects.PlayHit(position, back, _tower.PaletteColour(Colour), Size);
                Resolve(ShotOutcome.Destroyed);
                Destroy(gameObject);
                return;
            }

            _tower.Shockwave(transform.position, _settings.ShockRadius, _settings.ShockStrength);
            Vector3 outward = transform.position - _tower.SpinRoot.position;
            outward.y = 0f;
            if (hit.Inside) outward = -outward;   // off the far wall's back: into the hollow, not through the wall
            Vector3 toCamera = Camera.main != null ? Camera.main.transform.position - transform.position : Vector3.zero;
            toCamera.y = 0f;
            Ricochet(outward.normalized * _settings.RicochetOut + Vector3.up * _settings.RicochetUp
                     + toCamera.normalized * _settings.RicochetToCamera);
            Resolve(ShotOutcome.Bounced);
        }

        private void OnHitScenery(RaycastHit ground)
        {
            transform.position = ground.point + ground.normal * (Size * 0.5f + 0.01f);
            // a live projectile landing on the plinth / ground is a miss: bounce (restitution) and go dead
            float vn = Vector3.Dot(_velocity, ground.normal);
            Vector3 v = _velocity - (1f + _settings.Restitution) * Mathf.Min(0f, vn) * ground.normal;
            v -= Vector3.ProjectOnPlane(v, ground.normal) * 0.15f;   // a little tangential friction
            Ricochet(v);
            Resolve(ShotOutcome.Missed);
        }

        /// <summary>From here the cube is a physics body that ignores the tower: it bounces and settles on the scenery.</summary>
        private void Ricochet(Vector3 velocity)
        {
            _state = State.Dead;
            _deadTime = 0f;
            gameObject.AddComponent<BoxCollider>().sharedMaterial = BouncyMaterial(_settings.Restitution);
            _body = gameObject.AddComponent<Rigidbody>();
            _body.mass = 0.4f;
            _body.useGravity = false;   // arcade gravity from ShooterSettings (FixedUpdate)
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.linearVelocity = velocity;
            _body.angularVelocity = _spin * Mathf.Deg2Rad;
        }

        private static PhysicsMaterial _bouncy;

        private static PhysicsMaterial BouncyMaterial(float restitution)
        {
            if (_bouncy == null)
                _bouncy = new PhysicsMaterial("Projectile") { bounciness = restitution, dynamicFriction = 0.4f, staticFriction = 0.5f };
            return _bouncy;
        }

        private void FixedUpdate()
        {
            if (_body != null)
                _body.AddForce(Vector3.down * _settings.Gravity, ForceMode.Acceleration);
        }

        private void Resolve(ShotOutcome outcome)
        {
            Action<ShotOutcome> callback = _onResolved;
            _onResolved = null;
            callback?.Invoke(outcome);
        }
    }
}
