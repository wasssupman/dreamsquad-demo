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

        // origin = board world origin (Tilemap mode = zero). Default zero keeps
        // legacy callers identical. Cell boundaries are offset by origin so the trim
        // invariant holds in board-local space. See docs/spec/map-origin-placement.
        public static float3 ClampToBoundary(float3 desired, int2 currentCell, float tileSize, float3 origin = default)
        {
            float half = tileSize * 0.5f - kBoundaryEpsilon;
            float centerX = origin.x + currentCell.x * tileSize;
            float centerZ = origin.z + currentCell.y * tileSize;
            return new float3(
                math.clamp(desired.x, centerX - half, centerX + half),
                desired.y,
                math.clamp(desired.z, centerZ - half, centerZ + half));
        }

        // enemy-tile-movement-integrity unit 2 — flow 분기와 aggro 분기가 공유하는 cell-trim.
        // desired 가 wall(zero-flow) 또는 obstacle 셀로 넘어가면 currentCell 경계로 clamp.
        // 모든 이동 모드(flow follow, aggro target chase)를 walk 타일 위에 묶는 단일 지점.
        public static float3 Apply(float3 desired, int2 currentCell, in FlowFieldSingleton field,
                                   bool hasObstacles, in ObstacleSingleton obstacles)
        {
            int2 targetCell = GridMath.WorldToCell(desired, field.tileSize, field.gridSize, origin: field.origin);
            if (currentCell.Equals(targetCell)) return desired;
            bool isWall = IsWallCell(targetCell, in field);
            if (!isWall && hasObstacles) isWall = obstacles.blockedCells.Contains(targetCell);
            return isWall ? ClampToBoundary(desired, currentCell, field.tileSize, origin: field.origin) : desired;
        }
    }
}
