using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 실드 대상 선별자. 구 `Wassup.Data.ShieldTargetFilter` 이식.
    ///
    /// 참조가 아니라 복제인 이유는 <see cref="Wassup.Sim.Combat.DcTriggerKind"/> 와 같다 —
    /// 원본이 저작(엔진) 계층에 있어 sim asmdef 가 못 잇는다. ⚠ append-only, 값 고정.
    /// </summary>
    public enum ShieldTargetFilter : byte
    {
        Self = 0,
        All = 1,
        MinHealth = 2,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 실드 캐스트 상태. 구 `ShieldCastState` 이식.
    ///
    /// 공격과 **독립된** 캐스트다(해저드 캐스트 선례) — 공격 쿨다운을 공유하지 않는다.
    /// `range` 는 저작 `attackRange` 의 복사값이다(별도 저작 필드가 없다).
    /// 쿨다운의 소유자는 `ShieldCastSystem` 이다.
    /// </summary>
    public struct ShieldCastState
    {
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining;
        public float amount;
        public int targetCount;
        public ShieldTargetFilter filter;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 후보의 정렬 키. 구 `ShieldCandidate` 이식.
    /// 후보는 이미 범위(Chebyshev 타일) 필터를 통과했고 **자신을 포함**한다.
    /// </summary>
    public struct ShieldCandidate
    {
        /// 캐스터→후보 월드 거리² (`All` 정렬 키).
        public float distanceSq;
        /// (HP + 실드합) / maxHP (`MinHealth` 정렬 키).
        public float effectiveHpRatio;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 실드 부여 원샷. 구 `ShieldGrantedEvent` 이식.
    /// **실제로 실드가 오른 대상**에만 나간다(no-op 재부여는 VFX 도 없다).
    /// </summary>
    public struct ShieldGrantedEvent
    {
        public SimVec3 position;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/5 — 대상 선별 순수 계산. 구 `ShieldTargeting` 이식.
    ///
    /// ⚠ **동률은 인덱스 오름차순이 이긴다**(strict `&lt;`). 비동기 토너먼트가 양측에서 같은
    /// 시뮬을 돌리므로 이 결정론이 곧 판정의 일치다. 정렬 라이브러리로 바꾸면 안 되는 이유이기도
    /// 하다 — 안정 정렬이 아니면 동률 순서가 구현에 따라 달라진다.
    ///
    /// 선택 정렬인 것도 의도다: 후보 수 상한이 그리드 크기라 O(C×N) 이 충분히 작고,
    /// 매 회 "미선택 중 최소" 를 뽑는 형태가 위 동률 규칙을 자명하게 만든다.
    /// </summary>
    public static class ShieldTargeting
    {
        /// 선택된 후보 인덱스를 <paramref name="results"/> 에 담는다(**내부에서 비운다**).
        public static void Select(ShieldTargetFilter filter, int targetCount, int selfIndex,
                                  List<ShieldCandidate> candidates, List<int> results)
        {
            results.Clear();
            if (filter == ShieldTargetFilter.Self)
            {
                if (selfIndex >= 0 && selfIndex < candidates.Count)
                    results.Add(selfIndex);
                return;
            }

            int count = targetCount < candidates.Count ? targetCount : candidates.Count;
            if (count <= 0) return;

            var picked = new bool[candidates.Count];
            for (int n = 0; n < count; n++)
            {
                int best = -1;
                float bestKey = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (picked[i]) continue;
                    float key = filter == ShieldTargetFilter.All
                        ? candidates[i].distanceSq
                        : candidates[i].effectiveHpRatio;
                    if (key < bestKey)
                    {
                        bestKey = key;
                        best = i;
                    }
                }
                if (best < 0) break;
                picked[best] = true;
                results.Add(best);
            }
        }
    }
}
