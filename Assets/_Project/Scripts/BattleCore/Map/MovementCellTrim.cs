// salvaged from Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: ECS 싱글턴 어댑터 오버로드 3개를 뺐다(그 타입이 코어에 없다). 남은 것은
//   층 인지 마스크 조립 · 경계 clamp · 변위 상한 · 칸 트림 — 전부 plain 값만 받는다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    public static class MovementCellTrim
    {
        // 경계에 정확히 붙으면 다음 틱 칸 판정이 흔들린다. 살짝 띄운다.
        // `WorldToCell` 이 0.5 를 위 칸으로 올리므로, 이 오프셋이 없으면 정확히 ±0.5 인
        // 위치가 옆의 막힌 칸으로 매핑돼 트림 불변식(현재 칸 ≠ 목표 칸)이 깨진다.
        private const float BoundaryEpsilon = 1e-3f;

        /// <summary>층 인지 walk 마스크. 지형은 «칸 층 ∩ 유닛 통행 층», 장애물 합성은 여기서 함께 굽는다.</summary>
        // ⚠ **Air 비트가 있으면 지상 장애물은 벽이 아니다.** 라우팅·실이동 충돌·추격·가이드가
        // 모두 이 조립을 공유하므로 여기서 한 번만 결정한다. `Ground|Air` 같은 복합 마스크도
        // Air 경로를 열었으므로 같은 규칙이다.
        public static void FillWalkMask(byte[] cellLayers, int2 gridSize, byte traversalLayers,
                                        bool hasObstacles, bool[] blockedCells, byte[] outMask)
        {
            TraversalSlots.FillWalkMask(cellLayers, traversalLayers, outMask);
            bool applyObstacles = hasObstacles && (traversalLayers & LayerBits.Air) == 0;
            // `outMask` 를 층 마스크 버퍼로 먼저 쓰고 그대로 staticWalk 로 넘긴다(임시 배열 없음).
            // `MaterializeWalkMask` 가 칸마다 **자기 인덱스만** 읽고 쓰므로 in-place 가 안전하다.
            new NavGrid(outMask, applyObstacles ? blockedCells : null, applyObstacles,
                        gridSize, 1f).MaterializeWalkMask(outMask);
        }

        /// <summary>층 인지 `NavGrid`. 충돌·칸 트림이 쓰는 벽 질의를 그 유닛의 통행 층으로 조립한다.</summary>
        // 층마다 다른 벽인 이유: 틱당 하나(Path 전용)면 Ground 를 여는 유닛이 배치지에 서는
        // 순간 자기 칸이 벽으로 읽혀 영원히 clamp 된다(경로는 찾는데 발을 못 뗐다).
        // 장애물은 `FillWalkMask` 가 **이미 마스크에 구워** 놓으므로 `NavGrid` 에 다시 넘기지 않는다.
        public static NavGrid BuildNavGrid(byte[] cellLayers, int2 gridSize, float tileSize,
                                           byte traversalLayers, bool hasObstacles, bool[] blockedCells,
                                           byte[] scratch)
        {
            FillWalkMask(cellLayers, gridSize, traversalLayers, hasObstacles, blockedCells, scratch);
            return new NavGrid(scratch, null, false, gridSize, tileSize);
        }

        public static float3 ClampToBoundary(float3 desired, int2 currentCell, float tileSize, float3 origin = default)
        {
            float half = tileSize * 0.5f - BoundaryEpsilon;
            float centerX = origin.x + currentCell.x * tileSize;
            float centerZ = origin.z + currentCell.y * tileSize;
            return new float3(
                math.clamp(desired.x, centerX - half, centerX + half),
                desired.y,
                math.clamp(desired.z, centerZ - half, centerZ + half));
        }

        // 틱당 XZ 변위 상한(0.9칸). 칸 트림의 단일 목적 칸 검사는 「한 틱에 최대 인접 칸」을
        // 전제한다 — 강한 외력이 그 전제를 깨고 벽을 건너뛰는 터널링을 상한으로 차단한다.
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

        // 모든 이동 모드(흐름 추종·추격·순찰)를 걷는 칸 위에 묶는 단일 지점.
        public static float3 Apply(float3 desired, int2 currentCell, in NavGrid nav)
        {
            int2 targetCell = GridMath.WorldToCell(desired, nav.TileSize, nav.GridSize, nav.Origin);
            if (currentCell.Equals(targetCell)) return desired;
            return nav.IsBlocked(targetCell)
                ? ClampToBoundary(desired, currentCell, nav.TileSize, nav.Origin)
                : desired;
        }
    }
}
