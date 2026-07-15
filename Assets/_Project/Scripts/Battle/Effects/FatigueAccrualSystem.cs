// season-gimmick-overwork unit 3 — 야근 룰 1: 배치된 방어 유닛이 fatigueInterval 마다
// 피로도 +fatigueAmount. 임계 도달 시 번아웃은 StackModifierTickSystem + ThresholdRule
// (unit 0/1 데이터)이 처리 — 이 시스템은 누적 소스일 뿐이다.
// OverworkGimmickConfig 부재(기믹 비활성) 시 시스템 자체가 돌지 않는다 (self-gate).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct FatigueAccrualSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OverworkGimmickConfig>();
            state.RequireForUpdate<StackModifierApplyEventsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<OverworkGimmickConfig>();
            if (config.fatigueInterval <= 0f)
                return; // 무한 루프 방어 (잘못 저작된 SO)

            // Pass 1 — lazy attach: 배치된 defender(DefenderUnitTag, Units 읽기 전용)에 타이머 부착.
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<DefenderUnitTag>>()
                              .WithNone<FatigueAccrual>()
                              .WithEntityAccess())
            {
                ecb.AddComponent(entity, new FatigueAccrual { elapsed = 0f });
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Pass 2 — 주기마다 피로도 스택 enqueue. 시간은 BattleScaledRateManager 를 그대로
            // 타는 SystemAPI.Time.DeltaTime (정지/슬로우모 일관).
            float dt = SystemAPI.Time.DeltaTime;
            var stackQ = SystemAPI.GetSingleton<StackModifierApplyEventsSingleton>().queue;
            foreach (var (accrual, entity) in
                     SystemAPI.Query<RefRW<FatigueAccrual>>()
                              .WithAll<DefenderUnitTag>()
                              .WithEntityAccess())
            {
                accrual.ValueRW.elapsed += dt;
                while (accrual.ValueRO.elapsed >= config.fatigueInterval)
                {
                    accrual.ValueRW.elapsed -= config.fatigueInterval;
                    stackQ.Enqueue(new StackModifierApplyEvent
                    {
                        target         = entity,
                        kind           = StackKind.Fatigue,
                        countDelta     = config.fatigueAmount,
                        maxStack       = config.fatigueMaxStack,
                        perAppDuration = config.fatiguePerAppDuration,
                        source         = entity,
                    });
                }
            }
        }
    }
}
