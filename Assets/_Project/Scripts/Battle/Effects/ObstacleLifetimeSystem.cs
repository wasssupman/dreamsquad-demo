using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct ObstacleLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObstacleSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var blockedCells = SystemAPI.GetSingleton<ObstacleSingleton>().blockedCells;
            blockedCells.Clear();

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (obstacle, entity) in
                     SystemAPI.Query<RefRW<Obstacle>>()
                              .WithNone<BlockingHazardCellsBuffer>()
                              .WithEntityAccess())
            {
                obstacle.ValueRW.remainingLife -= dt;
                if (obstacle.ValueRO.remainingLife <= 0f)
                    ecb.DestroyEntity(entity);
                else
                    blockedCells.Add(obstacle.ValueRO.cell);
            }

            foreach (var cellsBuffer in
                     SystemAPI.Query<DynamicBuffer<BlockingHazardCellsBuffer>>()
                              .WithAll<BlockingHazard>()
                              .WithNone<DeadTag>())
            {
                for (int i = 0; i < cellsBuffer.Length; i++)
                    blockedCells.Add(cellsBuffer[i].cell);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
