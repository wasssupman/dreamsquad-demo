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

        // shadow-polish unit 6 — authored 블롭 기본값. 색은 런타임에 BattleBridge 전역값으로 덮인다(프리뷰용).
        private const string BlobShadowSpritePath = "Assets/_Project/Art/blob_shadow.png";
        private const float BlobBaseSize = 1f; // 1타일 = 유닛 블롭의 환산 기준(BattleBridge.BlobShadowSize = tileSize)과 같은 출발점
        // authoring 기본값(프랍별 조정 가능). 구 BlobShadowGroundY(절대 Y 0.216) − 0.02 에서 나온 수다.
        // tilted-billboard unit 7 이 유닛 블롭을 «평면 상대» 로 바꿨지만 프랍 authored 블롭은 프리팹이
        // 정본이라(shadow-polish unit 6) 이 값을 그대로 둔다 — 바꾸면 기존 프랍 프리팹과 어긋난다.
        private const float BlobGroundLiftLocal = 0.196f;
        private static readonly Color BlobPreviewColor = new Color(0f, 0f, 0f, 0.45f);

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
                var components = SkeletonAnimation.AddToGameObject(visual.gameObject, null);
                skeletonAnimation = components.skeletonAnimation;
                components.skeletonRenderer.SkeletonDataAsset = data.skeletonDataAsset;
                components.skeletonRenderer.InitialSkinName = string.IsNullOrEmpty(data.spineSkinName) ? "default" : data.spineSkinName;
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

            AttachAuthoredBlob(root.transform, data, sprite);

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

        // shadow-polish unit 6 — 블롭을 프리팹에 굽는다. 이미지마다 피벗/여백이 달라 런타임 계산으로는
        // 그림자 위치가 정규화되지 않으므로, 프리팹이 위치 XZ/크기의 source of truth (프랍별 미세조정 지점).
        // 재생성 시 기존 프리팹의 블롭 transform 을 보존한다 — 수동 튜닝을 날리지 않기 위함.
        private static void AttachAuthoredBlob(Transform root, PropData data, Sprite sprite)
        {
            var previous = data.prefab != null ? data.prefab.GetComponentInChildren<BlobShadow>(true) : null;

            var go = new GameObject("BlobShadow");
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BlobShadowSpritePath);
            sr.color = BlobPreviewColor;
            sr.sortingOrder = BoardSortOrder.ShadowOrder;
            go.AddComponent<BlobShadow>().MarkAuthored();

            if (previous != null)
            {
                go.transform.localPosition = previous.transform.localPosition;
                go.transform.localRotation = previous.transform.localRotation;
                go.transform.localScale = previous.transform.localScale;
                return;
            }

            // 기본값: footprint 종횡비(긴축 정규화) × visualScale, 틸트로 눕는 몸체 중심의 지면 투영.
            // prop-upright-root — 프랍은 upright 프레임(props 루트가 부모 90° 상쇄) 아래 놓인다.
            // 관례: +Y=월드 높이, +Z=월드 깊이(틸트 눕는 방향). 쿼드는 Euler(90,0,0)로 XZ 바닥에 눕힌다(양면 렌더).
            var fp = data.Footprint;
            float fpMax = Mathf.Max(fp.x, fp.y);
            float scale = Mathf.Max(0.01f, data.visualScale);
            float worldHeight = (sprite != null ? sprite.bounds.size.y : 1f) * scale;
            float depthOffset = data.billboardMode == PropBillboardMode.Tilted
                ? 0.5f * worldHeight * Mathf.Sin(data.tiltAngle * Mathf.Deg2Rad)
                : 0f;
            go.transform.localPosition = new Vector3(0f, BlobGroundLiftLocal, depthOffset);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(
                BlobBaseSize * scale * fp.x / fpMax,
                BlobBaseSize * scale * fp.y / fpMax,
                1f);
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

            // 접지 pivot: 틸트 빌보드는 Visual 원점(=지면)을 축으로 회전한다. pivot 이 중앙이면
            // 스프라이트 아래 절반이 지면 아래로 묻힌다(prop_tree_1x4 는 BottomCenter 라 정상, 기본 Center 는 깔림).
            // standing 프랍의 base 가 지면에 서도록 BottomCenter 강제. visualOffset 은 미세 조정용으로만.
            var spriteSettings = new TextureImporterSettings();
            textureImporter.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            textureImporter.SetTextureSettings(spriteSettings);

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
