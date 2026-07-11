using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // boss-defender-field unit 1 — 살아있는 방어유닛들의 walkable 4-이웃을 소스로
    // multi-source BFS 를 매 프레임 재빌드. 그리드가 작아 배치/사망 이벤트 훅·dirty
    // 추적 없이 매 프레임이 가장 단순(계약 4). 방어유닛 0 → BuildFromSources 가
    // 전 셀 int.MaxValue 로 리셋 → Movement 의 goal-fallback 신호(계약 5).
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateBefore(typeof(Wassup.Battle.Movement.MovementSystem))]
    public partial struct DefenderFieldSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DefenderFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var field = SystemAPI.GetSingleton<DefenderFieldSingleton>();
            if (!field.IsCreated) return;

            // 방어유닛 스냅샷 — FSM 후보 풀(EnemyAiStateSystem)과 동일 조건 + faction 필터.
            var defenderCells = new NativeList<int2>(16, Allocator.Temp);
            foreach (var (faction, transform) in
                     SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                              .WithAll<Health>()
                              .WithNone<PendingDeployment, DeadTag>())
            {
                if (((int)faction.ValueRO.value & (int)Faction.Defender) == 0) continue;
                defenderCells.Add(GridMath.WorldToCell(
                    transform.ValueRO.Position, field.tileSize, field.gridSize, origin: field.origin));
            }

            var sources = new NativeList<int2>(math.max(4, defenderCells.Length * 4), Allocator.Temp);
            FlowFieldBuilder.CollectDefenderSources(field.walkMask, field.gridSize,
                defenderCells.AsArray(), sources);
            FlowFieldBuilder.BuildFromSources(field.walkMask, field.gridSize,
                sources.AsArray(), field.flow, field.dist);

            defenderCells.Dispose();
            sources.Dispose();
        }
    }
}
