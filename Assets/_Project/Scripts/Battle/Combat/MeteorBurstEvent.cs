using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // Phase 8 §12 — ECS→MonoBehaviour crossing payload for the Meteor AoE
    // resolution moment. Carries what the particle spawner needs to draw the
    // burst (position + radius); no ECS refs so the VFX layer stays detached.
    public struct MeteorBurstEvent
    {
        public float3 center;
        public float radius;
    }
}
