using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>
    /// The no-go zone: the ground sector in front of the tower (facing the player) that debris bounces out of, so the
    /// player keeps a free view of the tower. Works in the horizontal plane on offsets from the tower axis, as (x, z);
    /// <see cref="Front"/> points towards the camera.
    /// </summary>
    public readonly struct DebrisZone
    {
        public readonly Vector2 Front;
        public readonly float OuterRadius;
        /// <summary>Half the sector's opening, in degrees.</summary>
        public readonly float HalfAngle;

        public DebrisZone(Vector2 front, float outerRadius, float halfAngle)
        {
            Front = front.normalized;
            OuterRadius = outerRadius;
            HalfAngle = halfAngle;
        }

        /// <summary>Signed angle from <see cref="Front"/> to <paramref name="offset"/>, degrees (counter-clockwise positive).</summary>
        public float AngleOf(Vector2 offset) => Vector2.SignedAngle(Front, offset);

        public bool Contains(Vector2 offset) =>
            offset.sqrMagnitude <= OuterRadius * OuterRadius && Mathf.Abs(AngleOf(offset)) <= HalfAngle;

        /// <summary>Unit direction straight away from the tower axis (towards the camera when on the axis).</summary>
        public Vector2 Away(Vector2 offset) => offset.sqrMagnitude > 1e-6f ? offset.normalized : Front;

        /// <summary>
        /// Unit direction a piece at <paramref name="offset"/> bounces out of the zone: facing the camera it leads around
        /// the tower towards the back (on the piece's side, never towards the player), turning into straight "away from
        /// the axis" behind the tower; <paramref name="outward"/> adds some away-from-the-axis everywhere so pieces don't
        /// hug the base.
        /// </summary>
        public Vector2 EscapeDirection(Vector2 offset, float outward)
        {
            Vector2 away = Away(offset);
            float side = AngleOf(offset) < 0f ? -1f : 1f;
            Vector2 around = new Vector2(-away.y, away.x) * side;   // counter-clockwise perpendicular grows the angle
            float facing = (1f + Vector2.Dot(away, Front)) * 0.5f;   // 1 towards the camera, 0 straight behind
            return (around * facing + away * Mathf.Max(outward, 1e-3f)).normalized;
        }
    }
}
