using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerLand.Meta
{
    /// <summary>Build scene overlay: the construction's name and progress bar at the top, a pulsing "Tap to continue" when done. Built at runtime.</summary>
    public sealed class BuildHud : MonoBehaviour
    {
        private static readonly Color BarBack = new Color(0.10f, 0.05f, 0.20f, 0.45f);
        private static readonly Color BarFill = new Color(1f, 0.76f, 0.16f);
        private const float BarWidth = 640f, BarHeight = 56f, Top = -170f;

        private RectTransform _fill;
        private TextMeshProUGUI _percent;
        private TextMeshProUGUI _continue;
        private float _shown, _target;
        private float _continueSince = -1f;

        public static BuildHud Create(UI.TitleStyle style, string title)
        {
            var go = new GameObject("BuildHud", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = go.AddComponent<BuildHud>();

            Sprite rounded = UI.DebugMenu.CreateRoundedSprite();
            Image back = Box(go.transform, "Bar", rounded, BarBack);
            var bar = back.rectTransform;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(BarWidth, BarHeight);
            bar.anchoredPosition = new Vector2(0f, Top);
            Image fill = Box(bar, "Fill", rounded, BarFill);
            hud._fill = fill.rectTransform;
            hud._fill.anchorMin = Vector2.zero;
            hud._fill.anchorMax = new Vector2(0f, 1f);
            hud._fill.offsetMin = hud._fill.offsetMax = Vector2.zero;

            TextMeshProUGUI name = Text(go.transform, style, title, 84f);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(0f, Top + 85f);
            hud._percent = Text(bar, style, "0%", 46f);
            hud._continue = Text(go.transform, style, "Tap to continue", 72f);
            hud._continue.rectTransform.anchorMin = hud._continue.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hud._continue.rectTransform.anchoredPosition = new Vector2(0f, 330f);
            hud._continue.color = new Color(1f, 1f, 1f, 0f);
            return hud;
        }

        /// <summary>Share of the construction built (the bar eases towards it).</summary>
        public void SetProgress(float fraction, bool immediate = false)
        {
            _target = Mathf.Clamp01(fraction);
            if (immediate) _shown = _target;
            Apply();
        }

        public void ShowContinue() => _continueSince = Time.time;

        public void HideContinue()
        {
            _continueSince = -1f;
            _continue.color = new Color(1f, 1f, 1f, 0f);
        }

        private void Update()
        {
            _shown = Mathf.MoveTowards(Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-12f * Time.deltaTime)), _target, Time.deltaTime * 0.02f);
            Apply();
            if (_continueSince >= 0f)
                _continue.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Time.time - _continueSince) * 4f) * (0.75f + 0.25f * Mathf.Sin((Time.time - _continueSince) * 4f)));
        }

        private void Apply()
        {
            // Never thinner than the rounded ends, so a sliver of progress still reads as a pill.
            float minimum = BarHeight / BarWidth;
            _fill.anchorMax = new Vector2(_shown <= 0f ? 0f : Mathf.Max(_shown, minimum), 1f);
            _percent.text = $"{Mathf.FloorToInt(_shown * 100f)}%";
        }

        private static Image Box(Transform parent, string name, Sprite sprite, Color colour)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, UI.TitleStyle style, string content, float size)
        {
            var text = new GameObject(content, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = style.Font;
            text.fontSharedMaterial = style.FaceMaterial;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = content;
            text.rectTransform.sizeDelta = new Vector2(900f, size * 1.4f);
            return text;
        }
    }
}
