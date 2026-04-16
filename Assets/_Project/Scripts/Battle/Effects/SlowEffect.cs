using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Attached to attack units to slow their path-follow speed. Owned by the
    // Effects context: Movement reads `multiplier` but never writes it.
    // `remaining` counts down in EffectTickSystem and triggers component removal
    // when it reaches zero.
    public struct SlowEffect : IComponentData
    {
        public float remaining;
        public float multiplier;
    }
}
