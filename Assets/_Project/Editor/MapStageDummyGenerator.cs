using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.EditorTools
{
    // map-diorama-stage unit 5 — KayKit 파일럿 스테이지 절차 조립기 (레이아웃 수정 → 재생성 반복용).
    // 프리팹 확정 후에도 재현용으로 유지한다. 바닥 윗면 = 논리 Y0 보정은 렌더러 바운즈 실측(저작 시 1회).
    public static class MapStageDummyGenerator
    {
        const string KayKit = "Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Prefabs";
        const string PilotPath = "Assets/_Project/Prefabs/Maps/MapStage_Pilot.prefab";

        [MenuItem("Window/Wassup/Map Stage/Generate Pilot Stage")]
        public static void GeneratePilot()
        {
            var root = new GameObject("MapStage_Pilot");
            try
            {
                var stage = root.AddComponent<MapStage>();
                stage.playAreaCells = new Vector2Int(16, 10);
                stage.gridOriginLocal = new Vector3(0f, 0.02f, 0f);
                stage.previewTileSize = 1f;
                stage.suppressEffectTiles = false;   // 본편 맵 — 효과 타일 허용

                // 바닥: 4×4 판 타일링 (16×10 → 마지막 행은 2셀 겹침 허용: min 모서리 기준 배치라 무해)
                for (int gy = 0; gy < 10; gy += 4)
                for (int gx = 0; gx < 16; gx += 4)
                    PlaceGround(root.transform, new Vector2Int(gx, Mathf.Min(gy, 6)));

                // 컬트오브램식 닫힌 마당 — 남/북 가장자리 barrier 링 (동서는 스폰/골 열어둠)
                for (int x = 0; x < 16; x += 2)
                {
                    Blocker(root, $"ring_s_{x}", "neutral/barrier_2x1x1.prefab", new Vector2Int(x, 0), new Vector2Int(2, 1));
                    Blocker(root, $"ring_n_{x}", "neutral/barrier_2x1x1.prefab", new Vector2Int(x, 9), new Vector2Int(2, 1));
                }
                // 내부 차단 — 동선을 조각하는 프랍들
                Blocker(root, "pillar_a", "neutral/pillar_2x2x2.prefab", new Vector2Int(5, 3), new Vector2Int(2, 2));
                Blocker(root, "pillar_b", "neutral/pillar_2x2x2.prefab", new Vector2Int(9, 6), new Vector2Int(2, 2));
                Blocker(root, "crate", "neutral/barrier_1x1x2.prefab", new Vector2Int(12, 3), Vector2Int.one);
                Marker<SpawnMarker>(root, "spawn0", "green/flag_A_green.prefab", new Vector2Int(0, 3), m => m.laneIndex = 0);
                Marker<SpawnMarker>(root, "spawn1", "green/flag_A_green.prefab", new Vector2Int(0, 6), m => m.laneIndex = 1);
                Marker<GoalMarker>(root, "goal", "neutral/signage_finish.prefab", new Vector2Int(15, 5), _ => { });

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PilotPath);

                // 풀 dev 슬롯 등록 (라이브 entries 는 건드리지 않는다 — 무핀 테스트의 기본판 보존)
                var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>("Assets/_Project/Data/Maps/MapStagePool.asset");
                if (pool != null && pool.EditorRegisterDevStage(prefab.GetComponent<MapStage>()))
                {
                    EditorUtility.SetDirty(pool);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log($"[MapStageDummyGenerator] 파일럿 생성 완료: {PilotPath}");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static GameObject Load(string rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{KayKit}/{rel}");
            if (go == null) throw new System.InvalidOperationException($"KayKit 프리팹 없음: {rel}");
            return go;
        }

        static void PlaceGround(Transform root, Vector2Int minCell)
        {
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(Load("green/platform_4x4x1_green.prefab"));
            piece.transform.SetParent(root, false);
            var rs = piece.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            piece.transform.localPosition = new Vector3(minCell.x - b.min.x, -b.max.y, minCell.y - b.min.z);
        }

        static void Blocker(GameObject root, string name, string rel, Vector2Int cell, Vector2Int size)
            => Marker<PropFootprint>(root, name, rel, cell, f => f.size = size);

        static void Marker<T>(GameObject root, string name, string rel, Vector2Int cell, System.Action<T> init)
            where T : Component
        {
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(Load(rel));
            visual.transform.SetParent(host.transform, false);
            init(host.AddComponent<T>());
        }
    }
}
