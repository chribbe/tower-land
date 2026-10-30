using TowerLand.Gameplay;
using TowerLand.Levels;
using TowerLand.Meta;
using TowerLand.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// Meta game: the build scene (<c>Scenes/Build.unity</c>) the win sequence leads to, where the vacuum hose spits the
    /// level's cubes as bricks onto the player's construction. Creates the construction blueprints (sized against the
    /// levels: 1 brick per tower cube), the brick mesh and tuning asset, lays out the World 1 environment around the
    /// platform (same framing as the Game scene) and registers the scene. Menu: Tools/TowerLand/Build Meta Scene.
    /// </summary>
    public static class MetaSceneBuilder
    {
        public const string BuildScenePath = "Assets/TowerLand/Scenes/Build.unity";
        private const string ConstructionsDir = "Assets/TowerLand/Data/Constructions";
        private const string ListPath = ConstructionsDir + "/ConstructionList.asset";
        private const string SettingsPath = "Assets/TowerLand/Data/BuildSettings.asset";
        private const string BrickMeshPath = "Assets/TowerLand/Art/Meshes/Brick.asset";
        private const string SpitVacuumPath = "Assets/TowerLand/Data/VacuumSettings_Spit.asset";

        // Bricks per construction: the first one completes on level 10 (levels 1-10 = 7182 cubes), each next one ~1.5x more.
        private static readonly (string asset, int bricks)[] Constructions =
        {
            ("Tower_01", 7100),
            ("Tower_02", 10800),
            ("Tower_03", 16200),
        };
        private const float BrickSize = 0.3f;
        // Chamfered brick: one bevel segment (108 triangles) - thousands of them must stay cheap.
        private const float BrickBevel = 0.15f;
        // Gap between the construction's widest ring and the plinth edge, metres.
        private const float PlinthMargin = 0.15f;

        [MenuItem("Tools/TowerLand/Build Meta Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            WireGameScene();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var visuals = AssetDatabase.LoadAssetAtPath<TowerVisualSettings>(CoreAssetsBuilder.VisualSettingsPath);
            var levels = AssetDatabase.LoadAssetAtPath<LevelList>(DebugSceneBuilder.LevelListPath);
            int columns = levels != null && levels.Count > 0 ? levels.Levels[0].Columns : 26;
            float faceRadius = EnvironmentBuilder.FaceRadius(visuals, columns);
            float platformRadius = faceRadius - visuals.PlinthInset;

            // Same framing as the Game scene (the painted background is made for it); the camera rises as the tower grows.
            var camera = Camera.main;
            camera.fieldOfView = 45f;
            camera.transform.position = new Vector3(0.4f, 9.0f, -35.5f);
            camera.transform.LookAt(new Vector3(0.4f, 4.7f, 0f));

            var site = new GameObject("Construction").transform;
            site.position = new Vector3(0f, CoreAssetsBuilder.PlinthHeight, CoreAssetsBuilder.TowerDepth);
            EnvironmentBuilder.ApplyToOpenScene(new Vector3(0f, 0f, CoreAssetsBuilder.TowerDepth), faceRadius, EnvironmentBuilder.Variant.Sky);

            var sequence = new GameObject("BuildSequence").AddComponent<BuildSequence>();
            var so = new SerializedObject(sequence);
            so.FindProperty("_session").objectReferenceValue = DebugSceneBuilder.Session();
            so.FindProperty("_constructions").objectReferenceValue = ConstructionListAsset(platformRadius);
            so.FindProperty("_settings").objectReferenceValue = CoreAssetsBuilder.LoadOrCreate<BuildSettings>(SettingsPath);
            so.FindProperty("_palette").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CubePalette>(CoreAssetsBuilder.PalettePath);
            so.FindProperty("_cubeMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CoreAssetsBuilder.CubeMaterialPath);
            so.FindProperty("_brickMesh").objectReferenceValue = BrickMesh();
            so.FindProperty("_site").objectReferenceValue = site;
            so.FindProperty("_effects").objectReferenceValue = CoreAssetsBuilder.CreateImpactEffects();
            so.FindProperty("_titleStyle").objectReferenceValue = WinSequenceBuilder.TitleStyleAsset();
            Material glass = WinSequenceBuilder.Glass();
            Material cuff = WinSequenceBuilder.Cuff();
            WinSequenceBuilder.ConfigureModel(WinSequenceBuilder.NozzlePath, glass, cuff);
            WinSequenceBuilder.ConfigureModel(WinSequenceBuilder.RingPath, glass, cuff);
            so.FindProperty("_vacuum").objectReferenceValue = SpitVacuumSettings();
            so.FindProperty("_glass").objectReferenceValue = glass;
            so.FindProperty("_nozzle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WinSequenceBuilder.NozzlePath);
            so.FindProperty("_jointRing").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WinSequenceBuilder.RingPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BuildScenePath);
            ProjectSetup.ApplyBuildScenes();
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && previous != BuildScenePath)
                EditorSceneManager.OpenScene(previous);
            Debug.Log("[TowerLand] Build (meta) scene built.");
        }

        /// <summary>The Game scene plays the player's level progression from the level list.</summary>
        private static void WireGameScene()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindFirstObjectByType<LevelBootstrap>();
            if (bootstrap == null) return;
            var so = new SerializedObject(bootstrap);
            so.FindProperty("_levels").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelList>(DebugSceneBuilder.LevelListPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Round castle towers filling the platform (widest ring just inside its edge), each sized to its brick count by
        /// the number of plain wall rows. Tuned shapes in existing assets are kept apart from the size.
        /// </summary>
        private static ConstructionList ConstructionListAsset(float platformRadius)
        {
            if (!AssetDatabase.IsValidFolder(ConstructionsDir))
                AssetDatabase.CreateFolder("Assets/TowerLand/Data", "Constructions");
            float radius = platformRadius - PlinthMargin - BrickSize * 1.5f;   // outer wall ring; footing/overhang one brick further out
            var blueprints = new TowerBlueprint[Constructions.Length];
            for (int i = 0; i < Constructions.Length; i++)
            {
                (string asset, int bricks) = Constructions[i];
                var blueprint = CoreAssetsBuilder.LoadOrCreate<TowerBlueprint>($"{ConstructionsDir}/{asset}.asset");
                var shape = new TowerShape(BrickSize, radius, 2, 3, 0, 2, 3, 3);
                while (shape.Generate().Count < bricks)
                    shape.ShaftRows++;
                blueprint.Configure("Tower", shape);
                EditorUtility.SetDirty(blueprint);
                blueprints[i] = blueprint;
                Debug.Log($"[TowerLand] {asset}: {blueprint.Size} bricks, {shape.Rows} rows, {shape.Height:0.0} m tall.");
            }

            var list = CoreAssetsBuilder.LoadOrCreate<ConstructionList>(ListPath);
            var so = new SerializedObject(list);
            SerializedProperty items = so.FindProperty("_constructions");
            items.arraySize = blueprints.Length;
            for (int i = 0; i < blueprints.Length; i++)
                items.GetArrayElementAtIndex(i).objectReferenceValue = blueprints[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return list;
        }

        /// <summary>
        /// The hose's own tuning for spitting: a copy of the win sequence's, made rigid (stiff, well-damped joints, no
        /// corkscrew, less swelling). Created once; tuned values are kept on rebuilds.
        /// </summary>
        private static Feel.VacuumSettings SpitVacuumSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<Feel.VacuumSettings>(SpitVacuumPath);
            if (settings != null) return settings;
            settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Feel.VacuumSettings>(WinSequenceBuilder.SettingsPath));
            var so = new SerializedObject(settings);
            so.FindProperty("_stiffness").floatValue = 350f;
            so.FindProperty("_damping").floatValue = 32f;
            so.FindProperty("_twistAmplitude").floatValue = 0f;
            so.FindProperty("_bulge").floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, SpitVacuumPath);
            return settings;
        }

        private static Mesh BrickMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BrickMeshPath);
            if (mesh == null)
            {
                mesh = BeveledCubeMesh.Create(1f, BrickBevel, 1, 0f);
                mesh.name = "Brick";
                AssetDatabase.CreateAsset(mesh, BrickMeshPath);
            }
            else
            {
                BeveledCubeMesh.Fill(mesh, 1f, BrickBevel, 1, 0f);
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }
    }
}
