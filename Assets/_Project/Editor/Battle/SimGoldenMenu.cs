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

        // 코퍼스에 시나리오를 **추가**했을 때 쓴다 — 기존 골든은 손대지 않고 파일이 없는 것만 굽는다.
        // ⚠ 전체 재생성으로 대신하면 안 된다: 그건 기존 7건의 기준선을 «지금 코드» 로 덮어써서
        // 그 시나리오들이 지키던 회귀 감시를 그 자리에서 무효로 만든다. 새 축을 여는 것과
        // 기존 축을 리베이스하는 것은 다른 동작이고, 후자는 의도적으로만(unit 6) 한다.
        [MenuItem("Wassup/Battle/Sim Harness/Bake Missing Goldens Only")]
        public static void BakeMissing() => RegenerateInternal(missingOnly: true);

        [MenuItem("Wassup/Battle/Sim Harness/Regenerate Golden Corpus")]
        public static void Regenerate() => RegenerateInternal(missingOnly: false);

        private static void RegenerateInternal(bool missingOnly)
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge))
            {
                // ⚠ **조용히 return 하지 않는다.** 그러면 보고서가 **이전 실행의 것**으로 남고,
                // 읽는 사람은 그것을 방금 실행의 결과로 읽는다 — 실제로 두 번 속았다
                // (2026-08-31: Play 가 로비 씬에서 시작해 브리지가 없었는데 「전건 통과」를
                //  그대로 믿었고, 그 사이 자가 바뀐 것이 기준선에 반영되지 않은 채 지나갔다).
                // 「통과하지만 아무것도 증언하지 않는다」의 같은 계열이다.
                System.IO.File.WriteAllText(ReportPath,
                    "# golden-corpus — **실행 실패**\n\n"
                    + "> BattleBridge 를 못 찾아 코퍼스가 **한 건도 돌지 않았다.**\n"
                    + "> Play 중인지, 그리고 **BattleScene** 인지 확인하라(로비 씬에서는 브리지가 없다).\n\n"
                    + "이전 표는 이 실패로 무효화됐다 — 다시 실행할 것.\n");
                AssetDatabase.Refresh();
                Debug.LogError("[Golden] BattleBridge 없음 — 한 건도 실행하지 않았다. "
                    + "보고서를 실패로 덮었다(이전 표를 결과로 오독하지 않게).");
                return;
            }
            System.IO.Directory.CreateDirectory(GoldenDir);

            var lines = new List<string>();
            int failed = 0;
            int skipped = 0;
            foreach (var sc in SimHarnessRunner.Corpus)
            {
                if (missingOnly && System.IO.File.Exists(Path(sc.name)))
                {
                    skipped++;
                    var kept = LegacyTraceV0.Deserialize(System.IO.File.ReadAllText(Path(sc.name)));
                    lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | {kept.events.Count} | "
                              + $"{kept.finalKills}/{kept.finalLeaks} | `{kept.configHash}` (유지) |");
                    continue;
                }
                var run = SimHarnessRunner.Run(bridge, sc, record: true);

                // ── 셋업 게이트 ──
                // 판을 세우다 실패했으면(예: 시나리오 덱을 못 만들었다) **저장하지 않는다.**
                // 로그만 남기고 통과시키면 그 시나리오가 증언하려던 축이 사라진 채로 기준선이
                // 구워진다 — 아래 공허·왕복 게이트와 같은 계열의 사고다.
                if (run.setupError != null)
                {
                    failed++;
                    Debug.LogError($"[Golden] '{sc.name}' 셋업 실패 — **저장하지 않는다**: {run.setupError}");
                    lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | — | — | ✗ 셋업 실패 |");
                    continue;
                }

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

            SimHarnessRunner.RestoreDefaultPool(bridge);
            System.IO.File.WriteAllText(ReportPath, BuildReport(lines, regenerated: true, failed: failed, diffs: null));
            AssetDatabase.Refresh();
            Debug.Log($"[Golden] 코퍼스 {SimHarnessRunner.Corpus.Length}건 중 "
                      + $"{SimHarnessRunner.Corpus.Length - skipped}건 {(missingOnly ? "신규 베이크" : "재생성")}"
                      + $"{(skipped > 0 ? $" · {skipped}건 기존 유지" : "")} (실패 {failed}). 보고서: {ReportPath}");
        }

        [MenuItem("Wassup/Battle/Sim Harness/Verify Against Golden Corpus")]
        public static void Verify()
        {
            if (!SimHarnessGuards.TryGetBridge(out var bridge))
            {
                // ⚠ **조용히 return 하지 않는다.** 그러면 보고서가 **이전 실행의 것**으로 남고,
                // 읽는 사람은 그것을 방금 실행의 결과로 읽는다 — 실제로 두 번 속았다
                // (2026-08-31: Play 가 로비 씬에서 시작해 브리지가 없었는데 「전건 통과」를
                //  그대로 믿었고, 그 사이 자가 바뀐 것이 기준선에 반영되지 않은 채 지나갔다).
                // 「통과하지만 아무것도 증언하지 않는다」의 같은 계열이다.
                System.IO.File.WriteAllText(ReportPath,
                    "# golden-corpus — **실행 실패**\n\n"
                    + "> BattleBridge 를 못 찾아 코퍼스가 **한 건도 돌지 않았다.**\n"
                    + "> Play 중인지, 그리고 **BattleScene** 인지 확인하라(로비 씬에서는 브리지가 없다).\n\n"
                    + "이전 표는 이 실패로 무효화됐다 — 다시 실행할 것.\n");
                AssetDatabase.Refresh();
                Debug.LogError("[Golden] BattleBridge 없음 — 한 건도 실행하지 않았다. "
                    + "보고서를 실패로 덮었다(이전 표를 결과로 오독하지 않게).");
                return;
            }

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
                var runResult = SimHarnessRunner.Run(bridge, sc, record: true);
                if (runResult.setupError != null)
                {
                    diffs.Add($"`{sc.name}` — 셋업 실패라 **대조 자체가 성립하지 않는다**: {runResult.setupError}");
                    lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | — | — | ✗ 셋업 실패 |");
                    continue;
                }
                var run = runResult.trace;
                string diff = golden.DiffAgainst(run);
                lines.Add($"| `{sc.name}` | {sc.seed} | {sc.ticks} | {run.events.Count} | "
                          + $"{run.finalKills}/{run.finalLeaks} | {(diff == null ? "✓" : "✗")} |");
                if (diff != null) diffs.Add($"`{sc.name}` — {diff}");
            }

            SimHarnessRunner.RestoreDefaultPool(bridge);
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
