using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // 광역 멤버십의 공유 primitive. 「중심에서 N칸 안인가」를 답한다.
    //
    // ── 정사각형에서 원으로 (distance-based-range unit 4b) ──
    //
    // 전에는 체비셰프 하나뿐이었다 — 대각 한 걸음이 1이라 사거리 N 의 모양이 **정사각형**이다.
    // 사거리 술어가 몸 기준 거리로 바뀐 지금(unit 4a) 광역만 정사각형으로 남으면 같은 「N칸」이
    // 물음에 따라 다른 모양이 된다. 그래서 `IsInRadius` 는 사거리 술어와 **같은 본체**를 쓴다.
    //
    // ⚠ **사거리 술어와 같은 본체를 쓴다**(`SkillMath.InBodyReach`). 처음엔 순수 원
    // (`dx²+dy² ≤ r²`)으로 썼다가 되돌렸다 — **반경 1 폭발이 대각을 통째로 잃어 십자 모양이
    // 됐다**(대각 칸은 중심거리 1.41 > 1). 격자에서 작은 반경의 원은 그렇게 무너진다.
    //
    // **0.5 의 정체 = 「한 칸의 몸 반지름」.** 이 상수는 사거리 술어와 광역에서 **같은 것**이고,
    // 어느 쪽이든 **칸인 쪽**에 한 번 붙는다:
    //   · 사거리 — 공격자가 칸 위에 서 있다 → **공격자**의 몸.
    //   · 광역   — 폭발은 점이고 **후보가 칸**이다 → **후보**의 몸.
    // 그래서 두 물음이 같은 식(`|Δ| ≤ r + 0.5 + 대상반경`)으로 수렴한다.
    // 반경 1 은 여덟 이웃 전부(1.414 ≤ 1.5), 반경 2 의 정대각만 빠진다(2.83 > 2.5) — 사거리와 동일.
    //
    // ⚠ **rev 2**(2026-08-31)에서 칸의 몸이 **정사각형 → 내접원**으로 바뀌었다. 사거리 쪽에서
    // 정사각형 몸이 경계에 직선 4개를 남겨 「원이 아니라 라운딩된 사각형」으로 읽혔기 때문이고,
    // 광역도 같은 본체를 타므로 함께 바뀌었다 — **의도한 일관성이다.** 반경 3 이상에서 얕은
    // 대각 칸 일부가 빠지는 것도 사거리와 같다(`TileAoeTests` 가 계약을 고정한다).
    //
    // ⚠ **셀 좌표를 그대로 받는다.** 소비처의 중심이 전부 이미 칸에 물려 있기 때문이다
    // (투사체 착탄은 발사 시점에 칸으로 고정 · 실드 파열과 어그로는 유닛의 칸). 월드 정밀도를
    // 얹어도 얻는 것이 없고, 정수 입력이라 `dx*dx + dy*dy` 가 정확해 parity 계약에 안전하다
    // (`battle-sim-extraction` — 광역 멤버십은 이산 결정이고 결과가 점수(int)를 낳는다).
    public static class TileAoe
    {
        // 체비셰프 타일 거리 — **격자 통계 전용으로 남았다.**
        public static int TileDistance(int2 a, int2 b)
            => math.max(math.abs(a.x - b.x), math.abs(a.y - b.y));

        // ⚠ **광역 멤버십에 쓰지 말 것** — 그 용도의 정본은 아래 `IsInRadius` 다.
        // 이 정사각형이 남은 곳은 둘뿐이고 **둘 다 「칸」이 물음의 일부**다(결정 4):
        //   · `DefenderDensity` — 「가장 밀집한 **칸**」. 보스 순간이동 착지 지점이라
        //     자를 바꾸면 착지가 조용히 바뀐다.
        //   · `EcsSkillContext` 스킬 arm — 저작이 칸 단위 조준이다.
        //   · `MovementSystem` 회오리 장 — 사거리가 아니라 **장(field) 멤버십**이다.
        public static bool IsInTileRange(int2 candidateCell, int2 centerCell, int tileRange)
            => TileDistance(candidateCell, centerCell) <= tileRange;

        // 광역 멤버십 — **모서리가 둥근** 사거리. `extraRadiusTiles` 는 대상의 몸 반경
        // (unit 3 의 축, 0 = 점). 큰 몸은 폭발에 더 잘 걸린다 — 사거리 술어와 같은 물성이다.
        public static bool IsInRadius(int2 candidateCell, int2 centerCell, int tileRange,
                                      float extraRadiusTiles = 0f)
            => Wassup.Skills.SkillMath.InBodyReach(
                   candidateCell.x - centerCell.x, candidateCell.y - centerCell.y,
                   tileRange,
                   // ⚠ **여기 붙는 것은 「후보 칸의 반폭」이지 유닛의 몸이 아니다**(unit 9).
                   // 폭발은 점이고 후보가 칸이라 칸의 크기가 붙는다. 칸은 언제나 1타일이므로
                   // 유닛 몸(`bodyRadius`, 일반 0.25)으로 바꾸면 **반경 1 이 십자가 된다**.
                   Wassup.Skills.SkillMath.CellHalfWidthTiles,
                   extraRadiusTiles);
    }
}
