using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Wassup.EditorTools
{
    // battle-sim-extraction — 락 잡힌 에디터에서 테스트를 실행/수확하는 러너.
    //
    // 왜 필요한가: 사용자 GUI 에디터가 프로젝트 락을 보유하면 `-batchmode -runTests` 로 두 번째
    // 인스턴스를 띄울 수 없다. 그래서 **살아 있는 에디터에게** 트리거 파일로 일을 시키고 결과를
    // 파일로 받는다. `dotnet build` 는 컴파일만 증명하고 Unity Test Framework 를 실행하지 않는다.
    //
    // 사용법 (둘 중 아무거나):
    //   ① Temp/sim-test-request.txt 를 만든다 — 1행 `EditMode`|`PlayMode`,
    //      2행(선택) 그룹 정규식(예: `Wassup\.Tests\.EditMode\.ModifierFrameworkTests`).
    //      최대 1초 안에 소비(삭제)되고 실행이 시작된다.
    //   ② 메뉴 `Tools/Sim/Run EditMode Tests`.
    //
    // 결과는 항상 `Temp/sim-test-result.txt` 로 나간다 — **트리거 없이 사용자가 Test Runner 창에서
    // 직접 돌린 실행도 수확된다**(콜백을 로드 시 상시 등록하므로). 실패는 전건 기록한다.
    //
    // Temp/ 는 Unity 소유 · gitignored · 에디터 재시작 시 소실 = 저장소 오염 0.
    [InitializeOnLoad]
    internal static class SimTestAutoRunner
    {
        private const string RequestFile = "sim-test-request.txt";
        private const string ResultFile = "sim-test-result.txt";
        private const double PollIntervalSeconds = 1.0;

        private static double _nextPoll;

        static SimTestAutoRunner()
        {
            // 콜백은 **매 도메인 리로드마다 다시 등록**한다. EditMode/PlayMode 실행은 리로드를
            // 유발할 수 있고, 그때 인스턴스에 붙은 등록은 사라진다 — 상시 등록만이 RunFinished 를
            // 놓치지 않는 방법이다.
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter());

            EditorApplication.update += Poll;
        }

        private static string TempDir
        {
            get
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(root, "Temp");
            }
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            string path = Path.Combine(TempDir, RequestFile);
            if (!File.Exists(path)) return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
                File.Delete(path); // 소비는 1회 — 재진입 루프 방지(읽기 실패 시 남겨두고 다음 폴에 재시도)
            }
            catch (IOException)
            {
                return; // 쓰는 중이면 다음 폴에서
            }

            var mode = TestMode.EditMode;
            if (lines.Length > 0 && lines[0].Trim().Equals("PlayMode", StringComparison.OrdinalIgnoreCase))
                mode = TestMode.PlayMode;
            string group = lines.Length > 1 ? lines[1].Trim() : null;

            Run(mode, string.IsNullOrEmpty(group) ? null : group);
        }

        [MenuItem("Tools/Sim/Run EditMode Tests")]
        private static void MenuRunEditMode() => Run(TestMode.EditMode, null);

        [MenuItem("Tools/Sim/Run PlayMode Tests")]
        private static void MenuRunPlayMode() => Run(TestMode.PlayMode, null);

        private static void Run(TestMode mode, string groupRegex)
        {
            var filter = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(groupRegex))
                filter.groupNames = new[] { groupRegex };

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(filter));
            Debug.Log($"[SimTest] {mode} 실행 시작 — filter={groupRegex ?? "(전체)"}. " +
                      $"결과: Temp/{ResultFile}");
        }

        // 결과 수확기. 실행 주체(트리거·메뉴·Test Runner 창)와 무관하게 같은 파일로 쓴다.
        private class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Write($"RUNNING {testsToRun?.FullName}\n실행 중 — 완료 시 이 파일이 대체된다.\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"STATUS: {result.TestStatus}");
                sb.AppendLine($"passed={result.PassCount} failed={result.FailCount} " +
                              $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
                sb.AppendLine($"duration={result.Duration:F1}s");
                sb.AppendLine();

                var failures = new List<ITestResultAdaptor>();
                Collect(result, failures);
                sb.AppendLine($"FAILURES ({failures.Count}):");
                foreach (var f in failures)
                {
                    sb.AppendLine($"--- {f.Test?.FullName}");
                    if (!string.IsNullOrEmpty(f.Message)) sb.AppendLine($"    msg: {f.Message.Trim()}");
                    if (!string.IsNullOrEmpty(f.StackTrace))
                    {
                        // 스택은 첫 6줄만 — 원인 위치 파악에 그 이상은 필요 없고 파일이 비대해진다.
                        var st = f.StackTrace.Split('\n');
                        for (int i = 0; i < Math.Min(6, st.Length); i++)
                            sb.AppendLine($"    {st[i].TrimEnd()}");
                    }
                }

                Write(sb.ToString());
                Debug.Log($"[SimTest] 완료 — {result.TestStatus} " +
                          $"(passed={result.PassCount} failed={result.FailCount}). Temp/{ResultFile}");
            }

            // 리프만 수집한다 — 컨테이너(어셈블리/클래스)는 자식 실패를 그대로 반사해 중복이 된다.
            private static void Collect(ITestResultAdaptor node, List<ITestResultAdaptor> into)
            {
                if (node.HasChildren)
                {
                    foreach (var c in node.Children) Collect(c, into);
                    return;
                }
                if (node.TestStatus == TestStatus.Failed) into.Add(node);
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            private static void Write(string text)
            {
                try
                {
                    Directory.CreateDirectory(TempDir);
                    File.WriteAllText(Path.Combine(TempDir, ResultFile), text, new UTF8Encoding(false));
                }
                catch (IOException e)
                {
                    Debug.LogWarning($"[SimTest] 결과 기록 실패: {e.Message}");
                }
            }
        }
    }
}
