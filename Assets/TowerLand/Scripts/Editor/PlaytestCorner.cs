using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>
    /// Play-mode probe for the secret debug corner: taps the top-right corner <c>count</c> times (each a press + release,
    /// <c>interval</c> seconds apart) with a virtual mouse, then logs the active scene and how many cubes were thrown.
    /// </summary>
    public static class PlaytestCorner
    {
        public static void Tap(int count, float interval)
        {
            var host = new GameObject("PlaytestCorner").AddComponent<Host>();
            Object.DontDestroyOnLoad(host.gameObject);
            host.StartCoroutine(Run(host, count, interval));
        }

        private sealed class Host : MonoBehaviour { }

        private static IEnumerator Run(Host host, int count, float interval)
        {
            var mouse = (Mouse)InputSystem.GetDevice("PlaytestMouse") ?? InputSystem.AddDevice<Mouse>("PlaytestMouse");
            mouse.MakeCurrent();
            var at = new Vector2(Screen.width - 40f, Screen.height - 40f);
            int before = Object.FindObjectsByType<Gameplay.Projectile>(FindObjectsSortMode.None).Length;
            for (int i = 0; i < count; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = 1 });
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = at, buttons = 0 });
                yield return new WaitForSecondsRealtime(interval);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            int after = Object.FindObjectsByType<Gameplay.Projectile>(FindObjectsSortMode.None).Length;
            Debug.Log($"CORNER taps={count} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                      $"debugger={Core.PlayerProgress.Local().IsDebugger} projectiles {before}->{after}");
            Object.Destroy(host.gameObject);
        }
    }
}
