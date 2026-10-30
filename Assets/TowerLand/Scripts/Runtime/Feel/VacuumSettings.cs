using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>Tuning for the win sequence: the glass vacuum hose that sucks up all the debris (<see cref="VacuumHose"/>, <c>Gameplay.WinSequence</c>).</summary>
    [CreateAssetMenu(menuName = "TowerLand/Vacuum Settings", fileName = "VacuumSettings")]
    public sealed class VacuumSettings : ScriptableObject
    {
        [Header("Timing (seconds)")]
        [Tooltip("Pause after the last debris settles, before the hose comes in.")]
        [SerializeField, Range(0f, 2f)] private float _startDelay = 0.1f;
        [Tooltip("The hose lowers in from above the screen and eases to a stop (slight overshoot).")]
        [SerializeField, Range(0.2f, 2f)] private float _enterDuration = 0.9f;
        [Tooltip("Hover after arriving: the hose settles and gathers itself before the suction kicks in.")]
        [SerializeField, Range(0f, 1.5f)] private float _gatherDuration = 0.3f;
        [Tooltip("Suction spins up from nothing to full over this long (capture reach, air streaks, twist).")]
        [SerializeField, Range(0f, 1.5f)] private float _spinUpDuration = 0.4f;
        [Tooltip("The nozzle sweeps once around the tower over all the debris piles.")]
        [SerializeField, Range(1f, 8f)] private float _sweepDuration = 2.8f;
        [Tooltip("Anticipation dip, then the hose yanks back up out of the screen.")]
        [SerializeField, Range(0.2f, 2f)] private float _exitDuration = 0.45f;

        [Header("Suction")]
        [Tooltip("Debris within this horizontal distance of the nozzle gets sucked in, metres.")]
        [SerializeField, Range(1f, 10f)] private float _captureRadius = 9f;
        [Tooltip("Mouth height above the debris it hovers over, metres.")]
        [SerializeField, Range(0.5f, 5f)] private float _mouthHeight = 3.4f;
        [Tooltip("Each grabbed piece lifts off after its own delay, up to this long (further ones later, plus chance): pieces peel off one by one.")]
        [SerializeField, Range(0f, 1.5f)] private float _pullDelay = 0.3f;
        [Tooltip("Speed of the air rushing into the mouth, m/s, right at the opening; it falls off with distance.")]
        [SerializeField, Range(5f, 120f)] private float _airSpeed = 110f;
        [Tooltip("Distance (m) at which the air speed has dropped to half.")]
        [SerializeField, Range(0.5f, 10f)] private float _airFalloff = 5f;
        [Tooltip("Slowest the air gets, however far away (m/s): distant pieces still fly in decisively.")]
        [SerializeField, Range(0f, 40f)] private float _airMin = 32f;
        [Tooltip("The air around a flying piece speeds up with its flight time (x 1 + ramp * t), so no piece lingers.")]
        [SerializeField, Range(0f, 10f)] private float _airRamp = 3f;
        [Tooltip("Within this distance (m) of its spot at the mouth a piece is committed and heads straight in.")]
        [SerializeField, Range(0f, 5f)] private float _funnel = 2f;
        [Tooltip("Faint swirl of the air right at the opening, share of its speed.")]
        [SerializeField, Range(0f, 1f)] private float _vortex = 0f;
        [Tooltip("How quickly pieces take on the air's velocity, 1/s (each piece 70-130%): higher = they follow the flow more closely.")]
        [SerializeField, Range(0.5f, 30f)] private float _grip = 14f;
        [Tooltip("Gravity on flying pieces, m/s^2: the air has to lift them.")]
        [SerializeField, Range(0f, 40f)] private float _weight = 10f;
        [Tooltip("Upward speed (min, max) a piece is kicked with as it tears off the ground, m/s.")]
        [SerializeField] private Vector2 _liftKick = new Vector2(4f, 8f);
        [Tooltip("Random turbulence on flying pieces, m/s^2.")]
        [SerializeField, Range(0f, 60f)] private float _flutter = 4f;
        [Tooltip("Where pieces enter the mouth: spread over its opening, fraction of the mouth radius.")]
        [SerializeField, Range(0f, 1f)] private float _intakeSpread = 0.75f;
        [Tooltip("Speed pieces travel up the hose, m/s: entry speed at the mouth, then accelerating to the top speed.")]
        [SerializeField, Range(1f, 40f)] private float _intakeSpeed = 12f;
        [SerializeField, Range(5f, 80f)] private float _hoseSpeed = 80f;
        [SerializeField, Range(10f, 400f)] private float _hoseAcceleration = 110f;
        [Tooltip("How fast the stream corkscrews round the hose axis, radians per second.")]
        [SerializeField, Range(0f, 10f)] private float _hoseSpin = 2.5f;

        [Header("Hose")]
        [SerializeField, Range(0.2f, 1.5f)] private float _hoseRadius = 0.85f;
        [Tooltip("Corrugation ribs per metre, and their depth (fraction of the radius).")]
        [SerializeField, Range(0f, 6f)] private float _ribsPerMetre = 2.2f;
        [SerializeField, Range(0f, 0.2f)] private float _ribDepth = 0.07f;
        [Tooltip("How much the hose swells where pieces travel through it (fraction of the radius, max).")]
        [SerializeField, Range(0f, 1f)] private float _bulge = 0.35f;
        [Tooltip("Articulations along the hose (spring-driven joints, each marked by a ring).")]
        [SerializeField, Range(4, 16)] private int _joints = 9;
        [Tooltip("Joint spring stiffness and damping: lower = more lag, wobble and follow-through.")]
        [SerializeField, Range(5f, 400f)] private float _stiffness = 60f;
        [SerializeField, Range(0f, 40f)] private float _damping = 7f;
        [Tooltip("Corkscrew twist of the hose while it sucks: amplitude (metres) and speed (turns per second).")]
        [SerializeField, Range(0f, 3f)] private float _twistAmplitude = 0.4f;
        [SerializeField, Range(0f, 4f)] private float _twistSpeed = 0.9f;

        public float StartDelay => _startDelay;
        public float EnterDuration => _enterDuration;
        public float GatherDuration => _gatherDuration;
        public float SpinUpDuration => _spinUpDuration;
        public float SweepDuration => _sweepDuration;
        public float ExitDuration => _exitDuration;
        public float CaptureRadius => _captureRadius;
        public float MouthHeight => _mouthHeight;
        public float PullDelay => _pullDelay;
        public float AirSpeed => _airSpeed;
        public float AirFalloff => _airFalloff;
        public float AirMin => _airMin;
        public float AirRamp => _airRamp;
        public float Funnel => _funnel;
        public float Vortex => _vortex;
        public float Grip => _grip;
        public float Weight => _weight;
        public Vector2 LiftKick => _liftKick;
        public float Flutter => _flutter;
        public float IntakeSpread => _intakeSpread;
        public float IntakeSpeed => _intakeSpeed;
        public float HoseSpeed => _hoseSpeed;
        public float HoseAcceleration => _hoseAcceleration;
        public float HoseSpin => _hoseSpin;
        public float HoseRadius => _hoseRadius;
        public float RibsPerMetre => _ribsPerMetre;
        public float RibDepth => _ribDepth;
        public float Bulge => _bulge;
        public int Joints => _joints;
        public float Stiffness => _stiffness;
        public float Damping => _damping;
        public float TwistAmplitude => _twistAmplitude;
        public float TwistSpeed => _twistSpeed;
    }
}
