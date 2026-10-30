using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TowerLand.Gameplay;
using TowerLand.Tower;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>
    /// Play-mode bot for checking gameplay from the CLI: aims at a cube matching the waiting projectile (on the side
    /// facing the camera) through a virtual mouse, throws, and logs/screenshots the result. Runs as a coroutine on the
    /// Shooter, so it spans real frames. Usage (Play mode, Run In Background on):
    ///   unity cmd script eval --code 'TowerLand.Editor.Playtest.AutoShoot(@"C:/out", 3); return 0;'
    /// Writes autoshot.txt + shotN_aim/impact/after/landed.png per shot to the folder when done.
    /// </summary>
    public static class Playtest
    {
        private const string DeviceName = "PlaytestMouse";

        public static void AutoShoot(string outDir, int shots)
        {
            var shooter = Object.FindFirstObjectByType<Shooter>();
            var tower = Object.FindFirstObjectByType<TowerView>();
            if (!Application.isPlaying || shooter == null || tower == null)
            {
                Debug.LogWarning("[TowerLand] Playtest.AutoShoot needs Play mode with a Shooter and a TowerView.");
                return;
            }
            Directory.CreateDirectory(outDir);
            shooter.StartCoroutine(Run(shooter, tower, outDir, shots));
        }

        private static IEnumerator Run(Shooter shooter, TowerView tower, string outDir, int shots)
        {
            var mouse = (Mouse)InputSystem.GetDevice(DeviceName) ?? InputSystem.AddDevice<Mouse>(DeviceName);
            var log = new StringBuilder();
            for (int shot = 0; shot < shots; shot++)
            {
                while (shooter.CurrentProjectile == null) yield return null;
                yield return new WaitForSeconds(0.3f);
                Projectile projectile = shooter.CurrentProjectile;
                Vector2? aim = FingerFor(tower, projectile.Colour, out int targetCubes);
                for (float waited = 0f; aim == null && waited < 25f; waited += Time.deltaTime)
                {
                    yield return null;   // the colour may only be on the far side: wait for the tower to spin it round
                    aim = FingerFor(tower, projectile.Colour, out targetCubes);
                }
                if (aim == null)
                {
                    log.AppendLine($"shot {shot}: no visible cube of colour {projectile.Colour}");
                    break;
                }

                int before = CubeCount(tower);
                Send(mouse, aim.Value, true); yield return null;
                Send(mouse, aim.Value + Vector2.right, true); yield return null; yield return null;
                Capture(outDir, $"shot{shot}_aim");
                yield return null;
                Send(mouse, aim.Value, false);
                yield return new WaitForSeconds(0.2f);
                Capture(outDir, $"shot{shot}_hit");      // impact flash
                yield return new WaitForSeconds(0.05f);
                Capture(outDir, $"shot{shot}_spread");   // mid-ripple
                yield return new WaitForSeconds(0.08f);
                Capture(outDir, $"shot{shot}_impact");
                yield return new WaitForSeconds(0.7f);
                Capture(outDir, $"shot{shot}_after");
                yield return new WaitForSeconds(0.5f);
                Capture(outDir, $"shot{shot}_landed");   // debris on the ground
                log.AppendLine($"shot {shot}: colour {projectile.Colour}, cubes {before} -> {CubeCount(tower)}");
            }
            File.WriteAllText(Path.Combine(outDir, "autoshot.txt"), log.ToString());
        }

        /// <summary>Finger position that aims at the most central camera-facing exposed cube of a colour.</summary>
        private static Vector2? FingerFor(TowerView tower, int colour, out int candidates)
        {
            Camera camera = Camera.main;
            var exposed = new List<(int layer, CellCoord cell)>();
            TowerRules.GetExposedCubes(tower.Layers, exposed);
            candidates = 0;
            Vector3 best = default;
            float bestScore = float.MaxValue;
            foreach ((int layer, CellCoord cell) in exposed)
            {
                if (tower.Layers[layer][cell] != colour) continue;
                Vector3 local = tower.LocalPositionOf(layer, cell, out Quaternion rotation);
                Vector3 face = tower.SpinRoot.TransformPoint(local + rotation * Vector3.forward * 0.5f);
                Vector3 normal = tower.SpinRoot.rotation * rotation * Vector3.forward;
                if (Vector3.Dot(normal, (camera.transform.position - face).normalized) < 0.8f) continue;
                Vector3 screen = camera.WorldToScreenPoint(face);
                float score = Mathf.Abs(screen.x - Screen.width * 0.5f) + Mathf.Abs(screen.y - Screen.height * 0.55f);
                candidates++;
                if (score < bestScore) { bestScore = score; best = screen; }
            }
            if (candidates == 0) return null;
            // The shooter aims a fixed offset above the finger.
            float offset = Object.FindFirstObjectByType<Shooter>().Settings.FingerOffset;
            return new Vector2(best.x, best.y - offset * Screen.height);
        }

        private static void Send(Mouse mouse, Vector2 position, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });
            mouse.MakeCurrent();
        }

        private static int CubeCount(TowerView tower)
        {
            int count = 0;
            foreach (TowerGrid grid in tower.Layers) count += grid.CountFilled();
            return count;
        }

        private static void Capture(string dir, string name) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
    }
}
