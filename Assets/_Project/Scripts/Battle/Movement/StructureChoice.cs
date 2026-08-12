using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // instinct-content unit 3 — 「어느 거점으로 갈까」의 규칙. 아키텍처 중립 순수 함수
    // (제약 10): ECS 도 시간도 모르고, 위치 하나와 후보 셀 목록만 해석한다.
    public static class StructureChoice
    {
        // 가장 가까운 후보의 인덱스. 후보가 없으면 -1.
        //
        // 동률은 **먼저 온 후보**가 이긴다 — 후보 순서가 저작 순서(GeneratedMap.structures)라
        // 결정론이 유지된다. seeded RNG 로 흔들면 같은 판이 매번 달라진다.
        public static int NearestIndex(float2 from, NativeArray<float2> candidates)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                float2 d = candidates[i] - from;
                float sq = d.x * d.x + d.y * d.y;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = i;
            }
            return best;
        }
    }
}
