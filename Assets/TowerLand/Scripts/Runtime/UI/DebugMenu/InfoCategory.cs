using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TowerLand.UI
{
    /// <summary>Debug tab: build, device and screen facts for bug reports, plus a live frame rate.</summary>
    public sealed class InfoCategory : DebugCategory
    {
        private TextMeshProUGUI _fps;
        private float _smoothedDelta = 1f / 60f;

        public override string Title => "Info";

        public override void Build(DebugMenu menu, RectTransform content)
        {
            var rows = new List<(string, string)>
            {
                ("Version", Application.version),
                ("Unity", Application.unityVersion),
                ("Platform", Application.platform.ToString()),
                ("Device", SystemInfo.deviceModel),
                ("OS", SystemInfo.operatingSystem),
                ("GPU", SystemInfo.graphicsDeviceName),
                ("Memory", $"{SystemInfo.systemMemorySize} MB"),
                ("Screen", $"{Screen.width} x {Screen.height} @ {Screen.dpi:0} dpi"),
                ("Safe area", Screen.safeArea.ToString()),
                ("Quality", QualitySettings.names[QualitySettings.GetQualityLevel()]),
                ("Target FPS", Application.targetFrameRate <= 0 ? "platform default" : Application.targetFrameRate.ToString()),
            };
            _fps = Line(menu, content, "Frame rate", "-");
            foreach ((string key, string value) in rows)
                Line(menu, content, key, value);
        }

        private static TextMeshProUGUI Line(DebugMenu menu, RectTransform content, string key, string value)
        {
            RectTransform row = menu.Row(content, key, 84f);
            DebugMenu.Stretch(menu.Box(row, "Background", DebugMenu.Panel).rectTransform);
            TextMeshProUGUI label = menu.Text(row, key, 36f, DebugMenu.TextDim);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            DebugMenu.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            TextMeshProUGUI text = menu.Text(row, value, 36f, Color.white);
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.overflowMode = TextOverflowModes.Ellipsis;
            DebugMenu.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(300f, 0f);
            text.rectTransform.offsetMax = new Vector2(-28f, 0f);
            return text;
        }

        public override void Tick()
        {
            if (_fps == null) return;
            _smoothedDelta = Mathf.Lerp(_smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            _fps.text = $"{1f / _smoothedDelta:0} fps ({_smoothedDelta * 1000f:0.0} ms)";
        }
    }
}
