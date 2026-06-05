using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) took damage
    // this frame. position = enemy LocalTransform.Position (feet) at enqueue;
    // the spawner adds a head Y-offset. amount = total damage applied this frame
    // (post-mitigation, always > 0 at enqueue site). Magnitude drives popup
    // size/color in presentation.
    public struct DamageNumberEvent
    {
        public float3 position;
        public float amount;
    }
}
