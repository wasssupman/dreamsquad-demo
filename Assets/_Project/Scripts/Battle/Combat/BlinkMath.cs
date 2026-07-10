using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 3 — SelfBlink destination math, pure and
    // EditMode-pinned (제약 10). NaN-free and terminating by construction:
    // the two failure modes 렌즈 A flagged (MED-4/N1 degenerate direction,
    // MED-5 unbounded ring walk) are closed here, not at the call site.
    public static class BlinkMath
    {
        // World -Z: the degenerate-direction fallback axis. A COMPILE-TIME
        // constant on purpose — runtime-derived axes (velocity, path forward)
        // can themselves be zero/undefined and reintroduce NaN (N1).
        private static readonly float3 FallbackAxis = new float3(0f, 0f, -1f);

        // Destination = leader + 1 tile along the reverse of the leader→boss
        // direction (i.e., through the leader, away from where the boss came
        // from). Direction shorter than epsilon (boss on/next to the leader)
        // falls back to the constant axis. Y is passed through from the leader
        // — the blink consumer keeps the mover's own Y anyway.
        public static float3 OffsetDest(float3 leaderPos, float3 bossPos, float tileSize)
        {
            float3 dir = leaderPos - bossPos;
            dir.y = 0f;
            float lenSq = math.lengthsq(dir);
            float3 axis = lenSq < 1e-6f ? FallbackAxis : dir * math.rsqrt(lenSq);
            return leaderPos + axis * tileSize;
        }

        // Nearest landing cell to `desired` that the flow field can reach
        // (dist != int.MaxValue — walkable AND connected, so the boss never
        // blinks into a sealed pocket). Chebyshev rings r = 0..maxRingRadius,
        // row-major within each ring — fully deterministic. Returns false when
        // no candidate exists inside the cap (caller skips the blink; the
        // threshold latch stays consumed).
        public static bool TryFindLandingCell(
            int2 desired, in NativeArray<int> dist, int2 gridSize, int maxRingRadius, out int2 landing)
        {
            for (int r = 0; r <= maxRingRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue; // ring shell only
                        int2 c = new int2(desired.x + dx, desired.y + dy);
                        if (c.x < 0 || c.y < 0 || c.x >= gridSize.x || c.y >= gridSize.y) continue;
                        if (dist[c.y * gridSize.x + c.x] == int.MaxValue) continue;
                        landing = c;
                        return true;
                    }
                }
            }
            landing = default;
            return false;
        }
    }
}
