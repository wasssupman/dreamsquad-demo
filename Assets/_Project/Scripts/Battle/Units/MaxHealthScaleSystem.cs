// season-gimmick-overwork unit 1 — ModifierStats.maxHealthMul(Effects, 읽기 전용) 을 소비해
// Health.max 를 재계산하는 Units 시스템. Health 쓰기는 이 맥락(Units) 안에서만 일어난다.
// 순수 계산은 Health.ScaleMax (EditMode 테스트 대상).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(ModifierStatsAggregateSystem))]
    public partial struct MaxHealthScaleSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Pass 1 — lazy attach: 배율이 1 에서 벗어난 첫 프레임에 baseMax 를 캡처.
            // mul <= 0 은 미초기화 방어 (스폰 init 은 base 1 — BattleBridge 2곳).
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (health, stats, entity) in
                     SystemAPI.Query<RefRO<Health>, RefRO<ModifierStats>>()
                              .WithNone<MaxHealthScaleState>()
                              .WithEntityAccess())
            {
                float mul = stats.ValueRO.maxHealthMul;
                if (mul > 0f && mul != 1f)
                    ecb.AddComponent(entity, new MaxHealthScaleState
                    {
                        baseMax = health.ValueRO.max,
                        appliedMul = 1f,
                    });
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Pass 2 — 배율 변화 시에만 재계산 (복원 mul=1 포함).
            foreach (var (health, scale, stats) in
                     SystemAPI.Query<RefRW<Health>, RefRW<MaxHealthScaleState>, RefRO<ModifierStats>>())
            {
                float mul = stats.ValueRO.maxHealthMul;
                if (mul <= 0f || mul == scale.ValueRO.appliedMul)
                    continue;

                float2 scaled = Health.ScaleMax(health.ValueRO.value, scale.ValueRO.baseMax, mul);
                health.ValueRW.value = scaled.x;
                health.ValueRW.max = scaled.y;
                scale.ValueRW.appliedMul = mul;
            }
        }
    }
}
