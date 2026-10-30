using System.IO;
using TowerLand.Feel;
using TowerLand.Gameplay;
using TMPro;
using TowerLand.Tower;
using TowerLand.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TowerLand.Editor
{
    /// <summary>
    /// Win-sequence assets and scene wiring: imports the vacuum models exported from <c>ArtSource/Blender/Vacuum.blend</c>
    /// (nozzle + joint ring), creates the frosted glass / cuff materials and the tuning asset, and adds the
    /// <see cref="WinSequence"/> to the open scene. Menu: Tools/TowerLand/Setup Win Sequence (Build Core Assets runs it).
    /// </summary>
    public static class WinSequenceBuilder
    {
        private const string ModelFolder = "Assets/TowerLand/Art/Models/Vacuum";
        internal const string NozzlePath = ModelFolder + "/Vacuum_Nozzle.fbx";
        internal const string RingPath = ModelFolder + "/Vacuum_JointRing.fbx";
        private const string GlassPath = "Assets/TowerLand/Art/Materials/VacuumGlass.mat";
        private const string CuffPath = "Assets/TowerLand/Art/Materials/VacuumCuff.mat";
        private const string StreakPath = "Assets/TowerLand/Art/Materials/FX_Glow.mat";
        internal const string SettingsPath = "Assets/TowerLand/Data/VacuumSettings.asset";
        private static readonly Color CuffColour = new Color(1f, 0.76f, 0.16f);

        // Title text (win screen): Lilita One (SIL OFL, Assets/ThirdParty/Fonts/LilitaOne) as a TextMeshPro SDF font.
        private const string TitleFontPath = "Assets/ThirdParty/Fonts/LilitaOne/LilitaOne-Regular.ttf";
        private const string UiFolder = "Assets/TowerLand/Art/UI";
        private const string TitleFontAssetPath = UiFolder + "/LilitaOne SDF.asset";
        private const string TitleStylePath = "Assets/TowerLand/Data/TitleStyle.asset";
        private const string TitleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!?.,:;'\"-+%/()# ";
        private static readonly Color TitleOutline = new Color(0.16f, 0.05f, 0.24f);

        [MenuItem("Tools/TowerLand/Setup Win Sequence")]
        public static void SetupFromMenu()
        {
            AddToOpenScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void AddToOpenScene()
        {
            AssetDatabase.Refresh();
            Material glass = Glass();
            Material cuff = Cuff();
            ConfigureModel(NozzlePath, glass, cuff);
            ConfigureModel(RingPath, glass, cuff);
            var settings = AssetDatabase.LoadAssetAtPath<VacuumSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<VacuumSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            GameObject existing = GameObject.Find("WinSequence");
            if (existing != null) Object.DestroyImmediate(existing);
            var sequence = new GameObject("WinSequence").AddComponent<WinSequence>();
            var so = new SerializedObject(sequence);
            so.FindProperty("_tower").objectReferenceValue = Object.FindFirstObjectByType<TowerView>();
            so.FindProperty("_shooter").objectReferenceValue = Object.FindFirstObjectByType<Shooter>();
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_glass").objectReferenceValue = glass;
            so.FindProperty("_nozzle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(NozzlePath);
            so.FindProperty("_jointRing").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(RingPath);
            so.FindProperty("_streak").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(StreakPath);
            so.FindProperty("_titleStyle").objectReferenceValue = TitleStyleAsset();
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Title style asset with its generated font asset and materials; tuned values survive rebuilds.</summary>
        internal static TitleStyle TitleStyleAsset()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
                AssetDatabase.CreateFolder("Assets/TowerLand/Art", "UI");
            TMP_FontAsset font = TitleFont();
            var style = AssetDatabase.LoadAssetAtPath<TitleStyle>(TitleStylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<TitleStyle>();
                AssetDatabase.CreateAsset(style, TitleStylePath);
            }
            var so = new SerializedObject(style);
            so.FindProperty("_font").objectReferenceValue = font;
            so.FindProperty("_faceMaterial").objectReferenceValue = TitleMaterial("Title_Face", font, true, false);
            so.FindProperty("_extrudeMaterial").objectReferenceValue = TitleMaterial("Title_Extrude", font, false, false);
            so.FindProperty("_silhouetteMaterial").objectReferenceValue = TitleMaterial("Title_Silhouette", font, true, false);
            so.FindProperty("_shadowMaterial").objectReferenceValue = TitleMaterial("Title_ExtrudeShadow", font, true, true);
            so.FindProperty("_outlineColour").colorValue = TitleOutline;
            so.ApplyModifiedPropertiesWithoutUndo();
            return style;
        }

        private static TMP_FontAsset TitleFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
            if (font != null) return font;
            var source = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            // Generous padding: the title's outline is thick.
            font = TMP_FontAsset.CreateFontAsset(source, 90, 14, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "LilitaOne SDF";
            AssetDatabase.CreateAsset(font, TitleFontAssetPath);
            font.atlasTexture.name = "LilitaOne SDF Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            font.material.name = "LilitaOne SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.TryAddCharacters(TitleCharacters);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>Title layer material: thick even outline (or none, for the coloured extrusion), optionally a soft shadow below.</summary>
        private static Material TitleMaterial(string name, TMP_FontAsset font, bool outline, bool shadow)
        {
            string path = $"{UiFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = name };
                material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                material.SetFloat("_FaceDilate", 0.18f);
                material.SetColor("_OutlineColor", TitleOutline);
                material.SetFloat("_OutlineWidth", outline ? 0.12f : 0f);
                if (outline) material.EnableKeyword("OUTLINE_ON");
                else material.DisableKeyword("OUTLINE_ON");
                if (shadow)
                {
                    material.EnableKeyword("UNDERLAY_ON");
                    material.SetColor("_UnderlayColor", new Color(0.05f, 0.02f, 0.12f, 0.5f));
                    material.SetFloat("_UnderlayOffsetX", 0f);
                    material.SetFloat("_UnderlayOffsetY", -0.8f);
                    material.SetFloat("_UnderlayDilate", 0.35f);
                    material.SetFloat("_UnderlaySoftness", 0.55f);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_MainTex", font.atlasTexture);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Frosted glass; tuned values survive rebuilds.</summary>
        internal static Material Glass()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (material == null)
            {
                material = new Material(Shader.Find("TowerLand/Frosted Glass"));
                AssetDatabase.CreateAsset(material, GlassPath);
            }
            return material;
        }

        /// <summary>Glossy candy-yellow accents (nozzle lip and bands, hose joint rings); tuned values survive rebuilds.</summary>
        internal static Material Cuff()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CuffPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", CuffColour);
                material.SetFloat("_Smoothness", 0.85f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, CuffPath);
            }
            return material;
        }

        internal static void ConfigureModel(string path, Material glass, Material cuff)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TowerLand] {path} not found (export it from ArtSource/Blender/Vacuum.blend).");
                return;
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;   // root without rotation: the nozzle's mouth faces -Y
            importer.importNormals = ModelImporterNormals.Import;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "VacuumGlass"), glass);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "VacuumCuff"), cuff);
            importer.SaveAndReimport();
        }
    }
}
