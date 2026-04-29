using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CcApplySystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct HazardLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HazardSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var singletonRW = SystemAPI.GetSingletonRW<HazardSingleton>();
            singletonRW.ValueRW.cellToEffects.Clear();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (hazard, cells, effects, entity) in
                     SystemAPI.Query<RefRW<Hazard>, DynamicBuffer<HazardCellsBuffer>, DynamicBuffer<HazardEffectsBuffer>>()
                              .WithEntityAccess())
            {
                hazard.ValueRW.remainingLife -= dt;
                if (hazard.ValueRO.remainingLife <= 0f)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                for (int c = 0; c < cells.Length; c++)
                {
                    for (int e = 0; e < effects.Length; e++)
                        singletonRW.ValueRW.cellToEffects.Add(cells[c].cell, effects[e].effect);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
