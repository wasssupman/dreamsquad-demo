using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // bomb-thrower-defender unit 2 — 폭발 AoE 대상 상한 순수 선별 (sim-critical, EditMode).
    // 이미 착지 셀 범위(Chebyshev) 필터를 통과한 후보들 중 impact 중심 거리² 오름차순으로
    // 최대 cap 개를 고른다. ShieldTargeting.Select 의 All 분기 동형(선택 정렬).
    // cap<=0 = 무제한(전 인덱스 순서대로) — 기존 메테오/스킬/보스 TileAoe 경로 무회귀.
    // 동률은 인덱스 오름차순 = 결정론(비동기 토너먼트 양측 동일 시뮬).
    public static class AoeTargetCap
    {
        public static void SelectNearest(in NativeArray<float> distanceSq, int cap,
            ref NativeList<int> results)
        {
            results.Clear();
            int total = distanceSq.Length;
            if (cap <= 0)
            {
                for (int i = 0; i < total; i++) results.Add(i);
                return;
            }

            int count = math.min(cap, total);
            if (count <= 0) return;

            var picked = new NativeArray<bool>(total, Allocator.Temp);
            for (int n = 0; n < count; n++)
            {
                int best = -1;
                float bestKey = float.MaxValue;
                for (int i = 0; i < total; i++)
                {
                    if (picked[i]) continue;
                    if (distanceSq[i] < bestKey) // strict < → 동률은 앞 인덱스 승리
                    {
                        bestKey = distanceSq[i];
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
