using Unity.Entities;

namespace Wassup.Battle.Units
{
    // goal-tower-siege unit 0 — 골 셀에 선 "때릴 수 있는 대상".
    //
    // 아키타입은 Blocking 해저드와 동형이다(Health + IncomingDamage + FactionTag +
    // LocalTransform). 다른 점은 **체력이 엔티티마다가 아니라 판 전체 공유 1풀**이라는 것 —
    // 각 타워의 Health 는 표시·타겟팅용 미러이고 정본은 GoalTowerHealth 싱글턴이다.
    //
    // 계약: 이 엔티티에 ModifierStats / StatModifierSlot / ShieldSlot / IncomingHeal 을
    // 붙이지 않는다. MaxHealthScaleSystem 이 Health.max 를 재계산하면 미러가 깨진다.
    public struct GoalTowerTag : IComponentData { }
}
