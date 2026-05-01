using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal emitted when IncomingHeal pulses are drained.
    // RegenPerSec is intentionally excluded so passive regeneration does not spam VFX.
    // amount = sum of IncomingHeal entries drained this frame (always > 0 at enqueue site).
    // Reserved for future VFX scaling: large heal → larger particle burst.
    public struct HealAppliedEvent
    {
        public float3 position;
        public float amount;
    }
}
