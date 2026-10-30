using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Feel
{
    /// <summary>
    /// Keeps the player's view of the tower free while every piece of debris stays in the scene. Debris falls naturally;
    /// a piece that lands in the no-go zone in front of the tower (<see cref="DebrisZone"/>) bounces its way out of it -
    /// round the tower towards its back - passing through other debris while it does (<see cref="CubeChunk"/>).
    /// Owns the debris physics layers: "Debris" (pieces, falling cubes) and "DebrisEscaping" (pieces bouncing out of the
    /// zone), which ignores both, so an escaping piece only meets the ground, the base and the rocks.
    /// </summary>
    [RequireComponent(typeof(TowerView))]
    public sealed class DebrisSweeper : MonoBehaviour
    {
        public const string DebrisLayerName = "Debris";
        public const string EscapingLayerName = "DebrisEscaping";

        private TowerView _tower;
        private Camera _camera;
        private DebrisZone _zone;
        private DebrisZone _escapeZone;   // the zone widened by EscapeMargin: an escaping piece bounces until it leaves it
        private bool _hasZone;

        /// <summary>Layer for debris (-1 if the project lacks it: Tools/TowerLand/Apply Project Settings).</summary>
        public int DebrisLayer { get; private set; } = -1;
        /// <summary>Layer for pieces bouncing out of the zone (-1 if missing): they pass through other debris.</summary>
        public int EscapingLayer { get; private set; } = -1;
        public TowerVisualSettings Settings => _tower.Settings;

        private void Awake()
        {
            _tower = GetComponent<TowerView>();
            DebrisLayer = LayerMask.NameToLayer(DebrisLayerName);
            EscapingLayer = LayerMask.NameToLayer(EscapingLayerName);
            if (DebrisLayer < 0 || EscapingLayer < 0)
            {
                Debug.LogWarning("[TowerLand] Debris layers missing (run Tools/TowerLand/Apply Project Settings): escaping debris can't pass through piles.");
                return;
            }
            Physics.IgnoreLayerCollision(EscapingLayer, DebrisLayer, true);
            Physics.IgnoreLayerCollision(EscapingLayer, EscapingLayer, true);
        }

        private void Start() => _camera = Camera.main;

        private void Update()
        {
            _hasZone = _camera != null && _tower.Grid != null;
            if (!_hasZone) return;
            Vector3 toCamera = _camera.transform.position - transform.position;
            var front = new Vector2(toCamera.x, toCamera.z);
            float reach = _tower.FaceRadius(0) + Settings.ClearZoneReach;
            _zone = new DebrisZone(front, reach, Settings.ClearZoneHalfAngle);
            _escapeZone = new DebrisZone(front, reach, Mathf.Min(180f, Settings.ClearZoneHalfAngle + Settings.EscapeMargin));
        }

        public bool InNoGoZone(Vector3 position) => _hasZone && _zone.Contains(Offset(position));

        /// <summary>An escaping piece keeps bouncing while in here: the zone plus a margin, so pieces don't pile up on its edge.</summary>
        public bool InEscapeArea(Vector3 position) => _hasZone && _escapeZone.Contains(Offset(position));

        /// <summary>Horizontal unit direction a piece at <paramref name="position"/> bounces out of the zone.</summary>
        public Vector3 EscapeDirection(Vector3 position)
        {
            Vector2 d = _zone.EscapeDirection(Offset(position), Settings.EscapeOutward);
            return new Vector3(d.x, 0f, d.y);
        }

        private Vector2 Offset(Vector3 position) =>
            new Vector2(position.x - transform.position.x, position.z - transform.position.z);
    }
}
