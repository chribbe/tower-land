using UnityEngine;

namespace TowerLand.Tower
{
    [CreateAssetMenu(menuName = "TowerLand/Tower Visual Settings", fileName = "TowerVisualSettings")]
    public sealed class TowerVisualSettings : ScriptableObject
    {
        [Tooltip("Distance between neighbouring cube centres (cube mesh is 1 unit). >1 leaves a visible seam.")]
        [SerializeField, Min(0.5f)] private float _cubeSpacing = 1.04f;

        [Tooltip("Cube size relative to the 1-unit mesh. Below the spacing it opens the wedge gaps between columns, so each cube's shaded side face shows around the cylinder.")]
        [SerializeField, Range(0.7f, 1.1f)] private float _cubeScale = 1f;

        [Header("Irregularity (subtle, playful)")]
        [Tooltip("Max random offset of each cube from its grid slot, in metres (per axis).")]
        [SerializeField, Range(0f, 0.1f)] private float _positionJitter = 0f;

        [Tooltip("Max random tilt of each cube, in degrees (per axis).")]
        [SerializeField, Range(0f, 8f)] private float _rotationJitter = 0f;

        [Tooltip("Auto-spin speed of the tower, degrees per second.")]
        [SerializeField] private float _spinDegreesPerSecond = 18f;

        [Header("Base")]
        [Tooltip("How far the plinth's edge sits inside the tower's outer cube faces, in metres. The overhanging bottom cubes cast their shadow onto the plinth for depth. Applied by Tools/TowerLand/Setup World1 Environment.")]
        [SerializeField, Range(0f, 1f)] private float _plinthInset = 0.35f;
        [Tooltip("Radius (m) of the pole up the tower's axis that the innermost layer hangs on (seen through holes in the tower).")]
        [SerializeField, Range(0.05f, 1f)] private float _poleRadius = 0.28f;

        [Header("Debris")]
        [Tooltip("Broken pieces stay on the floor (to be collected at the end of the level) instead of shrinking away.")]
        [SerializeField] private bool _keepDebris = true;

        [Tooltip("The no-go zone: ground in front of the tower (facing the player) reaching this far past the outer cube faces, in metres. Debris landing there bounces out of it.")]
        [SerializeField, Min(0f)] private float _clearZoneReach = 5f;

        [Tooltip("Half the opening of the no-go zone around the camera direction, in degrees.")]
        [SerializeField, Range(10f, 150f)] private float _clearZoneHalfAngle = 35f;

        [Tooltip("Horizontal speed (min, max, m/s) of each bounce out of the no-go zone - round the tower towards its back.")]
        [SerializeField] private Vector2 _escapeHopSpeed = new Vector2(4f, 6f);

        [Tooltip("Upward speed (min, max, m/s) of each bounce out of the no-go zone (debris gravity is 60: 6 m/s = a 0.3 m hop).")]
        [SerializeField] private Vector2 _escapeHopUp = new Vector2(5f, 7f);

        [Tooltip("How much the bounces also lead straight away from the tower, relative to round to its back (behind the tower it is all outward).")]
        [SerializeField, Range(0f, 1f)] private float _escapeOutward = 0.3f;

        [Tooltip("Pieces keep bouncing this many degrees past the no-go zone's side edge, so they spread round the tower instead of piling up on the edge.")]
        [SerializeField, Range(0f, 90f)] private float _escapeMargin = 20f;

        [Tooltip("Share of pieces (0..1) that ignore the no-go zone and settle wherever they land, so debris still falls there now and then, naturally.")]
        [SerializeField, Range(0f, 1f)] private float _ignoreZoneChance = 0.1f;

        [Tooltip("A piece still in the no-go zone after bouncing this long (seconds) gives up and settles where it is.")]
        [SerializeField, Range(0.5f, 10f)] private float _escapeMaxTime = 3f;

        public float CubeSpacing => _cubeSpacing;
        public float CubeScale => _cubeScale;
        public float PositionJitter => _positionJitter;
        public float RotationJitter => _rotationJitter;
        public float SpinDegreesPerSecond => _spinDegreesPerSecond;
        public bool KeepDebris => _keepDebris;
        public float PlinthInset => _plinthInset;
        public float PoleRadius => _poleRadius;
        public float ClearZoneReach => _clearZoneReach;
        public float ClearZoneHalfAngle => _clearZoneHalfAngle;
        public Vector2 EscapeHopSpeed => _escapeHopSpeed;
        public Vector2 EscapeHopUp => _escapeHopUp;
        public float EscapeOutward => _escapeOutward;
        public float EscapeMaxTime => _escapeMaxTime;
        public float EscapeMargin => _escapeMargin;
        public float IgnoreZoneChance => _ignoreZoneChance;

        /// <summary>Radius at which cube centres sit so that neighbouring centres are <see cref="CubeSpacing"/> apart.</summary>
        public float RadiusFor(int columns) => columns * _cubeSpacing / (2f * Mathf.PI);
    }
}
