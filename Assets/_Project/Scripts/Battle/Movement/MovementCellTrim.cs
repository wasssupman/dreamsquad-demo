using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    // Static cell-trim utilities called from Burst-compiled MovementSystem.
    public static class MovementCellTrim
    {
        // Inset to keep the clamped position strictly inside currentCell.
        // WorldToCell rounds 0.5 up to the next cell, so without this offset a position at
        // exactly ±0.5*tileSize would be mapped to the adjacent blocked cell, breaking the
        // trim invariant (currentCell != targetCell) on the next frame.
        private const float kBoundaryEpsilon = 1e-3f;

        public static bool IsWallCell(int2 cell, in FlowFieldSingleton field)
        {
            if (cell.x < 0 || cell.x >= field.gridSize.x ||
                cell.y < 0 || cell.y >= field.gridSize.y)
                return true;
            if (cell.Equals(field.goalCell))
                return false;
            return math.lengthsq(field.flow[GridMath.CellIndex(cell, field.gridSize)]) < 1e-6f;
        }

        public static float3 ClampToBoundary(float3 desired, int2 currentCell, float tileSize)
        {
            float half = tileSize * 0.5f - kBoundaryEpsilon;
            return new float3(
                math.clamp(desired.x, currentCell.x * tileSize - half, currentCell.x * tileSize + half),
                desired.y,
                math.clamp(desired.z, currentCell.y * tileSize - half, currentCell.y * tileSize + half));
        }
    }
}
