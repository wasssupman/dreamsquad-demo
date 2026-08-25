using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.EditorTools
{
    // map-diorama-stage unit 0 — 스테이지 저작 인스펙터: 바운즈 제안 버튼 + 셀 스냅.
    // 제안은 초안일 뿐 선언이 정본이다(D6) — 버튼은 필드를 채워줄 뿐 어떤 것도 강제하지 않는다.
    internal static class MapStageEditorUtil
    {
        internal static bool TryGetStage(Component c, out MapStage stage)
        {
            stage = c.GetComponentInParent<MapStage>();
            if (stage == null)
                EditorGUILayout.HelpBox("부모 계층에 MapStage 가 없다 — 스테이지 프리팹 안에 배치할 것.", MessageType.Warning);
            return stage != null;
        }

        // 월드 bounds 의 8 모서리를 스테이지 로컬로 변환해 min/max — 스테이지가 회전해 있어도 안전.
        internal static bool TryGetLocalRendererBounds(MapStage stage, Component root, out Vector3 min, out Vector3 max)
        {
            min = max = default;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            bool first = true;
            foreach (var r in renderers)
            {
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 local = stage.transform.InverseTransformPoint(corner);
                    if (first) { min = max = local; first = false; }
                    else { min = Vector3.Min(min, local); max = Vector3.Max(max, local); }
                }
            }
            return true;
        }

        internal static void SnapToCellCenter(Component c, MapStage stage)
        {
            Vector3 local = stage.transform.InverseTransformPoint(c.transform.position);
            Vector2Int cell = MapStageMath.LocalToCell(local, stage.gridOriginLocal, stage.previewTileSize);
            Vector3 center = MapStageMath.CellCenterLocal(cell, stage.gridOriginLocal, stage.previewTileSize);
            Undo.RecordObject(c.transform, "Snap To Cell Center");
            // XZ 만 스냅 — 프랍의 로컬 높이(바닥 위 얹힘)는 저작값이라 건드리지 않는다.
            c.transform.position = stage.transform.TransformPoint(new Vector3(center.x, local.y, center.z));
        }

        internal static void SnapButton(Component c)
        {
            if (GUILayout.Button("셀 중심에 스냅") && c.GetComponentInParent<MapStage>() is { } stage)
                SnapToCellCenter(c, stage);
        }
    }

    [CustomEditor(typeof(MapStage))]
    internal class MapStageEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var stage = (MapStage)target;
            if (GUILayout.Button("자식 렌더러 바운즈에서 playArea 제안"))
            {
                if (MapStageEditorUtil.TryGetLocalRendererBounds(stage, stage, out var min, out var max))
                {
                    float t = stage.previewTileSize;
                    Undo.RecordObject(stage, "Suggest PlayArea");
                    // Y 는 저작값 유지 — 논리 평면 높이는 바운즈로 추정하지 않는다(바닥 두께/프랍 높이가 섞인다).
                    stage.gridOriginLocal = new Vector3(min.x, stage.gridOriginLocal.y, min.z);
                    stage.playAreaCells = Vector2Int.Max(Vector2Int.one, new Vector2Int(
                        Mathf.CeilToInt((max.x - min.x) / t),
                        Mathf.CeilToInt((max.z - min.z) / t)));
                    EditorUtility.SetDirty(stage);
                }
                else Debug.LogWarning("[MapStage] 자식 렌더러가 없어 playArea 를 제안할 수 없다.");
            }

            // unit 2 — «스크립트 붙이면 그 자체로 게임 진행 가능»의 마지막 마일: 풀 수동 편집 없이
            // dev 슬롯에 등록해 DevMapOverride 스테퍼로 바로 Play (MapPainter dev 등록 선례 승계).
            if (GUILayout.Button("Dev 엔트리로 등록 (MapStagePool)"))
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(stage) ?? stage;
                if (!EditorUtility.IsPersistent(prefab))
                {
                    // 씬 오브젝트를 에셋(풀)에 넣으면 저장 시 참조가 조용히 null 이 된다 — 프리팹만 허용.
                    Debug.LogWarning("[MapStage] 프리팹이 아니다 — 먼저 프리팹으로 저장한 뒤 등록할 것.");
                    return;
                }
                var guids = AssetDatabase.FindAssets("t:MapStagePool");
                if (guids.Length == 0) { Debug.LogWarning("[MapStage] MapStagePool 에셋이 없다."); return; }
                if (guids.Length > 1) Debug.LogWarning($"[MapStage] MapStagePool 이 {guids.Length}개 — 첫 번째에만 등록한다.");
                var pool = AssetDatabase.LoadAssetAtPath<Wassup.Data.MapStagePool>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                if (pool.EditorRegisterDevStage(prefab))
                {
                    EditorUtility.SetDirty(pool);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[MapStage] '{prefab.name}' 을 dev 슬롯에 등록했다.");
                }
                else Debug.Log($"[MapStage] '{prefab.name}' 은 이미 풀에 있다 — 등록 생략.");
            }
        }
    }

    [CustomEditor(typeof(PropFootprint))]
    internal class PropFootprintEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var fp = (PropFootprint)target;
            if (!MapStageEditorUtil.TryGetStage(fp, out var stage)) return;

            if (GUILayout.Button("렌더러 바운즈에서 footprint 제안"))
            {
                if (MapStageEditorUtil.TryGetLocalRendererBounds(stage, fp, out var min, out var max))
                {
                    float t = stage.previewTileSize;
                    Vector2Int anchor = MapStageMath.LocalToCell(
                        stage.transform.InverseTransformPoint(fp.transform.position), stage.gridOriginLocal, t);
                    Vector2Int minCell = MapStageMath.LocalToCell(min, stage.gridOriginLocal, t);
                    // max 가 셀 경계선 위에 정확히 얹히면 다음 셀로 새므로 미세 epsilon 을 뺀다.
                    Vector2Int maxCell = MapStageMath.LocalToCell(max - new Vector3(1e-4f, 0f, 1e-4f), stage.gridOriginLocal, t);
                    Undo.RecordObject(fp, "Suggest Footprint");
                    fp.size = Vector2Int.Max(Vector2Int.one, maxCell - minCell + Vector2Int.one);
                    fp.anchorOffset = minCell - anchor;
                    EditorUtility.SetDirty(fp);
                }
                else Debug.LogWarning("[PropFootprint] 렌더러가 없어 footprint 를 제안할 수 없다.");
            }
            MapStageEditorUtil.SnapButton(fp);
        }
    }

    [CustomEditor(typeof(SpawnMarker))]
    internal class SpawnMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            MapStageEditorUtil.SnapButton((Component)target);
        }
    }

    [CustomEditor(typeof(GoalMarker))]
    internal class GoalMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            MapStageEditorUtil.SnapButton((Component)target);
        }
    }

    [CustomEditor(typeof(RouteMarker))]
    internal class RouteMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            MapStageEditorUtil.SnapButton((Component)target);
        }
    }

    [CustomEditor(typeof(BonusSpawnMarker))]
    internal class BonusSpawnMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("보너스 포탈 칸 — 맵에 0개 또는 정확히 2개. 통행 가능하고 골에 닿는 칸이어야 한다.", MessageType.Info);
            MapStageEditorUtil.SnapButton((Component)target);
        }
    }

    [CustomEditor(typeof(PlacementBlockZone))]
    internal class PlacementBlockZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            MapStageEditorUtil.SnapButton((Component)target);
        }
    }
}
