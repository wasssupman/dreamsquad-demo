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

        // Phase 8 §13 follow-up — how many nearest in-range targets a melee
        // attack (projectile=null) hits per tick. Default 1 keeps prior
        // single-target behavior. Level-up / buff systems can mutate this at
        // runtime without touching the source SO.
        public int attackTargetCount;

        public int targetMask; // (int)Faction bitmask of attackable factions.

        // Movement-context pause request duration emitted after this attacker fires.
        // MovementSystem owns the EnemyAttackMovePause component write.
        public float movePauseOnAttackSec;
    }
}
