using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Debug scene UI: a header (title + back to the game), a tab bar with one tab per <see cref="DebugCategory"/>
    /// component found on this object or its children, and a scrolling panel per category (built when first opened).
    /// Everything is built at runtime on an overlay canvas; categories use the widget helpers below so the menu keeps
    /// one look.
    /// </summary>
    public sealed class DebugMenu : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private string _gameScene = "Game";

        public static readonly Color Background = new Color(0.10f, 0.08f, 0.16f);
        public static readonly Color Panel = new Color(0.17f, 0.14f, 0.26f);
        public static readonly Color PanelLight = new Color(0.25f, 0.21f, 0.37f);
        public static readonly Color Accent = new Color(1f, 0.74f, 0.16f);
        public static readonly Color TextDim = new Color(0.72f, 0.69f, 0.84f);

        private const float HeaderHeight = 150f, TabHeight = 110f, Margin = 32f;

        private readonly List<DebugCategory> _categories = new List<DebugCategory>();
        private readonly List<Image> _tabs = new List<Image>();
        private readonly List<TextMeshProUGUI> _tabLabels = new List<TextMeshProUGUI>();
        private readonly Dictionary<DebugCategory, GameObject> _panels = new Dictionary<DebugCategory, GameObject>();
        private RectTransform _body;
        private Sprite _rounded;
        private int _open = -1;

        public TMP_FontAsset TitleFont => _titleFont != null ? _titleFont : TMP_Settings.defaultFontAsset;
        public string GameScene => _gameScene;
        /// <summary>Rounded-rectangle sprite (9-sliced) for panels, cards and buttons.</summary>
        public Sprite Rounded => _rounded;

        private void Start()
        {
            EnsureEventSystem();
            _rounded = CreateRoundedSprite();
            GetComponentsInChildren(_categories);
            _categories.Sort((a, b) => a.Order.CompareTo(b.Order));
            BuildLayout();
            if (_categories.Count > 0) Open(0);
        }

        private void Update()
        {
            if (_open >= 0) _categories[_open].Tick();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ---------------------------------------------------------------- layout
        private void BuildLayout()
        {
            var go = new GameObject("DebugCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;   // portrait: width drives the scale, taller phones get more list

            Image background = Box(go.transform, "Background", Background, rounded: false);
            Stretch(background.rectTransform);

            // Everything else stays inside the safe area (notches, home indicator).
            RectTransform safe = NewRect(go.transform, "SafeArea");
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            RectTransform header = NewRect(safe, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.offsetMin = new Vector2(Margin, -HeaderHeight);
            header.offsetMax = new Vector2(-Margin, 0f);
            TextMeshProUGUI title = Text(header, "DEBUG", 84f, Accent, TitleFont);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(title.rectTransform);
            Button back = Button(header, "BACK TO GAME", () => SceneManager.LoadScene(_gameScene), 44f);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(1f, 0.5f);
            backRect.sizeDelta = new Vector2(400f, 100f);

            RectTransform tabs = NewRect(safe, "Tabs");
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = Vector2.one;
            tabs.offsetMin = new Vector2(Margin, -HeaderHeight - TabHeight);
            tabs.offsetMax = new Vector2(-Margin, -HeaderHeight);
            var row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 16f;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            row.childControlWidth = row.childControlHeight = true;
            for (int i = 0; i < _categories.Count; i++)
            {
                int index = i;
                Button tab = Button(tabs, _categories[i].Title.ToUpperInvariant(), () => Open(index), 46f);
                _tabs.Add(tab.GetComponent<Image>());
                _tabLabels.Add(tab.GetComponentInChildren<TextMeshProUGUI>());
            }

            _body = NewRect(safe, "Body");
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(0f, 0f);
            _body.offsetMax = new Vector2(0f, -HeaderHeight - TabHeight - 20f);
        }

        /// <summary>Shows a category's panel (built on first open) and highlights its tab.</summary>
        public void Open(int index)
        {
            _open = index;
            for (int i = 0; i < _categories.Count; i++)
            {
                bool selected = i == index;
                _tabs[i].color = selected ? Accent : Panel;
                _tabLabels[i].color = selected ? Background : Color.white;
                if (_panels.TryGetValue(_categories[i], out GameObject panel))
                    panel.SetActive(selected);
            }
            DebugCategory category = _categories[index];
            if (!_panels.ContainsKey(category))
                _panels[category] = BuildPanel(category);
        }

        private GameObject BuildPanel(DebugCategory category)
        {
            // Scroll view: viewport (clips) > content (vertical column that grows with its children).
            RectTransform root = NewRect(_body, category.Title);
            Stretch(root);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            RectTransform viewport = NewRect(root, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;   // catches drags between widgets

            RectTransform content = NewRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset((int)Margin, (int)Margin, 8, 60);
            column.spacing = 20f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            category.Build(this, content);
            return root.gameObject;
        }

        // ---------------------------------------------------------------- widget helpers
        public static RectTransform NewRect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public Image Box(Transform parent, string name, Color colour, bool rounded = true)
        {
            var image = NewRect(parent, name).gameObject.AddComponent<Image>();
            image.color = colour;
            if (rounded && _rounded != null)
            {
                image.sprite = _rounded;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public TextMeshProUGUI Text(Transform parent, string text, float size, Color colour, TMP_FontAsset font = null)
        {
            var label = NewRect(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        public Button Button(Transform parent, string text, Action onClick, float size = 44f)
        {
            Image box = Box(parent, text, Panel);
            var button = box.gameObject.AddComponent<Button>();
            ColorBlock colours = button.colors;
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colours.fadeDuration = 0.05f;
            button.colors = colours;
            button.onClick.AddListener(() => onClick());
            TextMeshProUGUI label = Text(box.transform, text, size, Color.white, TitleFont);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>A row in a category column: fixed height, full width.</summary>
        public RectTransform Row(Transform parent, string name, float height)
        {
            RectTransform row = NewRect(parent, name);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            return row;
        }

        /// <summary>White rounded-rectangle texture with 9-slice borders, drawn once (no editor-only UI skin at runtime).</summary>
        public static Sprite CreateRoundedSprite()
        {
            const int size = 64, radius = 22;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DebugRounded", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }
}
