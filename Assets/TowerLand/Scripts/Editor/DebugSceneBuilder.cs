using System.IO;
using TMPro;
using TowerLand.Gameplay;
using TowerLand.Levels;
using TowerLand.Levels.Generation;
using TowerLand.Tower;
using TowerLand.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// The level list and the debug scene:
    /// - <b>Build Level List</b>: pattern variants, the 10 starter levels (Levels/Level_01..10), the LevelList and LevelSession
    ///   assets, then bakes each level's thumbnail through the Game scene's camera (Art/UI/LevelThumbnails).
    /// - <b>Build Debug Scene</b>: Scenes/Debug.unity (DebugMenu with the Levels and Info tabs), added to the build after
    ///   the Game scene, and wires the Game scene (bootstrap reads the session, version label opens the debug scene).
    /// Re-run Build Level List after changing a level or the look of the game.
    /// </summary>
    public static class DebugSceneBuilder
    {
        public const string DebugScenePath = "Assets/TowerLand/Scenes/Debug.unity";
        private const string LevelsDir = "Assets/TowerLand/Levels";
        private const string PatternsDir = "Assets/TowerLand/Levels/Patterns";
        private const string ThumbnailsDir = "Assets/TowerLand/Art/UI/LevelThumbnails";
        internal const string LevelListPath = "Assets/TowerLand/Levels/LevelList.asset";
        private const string SessionPath = "Assets/TowerLand/Data/LevelSession.asset";
        private const string FontPath = "Assets/TowerLand/Art/UI/LilitaOne SDF.asset";
        private const int Columns = 26;   // the scene's plinth and camera are fitted to 26-column towers
        // Thumbnail = the game camera's view, cropped to the tower (top 3% .. 70% of the frame): 0.84 aspect.
        private const int RenderWidth = 540, RenderHeight = 960;
        private const float CropTop = 0.03f, CropBottom = 0.70f;

        private struct Pattern
        {
            public string Name;
            public int Regions;
            public VoronoiMetric Metric;
            public float Stretch;
        }

        private static readonly Pattern[] Patterns =
        {
            new Pattern { Name = "Voronoi_Large", Regions = 5, Metric = VoronoiMetric.Manhattan, Stretch = 1f },
            new Pattern { Name = "Voronoi_Default", Regions = 7, Metric = VoronoiMetric.Manhattan, Stretch = 1f },
            new Pattern { Name = "Voronoi_Wide", Regions = 8, Metric = VoronoiMetric.Manhattan, Stretch = 0.5f },
            new Pattern { Name = "Voronoi_Tall", Regions = 8, Metric = VoronoiMetric.Manhattan, Stretch = 2f },
            new Pattern { Name = "Voronoi_Round", Regions = 9, Metric = VoronoiMetric.Euclidean, Stretch = 1f },
            new Pattern { Name = "Voronoi_Busy", Regions = 11, Metric = VoronoiMetric.Manhattan, Stretch = 1f },
        };

        // Gentle ramp: taller towers, a third layer, more colours, busier patterns. Two layers minimum (one never collapses).
        private static readonly (int floors, int layers, int colours, string pattern, int seed)[] Levels =
        {
            (10, 2, 3, "Voronoi_Large", 101),
            (11, 2, 3, "Voronoi_Default", 202),
            (12, 2, 4, "Voronoi_Wide", 303),
            (12, 3, 4, "Voronoi_Default", 404),
            (13, 3, 4, "Voronoi_Tall", 505),
            (13, 3, 5, "Voronoi_Round", 606),
            (14, 3, 5, "Voronoi_Default", 707),
            (14, 3, 5, "Voronoi_Busy", 808),
            (15, 3, 6, "Voronoi_Tall", 909),
            (15, 3, 6, "Voronoi_Busy", 1010),
        };

        [MenuItem("Tools/TowerLand/Build Level List")]
        public static void BuildLevelList()
        {
            LevelList list = CreateLevels();
            BakeThumbnails(list);
            Debug.Log($"[TowerLand] Level list built: {list.Count} levels with thumbnails.");
        }

        [MenuItem("Tools/TowerLand/Build Debug Scene")]
        public static void BuildDebugScene()
        {
            if (AssetDatabase.LoadAssetAtPath<LevelList>(LevelListPath) == null) CreateLevels();
            // New scene first: it unloads unreferenced assets, which would invalidate anything loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelListPath);
            LevelSession session = Session();
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DebugMenu.Background;
            camera.cullingMask = 0;   // UI only (overlay canvas)

            var menu = new GameObject("DebugMenu").AddComponent<DebugMenu>();
            Set(menu, "_titleFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
            var levels = menu.gameObject.AddComponent<LevelsCategory>();
            Set(levels, "_levels", list);
            Set(levels, "_session", session);
            var info = menu.gameObject.AddComponent<InfoCategory>();
            var order = new SerializedObject(info);
            order.FindProperty("_order").intValue = 10;
            order.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, DebugScenePath);
            ProjectSetup.ApplyBuildScenes();
            WireGameScene();
            Debug.Log("[TowerLand] Debug scene built.");
        }

        /// <summary>Game scene: the bootstrap plays the level picked in the session; the secret corner opens the debug scene.</summary>
        public static void WireGameScene()
        {
            EditorSceneManager.OpenScene(ProjectSetup.GameScenePath, OpenSceneMode.Single);
            Set(Object.FindFirstObjectByType<LevelBootstrap>(), "_session", Session());
            AddDebugCorner();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>Forget this machine's debugger unlock, to try the 5-tap + password flow again.</summary>
        [MenuItem("Tools/TowerLand/Reset Debugger Unlock")]
        public static void ResetDebuggerUnlock()
        {
            Core.PlayerProgress.Local().IsDebugger = false;
            Debug.Log("[TowerLand] Debugger unlock reset: 5 taps on the top-right corner again.");
        }

        /// <summary>
        /// The open Game scene's secret debug hotspot (top-right of the HUD) - plus what UI taps need: an EventSystem
        /// (Input System module) and a raycaster on the HUD. The shooter ignores presses that land on UI.
        /// </summary>
        public static void AddDebugCorner()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null) return;
            if (hud.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                hud.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            foreach (Transform child in hud.transform)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);   // the old long-press shortcut
            Transform existing = hud.transform.Find("DebugCorner");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var corner = new GameObject("DebugCorner", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(DebugCorner));
            corner.transform.SetParent(hud.transform, false);
            corner.GetComponent<UnityEngine.UI.Image>().color = Color.clear;   // invisible in edit mode too
            var rect = (RectTransform)corner.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;      // runtime refines it to the safe area
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(200f, 200f);
            Set(corner.GetComponent<DebugCorner>(), "_titleFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
        }

        public static LevelSession Session()
        {
            var session = AssetDatabase.LoadAssetAtPath<LevelSession>(SessionPath);
            if (session != null) return session;
            session = ScriptableObject.CreateInstance<LevelSession>();
            AssetDatabase.CreateAsset(session, SessionPath);
            return session;
        }

        // ---------------------------------------------------------------- levels
        private static LevelList CreateLevels()
        {
            foreach (Pattern pattern in Patterns)
            {
                string path = $"{PatternsDir}/{pattern.Name}.asset";
                var generator = AssetDatabase.LoadAssetAtPath<VoronoiPatternGenerator>(path);
                if (generator == null)
                {
                    generator = ScriptableObject.CreateInstance<VoronoiPatternGenerator>();
                    AssetDatabase.CreateAsset(generator, path);
                }
                var so = new SerializedObject(generator);
                so.FindProperty("_settings.RegionCount").intValue = pattern.Regions;
                so.FindProperty("_settings.Metric").enumValueIndex = (int)pattern.Metric;
                so.FindProperty("_settings.VerticalStretch").floatValue = pattern.Stretch;
                so.FindProperty("_settings.AvoidSameColourNeighbours").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var levels = new LevelConfig[Levels.Length];
            for (int i = 0; i < Levels.Length; i++)
            {
                (int floors, int layers, int colours, string pattern, int seed) = Levels[i];
                string path = $"{LevelsDir}/Level_{i + 1:00}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelConfig>();
                    AssetDatabase.CreateAsset(level, path);
                }
                var so = new SerializedObject(level);
                so.FindProperty("_columns").intValue = Columns;
                so.FindProperty("_floors").intValue = floors;
                so.FindProperty("_layers").intValue = layers;
                so.FindProperty("_colourCount").intValue = colours;
                so.FindProperty("_seed").intValue = seed;
                so.FindProperty("_pattern").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TowerPatternGenerator>($"{PatternsDir}/{pattern}.asset");
                so.ApplyModifiedPropertiesWithoutUndo();
                levels[i] = level;
            }

            var list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<LevelList>();
                AssetDatabase.CreateAsset(list, LevelListPath);
            }
            var listSo = new SerializedObject(list);
            SerializedProperty array = listSo.FindProperty("_levels");
            array.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            listSo.ApplyModifiedPropertiesWithoutUndo();
            Session();
            AssetDatabase.SaveAssets();
            return list;
        }

        // ---------------------------------------------------------------- thumbnails
        /// <summary>Builds each level's tower (its own seed) in the Game scene and renders the game camera, cropped to the tower.</summary>
        private static void BakeThumbnails(LevelList list)
        {
            if (EditorSceneManager.GetActiveScene().path != ProjectSetup.GameScenePath)
                EditorSceneManager.OpenScene(ProjectSetup.GameScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(ThumbnailsDir);
            var tower = Object.FindFirstObjectByType<TowerView>();
            Camera camera = Camera.main;
            // Warm-up: the first render after building a tower can miss it (materials / shaders still being set up).
            tower.Build(list.Levels[0].GenerateLayers(list.Levels[0].Seed));
            Object.DestroyImmediate(Render(camera));

            for (int i = 0; i < list.Count; i++)
            {
                LevelConfig level = list.Levels[i];
                tower.Build(level.GenerateLayers(level.Seed));
                string path = $"{ThumbnailsDir}/{level.name}.png";
                File.WriteAllBytes(path, Render(camera).EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;   // shown well below its size in the list
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
                Set(level, "_thumbnail", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            tower.Clear();
            AssetDatabase.SaveAssets();
        }

        private static Texture2D Render(Camera camera)
        {
            var rt = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            int top = Mathf.RoundToInt(RenderHeight * CropTop), bottom = Mathf.RoundToInt(RenderHeight * CropBottom);
            var texture = new Texture2D(RenderWidth, bottom - top, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, RenderHeight - bottom, RenderWidth, bottom - top), 0, 0);   // pixel rows count from the bottom
            texture.Apply();
            RenderTexture.active = null;
            camera.targetTexture = previous;
            rt.Release();
            Object.DestroyImmediate(rt);
            return texture;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
