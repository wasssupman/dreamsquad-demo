using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) took damage
    // this frame. position = enemy LocalTransform.Position (feet) at enqueue;
    // the spawner adds a head Y-offset. amount = total damage applied this frame
    // (post-mitigation, always > 0 at enqueue site). Magnitude drives popup
    // size/color in presentation.
    //
    // unit-health-display unit 0 — enemy hit micro-bar plumbing:
    //   entity  = the damaged enemy (view anchor lookup key on the Mono side).
    //   hpRatio = clamp(newHp/max, 0, 1) AFTER this frame's damage+heal settle
    //             (0 when max <= 0). Enqueued post-settlement so막타=0.
    public struct DamageNumberEvent
    {
        public float3 position;
        public float amount;
        public Entity entity;
        public float hpRatio;
    }
}
