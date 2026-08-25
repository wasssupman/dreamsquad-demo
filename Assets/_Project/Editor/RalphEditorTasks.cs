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
                case "unit5_pilot":
                    MapStageDummyGenerator.GeneratePilot();
                    return "OK|pilot generated";
                case "duel_classic":
                    MapStageDummyGenerator.GenerateDuelClassic();
                    return "OK|duel classic generated";
                case "unit9_portals":
                    MapStageDummyGenerator.AuthorBonusPortals("Assets/_Project/Prefabs/Maps/MapStage_Duel.prefab", new Vector2Int(6, 1), new Vector2Int(6, 6));
                    MapStageDummyGenerator.AuthorBonusPortals("Assets/_Project/Prefabs/Maps/MapStage_DuelClassic.prefab", new Vector2Int(10, 3), new Vector2Int(10, 8));
                    return "OK|bonus portals authored (Duel, DuelClassic)";
                default: return $"ERROR|unknown task '{task}'";
            }
        }

    }
}
