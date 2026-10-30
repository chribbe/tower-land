using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TowerLand.Gameplay;
using TowerLand.Levels;
using TowerLand.Tower;

namespace TowerLand.Editor
{
    /// <summary>
    /// Imports the World 1 environment exported from Blender (ArtSource/Blender/World1_Environment.blend) and dresses
    /// the open Game scene with it: palette material, gradient sky, ambient, fog.
    /// Menu: Tools/TowerLand/Setup World1 Environment.
    /// Each world has two variants: <see cref="Variant.Ground"/> for gameplay and <see cref="Variant.Sky"/> for the build
    /// (meta) scene - the same meadow as a floating island above a sea of clouds (ArtSource/Blender/World1_Sky.blend).
    /// </summary>
    public static class EnvironmentBuilder
    {
        public enum Variant { Ground, Sky }

        private const string ModelFolder = "Assets/TowerLand/Art/Models/World1";
        private const string EnvironmentModelPath = ModelFolder + "/World1_Environment.fbx";
        private const string SkyModelPath = ModelFolder + "/World1_Sky_Environment.fbx";
        private const string PropsModelPath = ModelFolder + "/World1_Props.fbx";
        private const string PaletteTexturePath = "Assets/TowerLand/Art/Textures/World1_Palette.png";
        private const string PaletteMaterialPath = "Assets/TowerLand/Art/Materials/World1_Palette.mat";
        private const string SkyShaderName = "TowerLand/Sky Gradient";
        private const string SkyMaterialPath = "Assets/TowerLand/Art/Materials/World1_Sky.mat";
        private const string PaletteMaterialNameInFbx = "World1_Palette";
        private const string EnvironmentObjectName = "Environment";

        // Look of World 1 (sRGB), tuned against the mockup.
        private static readonly Color SkyTop = new Color(0.04f, 0.62f, 0.99f);
        private static readonly Color SkyMid = new Color(0.17f, 0.71f, 0.99f);
        private static readonly Color SkyHorizon = new Color(0.66f, 0.83f, 0.96f);
        private static readonly Color FogColour = new Color(0.66f, 0.83f, 0.96f);
        private static readonly Color AmbientSky = new Color(0.46f, 0.58f, 0.74f);
        private static readonly Color AmbientEquator = new Color(0.46f, 0.52f, 0.46f);
        private static readonly Color AmbientGround = new Color(0.26f, 0.32f, 0.18f);

        public static bool IsAvailable => File.Exists(EnvironmentModelPath);

        [MenuItem("Tools/TowerLand/Setup World1 Environment")]
        public static void SetupFromMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>Configures imports and replaces the placeholder ground/plinth in the open scene. Idempotent.</summary>
        public static void ApplyToOpenScene()
        {
            // The Blender layout is centred on the tower: follow the Tower object so plinth and surrounding props stay with it.
            var tower = Object.FindFirstObjectByType<TowerView>();
            Vector3 towerGround = tower == null ? Vector3.zero : Vector3.Scale(tower.transform.position, new Vector3(1f, 0f, 1f));
            ApplyToOpenScene(towerGround, TowerFaceRadius());
        }

        /// <summary>
        /// Same, centred on <paramref name="centre"/> (ground level) with the plinth fitted to a tower whose outer cube faces
        /// are at <paramref name="faceRadius"/> (&lt;= 0: the plinth keeps its Blender size).
        /// </summary>
        public static void ApplyToOpenScene(Vector3 centre, float faceRadius, Variant variant = Variant.Ground)
        {
            bool floating = variant == Variant.Sky;
            string modelPath = floating ? SkyModelPath : EnvironmentModelPath;
            if (!File.Exists(modelPath))
            {
                Debug.LogWarning($"[TowerLand] {modelPath} not found; keeping placeholder ground.");
                return;
            }

            // Pick up files re-exported from Blender while the editor sat in the background.
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(PaletteTexturePath, ImportAssetOptions.ForceUpdate);
            Material palette = ConfigurePaletteMaterial();
            ConfigureModel(EnvironmentModelPath, palette);
            ConfigureModel(PropsModelPath, palette);
            if (floating) ConfigureModel(SkyModelPath, palette);
            Material sky = ConfigureSkyMaterial();
            Material ground = ConfigureGroundMaterial();
            Material cloud = SimpleMaterial(CloudMaterialPath, "TowerLand/Soft Cloud");
            Material cloudSea = CloudSeaMaterial();

            foreach (string placeholder in new[] { "Ground", "Plinth", EnvironmentObjectName })
            {
                GameObject existing = GameObject.Find(placeholder);
                if (existing != null) Object.DestroyImmediate(existing);
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(model);
            environment.name = EnvironmentObjectName;
            // Blender's baked-space FBX export lands the scene turned 180° (Blender +Y becomes Unity -Z); undo that so
            // the Blender "behind the tower" (+Y) is Unity +Z, away from the game camera.
            environment.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, 180f, 0f));
            foreach (Transform child in environment.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                if (child.TryGetComponent(out MeshRenderer renderer))
                {
                    // Blender's FBX exporter can drop material slots on instanced meshes; everything uses the palette.
                    renderer.sharedMaterial = child.name == "Ground" ? ground : child.name == "Clouds_Sea" ? cloudSea
                        : child.name.StartsWith("Clouds") ? cloud : palette;
                    // Sky and far background don't need to cast shadows; foreground props do.
                    bool far = child.name.StartsWith("Mountains") || child.name.StartsWith("Clouds") || child.name == "ForestBand"
                        || child.name.StartsWith("SkyPeaks") || child.name.StartsWith("Islet_") || child.name == "IslandCliff";
                    renderer.shadowCastingMode = far || child.name == "Ground" || child.name == "Lake" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    // Surfaces broken cube chunks land on (static, invisible; the tower itself has no colliders).
                    if (child.name == "Ground" || child.name == "TowerPlinth" || child.name.StartsWith("Rock_"))
                        child.gameObject.AddComponent<MeshCollider>();
                }
            }
            FitPlinth(environment.transform.Find("TowerPlinth"), faceRadius);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColour;
            // Atmospheric haze like the mockup: starts just behind the tower, distant ranges melt into the horizon.
            // Up in the sky the view reaches much further (islets, peaks above the clouds), so the haze is thinner.
            RenderSettings.fogStartDistance = floating ? SkyFogStart : 45f;
            RenderSettings.fogEndDistance = floating ? SkyFogEnd : 420f;

            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                // Key light from the upper-right front, like the mockup: glints top-right, shadows fall left/back.
                sun.transform.rotation = Quaternion.Euler(52f, -50f, 0f);
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.55f;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.farClipPlane = floating ? 2500f : 1000f;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            SetupPostProcessing();
            // Ground: the painted far background replaces the 3D mountains + clouds. Sky: islets, cloud sea and peaks stay 3D.
            BackdropBaker.Bake(!floating);

            Debug.Log("[TowerLand] World 1 environment applied.");
        }

        /// <summary>Outer cube face radius of the open scene's tower (its default level), or 0 when there is none.</summary>
        public static float TowerFaceRadius()
        {
            var tower = Object.FindFirstObjectByType<TowerView>();
            var bootstrap = Object.FindFirstObjectByType<LevelBootstrap>();
            var level = bootstrap == null ? null : new SerializedObject(bootstrap).FindProperty("_level").objectReferenceValue as LevelConfig;
            if (tower == null || tower.Settings == null || level == null) return 0f;
            return FaceRadius(tower.Settings, level.Columns);
        }

        private const float SkyFogStart = 120f, SkyFogEnd = 1600f;

        public static float FaceRadius(TowerVisualSettings settings, int columns) => settings.RadiusFor(columns) + 0.5f * settings.CubeScale;

        /// <summary>
        /// Shrinks the plinth (radially only) to just inside the tower's outer cube faces, so the overhanging bottom
        /// row casts its shadow onto the base. The mesh keeps its height.
        /// </summary>
        private static void FitPlinth(Transform plinth, float faceRadius)
        {
            TowerVisualSettings settings = AssetDatabase.LoadAssetAtPath<TowerVisualSettings>(VisualSettingsPath);
            if (plinth == null || faceRadius <= 0f || settings == null)
            {
                Debug.LogWarning("[TowerLand] TowerPlinth, tower size or visual settings not found; plinth keeps its Blender size.");
                return;
            }

            float meshRadius = plinth.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x;
            float scale = (faceRadius - settings.PlinthInset) / meshRadius;
            plinth.localScale = new Vector3(scale, 1f, scale);
        }

        private const string VisualSettingsPath = "Assets/TowerLand/Data/TowerVisualSettings.asset";

        private const string PostProfilePath = "Assets/TowerLand/Art/World1_PostProcessing.asset";

        /// <summary>Light, mobile-friendly polish matching the mockup: soft bloom on highlights, a touch more saturation.</summary>
        private static void SetupPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));   // warm glow

            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(8f);               // warmer, sunnier overall treatment
            whiteBalance.tint.Override(2f);
            bloom.highQualityFiltering.Override(false);

            ColorAdjustments colour = GetOrAdd<ColorAdjustments>(profile);
            colour.saturation.Override(6f);
            colour.contrast.Override(6f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);

            // Focus falloff: tower (~36 m) and foreground sharp, scenery behind blurs progressively with distance.
            // Bokeh mode: Gaussian is cheaper but its max radius (1.5) is too weak to read on phones.
            // Cost: full-screen bokeh pass - profile on low-end Android; fall back to Gaussian on low quality tier.
            DepthOfField dof = GetOrAdd<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            // Focus between the waiting projectile (~21 m) and the tower front (~34 m): bokeh blurs both nearer and
            // further than the focus, so both stay crisp while scenery behind the tower softens.
            dof.focusDistance.Override(27.5f);
            dof.focalLength.Override(180f);
            dof.aperture.Override(2.0f);
            dof.bladeCount.Override(6);
            dof.gaussianStart.Override(40f);
            dof.gaussianEnd.Override(95f);
            dof.gaussianMaxRadius.Override(1.5f);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.None);   // keep the candy colours unshifted
            EditorUtility.SetDirty(profile);

            GameObject existing = GameObject.Find("PostProcessing");
            if (existing != null) Object.DestroyImmediate(existing);
            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        private static Material ConfigurePaletteMaterial()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(PaletteTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Point;   // swatches must not bleed into each other
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            // Soft "fake GI" shading (wrapped light, sky/ground bounce, baked vertex AO). Tuned values survive rebuilds.
            Shader shader = Shader.Find("TowerLand/Soft Environment");
            var material = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PaletteMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexturePath));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private const string CloudMaterialPath = "Assets/TowerLand/Art/Materials/World1_Clouds.mat";

        private const string CloudSeaMaterialPath = "Assets/TowerLand/Art/Materials/World1_CloudSea.mat";

        /// <summary>The sea of clouds below the floating island (sky variant): bluer sides than the sky clouds so the puffs read from afar.</summary>
        private static Material CloudSeaMaterial()
        {
            bool created = AssetDatabase.LoadAssetAtPath<Material>(CloudSeaMaterialPath) == null;
            Material material = SimpleMaterial(CloudSeaMaterialPath, "TowerLand/Soft Cloud");
            if (created)
            {
                material.SetColor("_BottomColor", new Color(0.56f, 0.74f, 0.96f));
                material.SetFloat("_Softness", 0.5f);
                material.SetFloat("_RimStrength", 0.3f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        /// <summary>Creates a material on <paramref name="shader"/> if missing; existing tuned values are kept.</summary>
        private static Material SimpleMaterial(string path, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private const string GroundDetailPath = "Assets/TowerLand/Art/Textures/Ground_Detail.png";
        private const string GroundMaterialPath = "Assets/TowerLand/Art/Materials/World1_Ground.mat";

        /// <summary>Meadow ground: palette colours + tiling grass detail (Tools/TextureGen/ground_detail.py). Tuned values survive rebuilds.</summary>
        private static Material ConfigureGroundMaterial()
        {
            AssetDatabase.ImportAsset(GroundDetailPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GroundDetailPath);
            importer.sRGBTexture = false;   // data, not colour
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("TowerLand/Meadow Ground"));
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexturePath));
            material.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundDetailPath));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ConfigureSkyMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(SkyShaderName));
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }

            material.SetColor("_TopColor", SkyTop);
            material.SetColor("_MidColor", SkyMid);
            material.SetColor("_HorizonColor", SkyHorizon);
            material.SetColor("_BottomColor", FogColour);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureModel(string path, Material palette)
        {
            if (!File.Exists(path)) return;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importNormals = ModelImporterNormals.Import;   // Blender normals: smooth foliage/ground, crisp chamfered rocks
            importer.importTangents = ModelImporterTangents.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), PaletteMaterialNameInFbx), palette);
            importer.SaveAndReimport();
        }
    }
}
