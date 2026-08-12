using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // instinct-content unit 3 — 「어느 거점으로 갈까」의 규칙. 아키텍처 중립 순수 함수
    // (제약 10): ECS 도 시간도 모르고, 위치 하나와 후보 셀 목록만 해석한다.
    public static class StructureChoice
    {
        // 규칙 한 줄: **내가 팰 수 있는 거점 중 가장 가까운 것.** 없으면 -1.
        //
        // 「팰 수 있는가」는 진영 비트로만 묻는다 — 종류를 열거하지 않는다. 마음이든 본능이든,
        // 방어측이든 적측이든 같은 규칙을 받는다. 여기서 `DefenderInstinct` 같은 상수를 박으면
        // 「본능만 특별하다」가 되고, 그건 타게팅에서 이미 한 번 걷어낸 실수다
        // (`EnemyTargetDefaults` — 기본 마스크를 열거로 적었다가 방어 본능이 무적이 됐다).
        //
        // 동률은 **먼저 온 후보**가 이긴다. 「먼저」의 기준은 호출자가 정한다 — 호출자는
        // 후보를 셀 사전순으로 정렬해 넘긴다(ECS 쿼리 순서는 스폰·사망 이력이라 보장이 아니다).
        public static int NearestIndex(
            float2 from, NativeArray<float2> candidates, NativeArray<int> factions, int targetMask)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                if ((factions[i] & targetMask) == 0) continue;
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
