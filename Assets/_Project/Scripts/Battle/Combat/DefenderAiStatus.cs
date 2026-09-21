using Unity.Entities;
using Wassup.UnitAi;

namespace Wassup.Battle.Combat
{
    // defender-autobattle-ai unit 1 — 방어유닛 행동 상태 컴포넌트(값 = UnitAi.DefenderAiState). 이름을 enum 과 가른 이유: 두 네임스페이스를 함께 여는 파일에서 모호해진다. Combat 소유, DefenderAiStateSystem 만 쓴다(유일 writer).
    // AttackSystem·브리지·뷰는 읽기만. 값의 뜻과 우선순위는 로직 레이어(Wassup.UnitAi.DefenderAi)가 정본.
    public struct DefenderAiStatus : IComponentData
    {
        public Wassup.UnitAi.DefenderAiState value;
    }

    // 공격 정책 — 스폰 시 능력 SO 존재에서 한 번 bake(SummonPatrolAbility → Summon · BombThrowAbility → Bomb · 그 외 Target).
    // 로직은 이 값만 본다(타입 체크 금지, README 계약 7).
    public struct DefenderAiPolicy : IComponentData
    {
        public DefenderAttackPolicy value;
    }
}
