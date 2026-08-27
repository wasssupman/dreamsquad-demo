using UnityEngine;

namespace Wassup.Data
{
    // defender-footprint unit 0 — 유닛 W×H 점유의 단일 산식.
    //
    // 앵커 = 점유 rect 의 min 코너(셀). 규약은 MapStageMath.FootprintCells(프랍/차단존)와
    // 동일하며, 셀 rect 는 그 함수에 위임해 클램프(최소 1×1) 규칙을 한 곳에 둔다.
    //
    // 대표 셀(primary) = 이 유닛의 sim 위치·사거리·셀 파생이 전부 읽는 단 한 칸.
    // 홀수 변은 정중앙, 짝수 변은 floor (README 계약 2). 셀 기반 소비처는 반드시 이
    // 함수를 거친다 — 후속 「타일 판정 → 거리 기반 전환」의 편집 지점을 한 곳으로 남긴다.
    public static class FootprintMath
    {
        public static RectInt Cells(Vector2Int anchor, Vector2Int size)
            => MapStageMath.FootprintCells(anchor, Vector2Int.zero, size);

        public static Vector2Int PrimaryOffset(Vector2Int size)
        {
            var s = Vector2Int.Max(size, Vector2Int.one);
            return new Vector2Int((s.x - 1) / 2, (s.y - 1) / 2);
        }

        public static Vector2Int PrimaryCell(Vector2Int anchor, Vector2Int size)
            => anchor + PrimaryOffset(size);

        public static Vector2Int AnchorFromPrimary(Vector2Int primary, Vector2Int size)
            => primary - PrimaryOffset(size);
    }
}
