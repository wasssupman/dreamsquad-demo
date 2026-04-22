using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Editor
{
    [CustomEditor(typeof(PropData))]
    public class PropDataEditor : UnityEditor.Editor
    {
        private const string DefaultPrefabFolder = "Assets/_Project/Prefabs/Props";
        private const float PropSpritePixelsPerUnit = 256f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Generate Billboard Prefab", GUILayout.Height(32f)))
                    GeneratePrefab((PropData)target);
            }
        }

        private static void GeneratePrefab(PropData data)
        {
            if (data == null) return;

            var sprite = ResolveSprite(data);
            if (sprite == null && !data.HasSpineVisual)
            {
                EditorUtility.DisplayDialog("Prop Prefab Generator", "Sprite, Source Texture 또는 SkeletonDataAsset 중 하나를 지정해야 합니다.", "OK");
                return;
            }

            var prefabFolder = GetPrefabFolder(data);
            Directory.CreateDirectory(prefabFolder);

            var root = new GameObject(string.IsNullOrEmpty(data.id) ? data.name : data.id);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = data.visualOffset;
            visual.localScale = Vector3.one * Mathf.Max(0.01f, data.visualScale);

            SpriteRenderer spriteRenderer = null;
            SkeletonAnimation skeletonAnimation = null;
            if (data.HasSpineVisual)
            {
                skeletonAnimation = visual.gameObject.AddComponent<SkeletonAnimation>();
                skeletonAnimation.skeletonDataAsset = data.skeletonDataAsset;
                skeletonAnimation.initialSkinName = string.IsNullOrEmpty(data.spineSkinName) ? "default" : data.spineSkinName;
                skeletonAnimation.Initialize(false);
            }
            else
            {
                spriteRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = data.spriteColor;
                spriteRenderer.sortingOrder = data.sortingOrder;
            }

            var billboard = root.AddComponent<PropBillboard>();
            billboard.Configure(data, visual, spriteRenderer, skeletonAnimation);

            var path = $"{prefabFolder}/{data.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Prop Prefab Generator", "Prefab 생성에 실패했습니다.", "OK");
                return;
            }

            data.prefab = prefab;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        private static Sprite ResolveSprite(PropData data)
        {
            if (data.sprite != null)
            {
                ConfigureTextureImporter(AssetDatabase.GetAssetPath(data.sprite));
                return data.sprite;
            }

            var texture = data.sourceTexture != null ? data.sourceTexture : LoadThemeTexture(data);
            if (texture == null)
                texture = LoadSiblingTexture(data);
            if (texture == null)
                return null;

            var texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath))
                return null;

            ConfigureTextureImporter(texturePath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite != null)
            {
                data.sprite = sprite;
                EditorUtility.SetDirty(data);
            }

            return sprite;
        }

        private static void ConfigureTextureImporter(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath) ||
                AssetImporter.GetAtPath(texturePath) is not TextureImporter textureImporter)
                return;

            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.alphaIsTransparency = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Bilinear;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.crunchedCompression = false;
            textureImporter.spritePixelsPerUnit = PropSpritePixelsPerUnit;

            ConfigurePlatformTexture(textureImporter, "Standalone");
            ConfigurePlatformTexture(textureImporter, "Android");
            ConfigurePlatformTexture(textureImporter, "iPhone");
            ConfigurePlatformTexture(textureImporter, "WebGL");

            textureImporter.SaveAndReimport();
        }

        private static void ConfigurePlatformTexture(TextureImporter textureImporter, string platformName)
        {
            var settings = textureImporter.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.compressionQuality = 100;
            textureImporter.SetPlatformTextureSettings(settings);
        }

        private static Texture2D LoadSiblingTexture(PropData data)
        {
            var dataPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(dataPath))
                return null;

            var folder = Path.GetDirectoryName(dataPath);
            if (string.IsNullOrEmpty(folder))
                return null;

            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{data.name}.png");
        }

        private static Texture2D LoadThemeTexture(PropData data)
        {
            var themeName = GetThemeName(data);
            if (string.IsNullOrEmpty(themeName))
                return null;

            return AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/_Project/Art/Theme/{themeName}/{data.name}.png");
        }

        private static string GetPrefabFolder(PropData data)
        {
            var themeName = GetThemeName(data);
            return string.IsNullOrEmpty(themeName)
                ? DefaultPrefabFolder
                : $"{DefaultPrefabFolder}/{themeName}";
        }

        private static string GetThemeName(PropData data)
        {
            var dataPath = AssetDatabase.GetAssetPath(data);
            const string themeRoot = "Assets/_Project/Data/Theme/";
            if (string.IsNullOrEmpty(dataPath) || !dataPath.StartsWith(themeRoot))
                return null;

            var relative = dataPath.Substring(themeRoot.Length);
            var separator = relative.IndexOf('/');
            return separator <= 0 ? null : relative.Substring(0, separator);
        }
    }
}
