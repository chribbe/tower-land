using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerLand.UI
{
    /// <summary>
    /// Development aid: draws a reference image (the art mockup) over the game view so visuals can be matched.
    /// Put it on a GameObject tagged "EditorOnly" so it (and its texture) never ship.
    /// Starts hidden; Tab cycles Corner → Onion skin (full-screen, semi-transparent) → Hidden.
    /// </summary>
    public sealed class ReferenceOverlay : MonoBehaviour
    {
        public enum Mode
        {
            Corner,
            Onion,
            Hidden,
        }

        [SerializeField] private Texture2D _reference;
        [SerializeField] private Mode _mode = Mode.Hidden;
        [Tooltip("Corner thumbnail height as a fraction of the screen height.")]
        [SerializeField, Range(0.1f, 0.6f)] private float _cornerHeight = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _onionOpacity = 0.5f;

        public Mode CurrentMode
        {
            get => _mode;
            set => _mode = value;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                _mode = (Mode)(((int)_mode + 1) % 3);
        }

        private void OnGUI()
        {
            if (_reference == null || _mode == Mode.Hidden)
                return;

            if (_mode == Mode.Onion)
            {
                GUI.color = new Color(1f, 1f, 1f, _onionOpacity);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _reference, ScaleMode.StretchToFill);
                GUI.color = Color.white;
                return;
            }

            float height = Screen.height * _cornerHeight;
            float width = height * _reference.width / _reference.height;
            const float margin = 8f;
            var rect = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(rect, _reference, ScaleMode.ScaleToFit);
        }
    }
}
