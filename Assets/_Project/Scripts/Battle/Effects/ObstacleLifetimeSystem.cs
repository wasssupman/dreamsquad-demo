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

            // battle-structures unit 4 정정(투트랙 리뷰 중 자체 적발) — WithAll<BlockingHazard>
            // 를 제거했다. 버퍼 보유 = 다중셀 점유 선언이다. 컴포넌트를 요구하면 본능(거점,
            // 버퍼만 보유)의 3×3 이 blockedCells 에 못 들어가 **통행을 전혀 막지 않고**,
            // FlowFieldRebuildSystem 시그니처도 이 집합만 보므로 필드 리빌드도 안 돈다.
            // 현재 버퍼 생산자 = 방벽(EffectSpawner — BlockingHazard 동반)과 본능(브리지) 둘뿐.
            foreach (var cellsBuffer in
                     SystemAPI.Query<DynamicBuffer<BlockingHazardCellsBuffer>>()
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
