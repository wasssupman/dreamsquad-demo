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

            Directory.CreateDirectory(DefaultPrefabFolder);

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

            var path = $"{DefaultPrefabFolder}/{data.name}.prefab";
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
                return data.sprite;

            var texture = data.sourceTexture != null ? data.sourceTexture : LoadSiblingTexture(data);
            if (texture == null)
                return null;

            var texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath))
                return null;

            if (AssetImporter.GetAtPath(texturePath) is TextureImporter textureImporter &&
                textureImporter.textureType != TextureImporterType.Sprite)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.alphaIsTransparency = true;
                textureImporter.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite != null)
            {
                data.sprite = sprite;
                EditorUtility.SetDirty(data);
            }

            return sprite;
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
    }
}
