using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;

namespace Wassup.EditorTools.BattleCore
{
    // battle-core-rebuild unit 1 — 새 코어 골든의 **정본 베이커**.
    //
    // 파일 자리·왕복 게이트는 코어의 `CoreGoldenStore` 가 갖는다(헤드리스 lane 과 한 벌).
    // 이 메뉴가 갖는 것은 **사람에게 보고하는 일**뿐이다.
    //
    // 옛 `SimGoldenMenu` 에서 그대로 가져온 규율 둘:
    //   ① **`Bake Missing` 과 전체 재생성은 다른 동작이다.** 전체 재생성은 기존 기준선을
    //      «지금 코드» 로 덮어써 그 시나리오가 지키던 회귀 감시를 무효로 만든다.
    //      그래서 이 unit 은 `Bake Missing` 만 연다 — 리베이스는 의도적으로만 한다.
    //   ② **빈 트레이스를 저장하지 않는다.** 통과하지만 아무것도 증언하지 않는 골든이
    //      가장 비싼 실패다(옛 코퍼스가 203 커밋 동안 킬 0 으로 통과했다).
    public static class CoreGoldenMenu
    {
        [MenuItem("Wassup/BattleCore/Golden/Bake Missing")]
        public static void BakeMissing()
        {
            if (!TryDir(out string dir)) return;

            int baked = 0, kept = 0;
            foreach (var sc in CoreGoldenCorpus.All)
            {
                if (CoreGoldenStore.Exists(sc.Name)) { kept++; continue; }

                var trace = CoreGoldenCorpus.Run(sc);
                if (trace.events.Count == 0 || string.IsNullOrEmpty(trace.configHash))
                {
                    Debug.LogError($"[CoreGolden] '{sc.Name}' 이 비었다(이벤트 {trace.events.Count}, "
                                   + $"configHash '{trace.configHash}') — **저장하지 않는다.**");
                    continue;
                }
                CoreGoldenStore.Write(sc.Name, trace);
                baked++;
                Debug.Log($"[CoreGolden] '{sc.Name}' 저장 — 이벤트 {trace.events.Count}, "
                          + $"stateHash {trace.finalStateHash:X16}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CoreGolden] {dir} — 신규 {baked}건 · 기존 유지 {kept}건");
        }

        [MenuItem("Wassup/BattleCore/Golden/Verify")]
        public static void Verify()
        {
            if (!TryDir(out _)) return;

            var diffs = new List<string>();
            foreach (var sc in CoreGoldenCorpus.All)
            {
                if (!CoreGoldenStore.Exists(sc.Name))
                {
                    diffs.Add($"`{sc.Name}` — 골든 파일이 없다. 먼저 `Bake Missing`.");
                    continue;
                }
                var golden = CoreGoldenStore.Read(sc.Name);
                string diff = golden.DiffAgainst(CoreGoldenCorpus.Run(sc));
                if (diff != null) diffs.Add($"`{sc.Name}` — {diff}");
            }

            if (diffs.Count == 0)
            {
                Debug.Log($"[CoreGolden] 코퍼스 {CoreGoldenCorpus.All.Length}건 전부 일치.");
                return;
            }
            foreach (var d in diffs) Debug.LogError("[CoreGolden] " + d);
            Debug.LogError($"[CoreGolden] {diffs.Count}건 불일치. "
                           + "configHash 가 다르면 코드 회귀가 아니라 **조건 드리프트**다.");
        }

        // ⚠ 조용히 return 하지 않는다 — 실패를 로그로 말하지 않으면 다음 사람이
        // 「아무 말 없었으니 통과」로 읽는다. 옛 골든 메뉴가 그 함정에 두 번 빠졌다.
        private static bool TryDir(out string dir)
        {
            dir = CoreGoldenStore.Dir;
            if (dir != null) return true;
            Debug.LogError("[CoreGolden] 저장소 루트를 못 찾았다 — 한 건도 실행하지 않았다. "
                           + $"표식 '{CoreGoldenStore.RelativeDir}' 가 있는지 확인하라.");
            return false;
        }
    }
}
