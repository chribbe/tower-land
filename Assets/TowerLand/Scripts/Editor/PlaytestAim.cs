using System.Collections;
using System.IO;
using TowerLand.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>Play-mode probe: holds a virtual finger at a few aim positions (viewport x, y) and screenshots each, then releases without throwing far.</summary>
    public static class PlaytestAim
    {
        public static void Hold(string outDir, float pressX, float pressY, Vector2[] drags)
        {
            var shooter = Object.FindFirstObjectByType<Shooter>();
            shooter.StartCoroutine(Run(outDir, new Vector2(pressX, pressY), drags));
        }

        private static IEnumerator Run(string outDir, Vector2 press, Vector2[] drags)
        {
            Directory.CreateDirectory(outDir);
            var mouse = (Mouse)InputSystem.GetDevice("PlaytestMouse") ?? InputSystem.AddDevice<Mouse>("PlaytestMouse");
            mouse.MakeCurrent();
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = press * screen, buttons = 1 });
            yield return null;
            for (int i = 0; i < drags.Length; i++)
            {
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = drags[i] * screen, buttons = 1 });
                    yield return null;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"aim{i}.png"));
                yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = drags[drags.Length - 1] * screen, buttons = 0 });
        }
    }
}
