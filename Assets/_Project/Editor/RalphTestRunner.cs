using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Wassup.EditorTools
{
    // map-diorama-stage 작업용 일회성 테스트 러너 (Unity MCP 미연결 세션의 원격 검증 채널).
    // 프로토콜: .omc/ralph/test_request.json 에 요청을 쓰면, 다음 도메인 리로드(에디터 포커스) 때
    // 테스트를 실행하고 .omc/ralph/test_result_<token>.txt 에 결과를 쓴다. 토큰 sentinel 로 1회 실행.
    // 결과 줄 형식: "Passed|Full.Test.Name|" / "Failed|Full.Test.Name|message". 종료 줄: "RUNFINISHED|...".
    // PlayMode 도메인 리로드 대비: per-test 줄을 즉시 append (수집 상태를 메모리에 두지 않는다).
    // spec unit 7 은퇴 대상 — 브랜치 종료 시 삭제.
    [InitializeOnLoad]
    internal static class RalphTestRunner
    {
        const string Dir = ".omc/ralph";
        const string RequestPath = Dir + "/test_request.json";
        const string TokenPath = Dir + "/last_run_token.txt";

        [Serializable]
        class Request
        {
            public string token;
            public string mode;            // "EditMode" | "PlayMode"
            public string[] assemblies;    // 선택
            public string[] groups;        // 선택 — 정규식 (풀네임)
        }

        static double _nextPollAt;

        static RalphTestRunner()
        {
            // 도메인 리로드마다 콜백 재등록 — PlayMode 리로드를 건너 RunFinished 를 받기 위함.
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter());
            EditorApplication.delayCall += TryStart;
            // 스크립트 변경(=리로드) 없이 요청 파일만 갱신되는 경우를 위해 상시 폴링(3초 스로틀).
            // TryStart 는 토큰/결과 가드로 멱등이라 반복 호출이 안전하다.
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < _nextPollAt) return;
                _nextPollAt = EditorApplication.timeSinceStartup + 3.0;
                TryStart();
            };
        }

        [MenuItem("Window/Wassup/Ralph/Run Requested Tests")]
        static void ForceRun()
        {
            if (File.Exists(TokenPath)) File.Delete(TokenPath);
            TryStart();
        }

        static void TryStart()
        {
            try
            {
                if (!File.Exists(RequestPath)) return;
                // 도메인 상태 가드(IsRunActive 류)는 쓰지 않는다 — IsRunActive 는 이 UTF 버전에
                // 없고(CS0117), isPlaying 가드는 플레이 진입 리로드의 짧은 창에서 레이스가 실측됐다.
                // 재진입 방지는 아래 파일 마커 신선도가 단독 담당한다.
                // 에디터 태스크(에셋 생성 → 임포트/리로드 유발)가 대기 중이면 테스트는 다음 리로드로 —
                // 테스트 실행 중 에셋 임포트가 끼어드는 충돌 방지.
                if (RalphEditorTasks.HasPendingTask()) return;
                var req = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                if (req == null || string.IsNullOrEmpty(req.token)) return;
                string done = File.Exists(TokenPath) ? File.ReadAllText(TokenPath).Trim() : "";
                if (done == req.token)
                {
                    if (File.Exists(FinalPath(req.token))) return;   // 완료
                    // 재시도 판정은 **파일 마커 신선도**로만 — IsRunActive/isPlaying 은 플레이 진입
                    // 리로드의 짧은 창에서 전부 거짓이 되는 레이스가 실측됐다(smoke-02/04 이중
                    // Execute 사멸 + 무한 kill-restart). 콜백이 마커를 계속 갱신하므로 진행 중이면
                    // 항상 신선하고, 죽은 런만 10분 뒤 재시도된다.
                    if (File.Exists(MarkerPath(req.token))
                        && (DateTime.UtcNow - File.GetLastWriteTimeUtc(MarkerPath(req.token))).TotalSeconds < 600)
                        return;
                }

                Directory.CreateDirectory(Dir);
                File.WriteAllText(TokenPath, req.token);   // 실행 전에 sentinel — 리로드 재진입 방지
                File.WriteAllText(MarkerPath(req.token), DateTime.UtcNow.ToString("o"));
                File.WriteAllText(TmpPath(req.token), $"RUNSTART|{DateTime.Now:HH:mm:ss}|{req.mode}\n");

                var filter = new Filter
                {
                    testMode = req.mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode,
                };
                if (req.assemblies != null && req.assemblies.Length > 0) filter.assemblyNames = req.assemblies;
                if (req.groups != null && req.groups.Length > 0) filter.groupNames = req.groups;

                Debug.Log($"[RalphTestRunner] run token={req.token} mode={req.mode}");
                ScriptableObject.CreateInstance<TestRunnerApi>()
                    .Execute(new ExecutionSettings(filter));
            }
            catch (Exception e)
            {
                Debug.LogError($"[RalphTestRunner] 시작 실패: {e}");
            }
        }

        internal static string ActiveToken()
            => File.Exists(TokenPath) ? File.ReadAllText(TokenPath).Trim() : "";

        internal static string TmpPath(string token) => $"{Dir}/test_result_{token}.tmp";
        internal static string FinalPath(string token) => $"{Dir}/test_result_{token}.txt";
        internal static string MarkerPath(string token) => $"{Dir}/running_{token}.marker";

        static void TouchMarker()
        {
            string token = ActiveToken();
            if (!string.IsNullOrEmpty(token))
                File.WriteAllText(MarkerPath(token), DateTime.UtcNow.ToString("o"));
        }

        class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) => TouchMarker();
            public void TestStarted(ITestAdaptor test) => TouchMarker();

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                string token = ActiveToken();
                if (string.IsNullOrEmpty(token)) return;
                string msg = (result.Message ?? "").Replace("\r", " ").Replace("\n", " ");
                if (msg.Length > 300) msg = msg.Substring(0, 300);
                File.AppendAllText(TmpPath(token), $"{result.TestStatus}|{result.Test.FullName}|{msg}\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string token = ActiveToken();
                if (string.IsNullOrEmpty(token)) return;
                string tmp = TmpPath(token);
                File.AppendAllText(tmp, $"RUNFINISHED|{result.TestStatus}|{DateTime.Now:HH:mm:ss}\n");
                string final = FinalPath(token);
                if (File.Exists(final)) File.Delete(final);
                File.Move(tmp, final);
                if (File.Exists(MarkerPath(token))) File.Delete(MarkerPath(token));
                Debug.Log($"[RalphTestRunner] done token={token} → {final}");
            }
        }
    }
}
