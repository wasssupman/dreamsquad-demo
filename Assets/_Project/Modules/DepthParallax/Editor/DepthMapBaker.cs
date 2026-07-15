using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wassup.DepthParallax.Editor
{
    // depth-parallax unit 4 — 오프라인에서 만든 뎁스 PNG 를 뎁스 패럴랙스 셰이더가
    // 기대하는 임포트 설정의 Texture2D 로 강제하는 Editor 유틸.
    //
    // 이 클래스는 파이썬을 실행하지 않는다. bake 는 `Tools~/depth_bake.py` 로 만든
    // 오프라인 산물이고, 여기서는 그 PNG 들의 TextureImporter 설정만 교정한다:
    //   textureType = Default (Sprite 아님 → 스프라이트 아틀라스 후보가 아예 아님),
    //   sRGBTexture = false (linear — 뎁스는 색이 아니라 수치),
    //   mipmapEnabled = false, textureCompression = Uncompressed,
    //   filterMode = Bilinear, wrapMode = Clamp, alphaSource = None,
    //   default 플랫폼 format = R8 (단일 채널 8bit).
    //
    // non-atlased full-rect 근거: 셰이더가 plain [0,1] UV 로 샘플하므로 아틀라스 UV
    // remap 이 끼면 안 된다. Default 타입 텍스처는 스프라이트 아틀라스에 팩되지 않아
    // 항상 [0,1] 전체 rect 로 샘플된다 — Sprite 타입만 아니면 자동으로 만족된다.
    public static class DepthMapBaker
    {
        // 뎁스 텍스처가 CPU readback 을 필요로 하지 않는다(GPU 샘플 전용). 메모리 절약.
        private const bool ReadableIntent = false;

        // ── 진입점 1: 폴더 선택 다이얼로그 ────────────────────────────────
        // 프로젝트 밖에서 bake 한 PNG 폴더를 Assets 안으로 복사한 뒤 이 메뉴로 일괄 교정.
        [MenuItem("Wassup/Depth Parallax/Import Baked Depth Folder...")]
        public static void ImportBakedDepthFolder()
        {
            string startDir = Application.dataPath;
            string absFolder = EditorUtility.OpenFolderPanel(
                "Select baked depth PNG folder (must be inside Assets/)", startDir, "");
            if (string.IsNullOrEmpty(absFolder))
                return; // 취소

            if (!TryAbsoluteToAssetPath(absFolder, out string assetFolder))
            {
                EditorUtility.DisplayDialog("Depth Map Baker",
                    "선택한 폴더가 프로젝트 Assets/ 밖이다. 먼저 Assets 안으로 복사해라.", "OK");
                return;
            }

            // 하위 포함 모든 .png 수집(절대 경로 → asset 경로).
            var pngAbs = Directory.GetFiles(absFolder, "*.png", SearchOption.AllDirectories);
            var assetPaths = new List<string>(pngAbs.Length);
            foreach (var abs in pngAbs)
            {
                if (TryAbsoluteToAssetPath(abs, out string ap))
                    assetPaths.Add(ap);
            }

            ApplyToAssetPaths(assetPaths);
        }

        // ── 진입점 2: Project 창에서 선택한 텍스처/폴더 우클릭 ──────────────
        [MenuItem("Assets/Wassup/Apply Depth Import Settings", false, 2000)]
        public static void ApplyToSelection()
        {
            var assetPaths = new List<string>();
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    // 폴더면 그 안의 모든 Texture2D 를 대상으로.
                    foreach (string texGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                        assetPaths.Add(AssetDatabase.GUIDToAssetPath(texGuid));
                }
                else
                {
                    assetPaths.Add(path);
                }
            }

            ApplyToAssetPaths(assetPaths);
        }

        [MenuItem("Assets/Wassup/Apply Depth Import Settings", true)]
        private static bool ApplyToSelectionValidate()
        {
            // 텍스처나 폴더가 하나라도 선택돼 있으면 활성화.
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (AssetDatabase.IsValidFolder(path))
                    return true;
                if (AssetImporter.GetAtPath(path) is TextureImporter)
                    return true;
            }
            return false;
        }

        // ── 코어: asset 경로 목록에 설정 강제 + 재임포트 ───────────────────
        private static void ApplyToAssetPaths(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths == null || assetPaths.Count == 0)
            {
                Debug.Log("[DepthMapBaker] 대상 텍스처가 없다.");
                return;
            }

            var log = new StringBuilder();
            int applied = 0, skipped = 0;

            // 배치 재임포트: 개별 SaveAndReimport 를 묶어 부분 리임포트/도메인 튐 억제.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string assetPath in assetPaths)
                {
                    if (ApplyDepthImportSettings(assetPath, log))
                        applied++;
                    else
                        skipped++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            log.Insert(0, $"[DepthMapBaker] applied={applied} skipped={skipped}\n");
            Debug.Log(log.ToString());
        }

        // 단일 텍스처에 뎁스 임포트 설정을 강제한다. 실제로 바꾼 게 있을 때만 재임포트.
        // return: 적용 대상이었으면 true, 텍스처가 아니면 false.
        public static bool ApplyDepthImportSettings(string assetPath, StringBuilder log = null)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                log?.AppendLine($"  skip (not a texture): {assetPath}");
                return false;
            }

            // 뎁스는 색이 아니라 수치 필드 → Default + linear + no-mip + 무압축.
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;            // linear
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = ReadableIntent;    // GPU 샘플 전용
            importer.npotScale = TextureImporterNPOTScale.None; // half-res 원본 크기 보존
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // default 플랫폼을 R8(단일 채널 8bit) 무압축으로 강제.
            // default 를 R8 로 두면 플랫폼별 override 가 없는 한 전 플랫폼에서 R8.
            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.R8;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);

            importer.SaveAndReimport();
            log?.AppendLine($"  R8/linear/no-mip/uncompressed/clamp: {assetPath}");
            return true;
        }

        // 절대 경로를 "Assets/..." 프로젝트 상대 경로로. Assets 밖이면 false.
        private static bool TryAbsoluteToAssetPath(string absolutePath, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrEmpty(absolutePath))
                return false;

            // 슬래시 정규화(macOS/Windows 혼용 안전).
            string abs = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/'); // .../Assets

            if (abs == dataPath)
            {
                assetPath = "Assets";
                return true;
            }
            if (!abs.StartsWith(dataPath + "/"))
                return false;

            assetPath = "Assets" + abs.Substring(dataPath.Length);
            return true;
        }
    }
}
