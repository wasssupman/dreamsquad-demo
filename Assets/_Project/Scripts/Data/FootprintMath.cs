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

        // defender-footprint unit 2 — 드래그 손끝 규약: 손가락 셀 = footprint **하단(min y) 행의
        // 가로 중앙**. 유닛은 손가락 위로 자란다(요구 문서 5절 — 하단 중앙 기준). 1×1 은 항등.
        public static Vector2Int AnchorFromBottomCenter(Vector2Int fingerCell, Vector2Int size)
        {
            var s = Vector2Int.Max(size, Vector2Int.one);
            return new Vector2Int(fingerCell.x - (s.x - 1) / 2, fingerCell.y);
        }

        // defender-footprint unit 3 — 두 셀 rect 의 체비셰프 거리. 0 = 겹침, 1 = 둘레 접촉(인접).
        // 1×1 끼리 거리 1 = 8이웃과 동치 — 시너지 인접의 footprint 일반화가 이 함수 하나다.
        public static int RectChebyshevDistance(RectInt a, RectInt b)
        {
            int dx = Mathf.Max(0, Mathf.Max(b.xMin - (a.xMax - 1), a.xMin - (b.xMax - 1)));
            int dy = Mathf.Max(0, Mathf.Max(b.yMin - (a.yMax - 1), a.yMin - (b.yMax - 1)));
            return Mathf.Max(dx, dy);
        }

        // defender-footprint unit 2 — 대표 셀 중심 ↔ footprint 기하 중심의 칸 단위 오프셋.
        // 홀수 변 0, 짝수 변 +0.5. sim 위치는 대표 셀 중심 불변(README 계약 2)이고, **뷰만**
        // 이 오프셋으로 기하 중심에 선다 — 소비처는 뷰 피드(sync·RestViewPos·비행 앵커)뿐이다.
        public static Vector2 CenterOffsetFromPrimary(Vector2Int size)
        {
            var s = Vector2Int.Max(size, Vector2Int.one);
            return new Vector2(
                (s.x - 1) * 0.5f - (s.x - 1) / 2,
                (s.y - 1) * 0.5f - (s.y - 1) / 2);
        }
    }
}
