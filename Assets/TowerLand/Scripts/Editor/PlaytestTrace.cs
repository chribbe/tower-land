using System.Collections;
using System.IO;
using System.Text;
using TowerLand.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>Play-mode probe: throws at a finger position and logs the thrown cube's path every frame.</summary>
    public static class PlaytestTrace
    {
        public static void ThrowAndTrace(string outFile, float fingerX, float fingerY)
        {
            var shooter = Object.FindFirstObjectByType<Shooter>();
            shooter.StartCoroutine(Run(shooter, outFile, new Vector2(fingerX, fingerY)));
        }

        private static IEnumerator Run(Shooter shooter, string outFile, Vector2 finger)
        {
            var mouse = (Mouse)InputSystem.GetDevice("PlaytestMouse") ?? InputSystem.AddDevice<Mouse>("PlaytestMouse");
            while (shooter.CurrentProjectile == null) yield return null;
            Projectile thrown = shooter.CurrentProjectile;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = finger, buttons = 1 }); mouse.MakeCurrent(); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = finger + Vector2.right, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = finger, buttons = 0 }); yield return null;
            var log = new StringBuilder();
            for (float t = 0f; t < 2f && thrown != null; t += Time.deltaTime)
            {
                log.AppendLine($"{t:F2} {thrown.transform.position:F2} {(thrown.GetComponent<Rigidbody>() ? "physics" : "scripted")}");
                if (Mathf.Abs(t - 0.5f) < 0.02f) ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outFile, ".png"));
                yield return null;
            }
            log.AppendLine(thrown == null ? "destroyed" : "alive");
            File.WriteAllText(outFile, log.ToString());
        }
    }
}
