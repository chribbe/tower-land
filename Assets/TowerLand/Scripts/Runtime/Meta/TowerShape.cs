using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerLand.Meta
{
    /// <summary>One brick of a construction: centre (construction space, y = 0 on the platform) and facing.</summary>
    public readonly struct BrickSlot
    {
        public readonly Vector3 Position;
        /// <summary>Degrees about Y; the brick's forward faces away from the tower axis.</summary>
        public readonly float Yaw;

        public BrickSlot(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// A round castle tower of tiny bricks: a footing one ring wider than the wall, a hollow shaft
    /// (<see cref="WallThickness"/> rings), an overhanging corbel and crenellated battlements on top. Rows are staggered
    /// like brickwork. Slots come in placing order: bottom row first, each row round by angle, so the tower spirals up.
    /// Plain C#: the build scene places bricks on them, tests check the shape.
    /// </summary>
    [Serializable]
    public sealed class TowerShape
    {
        [Tooltip("Brick edge length and row height, metres.")]
        [SerializeField, Range(0.1f, 1f)] private float _brickSize = 0.3f;
        [Tooltip("Radius of the outermost wall ring (brick centres), metres.")]
        [SerializeField, Range(1f, 10f)] private float _radius = 3.7f;
        [SerializeField, Range(1, 4)] private int _wallThickness = 2;
        [Tooltip("Rows of the footing: one extra ring outside the wall.")]
        [SerializeField, Range(0, 10)] private int _baseRows = 3;
        [Tooltip("Plain wall rows; the construction's size is mostly set here.")]
        [SerializeField, Range(0, 400)] private int _shaftRows = 39;
        [Tooltip("Overhang under the battlements: one extra ring outside the wall.")]
        [SerializeField, Range(0, 10)] private int _corbelRows = 2;
        [Tooltip("Merlon rows on top (on the overhang ring and the outer wall ring), with gaps between them.")]
        [SerializeField, Range(0, 10)] private int _battlementRows = 3;
        [Tooltip("Width of each merlon (and of each gap), in bricks.")]
        [SerializeField, Range(1, 10)] private int _merlonWidth = 3;

        public float BrickSize => _brickSize;
        public float Radius => _radius;
        public int Rows => _baseRows + _shaftRows + _corbelRows + _battlementRows;
        public float Height => Rows * _brickSize;
        /// <summary>Widest ring (footing / overhang), brick centres.</summary>
        public float OuterRadius => _radius + (_baseRows > 0 || _corbelRows > 0 || _battlementRows > 0 ? _brickSize : 0f);

        public int ShaftRows
        {
            get => _shaftRows;
            set => _shaftRows = Mathf.Max(0, value);
        }

        public TowerShape() { }

        public TowerShape(float brickSize, float radius, int wallThickness, int baseRows, int shaftRows, int corbelRows, int battlementRows, int merlonWidth)
        {
            _brickSize = brickSize;
            _radius = radius;
            _wallThickness = wallThickness;
            _baseRows = baseRows;
            _shaftRows = shaftRows;
            _corbelRows = corbelRows;
            _battlementRows = battlementRows;
            _merlonWidth = merlonWidth;
        }

        public List<BrickSlot> Generate()
        {
            var slots = new List<BrickSlot>();
            var row = new List<(float angle, BrickSlot slot)>();
            float s = _brickSize;
            float overhang = _radius + s;
            int merlons = Mathf.Max(2, Mathf.RoundToInt(Bricks(overhang) / (2f * _merlonWidth)));
            for (int r = 0; r < Rows; r++)
            {
                row.Clear();
                bool footing = r < _baseRows;
                bool corbel = r >= _baseRows + _shaftRows && r < Rows - _battlementRows;
                bool battlement = r >= Rows - _battlementRows;
                float y = (r + 0.5f) * s;
                if (battlement)
                {
                    AddRing(row, overhang, y, r, merlons);
                    AddRing(row, _radius, y, r, merlons);
                }
                else
                {
                    if (footing || corbel) AddRing(row, overhang, y, r, 0);
                    for (int w = 0; w < _wallThickness; w++)
                        AddRing(row, _radius - w * s, y, r, 0);
                }
                row.Sort((a, b) => a.angle.CompareTo(b.angle));
                foreach ((float _, BrickSlot slot) in row) slots.Add(slot);
            }
            return slots;
        }

        private int Bricks(float ringRadius) => Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * ringRadius / _brickSize));

        /// <summary>One ring of a row; odd rows shift half a brick (brickwork). <paramref name="merlons"/> &gt; 0 leaves crenel gaps.</summary>
        private void AddRing(List<(float, BrickSlot)> row, float ringRadius, float y, int r, int merlons)
        {
            int n = Bricks(ringRadius);
            float step = 2f * Mathf.PI / n;
            for (int i = 0; i < n; i++)
            {
                float angle = (i + (r % 2 == 1 ? 0.5f : 0f)) * step;
                if (merlons > 0 && (int)(angle / (2f * Mathf.PI) * merlons * 2f) % 2 == 1) continue;
                var position = new Vector3(Mathf.Cos(angle) * ringRadius, y, Mathf.Sin(angle) * ringRadius);
                row.Add((angle, new BrickSlot(position, 90f - angle * Mathf.Rad2Deg)));
            }
        }
    }
}
