using System.Collections;
using System.IO;
using TowerLand.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>
    /// Play-mode probe for the colour swap: screenshots the idle cubes, taps the queued (smaller) cube with a virtual
    /// mouse, screenshots mid-hop and after, and logs the main cube's colour before / after.
    /// </summary>
    public static class PlaytestSwap
    {
        public static void Tap(string outDir)
        {
            var shooter = Object.FindFirstObjectByType<Shooter>();
            shooter.StartCoroutine(Run(shooter, outDir));
        }

        private static IEnumerator Run(Shooter shooter, string outDir)
        {
            Directory.CreateDirectory(outDir);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "swap0_idle.png"));
            yield return null;
            int before = shooter.CurrentProjectile != null ? shooter.CurrentProjectile.Colour : -1;

            Projectile next = null;
            foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if (p != shooter.CurrentProjectile && (next == null || p.transform.localScale.x < next.transform.localScale.x))
                    next = p;
            if (next == null) { Debug.Log("SWAP no queued cube"); yield break; }
            int queued = next.Colour;
            Vector2 at = Camera.main.WorldToScreenPoint(next.transform.position);

            var mouse = (Mouse)InputSystem.GetDevice("PlaytestMouse") ?? InputSystem.AddDevice<Mouse>("PlaytestMouse");
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = 0 });
            yield return new WaitForSeconds(0.08f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "swap1_hop.png"));
            yield return new WaitForSeconds(0.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "swap2_after.png"));
            yield return null;
            int after = shooter.CurrentProjectile != null ? shooter.CurrentProjectile.Colour : -1;
            Debug.Log($"SWAP main {before} -> {after}, queued was {queued}, tap at {at}, aiming line {(GameObject.Find("AimLine")?.GetComponent<LineRenderer>().enabled ?? false)}");
        }
    }
}
