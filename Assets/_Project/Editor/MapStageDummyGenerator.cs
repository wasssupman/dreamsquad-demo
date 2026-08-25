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

        // 구 MapDocument_Duel(21×12, git ba70aaab~1)의 지형 충실 카피 — 열린 마당 스타일이라
        // 거의 완전 등가: Deco 테두리→차단, Env 강→차단(공중 통과·배치 불가 동일), placeMask 04
        // 전선→BlockZone, 마음 파생 스폰(18,4)/(18,6), 골(2,5). 비가용(계약 11): 본능 공격·공성 모드
        // — 본능은 3×3 차단+시각 프랍, 마음은 배치 금지 1칸+장식(원본도 마음은 통행 비차단).
        // 공중 경유점 (10,5)는 강 위라 형식 검증에 걸려 북쪽 다리 (10,2)로 근사 (후속 후보:
        // 차단 셀 위 공중 waypoint 허용).
        [MenuItem("Window/Wassup/Map Stage/Generate Duel Classic Stage")]
        public static void GenerateDuelClassic()
        {
            var root = new GameObject("MapStage_DuelClassic");
            try
            {
                var stage = root.AddComponent<MapStage>();
                stage.playAreaCells = new Vector2Int(21, 12);
                stage.gridOriginLocal = new Vector3(0f, 0.02f, 0f);
                stage.previewTileSize = 1f;
                stage.suppressEffectTiles = false;

                for (int gy = 0; gy < 12; gy += 4)
                for (int gx = 0; gx < 21; gx += 4)
                    PlaceGround(root.transform, new Vector2Int(gx, Mathf.Min(gy, 8)));

                // 테두리 (원본 Deco 4 rect)
                BlockerRect(root, "wall_n", new RectInt(0, 0, 21, 1), "neutral/barrier_4x1x1.prefab", 4);
                BlockerRect(root, "wall_w", new RectInt(0, 1, 1, 11), "neutral/barrier_1x1x1.prefab", 2);
                BlockerRect(root, "wall_e", new RectInt(20, 1, 1, 11), "neutral/barrier_1x1x1.prefab", 2);
                BlockerRect(root, "wall_s", new RectInt(1, 11, 19, 1), "neutral/barrier_4x1x1.prefab", 4);
                // 강 (원본 Env 3 rect — 다리 2개: y2~3, y8~9)
                BlockerRect(root, "river_a", new RectInt(10, 1, 1, 1), "blue/barrier_1x1x1_blue.prefab", 1);
                BlockerRect(root, "river_b", new RectInt(10, 4, 1, 4), "blue/barrier_1x1x1_blue.prefab", 1);
                BlockerRect(root, "river_c", new RectInt(10, 10, 1, 1), "blue/barrier_1x1x1_blue.prefab", 1);
                // 본능 4기 — 시각+3×3 차단 (footprint 가 원본 CloseCellLayers/동적 벽을 근사)
                foreach (var (cell, name) in new[] {
                    (new Vector2Int(4, 3), "instinct_ally_a"), (new Vector2Int(4, 8), "instinct_ally_b"),
                    (new Vector2Int(16, 3), "instinct_enemy_a"), (new Vector2Int(16, 8), "instinct_enemy_b") })
                    Marker<PropFootprint>(root, name, "neutral/structure_A.prefab", cell,
                        f => { f.size = new Vector2Int(3, 3); f.anchorOffset = new Vector2Int(-1, -1); });
                // 적 마음 — 원본은 통행 비차단·배치만 금지 → BlockZone 1칸 + 장식
                Marker<PlacementBlockZone>(root, "enemy_heart", "neutral/structure_C.prefab",
                    new Vector2Int(18, 5), z => z.size = Vector2Int.one);
                // 전선 — 적 진영 5×10 배치 금지 (원본 placeMask 04 구역)
                Marker<PlacementBlockZone>(root, "frontline", "neutral/sign.prefab",
                    new Vector2Int(15, 1), z => z.size = new Vector2Int(5, 10));

                Marker<SpawnMarker>(root, "spawn0", "green/flag_A_green.prefab", new Vector2Int(18, 4), m => m.laneIndex = 0);
                Marker<SpawnMarker>(root, "spawn1", "green/flag_A_green.prefab", new Vector2Int(18, 6), m => m.laneIndex = 1);
                Marker<GoalMarker>(root, "goal", "neutral/signage_finish.prefab", new Vector2Int(2, 5), _ => { });
                Marker<RouteMarker>(root, "route_0_0", "neutral/sign.prefab", new Vector2Int(10, 2),
                    r => { r.routeIndex = 0; r.order = 0; });

                var prefab = PrefabUtility.SaveAsPrefabAsset(root,
                    "Assets/_Project/Prefabs/Maps/MapStage_DuelClassic.prefab");
                var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>("Assets/_Project/Data/Maps/MapStagePool.asset");
                if (pool != null && pool.EditorRegisterDevStage(prefab.GetComponent<MapStage>()))
                {
                    EditorUtility.SetDirty(pool);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[MapStageDummyGenerator] DuelClassic 생성 완료");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // unit 9 — 기존 스테이지 프리팹에 보너스 포탈 마커 2개를 저작한다(비주얼 없음 — 포탈 뷰는
        // BattleBridge.BonusWave 가 웨이브 수명으로 띄운다). 이미 있으면 갈아끼운다(멱등).
        public static void AuthorBonusPortals(string prefabPath, Vector2Int a, Vector2Int b)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var old in root.GetComponentsInChildren<BonusSpawnMarker>(true))
                    Object.DestroyImmediate(old.gameObject);
                Portal(root, "bonus_portal_0", a);
                Portal(root, "bonus_portal_1", b);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log($"[MapStageDummyGenerator] 보너스 포탈 저작: {prefabPath} {a} {b}");
        }

        // 사용자 저작 스테이지에 스폰 2 + 골 1 마커를 넣고(비주얼 없음 — 기즈모로 확인) dev 슬롯 등록.
        // 기존 스폰/골 마커는 갈아끼운다(멱등). 비주얼을 얹으려면 마커 호스트의 자식으로 프리팹을 넣으면 된다.
        public static void AuthorSpawnsAndGoal(string prefabPath, Vector2Int spawn0, Vector2Int spawn1, Vector2Int goal)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var old in root.GetComponentsInChildren<SpawnMarker>(true)) Object.DestroyImmediate(old.gameObject);
                foreach (var old in root.GetComponentsInChildren<GoalMarker>(true)) Object.DestroyImmediate(old.gameObject);
                Host(root, "spawn0", spawn0).AddComponent<SpawnMarker>().laneIndex = 0;
                Host(root, "spawn1", spawn1).AddComponent<SpawnMarker>().laneIndex = 1;
                Host(root, "goal", goal).AddComponent<GoalMarker>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var stage = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)?.GetComponent<MapStage>();
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>("Assets/_Project/Data/Maps/MapStagePool.asset");
            if (stage != null && pool != null && pool.EditorRegisterDevStage(stage))
            {
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[MapStageDummyGenerator] 스폰/골 저작: {prefabPath} S0{spawn0} S1{spawn1} G{goal}");
        }

        // 마커 호스트 — 스테이지 로컬 격자 기준 셀 중심(gridOriginLocal 반영).
        static GameObject Host(GameObject root, string name, Vector2Int cell)
        {
            var stage = root.GetComponent<MapStage>();
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = stage.gridOriginLocal + new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            return host;
        }

        static void Portal(GameObject root, string name, Vector2Int cell)
        {
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            host.AddComponent<BonusSpawnMarker>();
        }

        // rect 차단: 호스트 1개(footprint = rect 전체) + 시각 프랍을 step 간격으로 자식 배치.
        static void BlockerRect(GameObject root, string name, RectInt rect, string visualRel, int visualStep)
        {
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = new Vector3(rect.xMin + 0.5f, 0f, rect.yMin + 0.5f);
            var fp = host.AddComponent<PropFootprint>();
            fp.size = new Vector2Int(rect.width, rect.height);
            for (int y = 0; y < rect.height; y += Mathf.Max(1, rect.width == 1 ? visualStep : 1))
            for (int x = 0; x < rect.width; x += Mathf.Max(1, rect.width == 1 ? 1 : visualStep))
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(Load(visualRel));
                visual.transform.SetParent(host.transform, false);
                visual.transform.localPosition = new Vector3(x, 0f, y);
            }
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
