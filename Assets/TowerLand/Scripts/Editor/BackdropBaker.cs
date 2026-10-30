using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerLand.Editor
{
    /// <summary>
    /// Replaces distant 3D scenery (mountains, clouds) with images pre-rendered through the game camera, shown on far
    /// camera-facing planes. Cheaper at runtime and a single flat "painted" layer per depth.
    /// Menu: Tools/TowerLand/Bake Backdrop. Called by EnvironmentBuilder after the environment is set up.
    /// Re-bake after changing the camera, lighting/fog, or the Blender mountains/clouds.
    /// </summary>
    public static class BackdropBaker
    {
        private sealed class Layer
        {
            public string Name;
            public string[] Sources;      // Environment children rendered into the image (then hidden)
            public string[] Occluders;    // Environment children written to depth only while baking
            public float Distance;        // plane distance from the camera
            public int BlurRadius;        // >0: gaussian blur in pixels (fake lens focus for foreground props)
        }

        // Pre-rendered layers baked from Environment geometry (none at the moment: the far background is a painted image).
        private static readonly Layer[] Layers = new Layer[0];

        // Painted far background (sky + clouds + mountains, 9:16), shown on one plane filling the game camera's view.
        // The 3D sky props it replaces stay in the Environment, deactivated.
        private const string BackgroundImagePath = "Assets/TowerLand/Art/Textures/World1_Background.png";
        private static readonly string[] ReplacedByBackground = { "Mountains_Near", "Mountains_Far", "Clouds" };
        private const float BackgroundDistance = 700f;
        // Vertical shift of the background image, in fractions of the screen height (+ = up).
        private const float BackgroundScale = 1f;
        private const float BackgroundOffset = 0.17f;   // peaks at ~22% from the top, like the mockup

        private const string TextureFolder = "Assets/TowerLand/Art/Textures";
        private const string MaterialFolder = "Assets/TowerLand/Art/Materials";
        private const string RootName = "Backdrop";
        private const int Height = 1600;
        private const float Aspect = 0.8f;   // wider than 9:16 so 3:4 tablets are covered too

        [MenuItem("Tools/TowerLand/Bake Backdrop")]
        public static void BakeFromMenu()
        {
            Bake();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void Bake() => Bake(true);

        /// <param name="background">Show the painted far background (replacing the 3D mountains + clouds); false keeps the 3D sky.</param>
        public static void Bake(bool background)
        {
            GameObject environment = GameObject.Find("Environment");
            Camera gameCamera = Camera.main;
            if (environment == null || gameCamera == null)
            {
                Debug.LogWarning("[TowerLand] Backdrop bake needs the Environment and a main camera.");
                return;
            }

            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            foreach (Layer layer in Layers)
                SetActive(environment, layer.Sources, true);   // bake from the real geometry

            var root = new GameObject(RootName);
            foreach (Layer layer in Layers)
            {
                Texture2D image = RenderLayer(environment, gameCamera, layer);
                if (layer.BlurRadius > 0) Blur(image, layer.BlurRadius);
                string texturePath = $"{TextureFolder}/World1_Backdrop_{layer.Name}.png";
                File.WriteAllBytes(texturePath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();

                Material material = BackdropMaterial($"{MaterialFolder}/World1_Backdrop_{layer.Name}.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                CreatePlane(root.transform, gameCamera, layer, material);
                SetActive(environment, layer.Sources, false);
            }

            if (background)
            {
                CreateBackgroundPlane(root.transform, gameCamera);
                SetActive(environment, ReplacedByBackground, false);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[TowerLand] Backdrop baked.");
        }

        /// <summary>Renders the layer on black and on white; the difference gives exact, anti-aliased alpha.</summary>
        private static Texture2D RenderLayer(GameObject environment, Camera gameCamera, Layer layer)
        {
            var keep = new HashSet<Renderer>();
            foreach (string name in layer.Sources)
                foreach (Renderer r in Find(environment, name)) keep.Add(r);

            var occluders = new List<(Renderer renderer, Material[] materials)>();
            var depthMask = new Material(Shader.Find("TowerLand/Depth Mask"));
            foreach (string name in layer.Occluders)
                foreach (Renderer r in Find(environment, name))
                {
                    occluders.Add((r, r.sharedMaterials));
                    r.sharedMaterials = new[] { depthMask };
                    keep.Add(r);
                }

            var disabled = new List<Renderer>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.enabled && !keep.Contains(r)) { r.enabled = false; disabled.Add(r); }

            var cameraObject = new GameObject("BackdropBakeCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(gameCamera);
            camera.aspect = Aspect;
            camera.clearFlags = CameraClearFlags.SolidColor;

            int width = Mathf.RoundToInt(Height * Aspect);
            // Right after a scene rebuild the editor compiles shader variants asynchronously and draws placeholders,
            // which would bake garbage; force synchronous compilation for these renders.
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Color[] onBlack = Render(camera, width, Color.black);
            Color[] onWhite = Render(camera, width, Color.white);
            ShaderUtil.allowAsyncCompilation = async;

            var result = new Texture2D(width, Height, TextureFormat.RGBA32, false);
            var pixels = new Color[onBlack.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float alpha = Mathf.Clamp01(1f - (onWhite[i].r - onBlack[i].r + onWhite[i].g - onBlack[i].g + onWhite[i].b - onBlack[i].b) / 3f);
                Color c = alpha > 0.002f ? onBlack[i] / alpha : Color.black;   // un-premultiply
                pixels[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), alpha);
            }
            result.SetPixels(pixels);
            result.Apply();

            Object.DestroyImmediate(cameraObject);
            foreach (Renderer r in disabled) r.enabled = true;
            foreach ((Renderer r, Material[] materials) in occluders) r.sharedMaterials = materials;
            Object.DestroyImmediate(depthMask);
            return result;
        }

        /// <summary>Separable gaussian blur on premultiplied colour, so transparent edges blur without dark fringes.</summary>
        private static void Blur(Texture2D texture, int radius)
        {
            int w = texture.width, h = texture.height;
            Color[] src = texture.GetPixels();
            for (int i = 0; i < src.Length; i++) { Color c = src[i]; src[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a); }

            float sigma = radius / 2.5f;
            var kernel = new float[radius * 2 + 1];
            float sum = 0f;
            for (int k = -radius; k <= radius; k++) sum += kernel[k + radius] = Mathf.Exp(-k * k / (2f * sigma * sigma));
            for (int k = 0; k < kernel.Length; k++) kernel[k] /= sum;

            var tmp = new Color[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = Color.clear;
                    for (int k = -radius; k <= radius; k++) acc += src[y * w + Mathf.Clamp(x + k, 0, w - 1)] * kernel[k + radius];
                    tmp[y * w + x] = acc;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = Color.clear;
                    for (int k = -radius; k <= radius; k++) acc += tmp[Mathf.Clamp(y + k, 0, h - 1) * w + x] * kernel[k + radius];
                    float a = acc.a;
                    src[y * w + x] = a > 0.002f ? new Color(acc.r / a, acc.g / a, acc.b / a, a) : Color.clear;
                }
            texture.SetPixels(src);
            texture.Apply();
        }

        private static Color[] Render(Camera camera, int width, Color background)
        {
            camera.backgroundColor = background;
            var rt = new RenderTexture(width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, Height), 0, 0);
            texture.Apply();
            Color[] pixels = texture.GetPixels();
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
            return pixels;
        }

        private static void CreateBackgroundPlane(Transform parent, Camera camera)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundImagePath);
            if (importer == null) return;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundImagePath);
            Material material = BackdropMaterial($"{MaterialFolder}/World1_Background.mat", texture);
            material.renderQueue = -1;   // shader default (after opaques + skybox); nearer scenery hides it via depth

            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "Background";
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.transform.SetParent(parent, false);
            Transform cam = camera.transform;
            float frameHeight = 2f * BackgroundDistance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Native scale for the 9:16 game frame (the image was painted for it). Wider screens show its edges.
            float imageAspect = (float)texture.width / texture.height;
            float height = frameHeight * BackgroundScale;
            float width = height * imageAspect;
            plane.transform.SetPositionAndRotation(cam.position + cam.forward * BackgroundDistance + cam.up * (BackgroundOffset * frameHeight), cam.rotation);
            plane.transform.localScale = new Vector3(width, height, 1f);
            var renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void CreatePlane(Transform parent, Camera camera, Layer layer, Material material)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = layer.Name;
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.transform.SetParent(parent, false);
            Transform cam = camera.transform;
            plane.transform.SetPositionAndRotation(cam.position + cam.forward * layer.Distance, cam.rotation);
            float height = 2f * layer.Distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            plane.transform.localScale = new Vector3(height * Aspect, height, 1f);
            var renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(plane, StaticEditorFlags.BatchingStatic);
        }

        private static Material BackdropMaterial(string path, Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("TowerLand/Backdrop"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static IEnumerable<Renderer> Find(GameObject environment, string name)
        {
            Transform child = environment.transform.Find(name);
            return child != null ? child.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        }

        private static void SetActive(GameObject environment, IEnumerable<string> names, bool active)
        {
            foreach (string name in names)
            {
                Transform child = environment.transform.Find(name);
                if (child != null) child.gameObject.SetActive(active);
            }
        }
    }
}
