using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            // 거리는 레시피 fov 로 풀고 렌더는 클램프값 — 레시피 25 < fovMin 31 이면 화면엔 31 이 걸린다
            // (main 93c1cc4c «저작 화각이 실제로 걸리게» 가 71688c0a 로 되돌려진 상태). 씬도 같은 값을 쓴다.
            float fovApplied = Mathf.Clamp(fov, config.fovMin, config.fovMax);
            Undo.RecordObject(cam.transform, "Frame battle camera");
            Undo.RecordObject(cam, "Frame battle camera");
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fovApplied;
            cam.nearClipPlane = 0.1f;   // BattleScene Main Camera 와 동일
            cam.farClipPlane = 100f;

            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            return $"OK|stage={stage.name} area={stage.playAreaCells} origin={stage.gridOriginLocal} " +
                   $"aspect={aspect:F3} pitch={framing.pitchDeg} fov={fovApplied}(recipe {fov}) pos={pos:F3} dof={(framing.dofEnabled ? "recipe on (씬 Volume 없으면 미적용)" : "off")}";
        }

        // 러너용: 씬 열기 → 적용 → 저장.
        public static string FrameScene(string scenePath, float? aspectOverride = null)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string result = FrameActiveScene(aspectOverride);
            if (result.StartsWith("OK")) EditorSceneManager.SaveScene(scene);
            return result;
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
