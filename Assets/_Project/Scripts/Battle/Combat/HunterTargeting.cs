using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // enemy-hunter-targeting unit 0 — pure nearest-pick geometry, EditMode-pinned
    // (제약 10 — sim-critical 타겟팅). Operates on plain arrays so the FSM's
    // ECS-coupled eligibility filter (faction/class) stays separate from the
    // architecture-neutral "which eligible one is nearest" decision.
    public static class HunterTargeting
    {
        // Index (into cells/tieKeys) of the Chebyshev-nearest cell to origin.
        // Distance ties break to the lower tieKey (= entity index → snapshot-order
        // independent). Empty → -1. Pure + Burst-safe.
        public static int NearestIndex(int2 origin, in NativeArray<int2> cells, in NativeArray<int> tieKeys)
        {
            int best = -1;
            int bestDist = int.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                int d = math.max(math.abs(cells[i].x - origin.x), math.abs(cells[i].y - origin.y));
                if (d < bestDist || (d == bestDist && best >= 0 && tieKeys[i] < tieKeys[best]))
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
