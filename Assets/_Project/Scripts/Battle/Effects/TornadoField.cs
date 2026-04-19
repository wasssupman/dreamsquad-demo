using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 8 §17 — Tornado carrier entity. One per active cast; MovementSystem
    // queries live fields each frame and applies a pull step toward `centerWorld`
    // to any attacker inside `radius`. Replaces the Phase 7 per-attacker
    // `TornadoPull` component, which only captured the snapshot at cast time and
    // missed enemies that entered the area during the duration.
    //
    // EffectTickSystem decrements `remaining` and destroys the entity when it
    // hits zero (PortalLink pattern).
    public struct TornadoField : IComponentData
    {
        public float3 centerWorld;
        public float radius;
        public float pullSpeed;
        public float remaining;
    }
}
