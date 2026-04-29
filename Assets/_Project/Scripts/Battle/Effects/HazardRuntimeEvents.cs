using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public enum HazardRuntimeEventType : byte
    {
        ZoneApply = 0,
        DotDamage = 1,
    }

    public struct HazardRuntimeEvent
    {
        public HazardRuntimeEventType eventType;
        public CcKind kind;
        public int2 cell;
        public Entity target;
        public float scalar;
        public float amount;
    }

    public struct HazardRuntimeEventsSingleton : IComponentData
    {
        public NativeQueue<HazardRuntimeEvent> queue;
    }
}
