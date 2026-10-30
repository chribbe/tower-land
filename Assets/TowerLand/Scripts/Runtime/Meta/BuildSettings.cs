using UnityEngine;

namespace TowerLand.Meta
{
    /// <summary>Tuning for the build scene: the vacuum hose spitting a level's bricks onto the construction (<see cref="BuildSequence"/>).</summary>
    [CreateAssetMenu(menuName = "TowerLand/Build Settings", fileName = "BuildSettings")]
    public sealed class BuildSettings : ScriptableObject
    {
        [Header("Timing (seconds)")]
        [Tooltip("Fade in from black as the scene opens.")]
        [SerializeField, Range(0f, 1f)] private float _fadeIn = 0.3f;
        [Tooltip("The hose drops in from above the screen and locks into place.")]
        [SerializeField, Range(0.1f, 2f)] private float _enterDuration = 0.4f;
        [Tooltip("Bricks pour out over this long, whatever their number (within the rate limits below).")]
        [SerializeField, Range(0.5f, 10f)] private float _spitDuration = 2.2f;
        [Tooltip("Bricks per second, at least / at most: few bricks still pour briskly, many take longer.")]
        [SerializeField] private Vector2 _spitRate = new Vector2(120f, 700f);
        [SerializeField, Range(0.1f, 2f)] private float _exitDuration = 0.3f;
        [Tooltip("Holding a finger down speeds the whole sequence up by this much.")]
        [SerializeField, Range(1f, 8f)] private float _holdSpeedUp = 3.5f;
        [Tooltip("Fade to black before the next level.")]
        [SerializeField, Range(0f, 1f)] private float _fadeOut = 0.25f;

        [Header("Spitting")]
        [Tooltip("Bricks come out in shots, one every this long (s): each shot kicks the nozzle back and squashes it.")]
        [SerializeField, Range(0.02f, 0.3f)] private float _shotInterval = 0.07f;
        [Tooltip("Upward kick of the nozzle per shot, m/s (stiff spring back to its place).")]
        [SerializeField, Range(0f, 15f)] private float _recoil = 6f;
        [Tooltip("Nozzle squash kick per shot.")]
        [SerializeField, Range(0f, 3f)] private float _shotSquash = 0.9f;
        [Tooltip("Metres a brick shoots down the glass hose before it leaves the mouth.")]
        [SerializeField, Range(0f, 15f)] private float _hoseRun = 5f;
        [Tooltip("Speed down the hose, m/s.")]
        [SerializeField, Range(5f, 120f)] private float _hoseSpeed = 70f;
        [Tooltip("Flight time from the mouth to its slot, seconds (min, max: random per brick): short = shot, not tossed.")]
        [SerializeField] private Vector2 _flightTime = new Vector2(0.16f, 0.24f);
        [Tooltip("Gravity on flying bricks, m/s^2.")]
        [SerializeField, Range(5f, 80f)] private float _gravity = 40f;
        [Tooltip("Spin while flying, degrees per second (it squares up to its slot as it arrives).")]
        [SerializeField, Range(0f, 2000f)] private float _flightSpin = 720f;

        [Header("Bricks")]
        [Tooltip("Brick size as a share of its slot (the rest is the gap between bricks).")]
        [SerializeField, Range(0.5f, 1f)] private float _brickScale = 0.9f;
        [Tooltip("Landing squash: a brick arrives this much bigger and snaps to its size.")]
        [SerializeField, Range(0f, 1f)] private float _landPop = 0.3f;
        [SerializeField, Range(0.02f, 0.5f)] private float _landPopDuration = 0.08f;

        [Header("Hose and camera")]
        [Tooltip("Mouth height above the construction's current top, metres.")]
        [SerializeField, Range(1f, 15f)] private float _mouthHeight = 4f;
        [Tooltip("Lowest the mouth goes, metres above the platform: it stays high even over a fresh construction.")]
        [SerializeField, Range(1f, 30f)] private float _minMouthHeight = 9f;
        [Tooltip("The nozzle turns towards the bricks it is shooting: share of the way from straight down to pointing right at them.")]
        [SerializeField, Range(0f, 1f)] private float _aimShare = 0.55f;
        [Tooltip("How quickly the nozzle turns to its aim, 1/s (critically damped: no swing).")]
        [SerializeField, Range(1f, 40f)] private float _aimSpeed = 14f;
        [Tooltip("The camera rises to keep the build top this far below the frame top, share of the screen height.")]
        [SerializeField, Range(0f, 0.6f)] private float _topMargin = 0.3f;

        public float FadeIn => _fadeIn;
        public float EnterDuration => _enterDuration;
        public float SpitDuration => _spitDuration;
        public Vector2 SpitRate => _spitRate;
        public float ExitDuration => _exitDuration;
        public float HoldSpeedUp => _holdSpeedUp;
        public float FadeOut => _fadeOut;
        public float ShotInterval => _shotInterval;
        public float Recoil => _recoil;
        public float ShotSquash => _shotSquash;
        public float HoseRun => _hoseRun;
        public float HoseSpeed => _hoseSpeed;
        public Vector2 FlightTime => _flightTime;
        public float Gravity => _gravity;
        public float FlightSpin => _flightSpin;
        public float BrickScale => _brickScale;
        public float LandPop => _landPop;
        public float LandPopDuration => _landPopDuration;
        public float MouthHeight => _mouthHeight;
        public float MinMouthHeight => _minMouthHeight;
        public float AimShare => _aimShare;
        public float AimSpeed => _aimSpeed;
        public float TopMargin => _topMargin;
    }
}
