using System;
using System.IO;
using TowerLand.Gameplay;
using TowerLand.Levels;
using TowerLand.Levels.Generation;
using TowerLand.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TowerLand.Editor
{
    /// <summary>
    /// Creates (or refreshes) the core prototype assets and the Game scene layout.
    /// Menu: Tools/TowerLand/Build Core Assets. Batch: -executeMethod TowerLand.Editor.CoreAssetsBuilder.BuildBatch
    /// Existing assets are updated in place so their GUIDs (and scene references) survive re-runs.
    /// </summary>
    public static class CoreAssetsBuilder
    {
        private const string MeshPath = "Assets/TowerLand/Art/Meshes/BeveledCube.asset";
        internal const string CubeMaterialPath = "Assets/TowerLand/Art/Materials/Cube.mat";
        private const string GroundMaterialPath = "Assets/TowerLand/Art/Materials/Ground.mat";
        private const string PlinthMaterialPath = "Assets/TowerLand/Art/Materials/Plinth.mat";
        private const string PoleMaterialPath = "Assets/TowerLand/Art/Materials/Pole.mat";
        private const string PoleCapMaterialPath = "Assets/TowerLand/Art/Materials/PoleCap.mat";
        private const string PoleSegmentPath = "Assets/TowerLand/Art/Models/Pole/Pole_Segment.fbx";   // ArtSource/Blender/Pole.blend
        private const string PoleCapPath = "Assets/TowerLand/Art/Models/Pole/Pole_Cap.fbx";
        internal const string PalettePath = "Assets/TowerLand/Data/CubePalette.asset";
        internal const string VisualSettingsPath = "Assets/TowerLand/Data/TowerVisualSettings.asset";
        private const string VoronoiPath = "Assets/TowerLand/Levels/Patterns/Voronoi_Default.asset";
        private const string LevelPath = "Assets/TowerLand/Levels/Level_Prototype.asset";

        private const float CubeBevel = 0.15f;
        private const int CubeBevelSegments = 4;
        private const float CubePillow = 0.12f;
        internal const float PlinthHeight = 1.2f;
        // How far the tower (and the environment laid out around it) sits behind the world origin, away from the camera.
        internal const float TowerDepth = 3f;

        [MenuItem("Tools/TowerLand/Rebuild Beveled Cube Mesh")]
        public static Mesh BuildCubeMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                Mesh created = BeveledCubeMesh.Create(1f, CubeBevel, CubeBevelSegments, CubePillow);
                EnsureFolder(MeshPath);
                AssetDatabase.CreateAsset(created, MeshPath);
                return created;
            }

            // Refill in place through the Mesh API (CopySerialized leaves a stale GPU copy when the vertex count changes).
            BeveledCubeMesh.Fill(existing, 1f, CubeBevel, CubeBevelSegments, CubePillow);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        [MenuItem("Tools/TowerLand/Build Core Assets")]
        public static void Build()
        {
            // New scene first: NewScene unloads unreferenced assets, which would invalidate anything loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Mesh cubeMesh = BuildCubeMesh();
            Material cubeMaterial = CandyCubeMaterial();
            Material groundMaterial = LitMaterial(GroundMaterialPath, new Color(0.36f, 0.72f, 0.22f), smoothness: 0.1f);
            Material plinthMaterial = LitMaterial(PlinthMaterialPath, new Color(0.93f, 0.90f, 0.82f), smoothness: 0.25f);

            var palette = LoadOrCreate<CubePalette>(PalettePath);
            var visuals = LoadOrCreate<TowerVisualSettings>(VisualSettingsPath);
            var voronoi = LoadOrCreate<VoronoiPatternGenerator>(VoronoiPath);
            var level = LoadOrCreate<LevelConfig>(LevelPath);
            SetObjectField(level, "_pattern", voronoi);

            BuildScene(cubeMesh, cubeMaterial, groundMaterial, plinthMaterial, palette, visuals, level);
            EnvironmentBuilder.ApplyToOpenScene();
            WinSequenceBuilder.AddToOpenScene();
            AddReferenceOverlay();
            AddVersionLabel();
            DebugSceneBuilder.AddDebugCorner();
            EditorSceneManager.SaveScene(scene, ProjectSetup.GameScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TowerLand] Core assets and Game scene built.");
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static void BuildScene(Mesh cubeMesh, Material cubeMaterial, Material groundMaterial, Material plinthMaterial,
            CubePalette palette, TowerVisualSettings visuals, LevelConfig level)
        {
            float towerRadius = visuals.RadiusFor(level.Columns);
            float towerHeight = level.Floors * visuals.CubeSpacing;

            // Matched to the gameplay mockup (Art/Reference/Mockup_Gameplay.png, 9:16): low, nearly level camera so the
            // horizon sits ~38% from the top; tower top ~10%, plinth base ~69%. Lower third is free for the shooter.
            var camera = Camera.main;
            camera.fieldOfView = 45f;
            camera.farClipPlane = 300f;
            camera.transform.position = new Vector3(0.4f, 9.0f, -35.5f);
            camera.transform.LookAt(new Vector3(0.4f, 4.7f, 0f));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.85f, 0.95f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 140f;

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = "Plinth";
            float plinthDiameter = (towerRadius + 0.2f) * 2f;
            plinth.transform.localScale = new Vector3(plinthDiameter, PlinthHeight * 0.5f, plinthDiameter);
            plinth.transform.position = new Vector3(0f, PlinthHeight * 0.5f, TowerDepth);
            plinth.GetComponent<Renderer>().sharedMaterial = plinthMaterial;
            Object.DestroyImmediate(plinth.GetComponent<Collider>());

            var towerObject = new GameObject("Tower");
            towerObject.transform.position = new Vector3(0f, PlinthHeight, TowerDepth);
            var tower = towerObject.AddComponent<TowerView>();
            SetObjectField(tower, "_cubeMesh", cubeMesh);
            SetObjectField(tower, "_cubeMaterial", cubeMaterial);
            SetObjectField(tower, "_palette", palette);
            SetObjectField(tower, "_settings", visuals);
            SetPole(tower);

            var bootstrap = new GameObject("LevelBootstrap").AddComponent<LevelBootstrap>();
            SetObjectField(bootstrap, "_level", level);
            SetObjectField(bootstrap, "_tower", tower);
            SetObjectField(bootstrap, "_session", DebugSceneBuilder.Session());
            SetObjectField(bootstrap, "_levels", AssetDatabase.LoadAssetAtPath<LevelList>(DebugSceneBuilder.LevelListPath));

            Feel.ImpactEffects effects = CreateImpactEffects();
            SetObjectField(tower, "_effects", effects);
            SetObjectField(tower, "_debris", towerObject.AddComponent<Feel.DebrisSweeper>());

            var shooter = new GameObject("Shooter").AddComponent<Shooter>();
            SetObjectField(shooter, "_tower", tower);
            SetObjectField(shooter, "_settings", LoadOrCreate<ShooterSettings>(ShooterSettingsPath));
            SetObjectField(shooter, "_lineMaterial", ShaderMaterial(AimLineMaterialPath, "TowerLand/Aim Line"));
        }

        private const string ShooterSettingsPath = "Assets/TowerLand/Data/ShooterSettings.asset";
        private const string AimLineMaterialPath = "Assets/TowerLand/Art/Materials/AimLine.mat";

        /// <summary>Hit / break sparkles (shared particle materials) in the open scene.</summary>
        internal static Feel.ImpactEffects CreateImpactEffects()
        {
            var effects = new GameObject("ImpactEffects").AddComponent<Feel.ImpactEffects>();
            SetObjectField(effects, "_glow", ParticleMaterial("Assets/TowerLand/Art/Materials/FX_Glow.mat", "_SHAPE_GLOW", 0f, 2.2f));
            SetObjectField(effects, "_star", ParticleMaterial("Assets/TowerLand/Art/Materials/FX_Star.mat", "_SHAPE_STAR", 1f, 3f));
            SetObjectField(effects, "_ring", ParticleMaterial("Assets/TowerLand/Art/Materials/FX_Ring.mat", "_SHAPE_RING", 2f, 2f));
            return effects;
        }

        private static Material ParticleMaterial(string path, string keyword, float shape, float intensity)
        {
            bool isNew = AssetDatabase.LoadAssetAtPath<Material>(path) == null;
            Material material = ShaderMaterial(path, "TowerLand/Particle FX");
            foreach (string k in new[] { "_SHAPE_GLOW", "_SHAPE_STAR", "_SHAPE_RING" }) material.DisableKeyword(k);
            material.EnableKeyword(keyword);
            material.SetFloat("_Shape", shape);
            if (isNew) material.SetFloat("_Intensity", intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Material on <paramref name="shader"/>, created if missing (tuned values are kept).</summary>
        private static Material ShaderMaterial(string path, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                EnsureFolder(path);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        /// <summary>
        /// Batch helper: opens the Game scene, builds the level for a few seeds and renders each to a PNG.
        /// Args: -captureDir &lt;folder&gt; [-captureSeeds 1,2,3]
        /// </summary>
        public static void CapturePreviewsBatch()
        {
            EditorSceneManager.OpenScene(ProjectSetup.GameScenePath);
            CapturePreviews(GetArg("-captureDir") ?? "Temp/Previews", GetArg("-captureSeeds") ?? "1,2,3");
            EditorApplication.Exit(0);
        }

        /// <summary>Renders the open Game scene's tower for each seed (comma-separated) plus a cube close-up.</summary>
        public static void CapturePreviews(string dir, string seeds)
        {
            Directory.CreateDirectory(dir);
            var bootstrap = Object.FindFirstObjectByType<LevelBootstrap>();
            var camera = Camera.main;

            foreach (string token in seeds.Split(','))
            {
                int seed = int.Parse(token.Trim());
                bootstrap.Generate(seed);
                string path = Path.Combine(dir, $"tower_seed{seed}.png");
                RenderToPng(camera, 720, 1560, path); // 9:19.5 phone
                Debug.Log($"[TowerLand] Captured {path}");
            }

            RenderCubeCloseUp(Path.Combine(dir, "cube_closeup.png"));
        }

        private static void RenderCubeCloseUp(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(CubeMaterialPath));
            material.SetColor("_BaseColor", new Color(0.10f, 0.52f, 1.00f));

            var cube = new GameObject("CloseUpCube");
            cube.transform.position = new Vector3(0f, 40f, 0f);
            cube.transform.rotation = Quaternion.Euler(-20f, 35f, 0f);
            cube.AddComponent<MeshFilter>().sharedMesh = mesh;
            cube.AddComponent<MeshRenderer>().sharedMaterial = material;

            var cameraObject = new GameObject("CloseUpCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 40.4f, -2.6f);
            camera.transform.LookAt(cube.transform.position);
            camera.fieldOfView = 35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.78f, 0.95f);

            RenderToPng(camera, 800, 800, path);
            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(material);
        }

        /// <summary>Renders the open scene's main camera (current tower state) to a PNG, e.g. for mockup comparisons.</summary>
        public static void RenderMainCamera(string path, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            RenderToPng(Camera.main, width, height, path);
        }

        private static void RenderToPng(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = previous;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        private const string MockupPath = "Assets/TowerLand/Art/Reference/Mockup_Gameplay.png";

        /// <summary>Mockup thumbnail in the Game view corner (EditorOnly: stripped from builds).</summary>
        private static void AddReferenceOverlay()
        {
            var importer = AssetImporter.GetAtPath(MockupPath) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var overlay = new GameObject("ReferenceOverlay") { tag = "EditorOnly" };
            var component = overlay.AddComponent<UI.ReferenceOverlay>();
            SetObjectField(component, "_reference", AssetDatabase.LoadAssetAtPath<Texture2D>(MockupPath));
        }

        /// <summary>HUD canvas (portrait 1080x1920 reference) with the top-left "Tower Land - version" label.</summary>
        public static void AddVersionLabel()
        {
            var hud = GameObject.Find("HUD");
            if (hud == null)
            {
                hud = new GameObject("HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            Transform existing = hud.transform.Find("VersionLabel");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var label = new GameObject("VersionLabel", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Shadow));
            label.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -20f);
            rect.sizeDelta = new Vector2(600f, 40f);
            var text = label.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(1f, 1f, 1f, 0.75f);
            text.raycastTarget = false;
            text.text = "Tower Land - " + PlayerSettings.bundleVersion;
            var shadow = label.GetComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(2f, -2f);
            label.AddComponent<UI.VersionLabel>();
        }

        /// <summary>Cube base material on the candy shader. Only the shader is enforced, so tuned values survive rebuilds.</summary>
        private static Material CandyCubeMaterial()
        {
            Shader shader = Shader.Find("TowerLand/Candy Cube");
            var material = AssetDatabase.LoadAssetAtPath<Material>(CubeMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                EnsureFolder(CubeMaterialPath);
                AssetDatabase.CreateAsset(material, CubeMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LitMaterial(string path, Color colour, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                EnsureFolder(path);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            EnsureFolder(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// Central pole the innermost layer hangs on (seen through holes in the tower): the cream white of the TowerPlinth
        /// wall (its World1_Palette swatch), so pole and base read as one piece.
        /// </summary>
        private static Material PoleMaterial() => LitMaterial(PoleMaterialPath, new Color(0.890f, 0.851f, 0.776f), smoothness: 0.2f);

        /// <summary>Golden collar + ball on top of the pole.</summary>
        private static Material PoleCapMaterial() => LitMaterial(PoleCapMaterialPath, new Color(1f, 0.72f, 0.18f), smoothness: 0.7f);

        /// <summary>Twisted pole segments + cap (models from ArtSource/Blender/Pole.blend) and their materials.</summary>
        private static void SetPole(TowerView tower)
        {
            SetObjectField(tower, "_poleMaterial", PoleMaterial());
            SetObjectField(tower, "_poleCapMaterial", PoleCapMaterial());
            SetObjectField(tower, "_poleSegment", PoleMesh(PoleSegmentPath));
            SetObjectField(tower, "_poleCap", PoleMesh(PoleCapPath));
        }

        private static Mesh PoleMesh(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogWarning($"[TowerLand] {path} not found (export it from ArtSource/Blender/Pole.blend); plain pole used.");
                return null;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;   // Blender Z-up -> mesh Y-up, no root rotation
            importer.importNormals = ModelImporterNormals.Import;   // analytic normals: segments tile without seams
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>Gives the open scene's tower its pole (Build Core Assets does this for a fresh scene).</summary>
        [MenuItem("Tools/TowerLand/Apply Pole")]
        public static void ApplyPoleToOpenScene()
        {
            var tower = Object.FindFirstObjectByType<TowerView>();
            SetPole(tower);
            EditorSceneManager.MarkSceneDirty(tower.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static void SetObjectField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field)
                ?? throw new ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;
            EnsureFolder(folder);
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
