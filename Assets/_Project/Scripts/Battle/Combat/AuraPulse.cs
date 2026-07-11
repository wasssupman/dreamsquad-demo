using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // nightmare-whip-aura unit 0 — pulse target pick for AllyMoveSpeedAura:
    // every candidate within Chebyshev tileRange of the host cell, boundary
    // inclusive (same idiom as the TileAoe impact check). Pure math over plain
    // arrays (제약 10 — sim-critical targeting), EditMode-pinned; the caller
    // snapshots the same-faction pool and owns entity identity — host
    // self-exclusion is NOT done here (a same-cell ally must still be hit).
    public static class AuraPulse
    {
        // Fills `results` (cleared on entry — safe to reuse across pulses) with
        // the indices into `candidateCells` within `tileRange` of `hostCell`.
        // Negative tileRange selects nothing (degenerate guard).
        public static void SelectTargets(in NativeArray<int2> candidateCells, int2 hostCell,
                                         int tileRange, ref NativeList<int> results)
        {
            results.Clear();
            if (tileRange < 0) return;
            for (int i = 0; i < candidateCells.Length; i++)
            {
                int2 d = math.abs(candidateCells[i] - hostCell);
                if (math.max(d.x, d.y) <= tileRange) results.Add(i);
            }
        }
    }
}
