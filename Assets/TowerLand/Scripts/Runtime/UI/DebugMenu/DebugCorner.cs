using TMPro;
using TowerLand.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Secret way into the debug scene: an invisible hotspot in the top-right corner of the HUD. Tap it 5 times quickly
    /// to get a password prompt; the right password marks the player as a debugger (<see cref="PlayerProgress"/>), and from
    /// then on 2 quick taps open the debug scene. In the editor the password is skipped (and backquote opens it too).
    /// The hotspot is a UI graphic, so the shooter ignores presses on it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DebugCorner : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private string _debugScene = "Debug";
        [SerializeField] private TMP_FontAsset _titleFont;
        [Tooltip("Hotspot size, canvas units (inside the safe area's top-right corner).")]
        [SerializeField] private float _size = 200f;
        [Tooltip("Taps count as a run while each comes within this many seconds of the previous one.")]
        [SerializeField] private float _maxGap = 0.5f;
        [SerializeField] private int _unlockTaps = 5;
        [SerializeField] private int _debuggerTaps = 2;
        [SerializeField] private string _password = "debaser";

        private TapSequence _taps;
        private PlayerProgress _progress;
        private PasswordPrompt _prompt;

        private void Awake()
        {
            _taps = new TapSequence(_maxGap);
            _progress = PlayerProgress.Local();
            if (!TryGetComponent(out Image image)) image = gameObject.AddComponent<Image>();
            image.color = Color.clear;   // invisible, but catches the taps
            image.raycastTarget = true;
        }

        private void Start() => Place();

        /// <summary>Top-right corner of the safe area (notch / status bar inset).</summary>
        private void Place()
        {
            var rect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(_size, _size);
            rect.anchoredPosition = new Vector2(-(Screen.width - safe.xMax) / scale, -(Screen.height - safe.yMax) / scale);
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
                Open();
        }
#endif

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_prompt != null) return;
            bool debugger = _progress.IsDebugger;
            if (!_taps.Tap(Time.unscaledTime, debugger ? _debuggerTaps : _unlockTaps)) return;
            if (debugger)
                Open();
            else
                AskPassword();
        }

        private void AskPassword()
        {
#if UNITY_EDITOR
            Unlock();   // no password in the editor
#else
            _prompt = PasswordPrompt.Show(_titleFont, Check, () => _prompt = null);
#endif
        }

        private bool Check(string entered)
        {
            if (!string.Equals(entered.Trim(), _password, System.StringComparison.OrdinalIgnoreCase))
                return false;
            Unlock();
            return true;
        }

        private void Unlock()
        {
            _progress.IsDebugger = true;
            Open();
        }

        private void Open() => SceneManager.LoadScene(_debugScene);
    }
}
