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
                origin:       field.origin);

        // summon-patrol-defender — BFS 소비자용 walk 마스크(1 = 걸을 수 있음).
        // outMask 는 gridSize.x * gridSize.y 길이여야 한다(호출자 책임).
        public static void FillWalkMask(
            in FlowFieldSingleton field,
            bool hasObstacles,
            in ObstacleSingleton obstacles,
            NativeArray<byte> outMask)
            => BuildNavGrid(in field, hasObstacles, in obstacles).MaterializeWalkMask(outMask);

        // traversal-layers unit 3 — **층 인지 walk 마스크.** 지형은 «셀 층 ∩ 유닛 통행 층»,
        // 장애물 합성은 `NavGrid` 가 한다(계약 4 — 벽 술어는 하나).
        //
        // 이 오버로드가 생긴 이유는 조립 지점이 셋이 됐기 때문이다: 재빌드(슬롯별)·순찰
        // 필드(유닛별)·그리고 기존 호출처들. `new NavGrid(...)` 를 각자 쓰면 tileSize·origin·
        // gridSize 를 어디서 가져오는지가 세 벌이 된다 — 이 파일 헤더가 못박은 "조립은 하나"
        // 계약이 깨진다.
        //
        // `outMask` 를 층 마스크 버퍼로 먼저 쓰고 그대로 staticWalk 로 넘긴다(임시 배열 없음).
        // `MaterializeWalkMask` 가 셀마다 **자기 인덱스만** 읽고 쓰므로 in-place 가 안전하다.
        public static void FillWalkMask(
            in FlowFieldSingleton field,
            byte traversalLayers,
            bool hasObstacles,
            in ObstacleSingleton obstacles,
            NativeArray<byte> outMask)
        {
            // cellLayers 미생성(직접 초기화 픽스처) → 현행 walkMask 경로.
            if (!field.cellLayers.IsCreated || field.cellLayers.Length != outMask.Length)
            {
                FillWalkMask(in field, hasObstacles, in obstacles, outMask);
                return;
            }

            TraversalSlots.FillWalkMask(in field.cellLayers, traversalLayers, outMask);
            // waypoint-routing unit 4 — Air 비트가 있으면 지상 차단 해저드는 벽이 아니다.
            // 라우팅·실이동 충돌·어그로 추격·가이드가 모두 이 조립을 공유하므로 여기서 한 번만
            // 결정한다. Ground|Air 같은 복합 마스크도 Air 경로를 열었으므로 같은 규칙이다.
            bool applyObstacles = hasObstacles
                && (traversalLayers & (byte)Wassup.Data.PlacementLayer.Air) == 0;
            new NavGrid(
                staticWalk:   outMask,
                blockedCells: applyObstacles ? obstacles.blockedCells : default,
                hasObstacles: applyObstacles,
                gridSize:     field.gridSize,
                tileSize:     field.tileSize,
                origin:       field.origin).MaterializeWalkMask(outMask);
        }

        // traversal-layers unit 5 — **층 인지 NavGrid.** 충돌·셀 트림이 쓰는 벽 질의를
        // 유닛의 통행 층으로 조립한다.
        //
        // unit 1b·3 은 라우팅(BFS) 마스크만 층 인지로 바꿨고, 충돌은 `field.walkMask`
        // (= Path 전용) 하나를 계속 봤다. 그래서 `Ground|Path` 유닛이 배치지에 서면 그 칸이
        // 충돌상 벽이라 자기 셀에 영원히 clamp 됐다 — 경로는 찾는데 발을 못 뗐다.
        //
        // `scratch` 는 호출자가 들고 다니는 길이 CellCount 버퍼다(프레임당 재사용).
        // 장애물은 `MaterializeWalkMask` 가 **이미 마스크에 구워** 놓으므로 NavGrid 에 다시
        // 넘기지 않는다 — 같은 판정에 해시 조회만 사라진다.
        public static NavGrid BuildNavGrid(
            in FlowFieldSingleton field,
            byte traversalLayers,
            bool hasObstacles,
            in ObstacleSingleton obstacles,
            NativeArray<byte> scratch)
        {
            FillWalkMask(in field, traversalLayers, hasObstacles, in obstacles, scratch);
            return new NavGrid(
                staticWalk:   scratch,
                blockedCells: default,
                hasObstacles: false,
                gridSize:     field.gridSize,
                tileSize:     field.tileSize,
                origin:       field.origin);
        }

        // Inset to keep the clamped position strictly inside currentCell.
        // WorldToCell rounds 0.5 up to the next cell, so without this offset a position at
        // exactly ±0.5*tileSize would be mapped to the adjacent blocked cell, breaking the
        // trim invariant (currentCell != targetCell) on the next frame.
        private const float kBoundaryEpsilon = 1e-3f;

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
        //
        // 싱글턴을 그대로 받는 어댑터 — 테스트/레거시 호출 편의용이다. 프로덕션 경로는
        // 아래 NavGrid 오버로드다(프레임당 한 번 조립한 NavGrid 를 들고 다닌다).
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
