// season-gimmick-overwork unit 5 — 야근 룰 2(후반): 유닛(적 통과 / defender 배치)이 레드불과
// 같은 셀에 있으면 소비 → 라스트런 부여 (공속 버프 즉시 + LastRun crash 타이머).
// non-Burst: 소비 telemetry 로그(저빈도) 용 — StackModifierTickSystem 전례. 소비 판정 자체는
// 유닛 수 순회로 가볍다. OverworkGimmickConfig 부재 시 미가동 (self-gate).
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(PickupSpawnSystem))]
    public partial struct PickupConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OverworkGimmickConfig>();
            state.RequireForUpdate<FlowFieldSingleton>();
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 현 픽업들을 cell→entity 로 색인. 없으면 early-return.
            var byCell = new NativeHashMap<int2, Entity>(16, Allocator.Temp);
            foreach (var (pickup, entity) in
                     SystemAPI.Query<RefRO<Pickup>>().WithEntityAccess())
            {
                byCell.TryAdd(pickup.ValueRO.cell, entity); // 같은 셀 중복 시 첫 픽업만 (스폰이 중복 회피)
            }
            if (byCell.IsEmpty)
            {
                byCell.Dispose();
                return;
            }

            var config = SystemAPI.GetSingleton<OverworkGimmickConfig>();
            var flow = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var statQ = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 통합 소비: 첫 소비자 승(맵에서 제거), 픽업 파괴, 공속 버프 인큐, LastRun add/refresh.
            void Consume(int2 cell, Entity unit)
            {
                if (!byCell.TryGetValue(cell, out var pickup))
                    return;
                byCell.Remove(cell);
                ecb.DestroyEntity(pickup);

                statQ.Enqueue(new StatModifierApplyEvent
                {
                    target    = unit,
                    stat      = StatKind.AttackSpeedMul,
                    op        = CombineOp.Multiplicative,
                    magnitude = config.lastRunAttackSpeedMul,
                    duration  = config.lastRunDuration,
                    source    = unit,
                    stackId   = 0,
                    origin    = ModifierOrigin.Unspecified,
                });

                var lastRun = new LastRun { remaining = config.lastRunDuration };
                if (em.HasComponent<LastRun>(unit)) ecb.SetComponent(unit, lastRun);
                else ecb.AddComponent(unit, lastRun);

                UnityEngine.Debug.Log($"[Redbull] {unit} consumed at cell ({cell.x},{cell.y}) → 라스트런 (공속 x{config.lastRunAttackSpeedMul:F2} {config.lastRunDuration:F0}s, 종료 시 최대체력의 {config.lastRunDamageFraction * 100f:F0}% 피해)");
            }

            // Defender: 배치 셀(권위값).
            foreach (var (tile, entity) in
                     SystemAPI.Query<RefRO<DefenderTile>>()
                              .WithAll<DefenderUnitTag>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                Consume(tile.ValueRO.cell, entity);
            }

            // Enemy: 현재 위치 → 셀.
            foreach (var (xform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                              .WithAll<AttackUnitTag>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                int2 cell = GridMath.WorldToCell(xform.ValueRO.Position, flow.tileSize, flow.gridSize, flow.origin);
                Consume(cell, entity);
            }

            ecb.Playback(em);
            ecb.Dispose();
            byCell.Dispose();
        }
    }
}
