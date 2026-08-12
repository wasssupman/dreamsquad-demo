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
    //
    // unit 9 — **반경 필드를 두지 않는다.** 담당 구역은 소환사 SO 의 attackRange 다
    // (쿨다운을 attackCooldown 에 맡긴 것과 같은 형태). 능력 에셋은 «누구를 소환하나»만
    // 소유하고 «얼마나 넓게 커버하나»는 유닛 스탯이다 — 두 곳에 두면 프리뷰가 그리는
    // 박스와 순찰병이 지키는 박스가 갈린다(실제로 갈려 있었다).
    [CreateAssetMenu(fileName = "Ability_SummonPatrol", menuName = "Wassup/Ability/Summon Patrol", order = 43)]
    public class SummonPatrolAbility : DefenderAbilityData
    {
        public DefenderUnitData patrolUnit;   // 소환할 순찰병 (카탈로그 미등록 에셋)

        // unit 10 — **이 능력이 만든 상태**의 애니 이름. 소환사 유닛 SO 가 아니라 여기 두는 이유:
        // "소환물이 살아 있다"는 상태를 아는 것은 이 능력이고, ISpineUnitVisualData 는 이미
        // 멤버가 열둘이다(README 계약 2 — 네 번 커졌다). 한 능력만 쓰는 이름을 공용 인터페이스에
        // 얹지 않는다. 다른 능력이 자기 상태 애니를 원하면 그 능력 SO 에 같은 모양으로 얹는다.
        //
        // 둘 다 비어 있으면 무동작 — 소환사는 평소 idle 루프를 그대로 돈다.
        [Header("Animation")]
        [Tooltip("소환물이 살아 있는 동안 반복할 애니. 비우면 평소 idle 유지.")]
        public string activeAnimation = "";
        [Tooltip("소환물을 잃은 순간 한 번 재생할 애니. 비우면 조용히 idle 로 복귀.")]
        public string lostAnimation = "";
    }
}
