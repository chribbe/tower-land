using System.Collections;
using System.IO;
using System.Text;
using TowerLand.Feel;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// Play-mode probe for the debris clear zone: rains broken-cube chunks in front of the tower in waves, logs how many
    /// settled chunks sit in the zone and how high the tallest is, and screenshots before/after the sweeps.
    /// </summary>
    public static class PlaytestDebris
    {
        public static void Rain(string outDir, int waves, int cubesPerWave)
        {
            var tower = Object.FindFirstObjectByType<TowerView>();
            tower.StartCoroutine(Run(tower, outDir, waves, cubesPerWave));
        }

        private static IEnumerator Run(TowerView tower, string outDir, int waves, int cubesPerWave)
        {
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            Vector3 centre = tower.transform.position;
            Vector3 toCamera = Camera.main.transform.position - centre;
            Vector3 front = new Vector3(toCamera.x, 0f, toCamera.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float face = tower.FaceRadius(0);

            for (int wave = 0; wave < waves; wave++)
            {
                for (int i = 0; i < cubesPerWave; i++)
                {
                    // Just in front of the tower, a little above the base: where tower debris lands.
                    Vector3 p = centre + front * (face + Random.Range(0.2f, 1.6f)) + side * Random.Range(-2.5f, 2.5f)
                                + Vector3.up * Random.Range(0.5f, 3f);
                    CubeChunk.Split(tower.CubeMesh, tower.LooseMaterial(Random.Range(0, 5)), p, Random.rotation, 0.93f, -front * 0.2f,
                        tower.Debris);
                }
                yield return new WaitForSeconds(0.6f);
                log.AppendLine($"wave {wave}: {Describe(tower, centre, front)}");
                if (wave == waves / 2) ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "debris_mid.png"));
            }

            yield return new WaitForSeconds(2f);
            log.AppendLine($"end: {Describe(tower, centre, front)}");
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "debris_end.png"));
            File.WriteAllText(Path.Combine(outDir, "debris.txt"), log.ToString());
        }

        private static string Describe(TowerView tower, Vector3 centre, Vector3 front)
        {
            TowerVisualSettings s = tower.Settings;
            var zone = new DebrisZone(new Vector2(front.x, front.z), tower.FaceRadius(0) + s.ClearZoneReach, s.ClearZoneHalfAngle);
            int total = 0, inZone = 0;
            float top = 0f;
            foreach (CubeChunk chunk in Object.FindObjectsByType<CubeChunk>(FindObjectsSortMode.None))
            {
                total++;
                Vector3 p = chunk.transform.position;
                if (!zone.Contains(new Vector2(p.x - centre.x, p.z - centre.z))) continue;
                inZone++;
                top = Mathf.Max(top, p.y + chunk.HalfSize);
            }
            return $"chunks {total}, in zone {inZone}, tallest top {top:F2} (base top {centre.y:F2})";
        }
    }
}
