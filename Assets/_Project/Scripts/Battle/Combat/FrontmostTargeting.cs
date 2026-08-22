using Unity.Burst;
using Unity.Collections;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-content-2 끝을 보는 눈 — pure "frontmost" ranking.
    //
    // Architecture-neutral per CLAUDE.md constraint 10: the ranking decides a value
    // (which candidate is frontmost) from plain inputs and knows nothing about ECS.
    // AttackSystem does the ECS-side work (build candidates from its target snapshot,
    // exclude DeadTag/PastGoal/out-of-range/mask before calling), then consumes the
    // chosen index. Unreachable candidates (flowDist == UnreachableDist) are ignored
    // here so the caller does not need a second filter pass.
    //
    // Rank order (fully deterministic — README contract 1):
    //   1. flowDist ascending  — smaller BFS remaining-cost to the goal is "more frontmost"
    //   2. sqDist  ascending   — attacker→candidate XZ squared distance
    //   3. simId ascending — total tie-break (spawn order, match-stable)
    [BurstCompile]
    public static class FrontmostTargeting
    {
        // Mirrors FlowFieldSingleton.dist sentinel for unreachable cells.
        public const int UnreachableDist = int.MaxValue;

        public struct Candidate
        {
            public int flowDist;      // FlowFieldSingleton.dist[cell]; UnreachableDist = excluded
            public float sqDist;      // XZ squared distance attacker→candidate
            public int simId;         // SimEntityId.value (unassigned = SimEntityId.Unassigned)
        }

        // True when `a` ranks strictly before `b` under the fixed order above.
        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.flowDist != b.flowDist) return a.flowDist < b.flowDist;
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            return a.simId < b.simId;
        }

        // Index into `cands` [0, count) of the frontmost reachable candidate, or -1 if
        // none is reachable. `count` lets callers pass an oversized scratch array.
        public static int SelectFrontmost(in NativeArray<Candidate> cands, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (cands[i].flowDist == UnreachableDist) continue;
                if (best < 0 || RanksBefore(cands[i], cands[best])) best = i;
            }
            return best;
        }
    }
}
