using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerLand.Meta
{
    /// <summary>
    /// Draws thousands of bricks without GameObjects: GPU-instanced batches per colour (one loose-cube material each), for
    /// bricks that have landed (kept) and for bricks in flight (re-submitted every frame).
    /// </summary>
    public sealed class BrickRenderer : MonoBehaviour
    {
        private const int Batch = 1023;

        private Mesh _mesh;
        private Material[] _materials;
        private List<Matrix4x4>[] _placed;
        private List<Matrix4x4>[] _flying;
        private RenderParams[] _params;

        public void Init(Mesh mesh, Material[] materials, Bounds bounds)
        {
            _mesh = mesh;
            _materials = materials;
            _placed = new List<Matrix4x4>[materials.Length];
            _flying = new List<Matrix4x4>[materials.Length];
            _params = new RenderParams[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                _placed[i] = new List<Matrix4x4>();
                _flying[i] = new List<Matrix4x4>();
                _params[i] = new RenderParams(materials[i])
                {
                    worldBounds = bounds,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = gameObject.layer,
                };
            }
        }

        /// <summary>A landed brick; returns its index (to animate it with <see cref="SetPlaced"/>).</summary>
        public int AddPlaced(int colour, Matrix4x4 matrix)
        {
            List<Matrix4x4> list = _placed[colour % _placed.Length];
            list.Add(matrix);
            return list.Count - 1;
        }

        public void SetPlaced(int colour, int index, Matrix4x4 matrix) => _placed[colour % _placed.Length][index] = matrix;

        /// <summary>A brick in flight, drawn this frame only.</summary>
        public void AddFlying(int colour, Matrix4x4 matrix) => _flying[colour % _flying.Length].Add(matrix);

        private void LateUpdate()
        {
            if (_mesh == null) return;
            for (int i = 0; i < _materials.Length; i++)
            {
                Draw(i, _placed[i]);
                Draw(i, _flying[i]);
                _flying[i].Clear();
            }
        }

        private void Draw(int colour, List<Matrix4x4> matrices)
        {
            for (int start = 0; start < matrices.Count; start += Batch)
                Graphics.RenderMeshInstanced(_params[colour], _mesh, 0, matrices, Mathf.Min(Batch, matrices.Count - start), start);
        }

        private void OnDestroy()
        {
            if (_materials == null) return;
            foreach (Material material in _materials)
                if (material != null) Destroy(material);
        }
    }
}
