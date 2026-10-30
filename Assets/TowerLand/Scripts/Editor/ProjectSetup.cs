using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// One-shot project configuration (player settings, build scenes).
    /// Menu: Tools/TowerLand/Apply Project Settings, or batch: -executeMethod TowerLand.Editor.ProjectSetup.ApplyBatch
    /// Safe to re-run: every step is idempotent.
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "A Small Game";
        public const string ProductName = "Tower Land";
        public const string BundleId = "com.asg.towerland";
        public const string GameScenePath = "Assets/TowerLand/Scenes/Game.unity";
        public const string MobilePipelinePath = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Tools/TowerLand/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)28;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;

            ConfigureShadows();
            ConfigureLayers();
            MoveSampleScene();
            ApplyBuildScenes();

            AssetDatabase.SaveAssets();
            Debug.Log("[TowerLand] Project settings applied.");
        }

        /// <summary>Build scenes: the Game scene first (boots the game), then the debug and build (meta) scenes when they exist.</summary>
        public static void ApplyBuildScenes()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(GameScenePath, true) };
            if (File.Exists(DebugSceneBuilder.DebugScenePath))
                scenes.Add(new EditorBuildSettingsScene(DebugSceneBuilder.DebugScenePath, true));
            if (File.Exists(MetaSceneBuilder.BuildScenePath))
                scenes.Add(new EditorBuildSettingsScene(MetaSceneBuilder.BuildScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void ApplyBatch()
        {
            Apply();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Main-light shadows on the mobile pipeline: 2048 map + soft (5x5 tent) filtering. The tower base catches the
        /// overhanging cubes' shadow up close, where a 1024 hard shadow over 50 m reads as jagged ~10 cm stair steps.
        /// Serialized fields because several URP asset setters are internal.
        /// </summary>
        static void ConfigureShadows()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(MobilePipelinePath);
            if (asset == null)
            {
                Debug.LogWarning($"[TowerLand] {MobilePipelinePath} not found; shadow settings unchanged.");
                return;
            }

            var so = new SerializedObject(asset);
            so.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_SoftShadowQuality").intValue = (int)UnityEngine.Rendering.Universal.SoftShadowQuality.Medium;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>Physics layers for debris (see Feel.DebrisSweeper): user layers 8 and 9.</summary>
        static void ConfigureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue = TowerLand.Feel.DebrisSweeper.DebrisLayerName;
            layers.GetArrayElementAtIndex(9).stringValue = TowerLand.Feel.DebrisSweeper.EscapingLayerName;
            tagManager.ApplyModifiedProperties();
        }

        static void MoveSampleScene()
        {
            const string sample = "Assets/Scenes/SampleScene.unity";
            if (File.Exists(GameScenePath) || !File.Exists(sample))
                return;
            string error = AssetDatabase.MoveAsset(sample, GameScenePath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"[TowerLand] Could not move sample scene: {error}");
            else if (Directory.GetFileSystemEntries("Assets/Scenes").Length == 0)
                AssetDatabase.DeleteAsset("Assets/Scenes");
        }
    }
}
