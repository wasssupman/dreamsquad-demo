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
        // enemy-ai-fsm Unit 0 — Engaging 이동 정책. Movement 가 RO 로 읽는다. (aimMode 는 3b 에서 제거)
        public Wassup.Data.EngageMovement engageMovement;
    }
}
