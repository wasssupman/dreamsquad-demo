using Unity.Entities;

namespace Wassup.Battle.Units
{
    // skill-layer-foundation unit 2b — 엔티티에서 진영을 **읽는** 쪽.
    //
    // 순수 계산(`FactionRelation`)과 갈라 둔 이유는 CLAUDE.md 제약 10 이다: 「상대 진영이
    // 무엇인가」는 값→값이라 월드가 필요 없고, 「이 엔티티가 어느 진영인가」는 컴포넌트를
    // 읽어야 하므로 아키텍처 종속이다. 전자는 EditMode 로 고정되고 후자는 여기 산다.
    //
    // ⚠ 오늘 arm 들은 이걸 **두 가지 방법으로** 한다 — `HasComponent<AttackUnitTag>` 태그
    // 존재로 묻는 곳과 `FactionTag` lookup 을 쓰는 곳. 같은 질문에 답이 둘이면 다음 사람이
    // 어느 쪽을 믿을지 모른다. 이 함수가 그 단일 답이다.
    public static class FactionQuery
    {
        // `FactionTag` 가 정본이다. 부재 시 유닛 태그로 폴백한다 — 타겟 후보가 아닌
        // 엔티티(요청 캐리어 등)는 `FactionTag` 를 안 달기 때문이다.
        // 둘 다 없으면 `None` 이고, `FactionRelation` 이 None 을 「아무도 안 때린다」로
        // 받는다(조용한 오폭 금지).
        public static Faction Of(
            Entity e,
            in ComponentLookup<FactionTag> factions,
            in ComponentLookup<AttackUnitTag> enemies,
            in ComponentLookup<DefenderUnitTag> defenders)
        {
            bool hasTag = factions.HasComponent(e);
            return FactionRelation.Resolve(
                hasTag,
                hasTag ? factions[e].value : Faction.None,
                enemies.HasComponent(e),
                defenders.HasComponent(e));
        }

        // 이 시전자가 때릴 유닛 진영. arm 이 후보 풀을 고를 때 부른다.
        public static Faction OpponentsOf(
            Entity caster,
            in ComponentLookup<FactionTag> factions,
            in ComponentLookup<AttackUnitTag> enemies,
            in ComponentLookup<DefenderUnitTag> defenders)
            => FactionRelation.OpponentUnitsOf(Of(caster, factions, enemies, defenders));

        // 이 시전자가 도울 유닛 진영. 오라·실드·버프가 부른다.
        public static Faction AlliesOf(
            Entity caster,
            in ComponentLookup<FactionTag> factions,
            in ComponentLookup<AttackUnitTag> enemies,
            in ComponentLookup<DefenderUnitTag> defenders)
            => FactionRelation.AllyUnitsOf(Of(caster, factions, enemies, defenders));
    }
}
