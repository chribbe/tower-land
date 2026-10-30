#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerLand.UI
{
    /// <summary>
    /// Development aid: F12 saves a PNG of the game view to <c>Screenshots/</c> (project root in the editor,
    /// persistent data path in desktop builds) and reveals it in the file browser with the new file selected.
    /// The <see cref="ReferenceOverlay"/> is hidden for the captured frame. Spawned automatically; editor and
    /// desktop builds only.
    /// </summary>
    public sealed class ScreenshotHotkey : MonoBehaviour
    {
        private bool _capturing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            var go = new GameObject(nameof(ScreenshotHotkey)) { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenshotHotkey>();
        }

        private static string Folder =>
            Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots")
                : Path.Combine(Application.persistentDataPath, "Screenshots");

        private void Update()
        {
            if (!_capturing && Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
                StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            _capturing = true;

            var overlays = FindObjectsByType<ReferenceOverlay>(FindObjectsSortMode.None);
            var modes = new ReferenceOverlay.Mode[overlays.Length];
            for (int i = 0; i < overlays.Length; i++)
            {
                modes[i] = overlays[i].CurrentMode;
                overlays[i].CurrentMode = ReferenceOverlay.Mode.Hidden;
            }

            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();

            for (int i = 0; i < overlays.Length; i++)
            {
                if (overlays[i] != null)
                    overlays[i].CurrentMode = modes[i];
            }

            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, $"TowerLand_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
                File.WriteAllBytes(path, shot.EncodeToPNG());
                Debug.Log($"Screenshot saved: {path}");
                Reveal(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Destroy(shot);
                _capturing = false;
            }
        }

        private static void Reveal(string path)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#elif UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
#elif UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
#endif
        }
    }
}
#endif
