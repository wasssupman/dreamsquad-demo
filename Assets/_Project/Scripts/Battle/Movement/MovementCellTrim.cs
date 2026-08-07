using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    // Static cell-trim utilities called from Burst-compiled MovementSystem.
    public static class MovementCellTrim
    {
        // continuous-agent-movement unit 1 — 벽 술어의 소유자는 NavGrid 로 옮겼다.
        // 여기 남은 것은 ECS 싱글턴 → NavGrid 조립 하나뿐이다(호출자 편의). 술어를 다시
        // 쓰지 않는다 — 벽 판정이 바뀔 때 고칠 곳은 NavGrid 하나여야 한다.
        public static NavGrid BuildNavGrid(
            in FlowFieldSingleton field,
            bool hasObstacles,
            in ObstacleSingleton obstacles)
            => new NavGrid(
                staticWalk:   field.walkMask,
                blockedCells: hasObstacles ? obstacles.blockedCells : default,
                hasObstacles: hasObstacles,
                gridSize:     field.gridSize,
                tileSize:     field.tileSize,
                origin:       field.origin,
                flow:         field.flow,
                goals:        field.goals,
                goalCell:     field.goalCell);

        // summon-patrol-defender — BFS 소비자용 walk 마스크(1 = 걸을 수 있음).
        // outMask 는 gridSize.x * gridSize.y 길이여야 한다(호출자 책임).
        public static void FillWalkMask(
            in FlowFieldSingleton field,
            bool hasObstacles,
            in ObstacleSingleton obstacles,
            NativeArray<byte> outMask)
            => BuildNavGrid(in field, hasObstacles, in obstacles).MaterializeWalkMask(outMask);

        // Inset to keep the clamped position strictly inside currentCell.
        // WorldToCell rounds 0.5 up to the next cell, so without this offset a position at
        // exactly ±0.5*tileSize would be mapped to the adjacent blocked cell, breaking the
        // trim invariant (currentCell != targetCell) on the next frame.
        private const float kBoundaryEpsilon = 1e-3f;

        // continuous-agent-movement unit 1 — 술어 본체는 NavGrid.IsBlocked 가 소유한다.
        // 이 오버로드는 장애물을 보지 않는 "정적 벽만" 질의라 기존 호출부·테스트 계약이 그대로다
        // (장애물 검사는 예나 지금이나 호출자가 따로 합성한다).
        // unit 2 에서 은퇴 — 그때 NavGrid.IsBlocked 로 완전히 흡수된다.
        public static bool IsWallCell(int2 cell, in FlowFieldSingleton field)
            => BuildNavGrid(in field, hasObstacles: false, in DefaultObstacles).IsBlocked(cell);

        private static readonly ObstacleSingleton DefaultObstacles = default;

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
            => Apply(desired, currentCell, BuildNavGrid(in field, hasObstacles, in obstacles));

        // continuous-agent-movement unit 1 — NavGrid 만 받는 본체. 이후 unit 들이 쓰는 형태다.
        public static float3 Apply(float3 desired, int2 currentCell, in NavGrid nav)
        {
            int2 targetCell = GridMath.WorldToCell(desired, nav.tileSize, nav.gridSize, origin: nav.origin);
            if (currentCell.Equals(targetCell)) return desired;
            return nav.IsBlocked(targetCell)
                ? ClampToBoundary(desired, currentCell, nav.tileSize, origin: nav.origin)
                : desired;
        }
    }
}
