using UnityEngine;
using UnityEngine.Serialization;

namespace TowerLand.Gameplay
{
    /// <summary>Aim + throw tuning (defaults follow Tower Buster's Fingershot / Projectile values, scaled to our scene).</summary>
    [CreateAssetMenu(menuName = "TowerLand/Shooter Settings", fileName = "ShooterSettings")]
    public sealed class ShooterSettings : ScriptableObject
    {
        [Header("Projectile")]
        [Tooltip("Where the waiting cube sits on screen (viewport coordinates); it rests on the ground there. Right of centre: under the thumb.")]
        [SerializeField] private Vector2 _launchViewport = new Vector2(0.68f, 0.115f);
        [Tooltip("Waiting cube width as a fraction of the screen width.")]
        [SerializeField, Range(0.04f, 0.25f)] private float _projectileScreenWidth = 0.11f;
        [Tooltip("Hover height of the waiting cube's bottom above the ground, in cube sizes.")]
        [SerializeField] private float _hoverHeight = 1.3f;
        [Tooltip("Idle bobbing amplitude (cube sizes) and period (seconds).")]
        [SerializeField] private float _hoverBob = 0.08f;
        [SerializeField] private float _hoverBobPeriod = 1.6f;
        [Tooltip("Scale multiplier while the player is aiming.")]
        [SerializeField] private float _aimingScale = 1.3f;
        [Tooltip("Overshoot of the grow-to-aiming-scale animation (0 = none), and its duration.")]
        [SerializeField] private float _aimPunch = 0.17f;
        [SerializeField] private float _aimPunchDuration = 0.18f;
        [SerializeField] private float _spawnDelay = 0.12f;
        [SerializeField] private float _spawnPopDuration = 0.16f;

        [Header("Next cube (colour swap)")]
        [Tooltip("The queued cube sits this far from the waiting cube on screen (viewport units): bottom-left, as in the Tower Buster playable.")]
        [SerializeField] private Vector2 _nextOffset = new Vector2(-0.2f, -0.03f);
        [Tooltip("Queued cube size relative to the waiting cube.")]
        [SerializeField, Range(0.3f, 1f)] private float _nextScale = 0.6f;
        [Tooltip("Tap target around the queued cube: multiple of its on-screen radius (covers the swap ring), and at least this many pixels.")]
        [SerializeField] private float _nextTapRadius = 2.5f;
        [SerializeField] private float _nextTapMinPixels = 60f;
        [Tooltip("Swap / promote hop: duration (s), arc over the top for the cube going to the main slot and dip under for the one going to the queue (in waiting-cube sizes).")]
        [SerializeField] private float _hopDuration = 0.2f;
        [SerializeField] private float _hopArc = 0.9f;
        [SerializeField] private float _hopDip = 0.5f;
        [Tooltip("Swap hint around the queued cube (a ring of two curved arrows): diameter in queued-cube sizes, spin (degrees/s, positive = clockwise) and opacity.")]
        [SerializeField, Range(1f, 5f)] private float _swapRingSize = 3.3f;
        [SerializeField, Range(-360f, 360f)] private float _swapRingSpin = 70f;
        [SerializeField, Range(0f, 1f)] private float _swapRingAlpha = 0.9f;

        [Header("Aim")]
        [Tooltip("The aim point sits this far above the finger (fraction of screen height). Tower Buster: 0.3 - the bottom 30% of the screen can't be aimed at.")]
        [SerializeField] private float _fingerOffset = 0.3f;
        [Tooltip("Drag amplification from the press point: x sideways, y when dragging up (down is never amplified). Tower Buster: (1, 2).")]
        [SerializeField] private Vector2 _dragMultiplier = new Vector2(1f, 2f);
        [Tooltip("While aiming, the waiting cube turns towards the aim point: share of the full turn, and the most it turns (degrees).")]
        [SerializeField, Range(0f, 1f)] private float _aimTurn = 0.5f;
        [SerializeField, Range(0f, 60f)] private float _aimTurnMax = 25f;
        [Tooltip("How quickly the cube follows the aim with its turn (higher = snappier, lower = lazier).")]
        [SerializeField, Range(1f, 40f)] private float _aimTurnSpeed = 14f;
        [Tooltip("Distance of the aim point when not aiming at the tower.")]
        [SerializeField] private float _missDistance = 45f;
        [Tooltip("Aiming through a hole at the far side: the throw curves onto the aim ray this far (m) in front of the tower, then flies straight along it.")]
        [SerializeField, Range(0.2f, 4f)] private float _holeEntryMargin = 1.2f;
        [Tooltip("Height of the throw arc at mid-flight, metres.")]
        [SerializeField] private float _arcHeight = 0.8f;
        [Tooltip("Aim line smoothness: straight segments along the arc.")]
        [SerializeField, Range(4, 64)] private int _lineSegments = 24;
        [Tooltip("The aim line starts/ends this far along the path (keeps it off the cube and the target).")]
        [FormerlySerializedAs("_dotRange")]
        [SerializeField] private Vector2 _lineRange = new Vector2(0.1f, 1f);
        [Tooltip("Aim line width (metres) when the aimed cube matches / doesn't match.")]
        [SerializeField] private float _lineWidth = 0.8f;
        [SerializeField] private float _lineWidthWrong = 0.55f;
        [Tooltip("Diameter (metres) of the dot capping the line where the throw will hit the tower.")]
        [SerializeField] private float _capSize = 1.6f;
        [FormerlySerializedAs("_dotAlphaWrong")]
        [SerializeField, Range(0f, 1f)] private float _lineAlphaWrong = 0.4f;

        [Header("Throw")]
        [SerializeField] private float _throwDuration = 0.18f;
        [Tooltip("Collider = this fraction of the projectile's rendered half-size (playable: 0.8).")]
        [SerializeField, Range(0.3f, 1.2f)] private float _colliderScale = 0.8f;
        [Tooltip("Ease-out power of the in-flight shrink to tower-cube size (most of it happens early).")]
        [SerializeField] private float _flightShrinkPower = 2.6f;
        [Tooltip("Wrong colour: look for a matching cube in a sphere this far ahead of the projectile, with this radius.")]
        [SerializeField] private float _secondChanceAhead = 1.6f;
        [SerializeField] private float _secondChanceRadius = 1.2f;
        [Tooltip("Break delay per metre from the impact (circular spread). ~0.035 = clearly visible spread.")]
        [SerializeField] private float _rippleStep = 0.035f;

        [Header("Miss")]
        [Tooltip("Wrong-colour ricochet velocity: outward from the tower, up, toward the camera.")]
        [SerializeField] private float _ricochetOut = 9f;
        [SerializeField] private float _ricochetUp = 10f;
        [SerializeField] private float _ricochetToCamera = 3.5f;
        [SerializeField, Range(0f, 1f)] private float _restitution = 0.42f;
        [SerializeField] private float _gravity = 60f;   // twice real-ish gravity: snappier, arcade feel
        [SerializeField] private float _shockRadius = 5f;
        [SerializeField] private float _shockStrength = 0.24f;

        public Vector2 LaunchViewport => _launchViewport;
        public float ProjectileScreenWidth => _projectileScreenWidth;
        public float AimingScale => _aimingScale;
        public float HoverHeight => _hoverHeight;
        public float HoverBob => _hoverBob;
        public float HoverBobPeriod => _hoverBobPeriod;
        public float AimPunch => _aimPunch;
        public float AimPunchDuration => _aimPunchDuration;
        public float SpawnDelay => _spawnDelay;
        public float SpawnPopDuration => _spawnPopDuration;
        public Vector2 NextOffset => _nextOffset;
        public float NextScale => _nextScale;
        public float NextTapRadius => _nextTapRadius;
        public float NextTapMinPixels => _nextTapMinPixels;
        public float HopDuration => _hopDuration;
        public float HopArc => _hopArc;
        public float HopDip => _hopDip;
        public float SwapRingSize => _swapRingSize;
        public float SwapRingSpin => _swapRingSpin;
        public float SwapRingAlpha => _swapRingAlpha;
        public float FingerOffset => _fingerOffset;
        public float HoleEntryMargin => _holeEntryMargin;
        public float AimTurn => _aimTurn;
        public float AimTurnMax => _aimTurnMax;
        public float AimTurnSpeed => _aimTurnSpeed;
        public Vector2 DragMultiplier => _dragMultiplier;
        public float MissDistance => _missDistance;
        public float ArcHeight => _arcHeight;
        public int LineSegments => _lineSegments;
        public Vector2 LineRange => _lineRange;
        public float LineWidth => _lineWidth;
        public float LineWidthWrong => _lineWidthWrong;
        public float CapSize => _capSize;
        public float LineAlphaWrong => _lineAlphaWrong;
        public float ThrowDuration => _throwDuration;
        public float ColliderScale => _colliderScale;
        public float FlightShrinkPower => _flightShrinkPower;
        public float SecondChanceAhead => _secondChanceAhead;
        public float SecondChanceRadius => _secondChanceRadius;
        public float RippleStep => _rippleStep;
        public float RicochetOut => _ricochetOut;
        public float RicochetUp => _ricochetUp;
        public float RicochetToCamera => _ricochetToCamera;
        public float Restitution => _restitution;
        public float Gravity => _gravity;
        public float ShockRadius => _shockRadius;
        public float ShockStrength => _shockStrength;
    }
}
