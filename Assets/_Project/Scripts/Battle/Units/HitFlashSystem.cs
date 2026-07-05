using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Wassup.Battle.Units
{
    // Briefly inflates a unit when it takes a projectile hit, then restores its
    // original scale. Read-only from Health; we only write the entity's own
    // LocalTransform.Scale while the flash is active.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct HitFlashSystem : ISystem
    {
        private const float PeakBonus = 0.2f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HitFlashTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, flash, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<HitFlashTag>>()
                              .WithEntityAccess())
            {
                flash.ValueRW.remaining -= dt;
                if (flash.ValueRO.remaining <= 0f || flash.ValueRO.duration <= 0f)
                {
                    transform.ValueRW.Scale = flash.ValueRO.originalScale;
                    ecb.RemoveComponent<HitFlashTag>(entity);
                    continue;
                }

                float t = flash.ValueRO.remaining / flash.ValueRO.duration;
                transform.ValueRW.Scale = flash.ValueRO.originalScale * (1f + PeakBonus * t);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
