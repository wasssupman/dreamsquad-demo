using Unity.Entities;

namespace Wassup.Battle.Units
{
    // dreamcatcher-content-1 unit 3 (① 가시 갑옷) — per-instance "N회 피격" counter,
    // owned by Units. Kept OUT of Combat's DcTriggerSlot on purpose: the count is
    // written where the defender takes damage (DamageApplicationSystem, Units), and
    // component/buffer writes must stay within the owning context (TRD 맥락 경계).
    // Buffer element, not a single component, so two copies of the same card get
    // independent counters (mirrors DcTriggerSlot). Attach via BattleBridge only.
    [InternalBufferCapacity(2)]
    public struct DamagedCounter : IBufferElementData
    {
        public int instanceId;
        public ushort period;   // OnDamagedN: fire on every N-th damaged frame
        public ushort counter;  // owned write: DamageApplicationSystem only
    }
}
