using System.Text;
using UnityEditor;
using UnityEngine;
using Wassup.Bridge;

namespace Wassup.EditorTools.Battle
{
    // battle-sim-extraction M0 unit 2 — 「같은 seed·같은 입력이면 같은 궤적」의 증거 수집기.
    // 실행 몸통은 `SimHarnessRunner` 가 갖는다(unit 4 에서 골든 러너와 공유).
    public static class SimHarnessRunMenu
    {
        private const string ReportPath = "docs/spec/battle-sim-extraction/harness-determinism.md";

        [MenuItem("Wassup/Battle/Sim Harness/Run Determinism Check (2 runs)")]
        public static void Run()
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge)) return;

            var sc = SimHarnessRunner.Corpus[0]; // basic
            var runA = SimHarnessRunner.Run(bridge, sc, record: false);
            var runB = SimHarnessRunner.Run(bridge, sc, record: false);

            int firstDiff = -1;
            for (int i = 0; i < sc.ticks; i++)
            {
                if (runA.digests[i].Equals(runB.digests[i])) continue;
                firstDiff = i;
                break;
            }

            System.IO.File.WriteAllText(ReportPath, BuildReport(sc, runA, runB, firstDiff));
            AssetDatabase.Refresh();
            Debug.Log(firstDiff < 0
                ? $"[SimHarness] 2회 {sc.ticks}틱 완전 일치 (configHash {runA.configHash}). 보고서: {ReportPath}"
                : $"[SimHarness] 틱 {firstDiff} 에서 갈렸다 (configHash A={runA.configHash} B={runB.configHash}). 보고서: {ReportPath}");
        }

        private static string BuildReport(
            in SimHarnessRunner.Scenario sc,
            in SimHarnessRunner.RunResult ra, in SimHarnessRunner.RunResult rb, int firstDiff)
        {
            var a = ra.digests;
            var b = rb.digests;
            bool configSame = ra.configHash == rb.configHash;
            var sb = new StringBuilder();
            sb.AppendLine("# harness-determinism — 고정 스텝 2회 실행 대조");
            sb.AppendLine();
            sb.AppendLine("> **자동 생성물.** `Wassup/Battle/Sim Harness/Run Determinism Check (2 runs)` 로 Play 중 갱신한다.");
            sb.AppendLine("> 손으로 고치지 말 것 — 이 파일은 «고정 스텝에서 같은 seed·같은 입력이 같은 궤적을 낸다» 는");
            sb.AppendLine("> 사실의 기록이고, 골든(unit 4)은 이 전제 위에서만 성립한다.");
            sb.AppendLine();
            sb.AppendLine($"- 시나리오 `{sc.name}` · seed `{sc.seed}` · 스텝 `{SimHarnessRunner.StepDt:F6}s` × **{sc.ticks}** 틱"
                          + $" · 입력: 틱 [{string.Join(", ", sc.placementTicks)}] 에 방어유닛 1기씩");
            sb.AppendLine($"- 판정: **{(firstDiff < 0 ? "완전 일치" : $"틱 {firstDiff} 에서 분기")}**");
            sb.AppendLine($"- `configHash`: run A `{ra.configHash}` · run B `{rb.configHash}` — "
                          + (configSame
                              ? "동일(조건 드리프트 없음)"
                              : "**갈렸다 — 코드 회귀가 아니라 조건 드리프트다.** 시트 임포트가 SO 를 덮었는지 먼저 본다"));
            sb.AppendLine();
            sb.AppendLine("다이제스트 = `_battleClock` / 살아있는 sim 엔티티 수 / 상태 지문(FNV-1a over ID·위치·체력, ID 정렬).");
            sb.AppendLine();
            sb.AppendLine("| 틱 | run A | run B | 일치 |");
            sb.AppendLine("|---:|---|---|:-:|");
            for (int i = 0; i < a.Length; i++)
            {
                bool same = a[i].Equals(b[i]);
                // 전량은 길다 — 앞뒤·주기·입력 시점·분기 지점만 남긴다(사실의 밀도가 거기 있다).
                bool keep = i < 3 || i >= a.Length - 3 || i % 60 == 0 || !same
                            || System.Array.IndexOf(sc.placementTicks, i) >= 0
                            || (firstDiff >= 0 && System.Math.Abs(i - firstDiff) <= 2);
                if (!keep) continue;
                sb.AppendLine($"| {i} | {a[i]} | {b[i]} | {(same ? "✓" : "✗")} |");
            }
            return sb.ToString();
        }
    }

    internal static class SimHarnessGuards
    {
        public static bool TryGetBridge(out BattleBridge bridge)
        {
            bridge = null;
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SimHarness] Play Mode 에서 실행하라 — 월드가 있어야 스텝이 의미를 가진다.");
                return false;
            }
            bridge = Object.FindFirstObjectByType<BattleBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[SimHarness] BattleBridge 를 못 찾았다. 전투 씬에서 실행하라.");
                return false;
            }
            return true;
        }
    }
}
