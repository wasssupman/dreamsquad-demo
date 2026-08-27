using UnityEngine;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 주기 실드 부여(shield-guardian-defender).
    // 실드 범위는 유닛 attackRange 재사용(그 spec 계약 5)이라 이 에셋엔 range 필드가 없다.
    // bake: `DcTriggerSlot`(PeriodicTimer × GrantShield) — 첫 캐스트는 배치 A초 후.
    // ⚠ 전용 상태(`ShieldCastState`)와 전용 시스템은 은퇴했다(skill-layer-migration).
    //   주기 트리거가 그 일을 하고 실행은 `GrantShieldSkill` 이다.
    [CreateAssetMenu(fileName = "Ability_Shield", menuName = "Wassup/Ability/Shield Cast", order = 42)]
    public class ShieldCastAbility : DefenderAbilityData
    {
        public float cooldown;                  // A. 캐스트 주기(초)
        public float amount;                    // B. 출처당 실드량(같은 출처 max 갱신)
        public int targetCount = 1;             // C. 대상 수
        public ShieldTargetFilter filter;       // Self / All / MinHealth
    }
}
