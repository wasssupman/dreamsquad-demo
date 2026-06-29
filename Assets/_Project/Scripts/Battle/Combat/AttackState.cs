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

        // attack-hit-delay — 공격 시작 후 타격 판정까지 지연(초). 0 = 즉시(현행). config.
        public float hitDelaySec;
        // 진행 중인 타격 지연 남은 시간(초). >0 = 시작됨/타격 전. runtime(Combat 소유).
        public float hitDelayRemaining;
    }
}
