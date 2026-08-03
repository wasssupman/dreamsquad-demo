using UnityEngine;

namespace Wassup.Data
{
    // summon-patrol-defender unit 3 — 거점 순찰 아군을 1기 유지하는 능력.
    //
    // **쿨다운 필드를 두지 않는다.** 소환 주기는 소환사 SO 의 attackCooldown 이다 —
    // "소환 = 공격"을 심에서도 유지하면 공격 애니·공격 SFX·UnitAttackVisualEvent 가
    // 전부 공짜로 붙는다(폭탄맨이 AttackState.cooldownRemaining 을 그대로 쓰는 것과 같다).
    //
    // 소환수 스탯은 이 에셋이 소유하지 않고 **가리킨다**. 신규 SO 타입을 만들지 않는
    // 이유는 README 계약 2 참조(ISpineUnitVisualData 구현체를 늘리지 않는다).
    // patrolUnit 은 DefenderCatalog 에 등록하지 않는다 — 플레이어가 배치하는 유닛이 아니다.
    [CreateAssetMenu(fileName = "Ability_SummonPatrol", menuName = "Wassup/Ability/Summon Patrol", order = 43)]
    public class SummonPatrolAbility : DefenderAbilityData
    {
        public DefenderUnitData patrolUnit;   // 소환할 순찰병 (카탈로그 미등록 에셋)
        public int leashTileRadius = 2;         // 거점 박스 반경 (Chebyshev)
    }
}
