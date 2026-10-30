using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Full-screen black overlay for scene switches: a scene that opens out of black fades in with <see cref="In"/> (black
    /// from its very first frame), one that leaves fades out with <see cref="Out"/>. Built at runtime, above all other UI.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private Image _image;

        public static ScreenFade In(float duration) => Create(1f).Run(1f, 0f, duration, null, true);

        public static ScreenFade Out(float duration, Action done) => Create(0f).Run(0f, 1f, duration, done, false);

        private static ScreenFade Create(float alpha)
        {
            var go = new GameObject("ScreenFade", typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var fade = go.AddComponent<ScreenFade>();
            fade._image = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            fade._image.transform.SetParent(go.transform, false);
            fade._image.rectTransform.anchorMin = Vector2.zero;
            fade._image.rectTransform.anchorMax = Vector2.one;
            fade._image.rectTransform.sizeDelta = Vector2.zero;
            fade._image.raycastTarget = false;
            fade._image.color = new Color(0f, 0f, 0f, alpha);
            return fade;
        }

        private ScreenFade Run(float from, float to, float duration, Action done, bool destroyAfter)
        {
            StartCoroutine(Fade(from, to, duration, done, destroyAfter));
            return this;
        }

        private IEnumerator Fade(float from, float to, float duration, Action done, bool destroyAfter)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                _image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, u * u * (3f - 2f * u)));
                yield return null;
            }
            _image.color = new Color(0f, 0f, 0f, to);
            done?.Invoke();
            if (destroyAfter) Destroy(gameObject);
        }
    }
}
