using System;
using System.IO;
using System.Text;
using UnityEditor;
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
                // 요청 처리 전 리프레시 — RalphTestRunner 와 같은 이유(u3 실사고): 이 에디터는
                // Auto Refresh 미동작이라 새 태스크 케이스가 임포트 전이면 구 어셈블리가
                // unknown task 로 토큰을 소모한다. 컴파일이 시작되면 리로드 후 새 코드로 재진입.
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling) return;
                // Play 중엔 씬 열기/프리팹 저장이 금지된다 — 토큰을 소모하지 않고 다음 폴링으로 미룬다.
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

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

        // 포탈 프랍을 마커 밑에 얹는다. tint 가 있으면 파티클 startColor(min/max·그라데이션 키)를 그 색으로 — 머티리얼 _Color 는
        // GoalMarker.SetStressTint 가 매 프레임 MPB 로 덮으므로 색을 거기 두면 지워진다(스폰은 브리지가 색을 안 건드린다).
        static void Attach(Transform host, GameObject portalPrefab, Color? tint)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(portalPrefab);
            go.transform.SetParent(host, false);
            go.transform.localPosition = Vector3.zero;
            if (tint is not Color c) return;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var sc = main.startColor;
                switch (sc.mode)
                {
                    case ParticleSystemGradientMode.Color: sc.color = c * sc.color.grayscale; break;
                    case ParticleSystemGradientMode.TwoColors: sc.colorMin = Color.white; sc.colorMax = c; break;
                    default: sc.mode = ParticleSystemGradientMode.TwoColors; sc.colorMin = Color.white; sc.colorMax = c; break;
                }
                main.startColor = sc;
            }
        }

        static string Run(string task)
        {
            switch (task)
            {
                case "goal_portal_yellow":
                    return MapStageAuthoringTools.CreateGoalPortalYellow();
                case "duel_stage":
                    return MapStageDuelGenerator.Generate();
                case "preview_duel_clean":
                    return MapStageCameraFraming.RenderPrefabPreview(MapStageDuelGenerator.PrefabPath, ".omc/ralph/preview_duel_clean.png", overlay: false);
                case "preview_duel":
                    return MapStageCameraFraming.RenderPrefabPreview(MapStageDuelGenerator.PrefabPath, ".omc/ralph/preview_duel.png");
                case "preview_subway":
                    return MapStageCameraFraming.RenderPrefabPreview("Assets/_Project/Art/Theme/subway/MapStage_Subway.prefab", ".omc/ralph/preview_subway.png");
                case "preview_streetday":
                    return MapStageCameraFraming.RenderPrefabPreview("Assets/_Project/Art/Theme/street_day/MapStage_StreetDay.prefab", ".omc/ralph/preview_streetday.png");
                case "preview_duel_portals":
                {
                    // unit 6 재해석(사용자 2026-08-27) — 스폰 = 빨간 포탈(SpawnPortal_Red 그대로), 골 = 노란 포탈(같은 프리팹,
                    // 파티클 startColor 만 노랑으로) 을 마커 visualRoot 로 얹어 본다. 프리팹은 저장하지 않는다(what-if).
                    var portal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/SpawnPortal_Red.prefab");
                    if (portal == null) return "ERROR|SpawnPortal_Red 없음";
                    return MapStageCameraFraming.RenderPrefabPreview(MapStageDuelGenerator.PrefabPath, ".omc/ralph/preview_duel_portals.png",
                        overlay: false, decorate: inst =>
                        {
                            foreach (var s in inst.GetComponentsInChildren<Wassup.Core.SpawnMarker>())
                                Attach(s.transform, portal, null);
                            foreach (var g in inst.GetComponentsInChildren<Wassup.Core.GoalMarker>())
                                Attach(g.transform, portal, new Color(1f, 0.85f, 0.15f, 1f));
                        });
                }
                case "preview_street":
                    return MapStageCameraFraming.RenderPrefabPreview("Assets/_Project/Art/Theme/street/MapStage_Street.prefab", ".omc/ralph/preview_street.png");
                case "live_portals":
                    // bonus-wave-pull 게이트(BonusWaveAuthored)는 bonusSpawns 2개를 요구 — Duel 관례(중앙 열, 위/아래 가장자리) 이관.
                    MapStageAuthoringTools.AuthorBonusPortals("Assets/_Project/Art/Theme/street/MapStage_Street.prefab", new Vector2Int(15, 1), new Vector2Int(15, 9));
                    MapStageAuthoringTools.AuthorBonusPortals("Assets/_Project/Art/Theme/subway/MapStage_Subway.prefab", new Vector2Int(15, 1), new Vector2Int(15, 9));
                    MapStageAuthoringTools.AuthorBonusPortals("Assets/_Project/Art/Theme/street_day/MapStage_StreetDay.prefab", new Vector2Int(15, 1), new Vector2Int(15, 7));
                    return "OK|bonus portals authored (Street, Subway, StreetDay)";
                case "street_markers":
                    MapStageAuthoringTools.AuthorSpawnsAndGoal("Assets/_Project/Art/Theme/street/MapStage_Street.prefab",
                        new Vector2Int(28, 3), new Vector2Int(28, 7), new Vector2Int(1, 5));
                    return "OK|street spawns/goal authored + dev registered";
                case "maptest_battle_camera":
                {
                    string norm = MapStageCameraFraming.NormalizePrefabRoot("Assets/_Project/Art/Theme/street/MapStage_Street.prefab");
                    string framed = MapStageCameraFraming.FrameScene("Assets/MapTest.unity", 16f / 9f);
                    return framed + " | prefab root: " + norm;
                }
                default: return $"ERROR|unknown task '{task}'";
            }
        }

    }
}
