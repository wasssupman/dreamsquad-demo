using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-behavior-components Unit 0 — Combat-owned. Baked from AttackUnitData
    // behavior fields. AttackSystem reads targetMode; MovementSystem reads engageMovement (RO).
    public struct EnemyBehavior : IComponentData
    {
        public Wassup.Data.EnemyTargetMode targetMode;
        // enemy-ai-fsm — Engaging 이동 정책(구 aimMode 대체).
        public Wassup.Data.EngageMovement engageMovement;
    }
}
