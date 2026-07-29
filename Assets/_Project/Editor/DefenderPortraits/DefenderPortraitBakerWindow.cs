using System;
using System.Collections.Generic;
using System.IO;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Editor.Portraits
{
    public sealed class DefenderPortraitBakerWindow : EditorWindow
    {
        private enum PreviewBackground
        {
            Black,
            White,
            GameTone,
        }

        private DefenderPortraitBakeProfile _profile;
        private Texture2D _selectedPreview;
        private readonly List<Texture2D> _contactPreviews = new();
        private readonly List<DefenderUnitData> _contactUnits = new();
        private readonly List<string> _messages = new();
        private Vector2 _scroll;
        private int _selectedIndex;
        private PreviewBackground _previewBackground = PreviewBackground.GameTone;

        [MenuItem("Window/Wassup/Defender Portrait Baker")]
        public static void Open()
        {
            GetWindow<DefenderPortraitBakerWindow>("Defender Portraits");
        }

        private void OnEnable()
        {
            if (_profile != null) return;
            string[] guids = AssetDatabase.FindAssets(
                "t:DefenderPortraitBakeProfile",
                new[] { "Assets/_Project/Editor/DefenderPortraits" });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _profile = AssetDatabase.LoadAssetAtPath<DefenderPortraitBakeProfile>(path);
            }
        }

        private void OnDisable()
        {
            ClearPreviews();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Spine Defender Portrait Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "DefenderCatalog의 현재 Spine 파츠/색을 고정 상체 Sprite로 굽습니다. " +
                "Bake는 PNG를 쓰고 같은 id의 DefenderUnitData.portrait를 교체합니다.",
                MessageType.Info);

            DefenderPortraitBakeProfile next = (DefenderPortraitBakeProfile)EditorGUILayout.ObjectField(
                "Bake Profile",
                _profile,
                typeof(DefenderPortraitBakeProfile),
                false);
            if (next != _profile)
            {
                _profile = next;
                _selectedIndex = 0;
                ClearPreviews();
                _messages.Clear();
            }

            if (_profile == null)
            {
                EditorGUILayout.HelpBox("DefenderPortraitBakeProfile을 지정하세요.", MessageType.Warning);
                return;
            }

            List<DefenderUnitData> units = DefenderPortraitBaker.GetValidUnits(_profile.Catalog);
            DrawSelection(units);
            DrawToolbar(units);
            DrawMessages();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSelectedPreview(units);
            DrawContactSheet();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSelection(List<DefenderUnitData> units)
        {
            if (units.Count == 0)
            {
                EditorGUILayout.HelpBox("Catalog에 유효한 Defender가 없습니다.", MessageType.Error);
                return;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, units.Count - 1);
            string[] labels = new string[units.Count];
            for (int i = 0; i < units.Count; i++)
                labels[i] = $"{units[i].id}  ({units[i].displayName})";

            int next = EditorGUILayout.Popup("Selected", _selectedIndex, labels);
            if (next != _selectedIndex)
            {
                _selectedIndex = next;
                DestroyTexture(ref _selectedPreview);
            }

            _previewBackground = (PreviewBackground)GUILayout.Toolbar(
                (int)_previewBackground,
                new[] { "Black", "White", "Game Tone" });
        }

        private void DrawToolbar(List<DefenderUnitData> units)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(units.Count == 0))
                {
                    if (GUILayout.Button("Preview Selected"))
                        RefreshSelectedPreview(units);
                    if (GUILayout.Button("Preview All"))
                        RefreshAllPreviews(units);
                }

                if (GUILayout.Button("Validate"))
                    ValidateProfile();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(units.Count == 0))
                {
                    if (GUILayout.Button("Bake Selected + Assign"))
                        ConfirmBake(units, false);
                    if (GUILayout.Button("Bake All + Assign"))
                        ConfirmBake(units, true);
                }
            }
        }

        private void DrawMessages()
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                bool ok = _messages[i].StartsWith("OK", StringComparison.Ordinal);
                EditorGUILayout.HelpBox(_messages[i], ok ? MessageType.Info : MessageType.Error);
            }
        }

        private void DrawSelectedPreview(List<DefenderUnitData> units)
        {
            if (_selectedPreview == null || units.Count == 0) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"Selected Preview — {units[_selectedIndex].id}", EditorStyles.boldLabel);
            float size = Mathf.Min(420f, position.width - 32f);
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            DrawPortrait(rect, _selectedPreview);
        }

        private void DrawContactSheet()
        {
            if (_contactPreviews.Count == 0) return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"Contact Sheet — {_contactPreviews.Count}", EditorStyles.boldLabel);

            const int columns = 5;
            const float gap = 6f;
            const float labelHeight = 20f;
            float available = Mathf.Max(300f, position.width - 44f);
            float cell = Mathf.Clamp((available - gap * (columns - 1)) / columns, 96f, 180f);
            int rows = Mathf.CeilToInt(_contactPreviews.Count / (float)columns);
            Rect area = GUILayoutUtility.GetRect(
                columns * cell + gap * (columns - 1),
                rows * (cell + labelHeight + gap),
                GUILayout.ExpandWidth(false));

            for (int i = 0; i < _contactPreviews.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                var portraitRect = new Rect(
                    area.x + column * (cell + gap),
                    area.y + row * (cell + labelHeight + gap),
                    cell,
                    cell);
                DrawPortrait(portraitRect, _contactPreviews[i]);

                string label = i < _contactUnits.Count ? _contactUnits[i].id : i.ToString();
                var labelRect = new Rect(portraitRect.x, portraitRect.yMax, cell, labelHeight);
                GUI.Label(labelRect, label, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawPortrait(Rect rect, Texture2D portrait)
        {
            Color background;
            switch (_previewBackground)
            {
                case PreviewBackground.Black:
                    background = Color.black;
                    break;
                case PreviewBackground.White:
                    background = Color.white;
                    break;
                default:
                    background = new Color(0.12f, 0.13f, 0.17f, 1f);
                    break;
            }

            EditorGUI.DrawRect(rect, background);
            GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit, true);
        }

        private void RefreshSelectedPreview(List<DefenderUnitData> units)
        {
            if (units.Count == 0) return;
            DestroyTexture(ref _selectedPreview);
            try
            {
                _selectedPreview = DefenderPortraitBaker.RenderPortrait(_profile, units[_selectedIndex]);
                _messages.Clear();
                _messages.Add($"OK — preview: {units[_selectedIndex].id}");
            }
            catch (Exception ex)
            {
                _messages.Clear();
                _messages.Add(ex.Message);
            }
        }

        private void RefreshAllPreviews(List<DefenderUnitData> units)
        {
            ClearContactPreviews();
            _messages.Clear();
            try
            {
                for (int i = 0; i < units.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Defender Portrait Preview",
                        units[i].id,
                        i / (float)Mathf.Max(1, units.Count));
                    _contactUnits.Add(units[i]);
                    _contactPreviews.Add(DefenderPortraitBaker.RenderPortrait(_profile, units[i]));
                }
                _messages.Add($"OK — contact sheet: {_contactPreviews.Count}");
            }
            catch (Exception ex)
            {
                _messages.Add(ex.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ValidateProfile()
        {
            _messages.Clear();
            List<string> errors = DefenderPortraitBaker.Validate(_profile, true);
            if (errors.Count == 0)
                _messages.Add("OK — profile/catalog/render/importer validation passed.");
            else
                _messages.AddRange(errors);
        }

        private void ConfirmBake(List<DefenderUnitData> units, bool all)
        {
            int count = all ? units.Count : 1;
            if (!EditorUtility.DisplayDialog(
                    "Bake Defender Portraits",
                    $"{count}개 PNG를 쓰고 DefenderUnitData.portrait를 교체합니다.",
                    "Bake",
                    "Cancel"))
                return;

            try
            {
                if (all)
                    DefenderPortraitBaker.BakeAndAssign(_profile, units);
                else
                    DefenderPortraitBaker.BakeAndAssign(
                        _profile,
                        new List<DefenderUnitData> { units[_selectedIndex] });

                _messages.Clear();
                _messages.Add($"OK — baked and assigned: {count}");
                RefreshSelectedPreview(units);
            }
            catch (Exception ex)
            {
                _messages.Clear();
                _messages.Add(ex.Message);
            }
        }

        private void ClearPreviews()
        {
            DestroyTexture(ref _selectedPreview);
            ClearContactPreviews();
        }

        private void ClearContactPreviews()
        {
            for (int i = 0; i < _contactPreviews.Count; i++)
                if (_contactPreviews[i] != null)
                    DestroyImmediate(_contactPreviews[i]);
            _contactPreviews.Clear();
            _contactUnits.Clear();
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null) DestroyImmediate(texture);
            texture = null;
        }
    }

    public static class DefenderPortraitBaker
    {
        private const int PortraitLayer = 31;
        private const string FilePrefix = "defender_portrait_";

        public static List<DefenderUnitData> GetValidUnits(DefenderCatalog catalog)
        {
            var result = new List<DefenderUnitData>();
            if (catalog == null || catalog.units == null) return result;

            for (int i = 0; i < catalog.units.Length; i++)
                if (catalog.units[i] != null)
                    result.Add(catalog.units[i]);
            return result;
        }

        public static List<string> ValidateCatalog(DefenderCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("Catalog is null.");
                return errors;
            }
            if (catalog.units == null || catalog.units.Length == 0)
            {
                errors.Add("Catalog has no units.");
                return errors;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.units.Length; i++)
            {
                DefenderUnitData unit = catalog.units[i];
                if (unit == null)
                {
                    errors.Add($"Catalog[{i}] is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(unit.id))
                {
                    errors.Add($"{unit.name}: id is empty.");
                    continue;
                }
                if (!IsSafeId(unit.id))
                    errors.Add($"{unit.name}: id '{unit.id}' must use lowercase ASCII, digits, or underscore.");
                if (!ids.Add(unit.id))
                    errors.Add($"Duplicate defender id: {unit.id}");
                if (unit.skeletonDataAsset == null)
                    errors.Add($"{unit.id}: skeletonDataAsset is null.");

                string output = FilePrefix + unit.id + ".png";
                if (!paths.Add(output))
                    errors.Add($"Duplicate output path: {output}");
            }
            return errors;
        }

        public static List<string> Validate(DefenderPortraitBakeProfile profile, bool renderProbe)
        {
            var errors = new List<string>();
            if (profile == null)
            {
                errors.Add("Bake profile is null.");
                return errors;
            }

            errors.AddRange(ValidateCatalog(profile.Catalog));
            string outputFolder = GetOutputFolderPath(profile, errors);
            List<DefenderUnitData> units = GetValidUnits(profile.Catalog);

            for (int i = 0; i < units.Count; i++)
                ValidateSkeletonContract(profile, units[i], errors);

            if (!string.IsNullOrEmpty(outputFolder))
                ValidateExistingOutputs(profile, units, outputFolder, errors);

            if (renderProbe && errors.Count == 0 && units.Count > 0)
                ValidateRenderIsolation(profile, units[0], errors);
            return errors;
        }

        public static Texture2D RenderPortrait(
            DefenderPortraitBakeProfile profile,
            DefenderUnitData defender)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (defender.skeletonDataAsset == null)
                throw new InvalidOperationException($"{defender.id}: skeletonDataAsset is null.");

            Scene previewScene = default;
            Camera camera = null;
            RenderTexture renderTarget = null;
            RenderTexture resolvedTarget = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                ulong sceneMask = EditorSceneManager.GetSceneCullingMask(previewScene);

                camera = BuildCamera(previewScene, sceneMask);
                BuildLight(previewScene);
                SkeletonAnimation skeleton = BuildSkeleton(previewScene, profile, defender);

                Bone head = skeleton.Skeleton.FindBone("Head");
                Bone pelvis = skeleton.Skeleton.FindBone("Pelvis");
                if (head == null || pelvis == null)
                    throw new InvalidOperationException($"{defender.id}: Head/Pelvis anchor bone missing.");

                profile.ResolveFraming(defender.id, out Vector2 unitOffset, out float sizeMultiplier);
                var anchorMidpoint = new Vector2(
                    (head.WorldX + pelvis.WorldX) * 0.5f,
                    (head.WorldY + pelvis.WorldY) * 0.5f);
                Vector2 center = anchorMidpoint + profile.AnchorOffset + unitOffset;
                camera.transform.position = new Vector3(center.x, center.y, -10f);
                camera.orthographicSize = profile.OrthographicSize * sizeMultiplier;

                int outputSize = profile.Resolution;
                int renderSize = outputSize * profile.Supersample;
                renderTarget = MakeRenderTexture(renderSize, 4);
                resolvedTarget = MakeRenderTexture(outputSize, 1);
                camera.targetTexture = renderTarget;
                camera.Render();
                Graphics.Blit(renderTarget, resolvedTarget);

                RenderTexture.active = resolvedTarget;
                var output = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false, false)
                {
                    name = FilePrefix + defender.id,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                output.ReadPixels(new Rect(0f, 0f, outputSize, outputSize), 0, 0, false);
                output.Apply(false, false);
                ConvertPremultipliedToStraightAlpha(output);
                return output;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (camera != null)
                    camera.targetTexture = null;
                ReleaseRenderTexture(renderTarget);
                ReleaseRenderTexture(resolvedTarget);
                if (previewScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        public static void BakeAndAssign(
            DefenderPortraitBakeProfile profile,
            IReadOnlyList<DefenderUnitData> units)
        {
            List<string> errors = ValidateCatalog(profile != null ? profile.Catalog : null);
            string outputFolder = GetOutputFolderPath(profile, errors);
            if (units == null || units.Count == 0)
                errors.Add("No defenders selected for bake.");

            var unitAssetPaths = new List<string>(units != null ? units.Count : 0);
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    DefenderUnitData unit = units[i];
                    string unitPath = unit != null ? AssetDatabase.GetAssetPath(unit) : null;
                    unitAssetPaths.Add(unitPath);
                    if (string.IsNullOrEmpty(unitPath))
                        errors.Add($"Defender[{i}] is not a persistent asset.");
                    else if (EditorUtility.IsDirty(unit))
                        errors.Add($"{unit.id}: source asset has unsaved changes.");
                }
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));

            var outputPaths = new List<string>(units.Count);
            try
            {
                for (int i = 0; i < unitAssetPaths.Count; i++)
                    AssetDatabase.ImportAsset(unitAssetPaths[i], ImportAssetOptions.ForceUpdate);

                for (int i = 0; i < units.Count; i++)
                {
                    DefenderUnitData unit = units[i];
                    EditorUtility.DisplayProgressBar(
                        "Bake Defender Portraits",
                        unit.id,
                        i / (float)Mathf.Max(1, units.Count));
                    Texture2D portrait = RenderPortrait(profile, unit);
                    try
                    {
                        string assetPath = OutputPath(outputFolder, unit.id);
                        File.WriteAllBytes(AssetPathToAbsolute(assetPath), portrait.EncodeToPNG());
                        outputPaths.Add(assetPath);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(portrait);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                for (int i = 0; i < outputPaths.Count; i++)
                {
                    ConfigureImporter(outputPaths[i], profile.Resolution);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPaths[i]);
                    if (sprite == null)
                        throw new InvalidOperationException($"Sprite import failed: {outputPaths[i]}");

                    DefenderUnitData unit = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(
                        unitAssetPaths[i]);
                    if (unit == null)
                        throw new InvalidOperationException($"Defender reimport failed: {unitAssetPaths[i]}");

                    Undo.RecordObject(unit, "Assign Spine Defender Portrait");
                    unit.portrait = sprite;
                    EditorUtility.SetDirty(unit);
                    AssetDatabase.SaveAssetIfDirty(unit);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static SkeletonAnimation BuildSkeleton(
            Scene scene,
            DefenderPortraitBakeProfile profile,
            DefenderUnitData defender)
        {
            SkeletonAnimation skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(
                defender.skeletonDataAsset);
            skeleton.gameObject.name = "__DefenderPortraitSkeleton";
            PreparePreviewObject(skeleton.gameObject, scene);
            skeleton.Initialize(false);
            if (skeleton.Skeleton == null || skeleton.AnimationState == null)
                throw new InvalidOperationException($"{defender.id}: Spine skeleton initialization failed.");

            SpineCombinedSkinCache.Apply(skeleton.Skeleton, defender);
            string animationName = string.IsNullOrWhiteSpace(profile.PoseAnimation)
                ? defender.idleAnimation
                : profile.PoseAnimation;
            Spine.Animation animation = skeleton.Skeleton.Data.FindAnimation(animationName);
            if (animation == null)
                throw new InvalidOperationException($"{defender.id}: pose animation '{animationName}' missing.");

            TrackEntry entry = skeleton.AnimationState.SetAnimation(0, animationName, false);
            entry.TrackTime = Mathf.Clamp(profile.PoseTimeSeconds, 0f, animation.Duration);
            skeleton.Update(0f);
            skeleton.LateUpdate();
            return skeleton;
        }

        private static Camera BuildCamera(Scene scene, ulong sceneMask)
        {
            var go = new GameObject("__DefenderPortraitCamera", typeof(Camera));
            PreparePreviewObject(go, scene);
            Camera camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.cullingMask = 1 << PortraitLayer;
            camera.overrideSceneCullingMask = sceneMask;
            return camera;
        }

        private static void BuildLight(Scene scene)
        {
            var go = new GameObject("__DefenderPortraitDirectionalLight", typeof(Light));
            PreparePreviewObject(go, scene);
            Light light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.cullingMask = 1 << PortraitLayer;
            go.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        }

        private static void PreparePreviewObject(GameObject go, Scene scene)
        {
            SetLayerRecursively(go, PortraitLayer);
            go.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }

        private static RenderTexture MakeRenderTexture(int size, int antiAliasing)
        {
            var target = new RenderTexture(
                size,
                size,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                antiAliasing = antiAliasing,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            target.Create();
            return target;
        }

        private static void ReleaseRenderTexture(RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static void ConvertPremultipliedToStraightAlpha(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                int alpha = pixels[i].a;
                if (alpha == 0)
                {
                    pixels[i].r = 0;
                    pixels[i].g = 0;
                    pixels[i].b = 0;
                    continue;
                }

                pixels[i].r = (byte)Mathf.Min(255, (pixels[i].r * 255 + alpha / 2) / alpha);
                pixels[i].g = (byte)Mathf.Min(255, (pixels[i].g * 255 + alpha / 2) / alpha);
                pixels[i].b = (byte)Mathf.Min(255, (pixels[i].b * 255 + alpha / 2) / alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private static void ValidateSkeletonContract(
            DefenderPortraitBakeProfile profile,
            DefenderUnitData unit,
            List<string> errors)
        {
            if (unit == null || unit.skeletonDataAsset == null) return;
            SkeletonData data = unit.skeletonDataAsset.GetSkeletonData(false);
            if (data == null)
            {
                errors.Add($"{unit.id}: SkeletonData load failed.");
                return;
            }

            string animation = string.IsNullOrWhiteSpace(profile.PoseAnimation)
                ? unit.idleAnimation
                : profile.PoseAnimation;
            if (string.IsNullOrWhiteSpace(animation) || data.FindAnimation(animation) == null)
                errors.Add($"{unit.id}: pose animation '{animation}' missing.");
            if (data.FindBone("Head") == null)
                errors.Add($"{unit.id}: Head anchor bone missing.");
            if (data.FindBone("Pelvis") == null)
                errors.Add($"{unit.id}: Pelvis anchor bone missing.");
        }

        private static void ValidateRenderIsolation(
            DefenderPortraitBakeProfile profile,
            DefenderUnitData probe,
            List<string> errors)
        {
            Scene activeBefore = SceneManager.GetActiveScene();
            int rootCountBefore = activeBefore.rootCount;
            bool dirtyBefore = activeBefore.isDirty;
            int previewsBefore = EditorSceneManager.previewSceneCount;
            Texture2D rendered = null;
            try
            {
                rendered = RenderPortrait(profile, probe);
                if (rendered == null || rendered.width != profile.Resolution || rendered.height != profile.Resolution)
                    errors.Add("Render probe returned an invalid texture.");
            }
            catch (Exception ex)
            {
                errors.Add($"Render probe failed: {ex.Message}");
            }
            finally
            {
                if (rendered != null)
                    UnityEngine.Object.DestroyImmediate(rendered);
            }

            Scene activeAfter = SceneManager.GetActiveScene();
            if (activeAfter.handle != activeBefore.handle)
                errors.Add("Active scene changed during portrait render.");
            if (activeBefore.rootCount != rootCountBefore)
                errors.Add("Active scene hierarchy changed during portrait render.");
            if (activeBefore.isDirty != dirtyBefore)
                errors.Add("Active scene dirty state changed during portrait render.");
            if (EditorSceneManager.previewSceneCount != previewsBefore)
                errors.Add("Preview scene leaked during portrait render.");
        }

        private static void ValidateExistingOutputs(
            DefenderPortraitBakeProfile profile,
            List<DefenderUnitData> units,
            string outputFolder,
            List<string> errors)
        {
            int existing = 0;
            for (int i = 0; i < units.Count; i++)
                if (File.Exists(AssetPathToAbsolute(OutputPath(outputFolder, units[i].id))))
                    existing++;

            if (existing == 0) return;
            if (existing != units.Count)
                errors.Add($"Baked output set is partial: {existing}/{units.Count}.");

            for (int i = 0; i < units.Count; i++)
            {
                string path = OutputPath(outputFolder, units[i].id);
                if (!File.Exists(AssetPathToAbsolute(path))) continue;
                ValidateImporter(path, profile.Resolution, errors);
            }
        }

        private static void ValidateImporter(string path, int resolution, List<string> errors)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                errors.Add($"TextureImporter missing: {path}");
                return;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                !importer.sRGBTexture ||
                !importer.alphaIsTransparency ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.maxTextureSize != resolution)
                errors.Add($"Importer contract mismatch: {path}");

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != resolution || texture.height != resolution)
                errors.Add($"Texture size mismatch: {path}");

            ValidatePlatform(importer, path, "Android", resolution, errors);
            ValidatePlatform(importer, path, "iPhone", resolution, errors);
        }

        private static void ValidatePlatform(
            TextureImporter importer,
            string path,
            string platform,
            int resolution,
            List<string> errors)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            if (!settings.overridden ||
                settings.maxTextureSize != resolution ||
                settings.format != TextureImporterFormat.ASTC_6x6)
                errors.Add($"{platform} importer mismatch: {path}");
        }

        private static void ConfigureImporter(string path, int resolution)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"TextureImporter missing: {path}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;
            importer.maxTextureSize = resolution;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            ConfigurePlatform(importer, "Android", resolution);
            ConfigurePlatform(importer, "iPhone", resolution);
            importer.SaveAndReimport();
        }

        private static void ConfigurePlatform(TextureImporter importer, string platform, int resolution)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = resolution;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
        }

        private static string GetOutputFolderPath(
            DefenderPortraitBakeProfile profile,
            List<string> errors)
        {
            if (profile == null)
            {
                errors.Add("Bake profile is null.");
                return null;
            }
            if (profile.OutputFolder == null)
            {
                errors.Add("Output folder is null.");
                return null;
            }

            string path = AssetDatabase.GetAssetPath(profile.OutputFolder);
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !AssetDatabase.IsValidFolder(path))
            {
                errors.Add($"Output folder is invalid: {path}");
                return null;
            }
            return path.TrimEnd('/');
        }

        private static string OutputPath(string folder, string id)
        {
            return $"{folder}/{FilePrefix}{id}.png";
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Unity project root could not be resolved.");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static bool IsSafeId(string id)
        {
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '_')
                    return false;
            }
            return true;
        }
    }
}
