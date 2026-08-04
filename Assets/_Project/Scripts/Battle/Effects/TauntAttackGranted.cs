using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 5 — marks an enemy that received a temporary AttackState
    // + outputs from its AggroAttackProfile when aggroed (Runner/Swift, which have
    // no normal attack). Stripped on release so the enemy does not attack defenders
    // on its way back to the exit.
    // goal-stability unit 3 — previousTargetMask: 도발 전에 이미 AttackState 를 갖고
    // 있던 적(walk-only goal-grant)의 원래 마스크. 0 = 도발이 AttackState 자체를
    // 부여했음(해제 시 통째 제거, 현행). 비0 = 해제 시 마스크만 원복하고 AttackState 유지.
    public struct TauntAttackGranted : IComponentData
    {
        public int previousTargetMask;
    }
}
