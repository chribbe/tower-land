using System.Collections;
using System.Collections.Generic;
using System.IO;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// Play-mode probe: punches a window through every layer of the tower on the side facing the camera (so the far
    /// side's inner faces show through the hollow core) and saves screenshots as it turns. <c>layers</c> limits it to
    /// the outermost ones (1 = outer layer only: the window's edges partly reveal the cubes below).
    /// </summary>
    public static class PlaytestHole
    {
        public static void Punch(string outDir, float halfAngle, int floorFrom, int floorTo, float[] shotTimes, int layers = int.MaxValue)
        {
            var tower = Object.FindFirstObjectByType<TowerView>();
            tower.StartCoroutine(Run(tower, outDir, halfAngle, floorFrom, floorTo, shotTimes, layers));
        }

        private static IEnumerator Run(TowerView tower, string outDir, float halfAngle, int floorFrom, int floorTo, float[] shotTimes, int layers)
        {
            Directory.CreateDirectory(outDir);
            // Aim the window a little ahead of the camera so it turns into view.
            Vector3 toCamera = tower.SpinRoot.InverseTransformPoint(Camera.main.transform.position);
            float facing = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
            var cubes = new List<RemovedCube>();
            for (int layer = 0; layer < Mathf.Min(layers, tower.Layers.Count); layer++)
            {
                TowerGrid grid = tower.Layers[layer];
                for (int column = 0; column < grid.Columns; column++)
                {
                    float angle = 360f * column / grid.Columns;
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, facing)) > halfAngle) continue;
                    for (int floor = floorFrom; floor <= Mathf.Min(floorTo, grid.Floors - 1); floor++)
                        if (grid[column, floor] != TowerGrid.Empty)
                            cubes.Add(new RemovedCube(layer, new CellCoord(column, floor), 0));
                }
            }
            tower.RemoveCubes(cubes, 0f);
            float start = Time.time;
            for (int i = 0; i < shotTimes.Length; i++)
            {
                while (Time.time - start < shotTimes[i]) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"hole{i}.png"));
                yield return null;
            }
        }
    }
}
