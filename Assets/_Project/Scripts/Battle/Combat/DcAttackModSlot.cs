using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-attack-mod-bounce unit 3 — baked, unmanaged form of one
    // always-on attack-output modifier (definition: DcAttackModSpec). Combat-owned,
    // read-only at use: AttackSystem aggregates these onto the base attack's
    // ProjectileSpawnRequest at spawn time. Attach/remove only through BattleBridge.
    // Trigger-less (card class c) — no counter, unlike DcTriggerSlot.
    [InternalBufferCapacity(2)]
    public struct DcAttackModSlot : IBufferElementData
    {
        public int instanceId;         // effect-instance id (독립 회수용, DcTriggerSlot 과 별 namespace 아님 — 공용 _dcInstanceCounter)
        public DcAttackModKind kind;
        public int count;              // ProjectileBounce: bounce count
        public int tileRange;          // retarget search radius (Chebyshev tiles)
        public float damageMul;        // per-bounce decay
    }
}
