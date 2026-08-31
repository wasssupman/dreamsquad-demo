using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Wassup.Core.Trace;

namespace Wassup.EditorTools.Battle
{
    // battle-sim-extraction M0 unit 4 — 골든 코퍼스 생성·검증.
    //
    // 이 메뉴가 M0 의 목적지다. 여기서 저장한 trace 가 M1 신 sim 의 **A/B 기준선**이 된다.
    //
    // 저장 전에 **직렬화 왕복을 반드시 통과시킨다**(쓰기 → 읽기 → 다시 쓰기가 바이트로 동일).
    // 왕복을 나중에 붙이면, 파일에 못 태울 것(오브젝트 참조 등)을 실은 채 코퍼스를 다 만든 뒤에야
    // 알게 된다. 첫날 걸러야 하는 종류의 결함이다.
    public static class SimGoldenMenu
    {
        private const string GoldenDir = "Assets/_Project/Tests/Golden";
        private const string ReportPath = "docs/spec/battle-sim-extraction/golden-corpus.md";

        [MenuItem("Wassup/Battle/Sim Harness/Regenerate Golden Corpus")]
        public static void Regenerate()
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge)) return;
            System.IO.Directory.CreateDirectory(GoldenDir);

            var lines = new List<string>();
            int failed = 0;
            foreach (var sc in SimHarnessRunner.Corpus)
            {
                var run = SimHarnessRunner.Run(bridge, sc, record: true);
                var trace = run.trace;
                string text = trace.Serialize();

                // ── 공허 게이트 ──
                // ⚠ **빈 트레이스를 저장하면 안 된다.** 재생성 직후 같은 Play 세션에서 다시
                // 돌리면(또는 판이 소진된 세션에서 돌리면) 시나리오가 **이벤트 0 · configHash 공백**
                // 으로 나오는데, 예전엔 그걸 그대로 써 놓고 「전건 통과」라고 보고했다.
                // 그 순간 골든은 존재 이유를 잃는다 — 통과하지만 아무것도 증언하지 않는다.
                // (실측 2026-08-31: 좋은 기준선을 빈 것으로 덮을 뻔했다.)
                if (trace.events.Count == 0 || string.IsNullOrEmpty(trace.configHash))
                {
                    failed++;
                    Debug.LogError($"[Golden] '{sc.name}' 이 비었다(이벤트 {trace.events.Count}, "
                        + $"configHash '{trace.configHash}') — **저장하지 않는다.** "
                        + "판이 소진된 세션에서 돌렸을 가능성이 높다. Play 를 껐다 켜고 "
                        + "**재생성을 그 세션의 첫 동작으로** 실행하라.");
                    lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | — | — | ✗ 빈 트레이스 |");
                    continue;
                }

                // ── 직렬화 왕복 게이트 ──
                string roundTripError = null;
                try
                {
                    string again = LegacyTraceV0.Deserialize(text).Serialize();
                    if (again != text) roundTripError = "재직렬화 바이트가 다르다";
                }
                catch (System.Exception e) { roundTripError = e.Message; }

                if (roundTripError != null)
                {
                    failed++;
                    Debug.LogError($"[Golden] '{sc.name}' 왕복 실패 — 저장하지 않는다: {roundTripError}");
                    lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | — | — | ✗ 왕복 실패: {roundTripError} |");
                    continue;
                }

                // ⚠ **침묵한 골든을 잡는 카나리아.** 배치 스케줄이 있는데 킬이 0 이면 그 골든은
                // 통과해도 아무것도 증언하지 않는다. 실제로 그 상태가 203 커밋 동안 방치됐다 —
                // 하네스가 (0,0) 부터 첫 가능 칸에 놓아 방어유닛이 골에서 15칸 떨어진 구석에
                // 몰렸고, 코퍼스 7건의 킬이 전부 0 이었는데 아무도 몰랐다.
                if (sc.placementTicks != null && sc.placementTicks.Length > 0 && trace.finalKills == 0)
                    Debug.LogWarning($"[Golden] '{sc.name}' — 배치 {sc.placementTicks.Length}회인데 킬 0. "
                        + "방어유닛이 교전에 닿지 않았을 수 있다(배치 자리·사거리 확인). "
                        + "이 골든은 통과해도 전투를 증언하지 않는다.");

                System.IO.File.WriteAllText(Path(sc.name), text);
                lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | {trace.events.Count} | "
                          + $"{trace.finalKills}/{trace.finalLeaks} | `{trace.configHash}` |");
                Debug.Log($"[Golden] '{sc.name}' 저장 — 이벤트 {trace.events.Count}, 킬 {trace.finalKills}, 유출 {trace.finalLeaks}");
            }

            System.IO.File.WriteAllText(ReportPath, BuildReport(lines, regenerated: true, failed: failed, diffs: null));
            AssetDatabase.Refresh();
            Debug.Log($"[Golden] 코퍼스 {SimHarnessRunner.Corpus.Length}건 재생성 (왕복 실패 {failed}). 보고서: {ReportPath}");
        }

        [MenuItem("Wassup/Battle/Sim Harness/Verify Against Golden Corpus")]
        public static void Verify()
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge)) return;

            var lines = new List<string>();
            var diffs = new List<string>();
            foreach (var sc in SimHarnessRunner.Corpus)
            {
                string path = Path(sc.name);
                if (!System.IO.File.Exists(path))
                {
                    diffs.Add($"`{sc.name}` — 골든 파일이 없다({path}). 먼저 재생성하라.");
                    lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | — | — | ✗ 골든 없음 |");
                    continue;
                }
                var golden = LegacyTraceV0.Deserialize(System.IO.File.ReadAllText(path));
                var run = SimHarnessRunner.Run(bridge, sc, record: true).trace;
                string diff = golden.DiffAgainst(run);
                lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | {run.events.Count} | "
                          + $"{run.finalKills}/{run.finalLeaks} | {(diff == null ? "✓" : "✗")} |");
                if (diff != null) diffs.Add($"`{sc.name}` — {diff}");
            }

            System.IO.File.WriteAllText(ReportPath, BuildReport(lines, regenerated: false, failed: diffs.Count, diffs: diffs));
            AssetDatabase.Refresh();
            Debug.Log(diffs.Count == 0
                ? $"[Golden] 코퍼스 {SimHarnessRunner.Corpus.Length}건 전부 골든과 일치. 보고서: {ReportPath}"
                : $"[Golden] {diffs.Count}건 불일치. 보고서: {ReportPath}");
        }

        private static string Path(string scenario) => $"{GoldenDir}/{scenario}.trace.txt";

        private static string BuildReport(List<string> rows, bool regenerated, int failed, List<string> diffs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# golden-corpus — LegacyTraceV0 코퍼스 상태");
            sb.AppendLine();
            sb.AppendLine("> **자동 생성물.** `Wassup/Battle/Sim Harness/Regenerate Golden Corpus` 또는");
            sb.AppendLine("> `… /Verify Against Golden Corpus` 로 Play 중 갱신한다. 손으로 고치지 말 것.");
            sb.AppendLine();
            sb.AppendLine($"- 마지막 동작: **{(regenerated ? "재생성" : "검증")}**");
            sb.AppendLine($"- 결과: **{(failed == 0 ? "전건 통과" : $"{failed}건 실패")}**");
            sb.AppendLine($"- 골든 파일: `{GoldenDir}/<scenario>.trace.txt` (추적 대상)");
            sb.AppendLine();
            sb.AppendLine("| 시나리오 | seed | 틱 | 이벤트 | 킬/유출 | " + (regenerated ? "configHash" : "판정") + " |");
            sb.AppendLine("|---|---:|---:|---:|---|---|");
            foreach (var r in rows) sb.AppendLine(r);
            if (diffs != null && diffs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 불일치");
                sb.AppendLine();
                foreach (var d in diffs) sb.AppendLine($"- {d}");
                sb.AppendLine();
                sb.AppendLine("> `configHash` 가 다르다고 나오면 **코드 회귀가 아니라 조건 드리프트**다");
                sb.AppendLine("> (대개 시트 임포트가 SO 를 덮었다). 그 경우 골든을 고치기 전에 값을 먼저 되돌린다.");
            }
            return sb.ToString();
        }
    }
}
