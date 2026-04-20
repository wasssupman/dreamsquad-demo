using Unity.Burst;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    [BurstCompile]
    public static class GridMath
    {
        public static int2 WorldToCell(float3 worldPos, float tileSize, int2 gridSize)
        {
            // Round half-away-from-zero-on-positive (floor(x + 0.5)) so 2.5 -> 3 consistently
            // for integer-grid lookup. Unity.Mathematics.math.round uses banker's rounding
            // (half-to-even) which would give 2.5 -> 2; we want predictable snap-up at half.
            int cx = (int)math.floor(worldPos.x / tileSize + 0.5f);
            int cy = (int)math.floor(worldPos.z / tileSize + 0.5f);
            return new int2(
                math.clamp(cx, 0, gridSize.x - 1),
                math.clamp(cy, 0, gridSize.y - 1)
            );
        }

        public static float3 CellToWorldCenter(int2 cell, float tileSize, float y = 0f)
            => new float3(cell.x * tileSize, y, cell.y * tileSize);

        public static int CellIndex(int2 cell, int2 gridSize) => cell.y * gridSize.x + cell.x;
    }
}
