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
    /// Play-mode probe for aiming through a hole: punches a window through every layer facing the camera, aims through it
    /// at the far wall, gives the waiting cube that patch's colour, throws, and checks the flight every frame: does the
    /// projectile's centre ever sit inside an occupied cube (= flew through the tower)? <see cref="Sweep"/> runs a series
    /// of holes / aim points on fresh towers and writes one line per throw.
    /// </summary>
    public static class PlaytestBehind
    {
        public static void Run(string outDir, int floorFrom, int floorTo, float halfAngle)
        {
            var shooter = Object.FindFirstObjectByType<Shooter>();
            shooter.StartCoroutine(Single(shooter, outDir, floorFrom, floorTo, halfAngle));
        }

        /// <summary>Several throws (fresh tower each): holes of different sizes, aim points across the window.</summary>
        public static void Sweep(string outFile)
        {
            var shooter = Object.FindFirstObjectByType<Shooter>();
            shooter.StartCoroutine(SweepRun(shooter, outFile));
        }

        private static IEnumerator Single(Shooter shooter, string outDir, int floorFrom, int floorTo, float halfAngle)
        {
            Directory.CreateDirectory(outDir);
            var result = new StringBuilder();
            yield return Throw(shooter, floorFrom, floorTo, halfAngle, new Vector2(0f, 0.5f), outDir, result);
            Debug.Log("BEHIND " + result);
        }

        private static IEnumerator SweepRun(Shooter shooter, string outFile)
        {
            var bootstrap = Object.FindFirstObjectByType<LevelBootstrap>();
            var holes = new (int from, int to, float half)[] { (4, 9, 40f), (5, 8, 25f), (6, 8, 16f), (3, 10, 30f) };
            var aims = new[] { new Vector2(0f, 0.5f), new Vector2(-0.35f, 0.3f), new Vector2(0.35f, 0.7f), new Vector2(0f, 0.1f), new Vector2(0f, 0.9f) };
            var log = new StringBuilder();
            int seed = 11;
            foreach ((int from, int to, float half) in holes)
            {
                foreach (Vector2 aim in aims)
                {
                    bootstrap.Generate(seed++);
                    yield return new WaitForSeconds(0.3f);
                    log.Append($"hole floors {from}-{to} +-{half} aim {aim}: ");
                    yield return Throw(shooter, from, to, half, aim, null, log);
                    log.AppendLine();
                }
            }
            File.WriteAllText(outFile, log.ToString());
            Debug.Log("BEHIND sweep done");
        }

        /// <param name="aimInWindow">Aim point inside the window: x = -0.5..0.5 of its width, y = 0..1 of its height.</param>
        private static IEnumerator Throw(Shooter shooter, int floorFrom, int floorTo, float halfAngle, Vector2 aimInWindow,
            string shotDir, StringBuilder result)
        {
            var tower = Object.FindFirstObjectByType<TowerView>();
            Camera camera = Camera.main;
            Vector3 toCamera = tower.SpinRoot.InverseTransformPoint(camera.transform.position);
            float facing = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
            var cubes = new List<RemovedCube>();
            for (int layer = 0; layer < tower.Layers.Count; layer++)
            {
                TowerGrid grid = tower.Layers[layer];
                for (int column = 0; column < grid.Columns; column++)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(360f * column / grid.Columns, facing)) > halfAngle) continue;
                    for (int floor = floorFrom; floor <= floorTo; floor++)
                        if (grid[column, floor] != TowerGrid.Empty)
                            cubes.Add(new RemovedCube(layer, new CellCoord(column, floor), 0));
                }
            }
            tower.RemoveCubes(cubes, 0f);
            while (shooter.CurrentProjectile == null) yield return null;
            yield return new WaitForSeconds(0.25f);

            // Aim point: on the window's plane through the tower axis, at the requested spot.
            float spacing = tower.Settings.CubeSpacing;
            float width = 2f * tower.FaceRadius(tower.Layers.Count - 1) * Mathf.Sin(halfAngle * Mathf.Deg2Rad);
            float height = Mathf.Lerp(floorFrom + 0.5f, floorTo + 0.5f, aimInWindow.y) * spacing;
            Vector3 right = Vector3.Cross(Vector3.up, camera.transform.position - tower.SpinRoot.position).normalized * -1f;
            Vector3 world = tower.SpinRoot.position + Vector3.up * height + right * (aimInWindow.x * width);
            Vector2 aim = camera.WorldToScreenPoint(world);
            Ray ray = camera.ScreenPointToRay(aim);
            if (!tower.Raycast(ray.origin, ray.origin + ray.direction * 500f, out TowerHit hit))
            {
                result.Append("no hit through the hole");
                yield break;
            }
            int colour = tower.Layers[hit.Layer][hit.Cell];
            shooter.CurrentProjectile.SetColour(colour);
            Projectile thrown = shooter.CurrentProjectile;
            int before = Count(tower);

            var mouse = (Mouse)InputSystem.GetDevice("PlaytestMouse") ?? InputSystem.AddDevice<Mouse>("PlaytestMouse");
            mouse.MakeCurrent();
            Vector2 finger = aim - Vector2.up * (shooter.Settings.FingerOffset * Screen.height);
            for (int i = 0; i < 6; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = finger, buttons = 1 });
                yield return null;
            }
            if (shotDir != null) ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, "aim.png"));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = finger, buttons = 0 });

            // Track the flight: centre inside an occupied cube = went through the tower.
            int inside = 0;
            string firstInside = null;
            for (float t = 0f; t < 1.5f && thrown != null; t += Time.deltaTime)
            {
                if (thrown.GetComponent<Rigidbody>() == null && InsideCube(tower, thrown.transform.position, out string where))
                {
                    inside++;
                    firstInside ??= $"{where} at {t:F2}s";
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            if (shotDir != null) ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, "after.png"));
            bool gone = tower.Layers[hit.Layer][hit.Cell] == TowerGrid.Empty;
            result.Append($"target L{hit.Layer} {hit.Cell.Column},{hit.Cell.Floor} inside={hit.Inside} destroyed={gone} " +
                          $"cubes {before}->{Count(tower)} | frames inside cubes: {inside}{(firstInside != null ? " first " + firstInside : "")}");
        }

        /// <summary>Whether a world point is inside an occupied cube's volume (its layer band, its cell), shrunk a little.</summary>
        private static bool InsideCube(TowerView tower, Vector3 world, out string where)
        {
            where = null;
            Vector3 p = tower.SpinRoot.InverseTransformPoint(world);
            float r = new Vector2(p.x, p.z).magnitude;
            float half = tower.Settings.CubeScale * 0.5f - 0.05f;
            int floor = Mathf.FloorToInt(p.y / tower.Settings.CubeSpacing);
            for (int layer = 0; layer < tower.Layers.Count; layer++)
            {
                TowerGrid grid = tower.Layers[layer];
                if (Mathf.Abs(r - tower.RadiusOf(layer)) > half || floor < 0 || floor >= grid.Floors) continue;
                int column = grid.WrapColumn(Mathf.RoundToInt(Mathf.Atan2(p.x, p.z) / (2f * Mathf.PI) * grid.Columns));
                if (grid[column, floor] == TowerGrid.Empty) continue;
                where = $"L{layer} {column},{floor}";
                return true;
            }
            return false;
        }

        private static int Count(TowerView tower)
        {
            int n = 0;
            foreach (TowerGrid grid in tower.Layers) n += grid.CountFilled();
            return n;
        }
    }
}
