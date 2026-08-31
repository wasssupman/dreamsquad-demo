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
    // 무엇을 잘못 읽었나: **0.5 는 「공격자의 몸」이 아니라 「칸의 반폭」이다.** 후보는
    // 점이 아니라 **한 변이 1인 정사각형**이고, 「반경 r 안인가」의 옳은 물음은 그 사각형의
    // **가장 가까운 점**까지의 거리다 — `max(|Δ| − 0.5, 0)`. 사거리 술어의 0.5 도 같은 것이다
    // (그쪽은 공격자가 칸 위에 서 있다). 그래서 두 물음이 **같은 식**으로 수렴한다.
    // 반경 1 은 여덟 이웃 전부(v=0.707), 반경 2 의 **정대각만** 빠진다(v=2.12) — 사거리와 동일.
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
                   tileRange, extraRadiusTiles);
    }
}
