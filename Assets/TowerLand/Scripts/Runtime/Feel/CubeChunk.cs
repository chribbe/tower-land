using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>
    /// A broken cube splits into 4 half-size cubes (its 2x2 front quarters) that tumble down with real physics and land
    /// on the plinth / ground. Then either they rest a moment and shrink away, or (with a <see cref="DebrisSweeper"/>) they
    /// stay there as debris: once landed and slowed down a piece locks in place (kinematic: no simulation cost, later
    /// chunks still pile onto it, projectiles - which ignore rigidbodies - fly through). A piece that lands in the no-go
    /// zone in front of the tower first bounces its way out of it, passing through other debris, then settles; one that
    /// can't get out within <see cref="TowerLand.Tower.TowerVisualSettings.EscapeMaxTime"/> just settles where it is.
    /// </summary>
    public sealed class CubeChunk : MonoBehaviour
    {
        // Arcade tuning: chunks pop out fast, drop hard (much stronger than world gravity), and clear away quickly.
        public const float Gravity = 60f;
        private const float RestTime = 0.75f;
        private const float ShrinkTime = 0.22f;
        // Persisting chunks lock once landed and nearly still (or after a while, whatever they are doing).
        private const float SettleMinAge = 0.3f;
        private const float LockSpeed = 1.2f;
        private const float SettleMaxAge = 3f;

        private static PhysicsMaterial _physicsMaterial;

        private float _age;
        private Vector3 _scale;
        private Rigidbody _body;
        private DebrisSweeper _debris;
        private bool _persist;
        private bool _landed;
        // Bouncing out of the no-go zone (on the escaping layer: passes through other debris).
        private bool _escaping;
        private bool _gaveUp;
        // A few pieces ignore the no-go zone altogether, so debris still lands there now and then, naturally.
        private bool _ignoresZone;
        private float _escapeAge;

        public float HalfSize => _scale.x * 0.5f;
        /// <summary>Locked in place (or never kept): nothing more will move it.</summary>
        public bool IsResting => _body == null || _body.isKinematic;

        /// <summary>Hands the piece over to a script (the win-sequence vacuum): no more physics or behaviour.</summary>
        public Transform Detach()
        {
            if (_body != null) Destroy(_body);
            if (TryGetComponent(out Collider collider)) Destroy(collider);
            _body = null;
            enabled = false;
            Destroy(this);
            return transform;
        }

        /// <param name="rotation">Orientation of the broken cube (its +Z = facing outward from the tower).</param>
        /// <param name="outward">Direction the pieces are pushed (away from the tower / impact).</param>
        /// <param name="debris">Leave the pieces on the floor, tended by this sweeper; null shrinks them away.</param>
        public static void Split(Mesh mesh, Material material, Vector3 position, Quaternion rotation, float cubeSize, Vector3 outward,
            DebrisSweeper debris)
        {
            if (_physicsMaterial == null)
                _physicsMaterial = new PhysicsMaterial("CubeChunk") { bounciness = 0.3f, dynamicFriction = 0.5f, staticFriction = 0.6f };

            float half = cubeSize * 0.5f;
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -0.5f : 0.5f;
                float sy = (i & 2) == 0 ? -0.5f : 0.5f;
                Vector3 offset = (right * sx + up * sy) * half;

                var go = new GameObject("CubeChunk");
                go.transform.SetPositionAndRotation(position + offset, rotation);
                go.transform.localScale = Vector3.one * (half * 0.96f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<BoxCollider>().sharedMaterial = _physicsMaterial;
                if (debris != null && debris.DebrisLayer >= 0)
                    go.layer = debris.DebrisLayer;

                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.2f;
                body.useGravity = false;   // own, heavier gravity (FixedUpdate)
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.maxAngularVelocity = 30f;
                body.linearVelocity = outward * Random.Range(4.5f, 7.5f) + offset.normalized * Random.Range(2f, 3.5f)
                    + Vector3.up * Random.Range(3f, 6f);
                body.angularVelocity = Random.insideUnitSphere * 14f;

                var chunk = go.AddComponent<CubeChunk>();
                chunk._scale = go.transform.localScale;
                chunk._body = body;
                chunk._debris = debris;
                chunk._persist = debris != null;
                chunk._ignoresZone = debris != null && Random.value < debris.Settings.IgnoreZoneChance;
            }
        }

        private void FixedUpdate()
        {
            _body.AddForce(Vector3.down * Gravity, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            _landed = true;
            if (!_persist || _gaveUp || _ignoresZone) return;

            if (_escaping)
            {
                if (_debris.InEscapeArea(transform.position)) Hop();
                else EndEscape();   // out (with a margin): from here on it bounces and settles like any piece
            }
            else if (_debris.InNoGoZone(transform.position))
            {
                // Landed in front of the tower (on the floor or on other debris): bounce out, through the other pieces.
                _escaping = true;
                _escapeAge = 0f;
                if (_debris.EscapingLayer >= 0)
                    gameObject.layer = _debris.EscapingLayer;
                Hop();
            }
        }

        /// <summary>One bounce out of the no-go zone: round the tower towards its back, with a tumble.</summary>
        private void Hop()
        {
            var s = _debris.Settings;
            _body.linearVelocity = _debris.EscapeDirection(transform.position) * Random.Range(s.EscapeHopSpeed.x, s.EscapeHopSpeed.y)
                                   + Vector3.up * Random.Range(s.EscapeHopUp.x, s.EscapeHopUp.y);
            _body.angularVelocity = Random.insideUnitSphere * 12f;
        }

        private void EndEscape()
        {
            _escaping = false;
            _age = 0f;   // settle timers start now
            if (_debris.DebrisLayer >= 0)
                gameObject.layer = _debris.DebrisLayer;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (transform.position.y < -10f)
            {
                Destroy(gameObject);
                return;
            }

            if (_persist)
            {
                if (_escaping)
                {
                    _escapeAge += Time.deltaTime;
                    if (_escapeAge > _debris.Settings.EscapeMaxTime)
                    {
                        _gaveUp = true;   // a few pieces may stay in the zone: settles wherever it lands
                        EndEscape();
                    }
                    return;
                }

                if ((_age > SettleMinAge && _landed && _body.linearVelocity.sqrMagnitude < LockSpeed * LockSpeed) || _age > SettleMaxAge)
                    Freeze();
                return;
            }

            if (_age > RestTime)
            {
                float k = (_age - RestTime) / ShrinkTime;
                transform.localScale = _scale * Mathf.Max(0f, 1f - k * k * k);   // holds, then snaps away
                if (k >= 1f)
                    Destroy(gameObject);
            }
        }

        private void Freeze()
        {
            _body.interpolation = RigidbodyInterpolation.None;
            _body.isKinematic = true;
            enabled = false;   // no more Update / gravity
        }
    }
}
