using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 5 — marks an enemy that received a temporary AttackState
    // + outputs from its AggroAttackProfile when aggroed (Runner/Swift, which have
    // no normal attack). Stripped on release so the enemy does not attack defenders
    // on its way back to the exit.
    public struct TauntAttackGranted : IComponentData { }
}
