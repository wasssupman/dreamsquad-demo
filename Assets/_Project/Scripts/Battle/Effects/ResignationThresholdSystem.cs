// season-gimmick-clockout unit 3 — 룰 2(전반): 살아있는 사직서가 resignationThreshold 도달 시
// 그 수만큼 소모(destroy) + MeteorBarrageRequest enqueue(→ BattleBridge 가 메테오 cast, unit 4).
// 번아웃 Consume 전례(임계 소모 후 재누적). ClockOutGimmickConfig 부재 시 미가동(self-gate).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct ResignationThresholdSystem : ISystem
    {
        private EntityQuery _resignationQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ClockOutGimmickConfig>();
            state.RequireForUpdate<MeteorBarrageRequestsSingleton>();
            _resignationQuery = state.GetEntityQuery(ComponentType.ReadOnly<Resignation>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ClockOutGimmickConfig>();
            int threshold = config.resignationThreshold;
            if (threshold <= 0)
                return; // 잘못 저작된 SO 방어(0 이면 무한 트리거)

            int count = _resignationQuery.CalculateEntityCount();
            if (count < threshold)
                return;

            // 임계마다 barrage 1건. 한 프레임에 여러 임계를 넘겼으면(다중 퇴근) 그 배수만큼.
            int barrages = count / threshold;
            int toDestroy = barrages * threshold;

            var entities = _resignationQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < toDestroy; i++)
                ecb.DestroyEntity(entities[i]);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            entities.Dispose();

            var queue = SystemAPI.GetSingleton<MeteorBarrageRequestsSingleton>().queue;
            for (int b = 0; b < barrages; b++)
                queue.Enqueue(new MeteorBarrageRequest { meteorCount = config.meteorCount });
        }
    }
}
