// Introduced in spec unit 1 (apply_channels_and_lifecycle) — producer-agnostic StackModifier attachment channel.
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct StackModifierApplyEvent
    {
        public Entity target;
        public StackKind kind;
        public byte countDelta;       // 부착당 누적량 (cap 은 Apply 시점 적용)
        public float perAppDuration;  // refresh 정책 (S1) — remaining = perAppDuration
        public Entity source;
    }

    public struct StackModifierApplyEventsSingleton : IComponentData
    {
        public NativeQueue<StackModifierApplyEvent> queue;
    }
}
