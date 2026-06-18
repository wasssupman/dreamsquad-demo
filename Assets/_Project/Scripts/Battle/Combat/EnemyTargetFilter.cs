using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 4 — enemy base (non-aggro) targeting rule.
    //   classMask    : allowed DefenderClass bits (bit = 1 << (int)DefenderClass).
    //                  -1 = all classes. Candidates without DefenderClassTag
    //                  (e.g. blocking hazards) are never filtered out.
    //   priorityClass: class picked first when any is in range ((int)DefenderClass);
    //                  -1 = no priority (nearest wins).
    // Aggro (Aggroed) overrides this entirely — see AttackSystem.
    public struct EnemyTargetFilter : IComponentData
    {
        public int classMask;
        public int priorityClass;
    }
}
