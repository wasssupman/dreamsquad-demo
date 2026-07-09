using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) took damage.
    // position = enemy LocalTransform.Position (feet) at enqueue; the spawner adds
    // a head Y-offset. amount = ONE hit's post-mitigation damage — the system
    // enqueues one event per IncomingDamage entry, not the frame's sum, so a
    // projectile hit and a same-frame dreamcatcher hit surface as two numbers
    // (damage-number-popup unit 6). Magnitude drives popup size/color.
    //
    // unit-health-display unit 0 — enemy hit micro-bar plumbing:
    //   entity  = the damaged enemy (view anchor lookup key on the Mono side).
    //   hpRatio = Health.ComputeRatio(newHp, max) AFTER this frame's damage+heal
    //             settle (clamp[0,1]; 0 when max<=0). Enqueued post-settlement so
    //             a killing blow reports 0.
    public struct DamageNumberEvent
    {
        public float3 position;
        public float amount;
        public Entity entity;
        public float hpRatio;
    }
}
