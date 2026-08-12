using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.Data
{
    // elite-enemy-tier unit 6 rev(2단계 분열) — 분열 사슬의 저작 검증.
    //
    // 왜 필요해졌나: 초판 가드는 «자식이 메커닉을 갖고 있으면 경고» 였다. 1단계 분열에서는
    // 그게 재귀 방어로 충분했지만, **2단계 분열이 의도가 되는 순간 그 경고는 거짓 신호**가
    // 된다(중간 슬라임은 메커닉을 가져야 한다). 무한 분열을 실제로 만드는 것은 «자식이
    // 메커닉을 갖는 것» 이 아니라 **사슬이 자기에게 돌아오는 것**이므로, 판정을 그쪽으로 옮긴다.
    //
    // 분열은 슬롯을 만들지 않고 브리지 킬 드레인이 SO 를 직독하므로(DcPayloadKind.SplitOnDeath),
    // 이 사슬이 그대로 런타임 동작이다 — 순환이면 죽을 때마다 자식이 태어나 판이 끝나지 않는다.
    //
    // 순수 함수(SO 참조만 따라간다. Entities/Battle 무참조) — EditMode 테스트 대상.
    public static class SplitChain
    {
        // 사슬 길이 상한. 저작 사고(아주 긴 체인)까지 잡는 방어선이며 밸런스 값이 아니다.
        public const int MaxDepth = 8;

        // 사슬 **전체**가 만들 수 있는 자손 총수 상한. 깊이·폭 상한만으로는 부족하다 —
        // 폭 8 × 깊이 8 저작은 둘 다 통과하면서 8⁸ ≈ 1.7e7 엔티티를 만든다(각각 CreateEntity +
        // 약 20회 구조 변경 + 뷰). 두 상한이 서로 독립이라 «방어선처럼 보이는데 안 막는» 상태였다
        // (2026-08-12 ECS 리뷰 M1). 배송 콘텐츠는 2×2 = 총 6 자손이라 여유가 크다.
        public const int MaxTotalOffspring = 32;

        // root 에서 OnDeath × SplitOnDeath 를 따라 내려가며 순환·과길이·과팬아웃을 찾는다.
        // 반환 false + error = 저작 오류. 사슬이 없는 유닛(대부분)은 **할당 0 으로** 즉시 true.
        public static bool Validate(AttackUnitData root, out string error)
        {
            error = null;
            if (root == null) return true;
            // ★조기 반환 — 이 함수는 스폰마다 도는 bake 경로에서 불린다(에디터 전용이 아니다).
            // 적 대부분은 사슬이 없으므로 여기서 끊어 managed 할당을 아예 만들지 않는다.
            if (NextInChain(root) == null) return true;

            var seen = new HashSet<AttackUnitData>();
            AttackUnitData cur = root;
            int totalOffspring = 0;
            int generation = 1; // 이 단계에서 태어나는 개체 수(누적곱)

            for (int depth = 0; depth <= MaxDepth; depth++)
            {
                if (!seen.Add(cur))
                {
                    error = $"분열 사슬이 순환한다: {Name(root)} … {Name(cur)} " +
                            "(죽을 때마다 자식이 태어나 판이 끝나지 않는다)";
                    return false;
                }

                AttackUnitData next = NextInChain(cur);
                if (next == null) return true;   // 사슬 종료 — 정상

                // 이 단계의 자손 수 = (직전 세대 수) × (이 유닛의 자식 수).
                generation = generation * math.max(1, CountAt(cur));
                totalOffspring += generation;
                if (totalOffspring > MaxTotalOffspring)
                {
                    error = $"분열 사슬의 자손 총수가 {MaxTotalOffspring} 를 넘는다" +
                            $"(≥{totalOffspring}): {Name(root)} … {Name(cur)} → {Name(next)}";
                    return false;
                }

                cur = next;
            }

            error = $"분열 사슬이 {MaxDepth} 단계를 넘는다: {Name(root)} … {Name(cur)} (저작 실수로 보인다)";
            return false;
        }

        // 이 유닛이 죽을 때 태어나는 자식 수(첫 SplitOnDeath 슬롯). 사슬이 없으면 0.
        public static int CountAt(AttackUnitData unit)
        {
            var mechanics = unit?.nightmareMechanics;
            if (mechanics == null) return 0;
            for (int i = 0; i < mechanics.Length; i++)
            {
                if (mechanics[i].trigger.kind != DcTriggerKind.OnDeath) continue;
                if (mechanics[i].payload.kind != DcPayloadKind.SplitOnDeath) continue;
                return (int)mechanics[i].payload.magnitude;
            }
            return 0;
        }

        // 이 유닛이 죽을 때 태어나는 자식 SO. 없으면 null.
        // 첫 SplitOnDeath 슬롯만 본다 — 런타임(SpawnSplitChildren)과 같은 규약이다.
        public static AttackUnitData NextInChain(AttackUnitData unit)
        {
            var mechanics = unit?.nightmareMechanics;
            if (mechanics == null) return null;
            for (int i = 0; i < mechanics.Length; i++)
            {
                if (mechanics[i].trigger.kind != DcTriggerKind.OnDeath) continue;
                if (mechanics[i].payload.kind != DcPayloadKind.SplitOnDeath) continue;
                return mechanics[i].payload.splitUnit;
            }
            return null;
        }

        private static string Name(AttackUnitData u)
            => u == null ? "<null>" : (string.IsNullOrEmpty(u.displayName) ? u.name : u.displayName);
    }
}
