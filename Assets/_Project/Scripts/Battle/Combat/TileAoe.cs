using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // Shared tile-AOE membership primitive. A candidate cell is "in range" of a
    // center cell when their Chebyshev (chessboard) tile distance is within
    // tileRange — the rule the legacy Meteor resolver applied inline (removed unit 8) and the
    // projectile TileAoe payload (unit 4) reuses. Pure + Burst-compatible; callers
    // convert world positions to cells via GridMath.WorldToCell, then loop
    // candidates calling IsInTileRange (no list is materialized here).
    public static class TileAoe
    {
        // Chebyshev tile distance: max(|dx|, |dy|). A diagonal step counts as 1,
        // matching the game's square-range convention (same as GridMath range use).
        public static int TileDistance(int2 a, int2 b)
            => math.max(math.abs(a.x - b.x), math.abs(a.y - b.y));

        public static bool IsInTileRange(int2 candidateCell, int2 centerCell, int tileRange)
            => TileDistance(candidateCell, centerCell) <= tileRange;

        // elite-enemy-tier unit 1 — 방향성 광역(부채꼴) 멤버십. 드래곤 화염 브레스가 유일한
        // 소비자다. 위 반경 판정과 **같은 계층**에 두는 이유: 둘 다 "이 대상이 이 광역 안인가"
        // 라는 한 가지 순수 술어이고, 도형을 데이터로 추상화하는 계층(EffectArea)은 소비자가
        // 도형을 런타임에 고르지 않아 과설계로 판정돼 접혔다(docs/spec/elite-enemy-tier/
        // 1_cone_predicate.md 에 근거 전문).
        //
        // ★**셀이 아니라 월드 XZ 로 판정한다.** 위 반경 판정이 셀인 것은 착탄이 셀에 락돼
        // 있어서(격자가 정의 그 자체)지만, 브레스는 연속 이동하는 비행 유닛에서 나가고 조준
        // 방향도 월드에서 만들어진다. 멤버십만 이산으로 하면 반경 1~2타일에서 셀 중심 양자화가
        // 방향 판정을 최대 ~45° 흔든다. 사거리만 타일→월드로 환산해 넘긴다.
        // 선례: AttackReach.InReach 도 «사거리는 타일 · 미세 판정은 월드» 하이브리드다.
        //
        // 인자: from=시전자 XZ · to=대상 XZ · dir=**정규화된** 조준 방향 · cosSq=cos²(반각) ·
        // rangeWorld=사거리(월드). cosSq 는 저작 각도에서 bake 가 1회 변환한다(sim 은 삼각함수를
        // 부르지 않고, 저작값 하나가 두 표현으로 갈리지 않는다).
        //
        // ⚠ **정의역은 반각 (0°, 90°) 다.** 아래 `dp > 0` 부호 가드는 제곱이 부호를 잃어
        // **등 뒤에 대칭 콘이 생기는 것**을 막는 필수 조건인데, 동시에 정의역을 90° 로 자른다.
        // 게다가 cos²θ = cos²(180−θ) 라 저작 120° 는 조용히 60° 콘으로 동작하고 180° 는
        // «전방위» 가 아니라 «정면 한 줄» 이 된다. 그래서 **bake 가 반각 >= 90 을 거절**한다 —
        // 이 함수는 클램프하지 않고 정의역을 문서로만 방어한다(순수 술어의 책임 밖).
        // ⚠ **판정 본체는 도메인이 소유한다**(unit 8). 화염 브레스가 스킬 레이어로
        // 가면서 판정도 따라갔고, 여기는 위임만 남는다 — 기존 테스트·문서 참조가
        // 그대로 살고 복사본이 생기지 않는다.
        public static bool IsInCone(float2 from, float2 to, float2 dir, float cosSq, float rangeWorld)
            => Wassup.Skills.SkillCone.IsInCone(from, to, dir, cosSq, rangeWorld);

        public const float SameSpotEpsSq = Wassup.Skills.SkillCone.SameSpotEpsSq;
    }
}
