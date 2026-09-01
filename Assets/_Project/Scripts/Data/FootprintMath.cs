using UnityEngine;

namespace Wassup.Data
{
    // defender-footprint unit 0 — 유닛 W×H 점유의 단일 산식.
    //
    // 앵커 = 점유 rect 의 min 코너(**셀 인덱스**). 규약은 MapStageMath.FootprintCells(프랍/차단존)와
    // 동일하며, 셀 rect 는 그 함수에 위임해 클램프(최소 1×1) 규칙을 한 곳에 둔다.
    //
    // ⚠ **distance-based-range unit 10 에서 「대표 셀」이 은퇴했다**(사용자 결정 2026-09-01).
    // 저장값은 **앵커 + W×H** 둘뿐이고 나머지는 전부 여기서 파생된다:
    //     점유 셀 집합 = `Cells(anchor, size)`
    //     기하 중심   = `anchor + GeometricCenterOffset(size)`   ← 실루엣이 서는 곳
    //     발밑       = `anchor + (GeometricCenterOffset(size).x, 0)`
    //
    // 은퇴 이유: 짝수 변 footprint 는 **중심 칸이 없다**(2×3 의 중심은 셀 경계 위). 대표 셀은
    // 「짝수 변의 대표가 어느 칸이냐」를 정수 나눗셈으로 정한 동전 던지기였고, 플레이어에겐
    // 안 보이는데 사거리를 반 칸 옮겼다(`distance-based-range/README` 사용자 확정 결정 1).
    //
    // ⚠ **「이 유닛은 어느 셀에 있나」를 다시 만들지 말 것.** 다칸 유닛에게 그건 답이 없는
    // 질문이다. 맞는 질문은 「어느 셀들을 점유하나」(`Cells`)이고, 정체성 키가 필요하면
    // **앵커**를 써라 — 이름이 `Anchor` 여야 다음 사람이 그걸 중심으로 오해하지 않는다.
    public static class FootprintMath
    {
        public static RectInt Cells(Vector2Int anchor, Vector2Int size)
            => MapStageMath.FootprintCells(anchor, Vector2Int.zero, size);

        // 앵커 → **기하 중심**(칸 단위). 앵커가 셀 인덱스이고 셀 N 의 중심이 정수 N 이므로
        // `(W−1)/2` 다 — `W/2` 가 아니다. 1×1 → 0, 2 → 0.5, 3 → 1.
        // 짝수 변에서 결과가 x.5(셀 경계)인 것은 **버그가 아니라 그 유닛의 실제 중심**이다.
        // ⚠ 이 값을 셀로 되접지 말 것(`WorldToCell`) — 경계에서 어느 칸이 나올지를
        // 설계가 아니라 부동소수점이 정하게 된다. 격자 질의는 `Cells` 또는 앵커로 가라.
        public static Vector2 GeometricCenterOffset(Vector2Int size)
        {
            var s = Vector2Int.Max(size, Vector2Int.one);
            return new Vector2((s.x - 1) * 0.5f, (s.y - 1) * 0.5f);
        }

        // 앵커 → 발밑(하단 행의 가로 중앙). Y 소팅·그림자·지면 VFX 가 쓴다.
        // ⚠ 높이 2 이상에서 **중심으로 소팅하면 앞줄 유닛이 뒤로 들어간다** — 발이 앞에 있는데
        // 중심은 뒤에 있기 때문이다. 소팅은 반드시 이 값으로.
        public static Vector2 FootOffset(Vector2Int size)
            => new Vector2(GeometricCenterOffset(size).x, 0f);

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
    }
}
