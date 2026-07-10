using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-hunter-targeting unit 0 — the defender a hunter (boss) is currently
    // pursuing. Combat-owned: written only by EnemyAiStateSystem, read RO by
    // MovementSystem (which self-walks toward the target instead of the goal).
    // Pre-attached at spawn bake on BossTag entities only — the FSM writes the
    // value (no per-frame structural add), non-boss enemies never carry it.
    // Entity.Null = no pursuit target (map has no defenders / in attack range).
    public struct HuntTarget : IComponentData
    {
        public Entity value;
    }
}
