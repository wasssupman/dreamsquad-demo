using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // ECS mirror of DefenderUnitData's CC fields. Written once at spawn by
    // BattleBridge; read by AttackSystem (knockback, Unit 5) and the on-place
    // dispatch (push, Unit 6). All fields default 0 → existing behavior unchanged.
    public struct DefenderCcData : IComponentData
    {
        public float knockbackDistance;
        public float knockbackDuration;
        public float onPlacePushDistance;
        public float onPlacePushDuration;
        public float onPlacePushRadius;
    }
}
