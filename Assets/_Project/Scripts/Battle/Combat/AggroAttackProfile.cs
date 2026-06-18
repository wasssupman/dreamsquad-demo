using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 1 — taunt-attack profile baked onto enemies that have
    // no normal outputs (Runner/Swift). Consumed only while the enemy is Aggroed:
    // AggroAssignmentSystem grants a temporary AttackState/outputs from this so the
    // held enemy can hit the guardian, then strips it on release.
    public struct AggroAttackProfile : IComponentData
    {
        public float damage;
        public float cooldown;
        public float range;
    }
}
