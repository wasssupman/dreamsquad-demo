using Unity.Entities;

namespace Wassup.Battle.Units
{
    // AttackOutput.Heal pulse enqueues here via ecb.AppendToBuffer.
    // DamageApplicationSystem drains every frame and calls Clear() to prevent accumulation.
    // RegenPerSec stat is NOT routed through this buffer — DamageApplicationSystem reads
    // BuffStats.regenPerSec directly and applies it each frame without touching this buffer.
    public struct IncomingHeal : IBufferElementData
    {
        public float amount;
    }
}
