using UnityEngine;

namespace TowerLand.Gameplay
{
    /// <summary>
    /// Throw trajectory. Normally a straight line from start to target lifted by a sine arc (Tower Buster's curve).
    /// Through a hole in the tower (target on the far side, reached from inside) it goes via a point in front of the
    /// hole instead: a curve from the start that arrives there along the aim ray, then straight along that ray to the
    /// target - the line the aim found clear - so the throw doesn't clip the hole's rim.
    /// </summary>
    public readonly struct AimPath
    {
        public readonly Vector3 Start;
        public readonly Vector3 Target;
        public readonly float ArcHeight;
        /// <summary>Through a hole: the point in front of it, and the aim direction from there to the target.</summary>
        public readonly bool HasVia;
        public readonly Vector3 Via;
        public readonly Vector3 Direction;
        /// <summary>Share of t spent on the curve to <see cref="Via"/> (the rest is the straight run to the target).</summary>
        private readonly float _viaT;

        public AimPath(Vector3 start, Vector3 target, float arcHeight)
        {
            Start = start;
            Target = target;
            ArcHeight = arcHeight;
            HasVia = false;
            Via = default;
            Direction = default;
            _viaT = 1f;
        }

        /// <summary>A throw through a hole: curve to <paramref name="via"/>, arriving along <paramref name="direction"/>, then straight to the target.</summary>
        public AimPath(Vector3 start, Vector3 via, Vector3 direction, Vector3 target, float arcHeight)
        {
            Start = start;
            Target = target;
            ArcHeight = arcHeight;
            HasVia = true;
            Via = via;
            Direction = direction.normalized;
            float curve = Vector3.Distance(start, via) * 1.15f, straight = Vector3.Distance(via, target);
            _viaT = Mathf.Clamp(curve / Mathf.Max(curve + straight, 1e-4f), 0.05f, 0.95f);
        }

        /// <summary>Point at <paramref name="t"/> (0 start, 1 target). Beyond 1 the path continues straight along its
        /// final direction, so a throw aimed exactly at a surface always goes through it.</summary>
        public Vector3 Evaluate(float t)
        {
            if (t > 1f)
                return Target + Tangent(1f) * (t - 1f);
            if (!HasVia)
                return Vector3.Lerp(Start, Target, t) + Vector3.up * (Mathf.Sin(Mathf.PI * t) * ArcHeight);
            if (t >= _viaT)
                return Vector3.Lerp(Via, Target, (t - _viaT) / (1f - _viaT));
            float u = t / _viaT, k = 1f - u;
            Curve(out Vector3 p1, out Vector3 p2);
            return k * k * k * Start + 3f * k * k * u * p1 + 3f * k * u * u * p2 + u * u * u * Via;
        }

        /// <summary>Velocity (per unit of t) along the path.</summary>
        public Vector3 Tangent(float t)
        {
            if (!HasVia)
                return (Target - Start) + Vector3.up * (Mathf.Cos(Mathf.PI * Mathf.Min(t, 1f)) * Mathf.PI * ArcHeight);
            if (t >= _viaT)
                return (Target - Via) / (1f - _viaT);
            float u = t / _viaT, k = 1f - u;
            Curve(out Vector3 p1, out Vector3 p2);
            return (3f * k * k * (p1 - Start) + 6f * k * u * (p2 - p1) + 3f * u * u * (Via - p2)) / _viaT;
        }

        /// <summary>Bezier handles of the curve to the hole: leaves upwards like the normal arc, arrives along the aim ray.</summary>
        private void Curve(out Vector3 p1, out Vector3 p2)
        {
            float span = Vector3.Distance(Start, Via);
            p1 = Start + (Via - Start) * 0.35f + Vector3.up * (ArcHeight * 0.9f);
            p2 = Via - Direction * (span * 0.35f);
        }
    }
}
