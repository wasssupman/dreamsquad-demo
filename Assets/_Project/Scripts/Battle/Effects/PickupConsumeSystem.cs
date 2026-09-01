// season-gimmick-overwork unit 5 — 야근 룰 2(후반): 유닛(적 통과 / defender 배치)이 레드불과
// 같은 셀에 있으면 소비 → 라스트런 부여 (공속 버프 즉시 + LastRun crash 타이머).
// 매 프레임 전체 defender+enemy 순회 hot-path → Burst. RedBullGimmickConfig 부재 시 미가동(self-gate).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(PickupSpawnSystem))]
    public partial struct PickupConsumeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RedBullGimmickConfig>();
            state.RequireForUpdate<FlowFieldSingleton>();
            state.RequireForUpdate<StatModifierApplyEventsSingleton>();
        }

        [BurstCompile]
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

            var config = SystemAPI.GetSingleton<RedBullGimmickConfig>();
            var flow = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var statQ = SystemAPI.GetSingleton<StatModifierApplyEventsSingleton>().queue;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Defender: 배치 셀(권위값).
            foreach (var (tile, entity) in
                     SystemAPI.Query<RefRO<DefenderFootprint>>()
                              .WithAll<DefenderUnitTag>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                TryConsume(tile.ValueRO.anchor, entity, ref byCell, ref ecb, statQ, config, em);
            }

            // Enemy: 현재 위치 → 셀.
            foreach (var (xform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                              .WithAll<AttackUnitTag>()
                              .WithNone<PendingDeployment, DeadTag>()
                              .WithEntityAccess())
            {
                int2 cell = GridMath.WorldToCell(xform.ValueRO.Position, flow.tileSize, flow.gridSize, flow.origin);
                TryConsume(cell, entity, ref byCell, ref ecb, statQ, config, em);
            }

            ecb.Playback(em);
            ecb.Dispose();
            byCell.Dispose();
        }

        // 첫 소비자 승(맵에서 제거) → 픽업 파괴 + 공속 버프 인큐 + LastRun 부착.
        // 공속 버프 origin=Gimmick (시즌 기믹 출처 태그). crash 데미지는 LastRunSystem.
        // 소비 락(review #2): 라스트런 진행 중(LastRun 보유)인 유닛은 소비하지 않는다 — 밟아도
        // 픽업은 보드에 남아 만료되거나 다른 유닛이 먹는다. crash 로 비용을 치른 뒤에야 재버프
        // 가능(재소비로 타이머 리셋 → crash 무한 회피하던 문제 차단).
        private static void TryConsume(
            int2 cell, Entity unit,
            ref NativeHashMap<int2, Entity> byCell,
            ref EntityCommandBuffer ecb,
            NativeQueue<StatModifierApplyEvent> statQ,
            in RedBullGimmickConfig config,
            EntityManager em)
        {
            if (em.HasComponent<LastRun>(unit))
                return; // 라스트런 중 — 재소비 락(픽업 잔존)
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
                origin    = ModifierOrigin.Gimmick,
            });

            // 소비 락 통과 = LastRun 미보유 → 단순 부착(refresh 분기 불요).
            ecb.AddComponent(unit, new LastRun { remaining = config.lastRunDuration });
        }
    }
}
