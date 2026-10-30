using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>
    /// A tower cube left without support: tumbles off the tower under heavy arcade gravity and shatters into chunks on
    /// the first static surface it lands on (ground, plinth, rock, or settled debris). Other falling cubes and moving
    /// chunks don't shatter it.
    /// </summary>
    public sealed class FallingCube : MonoBehaviour
    {
        private const float Gravity = 60f;
        private const float MaxLife = 3f;
        // A big collapse lands many cubes in the same frame: cap the shatters per frame. Over the cap, kept debris waits
        // for a later frame; otherwise the cube just vanishes.
        private const int MaxShattersPerFrame = 24;

        private static int _shatterFrame = -1;
        private static int _shattersThisFrame;

        private Rigidbody _body;
        private Mesh _mesh;
        private Material _material;
        private Color _colour;
        private ImpactEffects _effects;
        private float _age;
        private bool _shattered;
        private DebrisSweeper _debris;
        private bool _persist;
        private bool _shatterPending;
        private Vector3 _pendingBounce;

        public static void Launch(GameObject cube, Vector3 velocity, Mesh mesh, Material material, Color colour, ImpactEffects effects,
            DebrisSweeper debris)
        {
            var collider = cube.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.95f;
            var body = cube.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 6f;
            if (debris != null && debris.DebrisLayer >= 0)
                cube.layer = debris.DebrisLayer;   // pieces bouncing out of the no-go zone pass through it
            var falling = cube.AddComponent<FallingCube>();
            falling._body = body;
            falling._mesh = mesh;
            falling._material = material;
            falling._colour = colour;
            falling._effects = effects;
            falling._debris = debris;
            falling._persist = debris != null;
        }

        private void FixedUpdate() => _body.AddForce(Vector3.down * Gravity, ForceMode.Acceleration);

        private void Update()
        {
            _age += Time.deltaTime;
            if (transform.position.y < -10f)
                Destroy(gameObject);
            else if (_shatterPending)
                TryShatter(_pendingBounce);
            else if (_age > MaxLife)
            {
                if (_persist) TryShatter(Vector3.up);   // stuck somewhere: still leave its pieces
                else Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_shattered || _shatterPending || (collision.rigidbody != null && !collision.rigidbody.isKinematic))
                return;   // only static scenery (or settled debris) breaks it
            TryShatter(Vector3.Reflect(_body.linearVelocity, collision.GetContact(0).normal).normalized);
        }

        private void TryShatter(Vector3 bounce)
        {
            if (_shatterFrame != Time.frameCount) { _shatterFrame = Time.frameCount; _shattersThisFrame = 0; }
            if (++_shattersThisFrame > MaxShattersPerFrame)
            {
                if (_persist)
                {
                    _shatterPending = true;   // try again next frame
                    _pendingBounce = bounce;
                    return;
                }
                _shattered = true;
                Destroy(gameObject);
                return;
            }

            _shattered = true;
            _shatterPending = false;
            CubeChunk.Split(_mesh, _material, transform.position, transform.rotation, transform.localScale.x, bounce, _debris);
            if (_effects != null)
                _effects.PlayCubeBreak(transform.position, _colour, transform.localScale.x);
            Destroy(gameObject);
        }
    }
}
