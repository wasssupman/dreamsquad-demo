using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-behavior-components Unit 0 — Combat-owned. Baked from AttackUnitData
    // behavior fields. AttackSystem reads it to branch targeting (targetMode) and
    // move-pause gating (aimMode).
    public struct EnemyBehavior : IComponentData
    {
        public Wassup.Data.EnemyTargetMode targetMode;
        public Wassup.Data.EnemyAimMode aimMode;
    }
}
