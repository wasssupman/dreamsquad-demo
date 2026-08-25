using Unity.Entities;

namespace Wassup.Battle.Units
{
    // goal-tower-siege unit 0 — 골 셀에 선 "때릴 수 있는 대상".
    //
    // 아키타입은 Blocking 해저드와 동형이다(Health + IncomingDamage + IncomingHeal +
    // FactionTag + LocalTransform). 체력 정본은 **엔티티의 Health 자기 자신**이다
    // (rev 2 에서 per-entity 로 바뀌었다 — 구 «GoalTowerHealth 싱글턴 1풀» 서술은 stale 이었고
    // 그 타입은 코드에 존재하지 않는다. 브리지의 `_goalStability` 는 «가장 위험한 마음» 읽기
    // 캐시일 뿐이다).
    //
    // 계약: 이 엔티티에 ModifierStats / StatModifierSlot / ShieldSlot 을 붙이지 않는다.
    // MaxHealthScaleSystem 이 Health.max 를 재계산하면 표시·정규화가 어긋난다.
    //
    // ⚠ heart-stress-axis unit 2 — **IncomingHeal 은 이 금지에서 빠졌다**(계약을 의도적으로
    // 뒤집었다). 「악몽을 잡을수록 마음이 회복된다」가 이 spec 의 저울 절반이기 때문이다.
    // 하나만 연 근거: 원 금지의 명분은 `Health.max` 재계산인데 그건 **ModifierStats 전용**이고
    // IncomingHeal 은 `max` 를 건드리지 않는다(DamageApplicationSystem 이 value 만 clamp 한다).
    // 나머지 셋은 금지로 남는다.
    public struct GoalTowerTag : IComponentData { }
}
