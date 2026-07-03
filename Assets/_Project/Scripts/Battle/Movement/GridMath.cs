using Unity.Burst;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    [BurstCompile]
    public static class GridMath
    {
        // origin = board world origin (Tilemap mode = zero). Default zero keeps
        // legacy callers identical until they pass the captured board origin. See
        // docs/spec/map-origin-placement.
        public static int2 WorldToCell(float3 worldPos, float tileSize, int2 gridSize, float3 origin = default)
        {
            // Round half-away-from-zero-on-positive (floor(x + 0.5)) so 2.5 -> 3 consistently
            // for integer-grid lookup. Unity.Mathematics.math.round uses banker's rounding
            // (half-to-even) which would give 2.5 -> 2; we want predictable snap-up at half.
            float3 local = worldPos - origin;
            int cx = (int)math.floor(local.x / tileSize + 0.5f);
            int cy = (int)math.floor(local.z / tileSize + 0.5f);
            return new int2(
                math.clamp(cx, 0, gridSize.x - 1),
                math.clamp(cy, 0, gridSize.y - 1)
            );
        }

        public static float3 CellToWorldCenter(int2 cell, float tileSize, float y = 0f, float3 origin = default)
            => origin + new float3(cell.x * tileSize, y, cell.y * tileSize);

        public static int CellIndex(int2 cell, int2 gridSize) => cell.y * gridSize.x + cell.x;

        public static int ChebyshevDistance(int2 a, int2 b)
            => math.cmax(math.abs(a - b));

        // half-away-from-zero rounding — avoids banker's rounding from math.round.
        public static int RangeToTiles(float r)
            => (int)(r + 0.5f);
    }
}
