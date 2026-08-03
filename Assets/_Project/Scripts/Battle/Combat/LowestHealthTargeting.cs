using Unity.Burst;
using Unity.Collections;

namespace Wassup.Battle.Combat
{
    // healer-lowest-health-targeting — pure "most-hurt ally" ranking.
    //
    // Architecture-neutral per CLAUDE.md constraint 10: decides which ally candidate
    // is the best heal/buff target from plain inputs; knows nothing about ECS.
    // AttackSystem builds candidates from its target snapshot (already filtered to the
    // ally mask, self-excluded, in-range) and consumes the chosen index. Mirrors
    // FrontmostTargeting so the two targeting rankings read the same way.
    //
    // Rank order (fully deterministic):
    //   1. hpRatio ascending  — lower current-health fraction = "more hurt" = ranks first
    //   2. sqDist  ascending   — attacker->candidate squared distance (nearer breaks ties)
    //   3. simId ascending — total tie-break (SimEntityId, 스폰 순 stable — unit 1)
    [BurstCompile]
    public static class LowestHealthTargeting
    {
        public struct Candidate
        {
            public float hpRatio;     // Health.ComputeRatio(value, max) in [0,1]
            public float sqDist;      // squared distance attacker->candidate
            public int simId;         // SimEntityId — unit 1 이 Entity.Index/Version 축 대체
        }

        // True when `a` ranks strictly before `b` under the fixed order above.
        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.hpRatio != b.hpRatio) return a.hpRatio < b.hpRatio;
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            return a.simId < b.simId;
        }

        // Index into `cands` [0, count) of the most-hurt candidate, or -1 if count == 0.
        // `count` lets callers pass an oversized scratch array.
        public static int SelectLowest(in NativeArray<Candidate> cands, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (best < 0 || RanksBefore(cands[i], cands[best])) best = i;
            }
            return best;
        }
    }
}
