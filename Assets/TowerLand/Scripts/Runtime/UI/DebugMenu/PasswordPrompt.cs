using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Modal "secret code" prompt: dimmed screen, a password field (focusing it brings up the device keyboard) and
    /// OK / Cancel. A wrong code shakes the panel and clears the field. Built at runtime on its own overlay canvas;
    /// the dim layer swallows every tap, so nothing behind reacts while it is open.
    /// </summary>
    public sealed class PasswordPrompt : MonoBehaviour
    {
        private Func<string, bool> _check;
        private Action _closed;
        private TMP_InputField _input;
        private TextMeshProUGUI _title;
        private RectTransform _panel;
        private float _shake = -1f;

        /// <param name="check">Called with the entered code; true closes the prompt (the caller takes over).</param>
        /// <param name="closed">Called when the prompt goes away (cancelled or accepted).</param>
        public static PasswordPrompt Show(TMP_FontAsset titleFont, Func<string, bool> check, Action closed)
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = new GameObject("PasswordPrompt", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.SetActive(false);   // input field wiring must be complete before it enables
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;

            var prompt = root.AddComponent<PasswordPrompt>();
            prompt._check = check;
            prompt._closed = closed;
            Sprite rounded = DebugMenu.CreateRoundedSprite();

            Image dim = Box(root.transform, "Dim", new Color(0f, 0f, 0f, 0.8f), null);   // linear blending: reads ~half as dark
            DebugMenu.Stretch(dim.rectTransform);

            // Upper half of the screen: stays clear of the keyboard.
            Image panel = Box(root.transform, "Panel", DebugMenu.Background, rounded);
            prompt._panel = panel.rectTransform;
            prompt._panel.anchorMin = prompt._panel.anchorMax = new Vector2(0.5f, 0.72f);
            prompt._panel.sizeDelta = new Vector2(860f, 520f);

            prompt._title = Label(panel.transform, "SECRET CODE", 72f, DebugMenu.Accent, titleFont);
            Place(prompt._title.rectTransform, new Vector2(0f, 175f), new Vector2(780f, 110f));

            Image field = Box(panel.transform, "Field", DebugMenu.PanelLight, rounded);
            Place(field.rectTransform, new Vector2(0f, 20f), new Vector2(760f, 130f));
            RectTransform area = DebugMenu.NewRect(field.transform, "Text Area");
            DebugMenu.Stretch(area);
            area.offsetMin = new Vector2(32f, 10f);
            area.offsetMax = new Vector2(-32f, -10f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = Label(area, "password", 56f, DebugMenu.TextDim, null);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            DebugMenu.Stretch(placeholder.rectTransform);
            TextMeshProUGUI text = Label(area, "", 64f, Color.white, null);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            DebugMenu.Stretch(text.rectTransform);

            prompt._input = field.gameObject.AddComponent<TMP_InputField>();
            prompt._input.textViewport = area;
            prompt._input.textComponent = text;
            prompt._input.placeholder = placeholder;
            prompt._input.contentType = TMP_InputField.ContentType.Password;
            prompt._input.lineType = TMP_InputField.LineType.SingleLine;
            prompt._input.characterLimit = 32;
            prompt._input.onSubmit.AddListener(_ => prompt.Submit());

            Button cancel = Button(panel.transform, "CANCEL", DebugMenu.Panel, titleFont, rounded, prompt.Close);
            Place((RectTransform)cancel.transform, new Vector2(-195f, -165f), new Vector2(370f, 120f));
            Button ok = Button(panel.transform, "OK", DebugMenu.Accent, titleFont, rounded, prompt.Submit);
            Place((RectTransform)ok.transform, new Vector2(195f, -165f), new Vector2(370f, 120f));
            ok.GetComponentInChildren<TextMeshProUGUI>().color = DebugMenu.Background;

            root.SetActive(true);
            prompt._input.ActivateInputField();   // opens the device keyboard right away
            return prompt;
        }

        private void Submit()
        {
            if (_check(_input.text))
            {
                Close();
                return;
            }
            _title.text = "WRONG CODE";
            _title.color = new Color(1f, 0.35f, 0.35f);
            _input.text = "";
            _input.ActivateInputField();
            _shake = 0f;
        }

        private void Close()
        {
            _closed?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_shake < 0f) return;
            _shake += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_shake / 0.35f);
            _panel.anchoredPosition = new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * 28f * (1f - t), 0f);
            if (t >= 1f) _shake = -1f;
        }

        // ---------------------------------------------------------------- widgets
        private static Image Box(Transform parent, string name, Color colour, Sprite sprite)
        {
            var image = DebugMenu.NewRect(parent, name).gameObject.AddComponent<Image>();
            image.color = colour;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, float size, Color colour, TMP_FontAsset font)
        {
            var label = DebugMenu.NewRect(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string text, Color colour, TMP_FontAsset font, Sprite sprite, Action onClick)
        {
            Image box = Box(parent, text, colour, sprite);
            var button = box.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            DebugMenu.Stretch(Label(box.transform, text, 52f, Color.white, font).rectTransform);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
