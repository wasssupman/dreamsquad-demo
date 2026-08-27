using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.EditorTools
{
    // map-diorama-stage — 테스트 씬의 카메라를 «배틀 시작 시 전투 상태» 포즈로 맞춘다.
    // 런타임과 같은 산식(CameraFramingMath.SolveStatePose + CameraDirectionConfig 의 Battle 레시피)을
    // 쓰고, 보드 바운즈는 브리지 계약(스테이지 루트 = 원점·무회전·스케일 1, BattleBridge 스테이지
    // 인스턴스화 참조) 아래에서 gridOriginLocal + playAreaCells 로 결정한다 — 그래서 씬 카메라는
    // 스테이지 격자만의 함수가 되고, 씬마다 손으로 맞출 게 없다. DoF 는 씬에 Volume 이 있을 때만.
    public static class MapStageCameraFraming
    {
        const string ConfigPath = "Assets/_Project/Data/Camera/CameraDirectionConfig.asset";

        [MenuItem("Window/Wassup/Map Stage/Frame Scene Camera As Battle")]
        public static void FrameOpenScene() => Debug.Log(FrameActiveScene());

        // 열린 씬에 적용. 결과 요약 문자열을 돌려준다(러너 태스크가 파일에 쓴다).
        public static string FrameActiveScene(float? aspectOverride = null)
        {
            var stage = Object.FindAnyObjectByType<MapStage>();
            if (stage == null) return "ERROR|씬에 MapStage 없음";
            var cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            if (cam == null) return "ERROR|씬에 Camera 없음";
            var config = AssetDatabase.LoadAssetAtPath<CameraDirectionConfig>(ConfigPath);
            var framing = config?.stateFramings?.FirstOrDefault(f => f.state == CameraState.Battle);
            if (framing == null) return "ERROR|CameraDirectionConfig 의 Battle 레시피 없음";

            // 1) 루트 정규화 — 브리지 계약과 동일(원점·무회전·스케일 1).
            Undo.RecordObject(stage.transform, "Normalize stage root");
            stage.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            stage.transform.localScale = Vector3.one;
            // 격자 원점 xz = 0 (관례) — 아트·마커를 함께 옮겨 셀↔아트 관계는 그대로. 이제 보드 = [0,w]×[0,h].
            MapStageEditorUtil.NormalizeGridOrigin(stage);

            // 2) 보드 바운즈 = 격자 월드 rect (AlignGridTo 뒤 TryGetPlayfieldWorldBounds 와 같은 값).
            Vector3 min = stage.gridOriginLocal;
            Vector3 size = new Vector3(stage.playAreaCells.x, 0f, stage.playAreaCells.y);
            var bounds = new Bounds(min + size * 0.5f, size);

            // 3) 런타임 산식 그대로. aspect 는 게임뷰(폰 세로/가로에 따라 fit 거리가 달라진다).
            float aspect = aspectOverride ?? cam.aspect;
            if (!CameraFramingMath.SolveStatePose(bounds.center, framing, bounds, aspect,
                    out var pos, out var rot, out var fov))
                return "ERROR|SolveStatePose 실패";

            // 런타임 미러: CameraDirector.ComposeAndWrite 는 최종 fov 를 [fovMin, fovMax] 로 클램프한다.
            // 거리는 레시피 fov 로 풀고 렌더는 클램프값 — 레시피가 fovMin 아래면 화면엔 fovMin 이 걸린다
            // (main 93c1cc4c «저작 화각이 실제로 걸리게» 가 71688c0a 로 되돌려진 상태). 씬도 같은 값을 쓴다.
            // fovMin 은 2026-08-26 에 31 → 24 로 내려갔다(플레이 중 손으로 잡은 전투 포즈를 레시피로
            // 역산해 넣으면서). 그래도 **레시피 값을 여기서 읽고 판단하지 말 것** — 저작은 계속
            // 움직인다(2026-08-27 현재 Battle 23.8 < fovMin 24 라 화면엔 24 가 걸린다: 0.2° 차이).
            // 저작 화각을 실제로 걸고 싶으면 fovMin 을 그 아래로 내리는 것이 유일한 레버다.
            float fovApplied = Mathf.Clamp(fov, config.fovMin, config.fovMax);
            Undo.RecordObject(cam.transform, "Frame battle camera");
            Undo.RecordObject(cam, "Frame battle camera");
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fovApplied;
            cam.nearClipPlane = 0.1f;   // BattleScene Main Camera 와 동일
            cam.farClipPlane = 100f;

            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            return $"OK|stage={stage.name} area={stage.playAreaCells} origin={stage.gridOriginLocal} " +
                   $"aspect={aspect:F3} pitch={framing.pitchDeg} fov={fovApplied}(recipe {fov}) pos={pos:F3}";
        }

        // 러너용: 씬 열기 → 적용 → 저장.
        public static string FrameScene(string scenePath, float? aspectOverride = null)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string result = FrameActiveScene(aspectOverride);
            if (result.StartsWith("OK")) EditorSceneManager.SaveScene(scene);
            return result;
        }

        // unit 11 — 프리팹을 프리뷰 씬에 세워 Battle 카메라 포즈로 PNG 렌더(원격 육안 검증 — 에이전트가 이미지로 읽는다).
        // 논리 셀 오버레이(스캔→검증 결과 그대로): 차단=빨강 · 배치금지=주황 · 스폰=초록 · 골=노랑 · 포탈=핑크 ·
        // 본능=파랑/빨강 3×3. 조명은 프리뷰용 1개 — 색감이 아니라 «아트와 격자가 맞는가»를 보는 도구다.
        public static string RenderPrefabPreview(string prefabPath, string pngPath, float aspect = 16f / 9f, int width = 1600, bool overlay = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "ERROR|프리팹 없음 " + prefabPath;
            var config = AssetDatabase.LoadAssetAtPath<CameraDirectionConfig>(ConfigPath);
            var framing = config?.stateFramings?.FirstOrDefault(f => f.state == CameraState.Battle);
            if (framing == null) return "ERROR|CameraDirectionConfig 의 Battle 레시피 없음";

            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                inst.transform.localScale = Vector3.one;
                var stage = inst.GetComponent<MapStage>();
                if (stage == null) return "ERROR|루트에 MapStage 없음";

                var lightGo = new GameObject("PreviewLight");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var camGo = new GameObject("PreviewCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;

                Vector3 min = stage.gridOriginLocal;
                Vector3 size = new Vector3(stage.playAreaCells.x, 0f, stage.playAreaCells.y);
                var bounds = new Bounds(min + size * 0.5f, size);
                if (!CameraFramingMath.SolveStatePose(bounds.center, framing, bounds, aspect, out var pos, out var rot, out var fov))
                    return "ERROR|SolveStatePose 실패";
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = Mathf.Clamp(fov, config.fovMin, config.fovMax);

                string overlayInfo = overlay ? DrawCellOverlay(stage, scene) : string.Empty;

                int height = Mathf.RoundToInt(width / aspect);
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.aspect = aspect;
                cam.Render();
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath)));
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                return $"OK|{pngPath} {width}x{height} pos={pos:F2} fov={cam.fieldOfView:F1} {overlayInfo}";
            }
            finally
            {
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static string DrawCellOverlay(MapStage stage, Scene scene)
        {
            var scan = MapStageScanner.Scan(stage, 1f);
            var errors = DioramaMapBuilder.Validate(scan);
            if (errors.Count > 0) return "형식오류:" + string.Join(" ; ", errors);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return "overlay 생략(URP Unlit 셰이더 없음)";
            var parent = new GameObject("CellOverlay");
            SceneManager.MoveGameObjectToScene(parent, scene);
            float y = stage.gridOriginLocal.y + 0.03f;
            int count = 0;
            void Cell(Vector2Int c, Color col, float inset)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(parent.transform, false);
                q.transform.localPosition = new Vector3(c.x + 0.5f, y, c.y + 0.5f);
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = new Vector3(1f - inset * 2f, 1f - inset * 2f, 1f);
                var m = new Material(shader);
                m.SetColor("_BaseColor", col);
                q.GetComponent<MeshRenderer>().sharedMaterial = m;
                count++;
            }
            foreach (var r in scan.blockedRects)
                for (int x = r.xMin; x < r.xMax; x++) for (int yy = r.yMin; yy < r.yMax; yy++) Cell(new Vector2Int(x, yy), new Color(1f, 0.2f, 0.2f), 0.1f);
            foreach (var r in scan.placementBlockRects)
                for (int x = r.xMin; x < r.xMax; x++) for (int yy = r.yMin; yy < r.yMax; yy++) Cell(new Vector2Int(x, yy), new Color(1f, 0.6f, 0.1f), 0.42f);
            foreach (var s in scan.spawns) Cell(s.cell, new Color(0.2f, 1f, 0.3f), 0.1f);
            foreach (var g in scan.goals) Cell(g, new Color(1f, 0.95f, 0.2f), 0.1f);
            foreach (var b in scan.bonusSpawns) Cell(b, new Color(1f, 0.3f, 0.8f), 0.1f);
            foreach (var st in scan.structures)
            {
                int half = StructurePlacements.InstinctFootprint / 2;
                var col = st.side == StructureSide.Defender ? new Color(0.3f, 0.55f, 1f) : new Color(1f, 0.35f, 0.3f);
                for (int dy = -half; dy <= half; dy++)
                    for (int dx = -half; dx <= half; dx++)
                        Cell(st.cell + new Vector2Int(dx, dy), col, dx == 0 && dy == 0 ? 0.1f : 0.3f);
            }
            // 격자 네 모서리 — 판 경계가 아트와 맞는지.
            var w = stage.playAreaCells.x; var h = stage.playAreaCells.y;
            foreach (var c in new[] { new Vector2Int(0, 0), new Vector2Int(w - 1, 0), new Vector2Int(0, h - 1), new Vector2Int(w - 1, h - 1) })
                Cell(c, Color.white, 0.3f);
            return $"overlay cells={count} blocked={scan.blockedRects.Count} zones={scan.placementBlockRects.Count} spawns={scan.spawns.Count} goals={scan.goals.Count} portals={scan.bonusSpawns.Count} structures={scan.structures.Count}";
        }

        // 프리팹 에셋을 관례대로 — 루트 원점 + 격자 원점 xz 0 (아트·마커를 함께 옮겨 내부 배치 불변).
        public static string NormalizePrefabRoot(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var before = root.transform.localPosition;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var stage = root.GetComponent<MapStage>();
                var originBefore = stage != null ? stage.gridOriginLocal : Vector3.zero;
                if (stage != null) MapStageEditorUtil.NormalizeGridOrigin(stage);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return $"OK|root {before:F2} → 0, gridOrigin {originBefore:F3} → {(stage != null ? stage.gridOriginLocal : Vector3.zero):F3}";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
