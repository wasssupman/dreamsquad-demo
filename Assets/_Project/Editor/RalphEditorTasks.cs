using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.EditorTools
{
    // map-diorama-stage 작업용 일회성 에디터 태스크 러너 (RalphTestRunner 와 같은 파일 프로토콜).
    // .omc/ralph/editor_task_request.json 에 {token, task} 를 쓰면 다음 도메인 리로드 때 실행하고
    // editor_task_result_<token>.txt 에 결과를 쓴다. spec unit 7 은퇴 대상.
    [InitializeOnLoad]
    internal static class RalphEditorTasks
    {
        const string Dir = ".omc/ralph";
        const string RequestPath = Dir + "/editor_task_request.json";
        const string TokenPath = Dir + "/editor_task_done_token.txt";
        const string KayKitPrefabs =
            "Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Prefabs";

        [Serializable]
        class Request { public string token; public string task; }

        static double _nextPollAt;

        static RalphEditorTasks()
        {
            EditorApplication.delayCall += TryRun;
            // 리로드 없이 요청 파일만 갱신되는 경우 대비 상시 폴링(3초 스로틀, TryRun 은 토큰 멱등).
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < _nextPollAt) return;
                _nextPollAt = EditorApplication.timeSinceStartup + 3.0;
                TryRun();
            };
        }

        [MenuItem("Window/Wassup/Ralph/Run Requested Editor Task")]
        static void ForceRun()
        {
            if (File.Exists(TokenPath)) File.Delete(TokenPath);
            TryRun();
        }

        // RalphTestRunner 가 이 게이트를 본다 — 태스크(에셋 생성/임포트 유발)가 먼저, 테스트는 다음 리로드에.
        internal static bool HasPendingTask()
        {
            try
            {
                if (!File.Exists(RequestPath)) return false;
                var req = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                if (req == null || string.IsNullOrEmpty(req.token)) return false;
                string done = File.Exists(TokenPath) ? File.ReadAllText(TokenPath).Trim() : "";
                return done != req.token;
            }
            catch { return false; }
        }

        static void TryRun()
        {
            try
            {
                if (!File.Exists(RequestPath)) return;
                var req = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                if (req == null || string.IsNullOrEmpty(req.token)) return;
                string done = File.Exists(TokenPath) ? File.ReadAllText(TokenPath).Trim() : "";
                if (done == req.token) return;

                Directory.CreateDirectory(Dir);
                File.WriteAllText(TokenPath, req.token);
                string result;
                try { result = Run(req.task); }
                catch (Exception e) { result = "ERROR|" + e; }
                File.WriteAllText($"{Dir}/editor_task_result_{req.token}.txt", result);
                Debug.Log($"[RalphEditorTasks] {req.task} → {result.Split('\n')[0]}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RalphEditorTasks] 실행 실패: {e}");
            }
        }

        static string Run(string task)
        {
            switch (task)
            {
                case "unit2_setup": return Unit2Setup();
                case "unit4b_stages": return Unit4bStages();
                case "unit5_pilot":
                    MapStageDummyGenerator.GeneratePilot();
                    return "OK|pilot generated";
                default: return $"ERROR|unknown task '{task}'";
            }
        }

        // US-004b — e2e 가 이름으로 pin 하는 맵들의 스테이지 픽스처를 생성해 dev 슬롯에 등록.
        // v2: ① 효과 타일 억제(고정 셀 계측 오염 방지) ② 구 문서 풀에서 덱/플랜 짝 승계
        // ③ 루트 저작 — Coil/Zig 는 lane0→경로1(레인 기본 경로 계약), MovementLab 은 경로 0/1 두 스웜.
        static string Unit4bStages()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "ERROR|Play 중 — 정지 후 재실행";
            var log = new StringBuilder("OK");
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>(
                "Assets/_Project/Data/Maps/MapStagePool.asset");
            if (pool == null) return "ERROR|MapStagePool.asset 없음 (unit2_setup 선행)";
            var docPool = AssetDatabase.LoadAssetAtPath<Wassup.Data.MapGrid.MapDocumentPool>(
                "Assets/_Project/Data/Maps/MapDocumentPool.asset");

            // 픽스처(엔트리 0)도 억제 플래그 소급.
            const string fixturePath = "Assets/_Project/Prefabs/Maps/MapStage_Fixture.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fixturePath) != null)
            {
                using (var scope = new PrefabUtility.EditPrefabContentsScope(fixturePath))
                    scope.prefabContentsRoot.GetComponent<MapStage>().suppressEffectTiles = true;
                log.Append("\nFixture: suppressEffectTiles 적용");
            }

            foreach (var name in new[] { "Serpent", "Duel", "Coil", "Zig", "Tutorial", "MovementLab", "Ford", "Isle" })
            {
                string path = $"Assets/_Project/Prefabs/Maps/MapStage_{name}.prefab";
                var root = new GameObject($"MapStage_{name}");
                try
                {
                    var stage = root.AddComponent<MapStage>();
                    stage.playAreaCells = new Vector2Int(12, 8);
                    stage.gridOriginLocal = new Vector3(0f, 0.02f, 0f);
                    stage.previewTileSize = 1f;
                    stage.suppressEffectTiles = true;   // e2e 고정 셀 계측 보호
                    for (int gy = 0; gy < 8; gy += 4)
                    for (int gx = 0; gx < 12; gx += 4)
                        PlaceGroundPiece(root.transform, "green/platform_4x4x1_green.prefab",
                            new Vector2Int(gx, gy), log);

                    // 루트 구성: routedLane0 = Coil/Zig(레인 0 기본 경로 = 1 계약),
                    // dualRoutes = MovementLab(경로 0/1 두 스웜 검증).
                    bool routedLane0 = name == "Coil" || name == "Zig";
                    bool dualRoutes = name == "MovementLab" || name == "Tutorial";   // Tutorial: 저작 플랜이 경로 인덱스를 참조

                    // 경로 0 = 공중(플랜/종 저작 전용) 예약, 지상 레인은 경로 1 — Tutorial 플랜
                    // 검증(«지상 레인이 Air 경로(0)를 타면 안 된다»)과 Coil/Zig 레인 기본 경로 계약 공용.
                    int laneRoute = (routedLane0 || dualRoutes) ? 1 : -1;
                    AddChild(root, "flag_spawn0", "green/flag_A_green.prefab", new Vector2Int(0, 2),
                        go => { var m = go.AddComponent<SpawnMarker>(); m.laneIndex = 0; m.routeIndex = laneRoute; });
                    AddChild(root, "flag_spawn1", "green/flag_A_green.prefab", new Vector2Int(0, 5),
                        go => { var m = go.AddComponent<SpawnMarker>(); m.laneIndex = 1;
                                m.routeIndex = dualRoutes ? 1 : -1; });
                    AddChild(root, "goal_sign", "neutral/signage_finish.prefab", new Vector2Int(11, 4),
                        go => go.AddComponent<GoalMarker>());
                    AddChild(root, "block_pillar", "neutral/pillar_2x2x2.prefab", new Vector2Int(5, 3),
                        go => go.AddComponent<PropFootprint>().size = new Vector2Int(2, 2));

                    if (routedLane0 || dualRoutes)
                    {
                        // 경로 0: 아래로 도는 우회 / 경로 1: 위로 도는 우회 — 직행과 확실히 구분.
                        AddChild(root, "route_0_0", "neutral/sign.prefab", new Vector2Int(3, 0),
                            go => { var r = go.AddComponent<RouteMarker>(); r.routeIndex = 0; r.order = 0; });
                        AddChild(root, "route_0_1", "neutral/sign.prefab", new Vector2Int(9, 0),
                            go => { var r = go.AddComponent<RouteMarker>(); r.routeIndex = 0; r.order = 1; });
                        AddChild(root, "route_1_0", "neutral/sign.prefab", new Vector2Int(3, 7),
                            go => { var r = go.AddComponent<RouteMarker>(); r.routeIndex = 1; r.order = 0; });
                        AddChild(root, "route_1_1", "neutral/sign.prefab", new Vector2Int(9, 7),
                            go => { var r = go.AddComponent<RouteMarker>(); r.routeIndex = 1; r.order = 1; });
                    }

                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                    var stageComp = prefab.GetComponent<MapStage>();
                    bool added = pool.EditorRegisterDevStage(stageComp);

                    // 구 문서 풀에서 같은 이름의 덱/플랜 짝 승계 (deck null = 레거시 폴백 함정 방지).
                    AttackDeck deck = null; WavePlanAsset plan = null;
                    if (docPool != null)
                    {
                        string docName = "MapDocument_" + name;
                        for (int i = 0; i < docPool.Count; i++)
                            if (docPool.Get(i).document != null && docPool.Get(i).document.name == docName)
                            { deck = docPool.Get(i).deck; plan = docPool.Get(i).plan; break; }
                        if (deck == null)
                            for (int i = 0; i < docPool.DevCount; i++)
                                if (docPool.GetDev(i).document != null && docPool.GetDev(i).document.name == docName)
                                { deck = docPool.GetDev(i).deck; plan = docPool.GetDev(i).plan; break; }
                    }
                    bool paired = pool.EditorSetDevPairing(stageComp, deck, plan);
                    log.Append($"\n{name}: prefab ok, dev {(added ? "신규" : "기존")}, deck={(deck ? deck.name : "null")} plan={(plan ? plan.name : "null")} paired={paired}");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // unit 2 — ① KayKit 최소 픽스처 스테이지 프리팹 ② MapStagePool.asset(덱/플랜은 구 문서 풀
        // entry 0 승계) ③ BattleScene 브리지 mapPool 재배선(additive 열고 저장 — 사용자의 열린 씬 무접촉)
        // ④ 열린 씬의 DevMapOverridePanel 재배선(dirty 마킹만 — 저장은 사용자 몫).
        static string Unit2Setup()
        {
            // 리뷰 M-5 — Play 중 delayCall 진입은 OpenScene 이 던진다.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "ERROR|Play 중 — 정지 후 메뉴로 재실행";

            var log = new StringBuilder("OK");

            // ── ① 픽스처 스테이지 프리팹 (12×8 열린 마당) ──────────────────────────
            // SaveAsPrefabAsset 은 같은 경로에 GUID 를 보존하므로 재실행 안전(멱등).
            var root = new GameObject("MapStage_Fixture");
            try
            {
                var stage = root.AddComponent<MapStage>();
                stage.playAreaCells = new Vector2Int(12, 8);
                // 리뷰 M-7 — 논리 평면을 KayKit 바닥 윗면(Y0)보다 살짝 띄워 페인트/오버레이
                // z-fighting 을 피한다 (바닥 페인팅 은퇴 전까지의 잠정치 — unit 3 에서 재검토).
                stage.gridOriginLocal = new Vector3(0f, 0.02f, 0f);
                stage.previewTileSize = 1f;

                // 바닥: platform_4x4x1 을 12×8 전면에 6장 타일링. 피벗 미지 → 렌더러 바운즈로
                // «min 모서리 = 셀 모서리, 윗면 = Y0» 을 실측 보정 (unit 5 계약: 보정은 저작 시 1회).
                for (int gy = 0; gy < 8; gy += 4)
                for (int gx = 0; gx < 12; gx += 4)
                    PlaceGroundPiece(root.transform, "green/platform_4x4x1_green.prefab",
                        new Vector2Int(gx, gy), log);

                AddChild(root, "flag_spawn0", "green/flag_A_green.prefab", new Vector2Int(0, 2),
                    go => go.AddComponent<SpawnMarker>().laneIndex = 0);
                AddChild(root, "flag_spawn1", "green/flag_A_green.prefab", new Vector2Int(0, 5),
                    go => go.AddComponent<SpawnMarker>().laneIndex = 1);
                AddChild(root, "goal_sign", "neutral/signage_finish.prefab", new Vector2Int(11, 4),
                    go => go.AddComponent<GoalMarker>());
                AddChild(root, "block_pillar", "neutral/pillar_2x2x2.prefab", new Vector2Int(5, 3),
                    go => go.AddComponent<PropFootprint>().size = new Vector2Int(2, 2));
                AddChild(root, "block_barrier", "neutral/barrier_1x1x2.prefab", new Vector2Int(8, 2),
                    go => go.AddComponent<PropFootprint>().size = Vector2Int.one);

                Directory.CreateDirectory("Assets/_Project/Prefabs/Maps");
                var prefab = PrefabUtility.SaveAsPrefabAsset(
                    root, "Assets/_Project/Prefabs/Maps/MapStage_Fixture.prefab");
                log.Append("\nprefab=Assets/_Project/Prefabs/Maps/MapStage_Fixture.prefab");

                // ── ② MapStagePool.asset — 덱/플랜은 구 문서 풀 entry 0 을 승계 ──────────
                var docPool = AssetDatabase.LoadAssetAtPath<Wassup.Data.MapGrid.MapDocumentPool>(
                    "Assets/_Project/Data/Maps/MapDocumentPool.asset");
                AttackDeck deck = null; WavePlanAsset plan = null;
                if (docPool != null && docPool.Count > 0)
                {
                    deck = docPool.Get(0).deck;
                    plan = docPool.Get(0).plan;
                    log.Append($"\ndeck={(deck ? deck.name : "null")} plan={(plan ? plan.name : "null")}");
                }

                // 리뷰 M-5 — 재실행 시 CreateAsset 은 GUID 를 갈아 기존 참조를 끊는다 → 로드-또는-생성.
                const string poolPath = "Assets/_Project/Data/Maps/MapStagePool.asset";
                var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>(poolPath);
                if (pool == null)
                {
                    pool = ScriptableObject.CreateInstance<MapStagePool>();
                    AssetDatabase.CreateAsset(pool, poolPath);
                }
                var so = new SerializedObject(pool);
                var entries = so.FindProperty("entries");
                entries.arraySize = 1;
                var e0 = entries.GetArrayElementAtIndex(0);
                e0.FindPropertyRelative("stage").objectReferenceValue = prefab.GetComponent<MapStage>();
                e0.FindPropertyRelative("deck").objectReferenceValue = deck;
                e0.FindPropertyRelative("plan").objectReferenceValue = plan;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
                // 리뷰 C-1(실사고) — SaveAssets/임포트가 in-memory 인스턴스를 무효화해 이후 배선이
                // 가짜 null(fileID 0)로 저장됐다. 경로에서 **다시 집어** 배선한다.
                pool = AssetDatabase.LoadAssetAtPath<MapStagePool>(poolPath);
                if (pool == null) return "ERROR|pool reload 실패";
                log.Append("\npool=").Append(poolPath);

                // ── ③ BattleScene 브리지 재배선 (이미 열려 있으면 그 씬을 그대로 사용) ────
                const string battleScenePath = "Assets/_Project/Scenes/BattleScene.unity";
                var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(battleScenePath);
                bool wasLoaded = existing.isLoaded;
                var battleScene = wasLoaded
                    ? existing
                    : EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Additive);
                bool wired = false;
                foreach (var rootGo in battleScene.GetRootGameObjects())
                {
                    var bridge = rootGo.GetComponentInChildren<Wassup.Bridge.BattleBridge>(true);
                    if (bridge == null) continue;
                    var bso = new SerializedObject(bridge);
                    bso.FindProperty("mapPool").objectReferenceValue = pool;
                    bso.ApplyModifiedPropertiesWithoutUndo();
                    // 배선 사후 검증 — 가짜 null 로 저장되는 사고를 로그로 드러낸다.
                    bso.Update();
                    var check = bso.FindProperty("mapPool").objectReferenceValue;
                    wired = check != null;
                    log.Append($"\nbattleScene mapPool wired verify={(check != null ? "ok" : "NULL!")}");
                    break;
                }
                if (wired) EditorSceneManager.SaveScene(battleScene);
                else log.Append("\nERROR|BattleScene 브리지 배선 실패");
                if (!wasLoaded) EditorSceneManager.CloseScene(battleScene, true);

                // ── ④ 열린 씬의 DevMapOverridePanel — 할당 + dirty (저장은 사용자 몫) ─────
                var panels = UnityEngine.Object.FindObjectsByType<Wassup.UI.DevMapOverridePanel>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var panel in panels)
                {
                    var pso = new SerializedObject(panel);
                    var pp = pso.FindProperty("pool");
                    pp.objectReferenceValue = pool;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
                    log.Append($"\npanel wired (scene='{panel.gameObject.scene.name}', 저장은 사용자 몫)");
                }
                if (panels.Length == 0)
                    log.Append("\npanel: 열린 씬에 없음 — OutgameScene 배선은 그 씬이 열릴 때 재요청");

                return log.ToString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static GameObject LoadKayKit(string relPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{KayKitPrefabs}/{relPath}");
            if (go == null) throw new InvalidOperationException($"KayKit 프리팹 없음: {relPath}");
            return go;
        }

        // 셀 중심에 프랍 인스턴스 배치 (+마커/footprint 부착). 프리팹 링크 유지(중첩 프리팹).
        static void AddChild(GameObject root, string name, string kayKitRelPath, Vector2Int cell,
            Action<GameObject> attach)
        {
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(LoadKayKit(kayKitRelPath));
            visual.transform.SetParent(host.transform, false);
            attach(host);
        }

        // 바닥판: min 모서리 = (gx,0,gy), 윗면 = Y0 이 되도록 렌더러 바운즈 실측 오프셋.
        static void PlaceGroundPiece(Transform root, string kayKitRelPath, Vector2Int minCell,
            StringBuilder log)
        {
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(LoadKayKit(kayKitRelPath));
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = Vector3.zero;
            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { log.Append($"\nWARN|{kayKitRelPath} 렌더러 없음"); return; }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            piece.transform.localPosition = new Vector3(
                minCell.x - b.min.x, -b.max.y, minCell.y - b.min.z);
        }
    }
}
