using System.Collections;
using System.IO;
using TowerLand.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>
    /// Play-mode probe for the win sequence: presses D on a virtual keyboard (breaks the whole tower), then saves small JPEG
    /// frames of the game view every <c>interval</c> seconds for <c>seconds</c> - a flip-book of the vacuum sequence.
    /// </summary>
    public static class PlaytestWin
    {
        public static void Record(string outDir, float seconds, float interval)
        {
            var sequence = Object.FindFirstObjectByType<WinSequence>();
            sequence.StartCoroutine(Run(outDir, seconds, interval));
        }

        private static IEnumerator Run(string outDir, float seconds, float interval)
        {
            Directory.CreateDirectory(outDir);
            var keyboard = (Keyboard)InputSystem.GetDevice("PlaytestKeyboard") ?? InputSystem.AddDevice<Keyboard>("PlaytestKeyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            keyboard.MakeCurrent();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            int frame = 0;
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    next += interval;
                    yield return new WaitForEndOfFrame();
                    Save(Path.Combine(outDir, $"f{frame++:D3}_{t:F2}.jpg"));
                }
                yield return null;
            }
        }

        internal static void Save(string path)
        {
            Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
            var small = RenderTexture.GetTemporary(360, 640, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(full, small);
            var previous = RenderTexture.active;
            RenderTexture.active = small;
            var tex = new Texture2D(360, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 360, 640), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            File.WriteAllBytes(path, tex.EncodeToJPG(85));
            Object.Destroy(full);
            Object.Destroy(tex);
        }
    }
}
