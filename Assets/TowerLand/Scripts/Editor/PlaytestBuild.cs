using System.Collections;
using System.Collections.Generic;
using System.IO;
using TowerLand.Core;
using TowerLand.Meta;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// Probes for the build (meta) scene. <see cref="Prepare"/> (edit or play mode) resets the construction progress to
    /// <c>built</c> bricks already standing plus <c>pending</c> new ones to pour (random colours); <see cref="Record"/>
    /// (play mode, Build scene) saves small JPEG frames every <c>interval</c> seconds for <c>seconds</c>.
    /// </summary>
    public static class PlaytestBuild
    {
        public static void Prepare(int built, int pending, int construction = 0)
        {
            foreach (string key in new[] { "progress.bricks", "progress.bricksShown", "progress.construction" })
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt("progress.construction", construction);
            var progress = PlayerProgress.Local();
            DepositHauls(progress, built);
            progress.BricksShown = built;
            DepositHauls(progress, pending);
            Debug.Log($"[TowerLand] Build progress: construction {construction}, {built} built + {pending} to pour.");
        }

        public static void Record(string outDir, float seconds, float interval)
        {
            Object.FindFirstObjectByType<BuildSequence>().StartCoroutine(Run(outDir, seconds, interval));
        }

        private static IEnumerator Run(string outDir, float seconds, float interval)
        {
            Directory.CreateDirectory(outDir);
            int frame = 0;
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    next += interval;
                    yield return new WaitForEndOfFrame();
                    PlaytestWin.Save(Path.Combine(outDir, $"b{frame++:D3}_{t:F2}.jpg"));
                }
                yield return null;
            }
        }

        /// <summary><paramref name="count"/> bricks as level-sized hauls (~720 cubes of 4-6 colours), each sorted into bands like a win.</summary>
        private static void DepositHauls(PlayerProgress progress, int count)
        {
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<Tower.CubePalette>(CoreAssetsBuilder.PalettePath);
            while (count > 0)
            {
                var colours = new List<int>();
                int colourCount = UnityEngine.Random.Range(4, 7);
                while (colours.Count < colourCount)
                {
                    int colour = UnityEngine.Random.Range(0, palette.Count);
                    if (!colours.Contains(colour)) colours.Add(colour);
                }
                var haul = new List<int>();
                for (int i = Mathf.Min(count, 720); i > 0; i--) haul.Add(colours[UnityEngine.Random.Range(0, colours.Count)]);
                count -= haul.Count;
                List<int> stock = progress.Bricks();
                progress.Deposit(BrickSorting.Bands(haul, palette, stock.Count > 0 ? stock[stock.Count - 1] : -1));
            }
        }
    }
}
