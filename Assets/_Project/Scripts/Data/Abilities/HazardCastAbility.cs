using UnityEngine;
using Wassup.Battle.Effects;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 주기적 해저드 캐스트(hazard-caster-defenders).
    // BlockingHazardSO/HazardCastKind 는 네임스페이스만 Battle.Effects 인 authoring 타입 —
    // DefenderUnitData 가 이미 참조하던 기존 예외 승계(spec 계약 2).
    // bake: HazardCastState(Effects).
    [CreateAssetMenu(fileName = "Ability_Hazard", menuName = "Wassup/Ability/Hazard Cast", order = 41)]
    public class HazardCastAbility : DefenderAbilityData
    {
        public float castRange;          // 타겟 탐색 범위(world units, attackRange 와 동일 단위)
        public float cooldown;           // 캐스트 주기(초)
        public HazardCastKind kind;      // Zone / Blocking
        public HazardSO zoneHazard;
        public BlockingHazardSO blockingHazard;
        public int footprintWidth = 1;
        public int footprintHeight = 1;
    }
}
