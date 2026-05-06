using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct HazardCastState : IComponentData
    {
        public float range;
        public float cooldownDuration;
        public float cooldownRemaining;
        public int targetMask;
        public int dataIndex;
        public HazardCastKind kind;
        public int footprintWidth;
        public int footprintHeight;
    }
}
