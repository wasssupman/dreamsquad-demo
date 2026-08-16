using UnityEngine;

namespace Wassup.Data
{
    // on-place-skill-rework unit 0 — 방어유닛이 **자기 규칙**(트리거 × 페이로드)을 선언하는 자리.
    // 적의 `AttackUnitData.nightmareMechanics` 와 대칭이며, 같은 `DcTriggerSlot` 에 baked 되어
    // 같은 감지자·같은 payload arm 을 탄다(사용자 지시 2026-08-16: 「실행 조건 만족 → 스킬 실행」
    // 이라는 핵심 메커니즘은 적이나 방어유닛이 다르면 안 된다).
    //
    // `DefenderUnitData` 에 flat 필드를 늘리지 않는다 — `defender-ability-assets` 가 능력별 flat
    // 필드 산발을 걷어낸 뒤이고, 그 spec 의 후속 후보가 이 자리를 예약해 뒀다("통합 착수 시
    // ability SO 가 그 rule 의 데이터 홈이 될 수 있음").
    //
    // 정의 계층 계약 승계: `Unity.Entities`/ECS 타입 무참조. `DcMechanic` 자체가 ECS-free 다.
    // 해석(슬롯 bake)은 `BattleBridge.BakeUnitMechanics` 단독.
    [CreateAssetMenu(fileName = "Ability_UnitSkill", menuName = "Wassup/Ability/Unit Skill", order = 46)]
    public class UnitSkillAbility : DefenderAbilityData
    {
        [Tooltip("이 유닛의 고유 스킬 규칙. 트리거(언제) × 페이로드(무엇을)의 조합.")]
        public DcMechanic[] mechanics = new DcMechanic[0];
    }
}
