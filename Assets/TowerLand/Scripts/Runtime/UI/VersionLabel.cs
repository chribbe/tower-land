using UnityEngine;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Small "Tower Land - x.y.z" label in the top-left corner (build identification for testers and screenshots).
    /// The version comes from Player Settings (<see cref="Application.version"/>); the label stays inside the safe area.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class VersionLabel : MonoBehaviour
    {
        [SerializeField] private string _title = "Tower Land";
        [Tooltip("Distance from the safe-area corner, in canvas units.")]
        [SerializeField] private Vector2 _margin = new Vector2(24f, 20f);

        private void Start()
        {
            GetComponent<Text>().text = $"{_title} - {Application.version}";

            var rect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(_margin.x + safe.xMin / scale, -(_margin.y + (Screen.height - safe.yMax) / scale));
        }
    }
}
