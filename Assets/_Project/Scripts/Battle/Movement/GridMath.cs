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
            int2 cell = WorldToCellUnclamped(worldPos, tileSize, origin);
            return new int2(
                math.clamp(cell.x, 0, gridSize.x - 1),
                math.clamp(cell.y, 0, gridSize.y - 1)
            );
        }

        // active-dreamcatcher-tile-aim rev — clamp 하지 않는 셀 계산. `WorldToCell` 은 격자 안으로
        // 접어 넣기 때문에 "보드 밖인가" 를 물을 수 없다(맵 오른쪽 빈 배경도 가장자리 셀로 보인다).
        // 보드 밖을 **거절**해야 하는 경로(조준 커밋)는 이걸 쓰고 직접 bounds 를 본다.
        // 라운딩 규칙은 한 곳에만 둔다 — WorldToCell 이 이 함수를 감싼다.
        public static int2 WorldToCellUnclamped(float3 worldPos, float tileSize, float3 origin = default)
        {
            // Round half-away-from-zero-on-positive (floor(x + 0.5)) so 2.5 -> 3 consistently
            // for integer-grid lookup. Unity.Mathematics.math.round uses banker's rounding
            // (half-to-even) which would give 2.5 -> 2; we want predictable snap-up at half.
            float3 local = worldPos - origin;
            return new int2(
                (int)math.floor(local.x / tileSize + 0.5f),
                (int)math.floor(local.z / tileSize + 0.5f)
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

        // continuous-agent-movement unit 4 후속 — flow 단위벡터 → 인접 셀 스텝.
        //
        // ⚠ **버림 캐스트를 쓰지 말 것.** 4-이웃 시절엔 성분이 정확히 ±1/0 이라 `(int)f.x` 가
        // 맞았지만, 8-이웃이 되면서 대각 성분이 ±0.7071 이 됐다. 버리면 0 이 되어 스텝이
        // 사라지고, 필드를 따라가는 루프가 같은 셀에 갇힌다(경로 예고 라인이 첫 대각에서
        // 끊긴 실제 사고 — 2026-08-08).
        //
        // 반환 zero = 스텝 없음(골 도착·고립·미도달). 호출자는 루프를 끝내야 한다.
        public static int2 FlowStep(float2 flowDir)
        {
            if (math.lengthsq(flowDir) < 1e-6f) return int2.zero;
            return new int2(
                (int)math.round(flowDir.x),
                (int)math.round(flowDir.y));
        }
    }
}
