using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    // shield-guardian-defender unit 1 — 캐스트 대상 후보의 정렬 키.
    // 후보는 이미 범위(Chebyshev 타일) 필터를 통과한 것들이며 자신을 포함한다.
    public struct ShieldCandidate
    {
        public float distanceSq;        // 캐스터→후보 world 거리² (All 정렬 키)
        public float effectiveHpRatio;  // (HP+실드합)/maxHP (MinHealth 정렬 키 — 계약 6)
    }

    // 대상 선별 순수 계산 — sim-critical, EditMode 검증 대상 (제약 10).
    public static class ShieldTargeting
    {
        // 선택된 candidate 인덱스를 results 에 담는다(내부에서 Clear).
        // 동률은 인덱스 오름차순 = 결정론(비동기 토너먼트 양측 동일 시뮬).
        public static void Select(ShieldTargetFilter filter, int targetCount, int selfIndex,
            in NativeArray<ShieldCandidate> candidates, ref NativeList<int> results)
        {
            results.Clear();
            if (filter == ShieldTargetFilter.Self)
            {
                if (selfIndex >= 0 && selfIndex < candidates.Length)
                    results.Add(selfIndex);
                return;
            }

            int count = math.min(targetCount, candidates.Length);
            if (count <= 0) return;

            // 선택 정렬: 매 회 미선택 중 최소 키를 뽑는다. 후보 수가 그리드 상한이라
            // O(C×N) 이 충분히 작다. strict < 비교 → 동률은 앞 인덱스 승리.
            var picked = new NativeArray<bool>(candidates.Length, Allocator.Temp);
            for (int n = 0; n < count; n++)
            {
                int best = -1;
                float bestKey = float.MaxValue;
                for (int i = 0; i < candidates.Length; i++)
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
            picked.Dispose();
        }
    }
}
