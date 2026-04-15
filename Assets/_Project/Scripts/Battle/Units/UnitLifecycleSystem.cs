using Unity.Burst;
using Unity.Entities;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct UnitLifecycleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PastGoalTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (tag, entity) in
                     SystemAPI.Query<RefRO<PastGoalTag>>()
                              .WithAll<AttackUnitTag>()
                              .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
