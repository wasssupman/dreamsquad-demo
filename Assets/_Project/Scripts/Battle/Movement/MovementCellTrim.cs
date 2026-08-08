using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    // Static cell-trim utilities called from Burst-compiled MovementSystem.
    public static class MovementCellTrim
    {
        // summon-patrol-defender — BFS 소비자용 walk 마스크(1 = 걸을 수 있음).
        //
        // "무엇이 걸을 수 있는 칸인가" = IsWallCell + 장애물 이라는 합성이 이미 이 클래스의
        // Apply 안에 있다. 그 합성을 그리드 전체로 돌리는 코드가 AggroStateSystem 과
        // PatrolFieldSystem 두 곳에 그대로 복제돼 있었다 — 제약 10 의 (b) 호출처 2+ 와
        // (c) sim-critical(마스크가 틀리면 이동 전체가 깨진다)을 동시에 충족하므로 여기로 모은다.
        // 술어의 단일 정의를 유지하는 것이 목적이다: 벽 판정이 바뀔 때 한 곳만 고치면 된다.
        //
        // outMask 는 gridSize.x * gridSize.y 길이여야 한다(호출자 책임).
        public static void FillWalkMask(
            in FlowFieldSingleton field,
            bool hasObstacles,
            in ObstacleSingleton obstacles,
            NativeArray<byte> outMask)
        {
            for (int y = 0; y < field.gridSize.y; y++)
            for (int x = 0; x < field.gridSize.x; x++)
            {
                var cell = new int2(x, y);
                bool wall = IsWallCell(cell, in field)
                            || (hasObstacles && obstacles.blockedCells.Contains(cell));
                outMask[GridMath.CellIndex(cell, field.gridSize)] = wall ? (byte)0 : (byte)1;
            }
        }

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
            // multi-goal-map — 골 셀은 flow=0 이라 아래 zero-flow=wall 규칙에 걸린다. 모든 골을
            // wall 예외로 빼 적이 골 밖으로 clamp 되지 않게(IsGoalCell = goals 멤버십/goalCell 폴백).
            if (field.IsGoalCell(cell))
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

        // aggro-tile-chase unit 2 — 프레임당 XZ 변위 상한(0.9×tile). Apply 의 단일 목적 셀
        // 검사는 "한 프레임에 최대 인접 셀"을 전제한다 — dt 스파이크/강한 임펄스가 이 전제를
        // 깨고 벽을 건너뛰는 터널링을 상한으로 차단한다. trim(Apply) 직전에 호출.
        public static float3 ClampDisplacement(float3 current, float3 desired, float tileSize)
        {
            float dx = desired.x - current.x;
            float dz = desired.z - current.z;
            float maxD = tileSize * 0.9f;
            float lsq = dx * dx + dz * dz;
            if (lsq <= maxD * maxD) return desired;
            float scale = maxD / math.sqrt(lsq);
            return new float3(current.x + dx * scale, desired.y, current.z + dz * scale);
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
