using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Buffer element attached to units that can receive damage. Combat systems
    // append entries (via ECB); DamageApplicationSystem drains them and mutates Health.
    // This is the canonical cross-context event channel per TRD 2.5.2.
    public struct IncomingDamage : IBufferElementData
    {
        public float amount;
    }
}
