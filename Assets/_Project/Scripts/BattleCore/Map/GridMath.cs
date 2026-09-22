// salvaged from Assets/_Project/Scripts/Battle/Movement/GridMath.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `Unity.Burst` 제거(코어는 Burst 를 모른다). 그 외 규칙·상수·주석 의도는 그대로.
using Unity.Mathematics;

namespace Wassup.BattleCore.Map
{
    public static class GridMath
    {
        /// <summary>월드 → 칸. 격자 안으로 접는다(밖을 묻고 싶으면 `WorldToCellUnclamped`).</summary>
        public static int2 WorldToCell(float3 worldPos, float tileSize, int2 gridSize, float3 origin = default)
        {
            int2 cell = WorldToCellUnclamped(worldPos, tileSize, origin);
            return new int2(
                math.clamp(cell.x, 0, gridSize.x - 1),
                math.clamp(cell.y, 0, gridSize.y - 1));
        }

        // 라운딩 규칙은 한 곳에만 둔다 — `WorldToCell` 이 이 함수를 감싼다.
        // `floor(v + 0.5)` 를 쓰는 이유: `math.round` 는 banker's rounding 이라 2.5 → 2 가 되는데
        // 격자 조회는 반 칸에서 위로 붙는 쪽이 예측 가능하다.
        public static int2 WorldToCellUnclamped(float3 worldPos, float tileSize, float3 origin = default)
        {
            float3 local = worldPos - origin;
            return new int2(
                (int)math.floor(local.x / tileSize + 0.5f),
                (int)math.floor(local.z / tileSize + 0.5f));
        }

        public static float3 CellToWorldCenter(int2 cell, float tileSize, float y = 0f, float3 origin = default)
            => origin + new float3(cell.x * tileSize, y, cell.y * tileSize);

        public static int CellIndex(int2 cell, int2 gridSize) => cell.y * gridSize.x + cell.x;

        public static int ChebyshevDistance(int2 a, int2 b) => math.cmax(math.abs(a - b));

        // ⚠ **격자 계층 전용이다.** 사거리 판정은 이 함수를 지나지 않는다 — `attackRange` 는
        // 타일 길이 단위의 연속 반지름이고 술어(`AttackReach`)는 실수를 그대로 받는다.
        // 남은 소비처는 **정수 칸이 실제로 필요한 곳**뿐이다: BFS 소스 디스크 수집(어그로 추격·
        // 사냥·순찰), 순찰 박스, 스킬 광역.
        //
        // ⚠ **`ceil` 이다(반올림 아님).** 소스 디스크는 사격 가능 칸을 **덮어야** 한다 —
        // `round` 면 사거리 2.4 가 2 로 줄어 쏠 수 있는 칸이 소스에서 빠지고 적이 더 멀리서 멈춘다.
        // 넘치게 덮은 부분은 접근 보정(`TryCloseIn`)이 흡수한다.
        public static int RangeToTiles(float r) => (int)math.ceil(r);

        // 흐름장 단위벡터 → 인접 칸 스텝.
        //
        // ⚠ **버림 캐스트를 쓰지 말 것.** 4-이웃 시절엔 성분이 정확히 ±1/0 이라 `(int)f.x` 가
        // 맞았지만, 8-이웃이 되면서 대각 성분이 ±0.7071 이 됐다. 버리면 0 이 되어 스텝이
        // 사라지고 루프가 같은 칸에 갇힌다(경로 예고 라인이 첫 대각에서 끊긴 실제 사고).
        //
        // 반환 zero = 스텝 없음(도착·고립·미도달). 호출자는 루프를 끝내야 한다.
        public static int2 FlowStep(float2 flowDir)
        {
            if (math.lengthsq(flowDir) < 1e-6f) return int2.zero;
            return new int2((int)math.round(flowDir.x), (int)math.round(flowDir.y));
        }
    }
}
