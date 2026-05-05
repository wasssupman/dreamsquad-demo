using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 7 — Meteor skill. One entity per active meteor carries this telegraph:
    // a visible warning ring renders for `warningRemaining` seconds before the
    // damage resolves in a single burst. MeteorResolutionSystem consumes this
    // record, emits IncomingDamage to attackers within radius, then destroys the
    // carrier entity. Deliberately not attached to attackers — the entity count
    // is 1 per meteor, not N per-target.
    public struct MeteorPending : IComponentData
    {
        public float3 centerWorld;
        public int   tileRange;
        public float damage;
        public float warningRemaining;
    }
}
