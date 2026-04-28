using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    // Static cell-trim utilities called from Burst-compiled MovementSystem.
    public static class MovementCellTrim
    {
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
            float half = tileSize * 0.5f;
            return new float3(
                math.clamp(desired.x, currentCell.x * tileSize - half, currentCell.x * tileSize + half),
                desired.y,
                math.clamp(desired.z, currentCell.y * tileSize - half, currentCell.y * tileSize + half));
        }
    }
}
