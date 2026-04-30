// Introduced in spec unit 1 (apply_channels_and_lifecycle) — producer-agnostic StatModifier attachment channel.
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct StatModifierApplyEvent
    {
        public Entity target;
        public StatKind stat;
        public CombineOp op;
        public float magnitude;
        public float duration;
        public Entity source;
        public ushort stackId;       // producer 가 부여, 디폴트 0
    }

    public struct StatModifierApplyEventsSingleton : IComponentData
    {
        public NativeQueue<StatModifierApplyEvent> queue;
    }
}
