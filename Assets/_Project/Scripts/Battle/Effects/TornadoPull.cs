using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 7 — Tornado skill. Attached to each attacker captured in the cast
    // radius. While present, MovementSystem overrides the attacker's normal
    // waypoint step with a pull step toward `centerWorld` at `pullSpeed`.
    // EffectTickSystem removes the component when `remaining` reaches zero.
    //
    // Non-stacking: re-applying refreshes both fields from the newer cast so the
    // latest caster's center wins (mirrors SlowEffect semantics).
    public struct TornadoPull : IComponentData
    {
        public float3 centerWorld;
        public float pullSpeed;
        public float remaining;
    }
}
