using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-unit-trigger Unit 1 — baked, unmanaged form of one unit-bound
    // triggered card mechanic (definition layer: DcMechanic). Combat-owned:
    // counter writes happen only in AttackSystem RESOLVE; attach/remove happens
    // only through BattleBridge (the sole MonoBehaviour↔ECS gateway).
    [InternalBufferCapacity(2)]
    public struct DcTriggerSlot : IBufferElementData
    {
        // Effect-instance id — one slot per attached mechanic instance, so two
        // copies of the same card get independent counters. Separate namespace
        // from stat-modifier stackId: never compare the two.
        public int instanceId;
        public DcTriggerKind trigger;
        public ushort period;   // AttackN: fire on every N-th attack resolve
        public ushort counter;  // owned write: AttackSystem RESOLVE only
        public DcPayloadKind payload;
        public float magnitude; // flat damage — attacker damageMul intentionally not applied

        // ProjectileToTarget only — baked from ProjectileData at attach time
        // (AttackSystem cannot read managed SOs). projectileDataIndex lifetime =
        // session: _projectileDataByIndex is never reset by BeginPlacement.
        public int projectileDataIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
    }
}
