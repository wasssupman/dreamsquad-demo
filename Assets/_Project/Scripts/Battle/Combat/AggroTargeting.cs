using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 9 — 정의 계층. 가디언의 공격 타겟 선정(누구를 때릴지).
    // 히트 모델의 자석 작동 핵심: 여유가 있으면 아직 안 끌린 적을 우선 때려 신규
    // 팩을 흡수하고, 상한이 차면 겹친 어그로 팩을 정리한다. 순수 기하 — no system,
    // no frame, no Entity. Candidate 는 인덱스로만 참조된다(BounceRetarget 선례).
    //
    // gCell 은 Chebyshev 사거리 게이트용, gPos 는 XZ 거리 정렬용 (AttackSystem 의
    // atkCell/atkPos 이원화와 동일). NativeArray 는 BounceRetarget 과 통일.
    public struct AggroCandidate
    {
        public int2 cell;    // 사거리 판정용 셀
        public float3 pos;   // 최근접 정렬용 월드 위치
        public bool aggroed; // 이미 어그로된 적인가(선점 상태)
    }

    public static class AggroTargeting
    {
        // outIdx 에 이번 공격이 때릴 candidate 인덱스를 채우고 개수를 반환.
        // held < capacity → 비-어그로 최근접 우선 채움 + 부족분 일반 최근접.
        // held >= capacity → 일반 최근접(겹친 어그로 팩 정리).
        public static int SelectTargets(
            int2 gCell, float3 gPos, int tileRange, int held, int capacity,
            NativeArray<AggroCandidate> cands, NativeArray<int> outIdx)
        {
            int maxTargets = outIdx.Length;
            if (maxTargets <= 0 || tileRange < 0) return 0;

            int count = 0;
            // Pass A — 여유가 있으면 비-어그로만으로 먼저 채운다(신규 팩 흡수).
            if (held < capacity)
                count = FillNearest(gCell, gPos, tileRange, cands, outIdx, count, freshOnly: true);
            // Pass B — 남은 슬롯을 일반 최근접으로 채운다(이미 뽑힌 인덱스 제외).
            count = FillNearest(gCell, gPos, tileRange, cands, outIdx, count, freshOnly: false);
            return count;
        }

        private static int FillNearest(
            int2 gCell, float3 gPos, int tileRange,
            NativeArray<AggroCandidate> cands, NativeArray<int> outIdx, int count, bool freshOnly)
        {
            int maxTargets = outIdx.Length;
            while (count < maxTargets)
            {
                int best = -1;
                float bestSq = float.MaxValue;
                for (int i = 0; i < cands.Length; i++)
                {
                    if (AlreadyPicked(outIdx, count, i)) continue;
                    var c = cands[i];
                    if (freshOnly && c.aggroed) continue;
                    if (!TileAoe.IsInTileRange(c.cell, gCell, tileRange)) continue;
                    float dx = c.pos.x - gPos.x;
                    float dz = c.pos.z - gPos.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestSq) // strict < → lower index wins ties (deterministic)
                    {
                        bestSq = d2;
                        best = i;
                    }
                }
                if (best < 0) break;
                outIdx[count++] = best;
            }
            return count;
        }

        private static bool AlreadyPicked(NativeArray<int> outIdx, int count, int i)
        {
            for (int k = 0; k < count; k++)
                if (outIdx[k] == i) return true;
            return false;
        }
    }
}
