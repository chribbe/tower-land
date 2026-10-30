using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Cartoon "TOWER LAND" win screen in the <see cref="TitleStyle"/> look (gradient face, solid extrusion, outline,
    /// soft shadow): letters pop in one by one with an overshoot and keep bobbing while the scene behind fades to black
    /// (optional), then a pulsing "Tap to continue"; a tap calls back - or, with an auto-continue time, it calls back by
    /// itself (a tap still skips ahead). Built at runtime, on its own overlay canvas.
    /// </summary>
    public sealed class WinScreen : MonoBehaviour
    {
        private const float LetterStagger = 0.05f;
        private const float PopDuration = 0.38f;
        private const float ContinueDelay = 1.1f;
        // The scene behind the title fades to black.
        private const float FadeDelay = 0.15f;
        private const float FadeDuration = 0.95f;
        private const float TopLine = 330f, BottomLine = 150f;
        private const float TopRaise = 450f, BottomPrompt = -650f;   // atTop layout (canvas units, 1080x1920 reference)

        private struct Letter
        {
            public RectTransform Back, Front;   // extrusion stack, face (separate layers: no letter's extrusion covers a neighbour's face)
            public float Baseline, Tilt;
        }

        private readonly List<Letter> _letters = new List<Letter>();
        private TitleStyle _style;
        private RectTransform _backLayer, _frontLayer;
        private TextMeshProUGUI _continue;
        private Image _fade;
        private Action _onTap;
        private float _shownAt;
        private float _autoContinue;
        private bool _fadeToBlack;

        /// <param name="autoContinue">Seconds after which it continues by itself (no prompt); 0 = waits for a tap.</param>
        /// <param name="atTop">Title near the top of the screen and the prompt near the bottom, leaving the middle free (e.g. for a construction).</param>
        public static WinScreen Show(TitleStyle style, Action onTap, string top = "TOWER", string bottom = "LAND",
            float autoContinue = 0f, bool fadeToBlack = true, bool atTop = false)
        {
            var go = new GameObject("WinScreen", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = go.AddComponent<WinScreen>();
            screen._style = style;
            screen._onTap = onTap;
            screen._shownAt = Time.time;
            screen._autoContinue = autoContinue;
            screen._fadeToBlack = fadeToBlack;
            screen._fade = screen.Layer("Fade").gameObject.AddComponent<Image>();
            screen._fade.rectTransform.anchorMin = Vector2.zero;
            screen._fade.rectTransform.anchorMax = Vector2.one;
            screen._fade.color = new Color(0f, 0f, 0f, 0f);
            screen._fade.raycastTarget = false;
            screen._backLayer = screen.Layer("Extrusion");
            screen._frontLayer = screen.Layer("Faces");
            float raise = atTop ? TopRaise : 0f;
            screen.Word(top, style.TopSize, TopLine + raise, style.Top, 0);
            screen.Word(bottom, style.BottomSize, BottomLine + raise, style.Bottom, top.Length);
            screen._continue = screen.Text(screen.transform, "Tap to continue", 72f, style.FaceMaterial);
            ((RectTransform)screen._continue.transform).anchoredPosition = new Vector2(0f, atTop ? BottomPrompt : -250f);
            screen._continue.color = new Color(1f, 1f, 1f, 0f);
            return screen;
        }

        private RectTransform Layer(string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private void Word(string word, float size, float baseline, TitleStyle.WordColours colours, int firstIndex)
        {
            // Measure with the face settings, then centre the word.
            var widths = new float[word.Length];
            TextMeshProUGUI probe = Text(transform, "", size, _style.FaceMaterial);
            float total = 0f;
            for (int i = 0; i < word.Length; i++)
            {
                widths[i] = probe.GetPreferredValues(word[i].ToString()).x + _style.Tracking * size;
                total += widths[i];
            }
            Destroy(probe.gameObject);

            float x = -total * 0.5f;
            float depth = _style.ExtrudeDepth * size;
            int layers = _style.ExtrudeLayers;
            for (int i = 0; i < word.Length; i++)
            {
                string glyph = word[i].ToString();
                var position = new Vector2(x + widths[i] * 0.5f, baseline);
                x += widths[i];
                float tilt = (i % 2 == 0 ? -1f : 1f) * _style.Tilt;

                // Back to front: outlined dark silhouette of the stack (the back-most copy casts the shadow), then the
                // coloured stack without outline. The outlined face goes on the front layer.
                RectTransform back = Group(_backLayer, glyph, position, tilt);
                for (int k = layers; k >= 0; k--)
                {
                    TextMeshProUGUI slice = Text(back, glyph, size, k == layers ? _style.ShadowMaterial : _style.SilhouetteMaterial);
                    ((RectTransform)slice.transform).anchoredPosition = new Vector2(0f, -depth * k / layers);
                    slice.color = _style.OutlineColour;
                }
                for (int k = layers; k >= 1; k--)
                {
                    float f = k / (float)layers;
                    TextMeshProUGUI slice = Text(back, glyph, size, _style.ExtrudeMaterial);
                    ((RectTransform)slice.transform).anchoredPosition = new Vector2(0f, -depth * f);
                    slice.color = Color.Lerp(colours.ExtrudeFront, colours.ExtrudeBack, f);
                }

                RectTransform front = Group(_frontLayer, glyph, position, tilt);
                TextMeshProUGUI face = Text(front, glyph, size, _style.FaceMaterial);
                face.enableVertexGradient = true;
                face.colorGradient = new VertexGradient(colours.FaceTop, colours.FaceTop, colours.FaceBottom, colours.FaceBottom);

                var letter = new Letter { Back = back, Front = front, Baseline = baseline, Tilt = tilt };
                _letters.Add(letter);
                StartCoroutine(Pop(letter, (firstIndex + i) * LetterStagger));
            }
        }

        private static RectTransform Group(RectTransform parent, string name, Vector2 position, float tilt)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            rect.localScale = Vector3.zero;
            return rect;
        }

        private TextMeshProUGUI Text(Transform parent, string content, float size, Material material)
        {
            var go = new GameObject(content.Length > 0 ? content : "Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = _style.Font;
            text.fontSharedMaterial = material;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = content;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size * 1.4f, size * 1.4f);
            return text;
        }

        /// <summary>Scale 0 -> overshoot -> 1 while the tilt settles (elastic-ish ease out).</summary>
        private IEnumerator Pop(Letter letter, float delay)
        {
            yield return new WaitForSeconds(delay);
            for (float t = 0f; t < PopDuration; t += Time.deltaTime)
            {
                float u = t / PopDuration;
                float s = Mathf.Min(u * 4f, 1f) * (1f + Mathf.Sin(u * Mathf.PI * 2.4f) * Mathf.Exp(-u * 4.5f) * 0.55f);
                Quaternion r = Quaternion.Euler(0f, 0f, letter.Tilt * (1f + (1f - u) * 4f));
                Set(letter, Vector3.one * s, r);
                yield return null;
            }
            Set(letter, Vector3.one, Quaternion.Euler(0f, 0f, letter.Tilt));
        }

        private static void Set(Letter letter, Vector3 scale, Quaternion rotation)
        {
            letter.Back.localScale = letter.Front.localScale = scale;
            letter.Back.localRotation = letter.Front.localRotation = rotation;
        }

        private void Update()
        {
            float since = Time.time - _shownAt;
            float fade = _fadeToBlack ? Mathf.Clamp01((since - FadeDelay) / FadeDuration) : 0f;
            _fade.color = new Color(0f, 0f, 0f, fade * fade * (3f - 2f * fade));
            for (int i = 0; i < _letters.Count; i++)
            {
                if (since < i * LetterStagger + PopDuration) continue;
                Letter letter = _letters[i];
                float y = letter.Baseline + Mathf.Sin(Time.time * 3.2f + i * 0.55f) * 9f;
                letter.Back.anchoredPosition = new Vector2(letter.Back.anchoredPosition.x, y);
                letter.Front.anchoredPosition = new Vector2(letter.Front.anchoredPosition.x, y);
            }

            bool auto = _autoContinue > 0f;
            if (since < (auto ? Mathf.Min(ContinueDelay, _autoContinue) * 0.5f : ContinueDelay)) return;
            if (!auto)
                _continue.color = new Color(1f, 1f, 1f, 0.7f + 0.3f * Mathf.Sin((since - ContinueDelay) * 4f));
            if ((auto && since >= _autoContinue) || (Pointer.current != null && Pointer.current.press.wasPressedThisFrame))
            {
                enabled = false;
                _onTap?.Invoke();
            }
        }
    }
}
