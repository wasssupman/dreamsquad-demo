using System.Text;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.EditorTools.BattleCore
{
    // battle-core-rebuild unit 1 — 하네스를 에디터에서 한 번 돌려 본다.
    //
    // **얇다.** 몸통은 전부 코어의 `CoreHarness`·`CoreGoldenCorpus` 에 있다 —
    // 메뉴가 시나리오를 「자기 방식으로」 세우기 시작하면 에디터에서 통과하는 것과
    // 헤드리스에서 통과하는 것이 갈린다.
    //
    // 옛 `SimHarnessRunner` 와 달리 Play 도 브리지도 필요 없다. 코어는 엔진을
    // 모르므로 EditMode 에서 그냥 돈다.
    public static class CoreHarnessRunMenu
    {
        [MenuItem("Somnia/Battle/BattleCore/Harness/Run Corpus (log only)")]
        public static void RunCorpus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[CoreHarness] 코퍼스 실행 — 파일을 쓰지 않는다(굽기는 Golden 메뉴).");
            foreach (var sc in CoreGoldenCorpus.All)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var trace = CoreGoldenCorpus.Run(sc);
                sw.Stop();
                sb.AppendLine($"  · {sc.Name}: 틱 {sc.Ticks} · 이벤트 {trace.events.Count} · "
                              + $"configHash {trace.configHash} · stateHash {trace.finalStateHash:X16} "
                              + $"({sw.ElapsedMilliseconds} ms)");
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Somnia/Battle/BattleCore/Harness/Print Tick Order")]
        public static void PrintTickOrder()
        {
            var match = new BattleMatch(CoreGoldenCorpus.Fixture(0));
            var sb = new StringBuilder("[CoreHarness] 틱 단계 순서 — 이 순서가 계약이다\n");
            for (int i = 0; i < match.Pipeline.PhaseCount; i++)
                sb.Append("  ").Append(i).Append(". ").AppendLine(match.Pipeline.PhaseAt(i).Name);
            Debug.Log(sb.ToString());
        }
    }
}
