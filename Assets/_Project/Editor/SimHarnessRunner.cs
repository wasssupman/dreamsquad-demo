using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.EditorTools
{
    // battle-sim-extraction unit 2 — StepOneTick 결정론 러너.
    //
    // 같은 seed(TestModeContext 하네스 캐리) + 같은 입력 스케줄(tick 100 에 ForceNextWave)로
    // BattleScene 을 2회 Play 하고, tick별 다이제스트(배틀 클럭·개체 수·웨이브/이벤트 카운터)를 대조한다.
    // 스텝 루프는 한 에디터 콜백 안에서 동기 자가구동이라 에디터 포커스/배치 모드와 무관하게
    // 완주한다(lessons 01 비포커스 정지 회피). 배틀 진입은 PlayMode 테스트 관례와 동형
    // (bridge.StartBattle() 직행 — 페이즈 플로우 우회).
    // 배치 실행: -executeMethod Wassup.EditorTools.SimHarnessRunner.RunDeterminismCheck
    public static class SimHarnessRunner
    {
        private const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        private const string RunsKey = "SimHarness.runsRemaining";
        private const string ExitCodeKey = "SimHarness.pendingExitCode";
        private const int NoExitCode = int.MinValue;
        // TestModeContext.ApplyEditorHarnessCarry 가 소비하는 키와 동일해야 한다.
        private const string SeedKey = "SimHarness.seed";
        private const int Seed = 20260804;
        private const float FixedDt = 0.05f; // 20Hz
        private const int Ticks = 600;       // 30초 sim
        private const int ForceWaveTick = 100; // 입력 스케줄 표본 — 웨이브 당기기
        private const int StartupFrameCap = 1800;

        private static int _pollFrames;
        private static bool _startRequested;

        [MenuItem("Wassup/Battle/Sim Harness/Determinism Check (2 plays)", false, 310)]
        public static void RunDeterminismCheck()
        {
            SessionState.SetInt(RunsKey, 2);
            SessionState.EraseInt(ExitCodeKey);
            TryDelete(RunPath(1));
            TryDelete(RunPath(2));
            StartNextRun();
        }

        // Temp/ 는 에디터 종료 시 삭제된다 — 배치 2-run 대조가 프로세스를 넘어 살아야 하므로 Library/.
        private static string RunPath(int run) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", $"SimHarnessRun{run}.txt");

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void StartNextRun()
        {
            SessionState.SetInt(SeedKey, Seed); // 도메인 리로드 너머로 시드 운반(1회 소비)
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Init() => EditorApplication.playModeStateChanged += OnPlayMode;

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                if (SessionState.GetInt(RunsKey, 0) <= 0) return;
                _pollFrames = 0;
                _startRequested = false;
                EditorApplication.update += Drive;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                int exitCode = SessionState.GetInt(ExitCodeKey, NoExitCode);
                if (exitCode != NoExitCode)
                {
                    SessionState.EraseInt(ExitCodeKey);
                    EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
                    return;
                }
                if (SessionState.GetInt(RunsKey, 0) > 0) StartNextRun(); // 다음 run
            }
        }

        private static void Drive()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Drive; return; }
            if (++_pollFrames > StartupFrameCap) { Fail("배틀 시작 대기 초과"); return; }
            var bridge = Object.FindAnyObjectByType<BattleBridge>();
            if (bridge == null) return;
            if (!bridge.BattleRunning)
            {
                // PlayMode 테스트 관례 — 페이즈 플로우를 기다리지 않고 직행.
                // ⚠ StartBattle 과 BeginHarness 는 **같은 콜백** 안이어야 한다 — 프레임을
                // 넘기면 그 사이 Bridge.Update 가 가변 실프레임 dt 로 시계를 전진시켜
                // 하네스가 잡기 전에 두 run 이 갈라진다(1차 시도 FAIL 의 원인).
                if (_startRequested) return; // StartBattle 이 즉시 안 켜졌다 — 다음 프레임 재확인
                bridge.StartBattle();
                _startRequested = true;
                if (!bridge.BattleRunning) return;
            }
            EditorApplication.update -= Drive;
            RunHarness(bridge);
        }

        private static void RunHarness(BattleBridge bridge)
        {
            int runsRemaining = SessionState.GetInt(RunsKey, 0);
            int runNo = 3 - runsRemaining; // 2→run1, 1→run2

            var schedule = new HarnessInputSchedule();
            schedule.Add(ForceWaveTick, bridge.ForceNextWave);
            try
            {
                if (!bridge.BeginHarness(FixedDt, schedule)) { Fail("BeginHarness 실패"); return; }
                if (string.IsNullOrEmpty(bridge.ConfigHash)) { Fail("MatchConfig configHash 미생성"); return; }

                var sb = new StringBuilder(Ticks * 40);
                sb.Append("configHash:").Append(bridge.ConfigHash).Append('\n');
                int executedTicks = 0;
                for (int i = 0; i < Ticks && bridge.BattleRunning; i++)
                {
                    bridge.StepOneTick(FixedDt);
                    bridge.GetHarnessDigestCounts(
                        out int enemies, out int defenders, out int projectiles,
                        out int nextWave, out int pending, out int goals, out int killScore);
                    sb.Append(i).Append(':')
                      .Append(bridge.BattleClock.ToString("F4", CultureInfo.InvariantCulture)).Append(':')
                      .Append(enemies).Append(':')
                      .Append(defenders).Append(':')
                      .Append(projectiles).Append(':')
                      .Append(nextWave).Append(':')
                      .Append(pending).Append(':')
                      .Append(goals).Append(':')
                      .Append(killScore).Append('\n');
                    executedTicks++;
                }
                if (executedTicks == 0) { Fail("하네스가 한 tick도 실행되지 않음"); return; }

                File.WriteAllText(RunPath(runNo), sb.ToString());
                Debug.Log($"[SimHarness] run {runNo} 완료 — tick {bridge.HarnessTick}, digest {sb.Length} chars");

                SessionState.SetInt(RunsKey, runsRemaining - 1);
                if (runsRemaining - 1 > 0)
                {
                    EditorApplication.ExitPlaymode();
                    return;
                }

                string r1 = File.Exists(RunPath(1)) ? File.ReadAllText(RunPath(1)) : "";
                string r2 = File.Exists(RunPath(2)) ? File.ReadAllText(RunPath(2)) : "";
                bool pass = r1.Length > 0 && r1 == r2;
                if (pass) Debug.Log($"[SimHarness] PASS — 2회 실행 다이제스트 완전 동일 ({executedTicks} ticks × 20Hz)");
                else Debug.LogError($"[SimHarness] FAIL — 다이제스트 불일치 (run1 {r1.Length} / run2 {r2.Length} chars). Library/SimHarnessRun*.txt 를 diff 하라.");
                ExitPlayModeThenProcess(pass ? 0 : 1);
            }
            catch (System.Exception ex)
            {
                Fail($"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // ExitPlaymode 요청은 비동기다. Default World가 파괴되기 전에 match teardown을
                // 끝내야 FlowField/DefenderField Persistent 배열을 component에서 읽어 dispose할 수 있다.
                // StopBattle → TeardownCurrentBattle → EndHarness 순이라 하네스 전역 상태도 함께 복구된다.
                if (bridge != null) bridge.StopBattle();
            }
        }

        private static void Fail(string reason)
        {
            EditorApplication.update -= Drive;
            Debug.LogError($"[SimHarness] {reason}");
            SessionState.SetInt(RunsKey, 0);
            ExitPlayModeThenProcess(1);
        }

        private static void ExitPlayModeThenProcess(int exitCode)
        {
            if (Application.isBatchMode) SessionState.SetInt(ExitCodeKey, exitCode);
            EditorApplication.ExitPlaymode();
        }
    }
}
