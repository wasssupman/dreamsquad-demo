// season-gimmick-overwork unit 5 — 라스트런 crash: LastRun.remaining 만료 시 최대체력
// ×lastRunMaxHealthMul 을 영구(duration=+∞) 인큐하고 컴포넌트 제거. MaxHealthScaleSystem(Units)이
// 소비해 실제 Health.max/현재값 클램프 (Health 쓰기는 Units 소유 — 맥락 경계 유지).
// OverworkGimmickConfig 부재(기믹 비활성) 시 미가동.
// non-Burst: crash telemetry 로그(저빈도, PickupConsumeSystem 소비 로그와 대칭).
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct LastRunSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OverworkGimmickConfig>();
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<OverworkGimmickConfig>();
            float dt = SystemAPI.Time.DeltaTime;
            var statQ = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (lastRun, entity) in
                     SystemAPI.Query<RefRW<LastRun>>().WithEntityAccess())
            {
                lastRun.ValueRW.remaining -= dt;
                if (lastRun.ValueRO.remaining > 0f)
                    continue;

                // crash: 최대체력 영구 컷 (source=자기, +∞ 지속 → 판 끝까지).
                statQ.Enqueue(new StatModifierApplyEvent
                {
                    target    = entity,
                    stat      = StatKind.MaxHealthMul,
                    op        = CombineOp.Multiplicative,
                    magnitude = config.lastRunMaxHealthMul,
                    duration  = float.PositiveInfinity,
                    source    = entity,
                    stackId   = 0,
                    origin    = ModifierOrigin.Unspecified,
                });
                ecb.RemoveComponent<LastRun>(entity);
                UnityEngine.Debug.Log($"[Redbull] {entity} crash → 최대체력 x{config.lastRunMaxHealthMul:F2} (영구)");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
