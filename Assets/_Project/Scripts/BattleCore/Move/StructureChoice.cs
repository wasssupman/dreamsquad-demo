// salvaged from Assets/_Project/Scripts/Battle/Movement/StructureChoice.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray<T>` → `T[] + count`.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Move
{
    // 「어느 거점으로 갈까」의 규칙. 위치 하나와 후보 칸 목록만 해석한다.
    public static class StructureChoice
    {
        // 후보 정렬 기준 — 칸 사전순(x → y).
        //
        // ⚠ **이 기준을 전투 코어와 예고선이 함께 쓴다**(M18). 한쪽만 정렬하면 「가이드 ≠ 실제
        // 이동선」이 동률에서만 간헐적으로 재현되는, 가장 잡기 싫은 형태로 돌아온다.
        public static bool IsBefore(int2 a, int2 b)
            => a.x != b.x ? a.x < b.x : a.y < b.y;

        // 규칙 한 줄: **내가 팰 수 있는 거점 중 가장 가까운 것.** 없으면 -1.
        //
        // 「팰 수 있는가」는 진영 비트로만 묻는다 — 종류를 열거하지 않는다. 마음이든 본능이든,
        // 방어측이든 적측이든 같은 규칙을 받는다. 여기서 특정 진영 상수를 박으면 「본능만
        // 특별하다」가 되고, 그건 타게팅에서 이미 한 번 걷어낸 실수다.
        //
        // 동률은 **먼저 온 후보**가 이긴다. 「먼저」의 기준은 호출자가 정한다 — 호출자는 후보를
        // 칸 사전순(`IsBefore`)으로 정렬해 넘긴다.
        public static int NearestIndex(float2 from, float2[] candidates, int[] factions,
                                       int count, int targetMask)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < count; i++)
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
