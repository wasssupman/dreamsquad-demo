using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // Per-defender combat state. Combat context owns writes; other contexts may read.
    public struct AttackState : IComponentData
    {
        public float damage;
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining; // seconds until next shot is ready
    }
}
