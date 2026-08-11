using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Combat.Projectile
{
    // dreamcatcher-attack-mod-bounce unit 1 — the retarget DECISION for a
    // bouncing projectile: nearest living enemy within a Chebyshev tile radius of
    // the impact, excluding the just-hit target. Pure geometry — no world, no
    // system, no frame, no Entity. This is the architecture-neutral "logic" layer
    // (EditMode-testable with a plain float3 array); the ImpactSystem arm that
    // calls it is the thin ECS glue (unit 2). Mirrors TileAoe.IsInTileRange /
    // BallisticArc.ArcPosition — same pure-function-plus-EditMode pattern.
    public static class BounceRetarget
    {
        // Returns the positions index of the next bounce target, or -1 if none.
        // Skips excludeIndex (previous target); keeps candidates whose cell is
        // within Chebyshev tileRange of hitPos's cell; picks the smallest XZ
        // squared distance. Ties resolve to the lower index (snapshot order =
        // deterministic). tileRange <= 0 → -1.
        public static int FindNext(
            float3 hitPos, int excludeIndex,
            NativeArray<float3> positions,
            int tileRange, float tileSize, int2 gridSize, float3 origin)
            => FindNext(hitPos, excludeIndex, positions, default, 0,
                tileRange, tileSize, gridSize, origin);

        // waypoint-routing unit 4 rev 4 — same geometry with an optional
        // traversal-layer eligibility array aligned to positions. Keeping the
        // original overload preserves every legacy producer/test (mask 0).
        public static int FindNext(
            float3 hitPos, int excludeIndex,
            NativeArray<float3> positions,
            NativeArray<byte> targetTraversalLayers,
            byte attackTargetLayers,
            int tileRange, float tileSize, int2 gridSize, float3 origin)
        {
            if (tileRange <= 0) return -1;
            int2 centerCell = GridMath.WorldToCell(hitPos, tileSize, gridSize, origin);
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == excludeIndex) continue;
                if (targetTraversalLayers.IsCreated
                    && !Wassup.Data.PlacementLayers.CanTarget(
                        attackTargetLayers, targetTraversalLayers[i])) continue;
                float3 pos = positions[i];
                int2 cell = GridMath.WorldToCell(pos, tileSize, gridSize, origin);
                if (!TileAoe.IsInTileRange(cell, centerCell, tileRange)) continue;
                float dx = pos.x - hitPos.x;
                float dz = pos.z - hitPos.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestSq) // strict < → lower index wins ties (deterministic)
                {
                    bestSq = d2;
                    best = i;
                }
            }
            return best;
        }
    }
}
