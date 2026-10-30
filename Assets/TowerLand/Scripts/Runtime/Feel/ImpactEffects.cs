using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>
    /// Impact particles (one shared set of particle systems, emitted on demand - no allocations per hit):
    /// a white flash, an expanding shock ring, sparkle stars and puffs in the cube's colour.
    /// </summary>
    public sealed class ImpactEffects : MonoBehaviour
    {
        [SerializeField] private Material _glow;
        [SerializeField] private Material _star;
        [SerializeField] private Material _ring;

        private ParticleSystem _flash, _rings, _stars, _puffs, _pops;

        private void Awake()
        {
            _flash = Create("Flash", _glow, 4);
            _rings = Create("Ring", _ring, 4);
            _stars = Create("Sparkles", _star, 64);
            _puffs = Create("Puffs", _glow, 64);
            _pops = Create("CubePops", _star, 256);
        }

        /// <summary>A projectile broke a colour group at <paramref name="position"/>.</summary>
        public void PlayHit(Vector3 position, Vector3 outward, Color colour, float size)
        {
            var e = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };

            e.startColor = Color.white; e.startSize = size * 6f; e.startLifetime = 0.18f; e.velocity = Vector3.zero;
            _flash.Emit(e, 1);
            e.startColor = Color.Lerp(colour, Color.white, 0.25f); e.startSize = size * 4.5f; e.startLifetime = 0.24f;
            _flash.Emit(e, 1);

            e.startColor = new Color(1f, 1f, 1f, 0.9f); e.startSize = size * 2.4f; e.startLifetime = 0.32f;
            _rings.Emit(e, 1);

            for (int i = 0; i < 18; i++)
            {
                e.velocity = (outward * 0.8f + Random.insideUnitSphere).normalized * Random.Range(9f, 18f);
                e.startSize = size * Random.Range(0.8f, 1.6f);
                e.startLifetime = Random.Range(0.35f, 0.6f);
                e.startColor = Random.value < 0.35f ? Color.Lerp(colour, Color.white, 0.5f) : Color.white;
                e.rotation = Random.Range(0f, 45f);
                _stars.Emit(e, 1);
            }

            for (int i = 0; i < 12; i++)
            {
                e.velocity = (outward * 0.6f + Random.insideUnitSphere).normalized * Random.Range(2.5f, 6f);
                e.startSize = size * Random.Range(1.4f, 2.4f);
                e.startLifetime = Random.Range(0.25f, 0.4f);
                e.startColor = new Color(colour.r, colour.g, colour.b, 0.55f);
                e.rotation = 0f;
                _puffs.Emit(e, 1);
            }
        }

        /// <summary>Small sparkle when a single tower cube breaks (kept light: groups can be large).</summary>
        public void PlayCubeBreak(Vector3 position, Color colour, float size)
        {
            var e = new ParticleSystem.EmitParams
            {
                position = position, applyShapeToPosition = false,
                velocity = Random.insideUnitSphere * 2f,
                startSize = size * Random.Range(1f, 1.6f),
                startLifetime = Random.Range(0.2f, 0.3f),
                startColor = Color.Lerp(colour, Color.white, 0.6f),
                rotation = Random.Range(0f, 45f),
            };
            _pops.Emit(e, 1);
        }

        private ParticleSystem Create(string name, Material material, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;

            // Shrink + fade over life; rings grow instead.
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = material == _ring
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 2.6f))
                : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0.9f), new Keyframe(1f, 0f)));
            var colourOverLife = ps.colorOverLifetime;
            colourOverLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colourOverLife.color = fade;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;
            drag.multiplyDragByParticleSize = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
