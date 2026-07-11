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

            // unit 5 — 필드 소비자는 보스뿐. 보스 부재 시 재빌드 skip(보스 스폰 프레임엔
            // 이 시스템이 Movement 앞에서 다시 돌아 신선한 필드 보장).
            // R = 동시 헌터 사거리(타일)의 **min fold** — 소스는 "모든 헌터가 발사 가능한 셀"
            // 이어야 사거리 짧은 보스가 dist-0 셀에서 발사 불가로 서버리는 스톨이 구조적으로
            // 불가능하다. 사거리 긴 보스는 FSM 이 소스 도달 전에 Engaging 으로 먼저 멈춘다.
            // (현 콘텐츠는 보스 1종 → min==max. 이질 사거리 동시 헌터의 "긴 보스만 공격 가능한
            // 심층 배치"까지 사냥하려면 R-별 필드 분리가 필요 — README 후속 후보.)
            var bossQuery = SystemAPI.QueryBuilder().WithAll<Wassup.Battle.Combat.BossTag>().Build();
            if (bossQuery.IsEmpty) return;
            int rangeTiles = int.MaxValue;
            foreach (var atk in SystemAPI.Query<RefRO<Wassup.Battle.Combat.AttackState>>()
                                         .WithAll<Wassup.Battle.Combat.BossTag>())
                rangeTiles = math.min(rangeTiles, GridMath.RangeToTiles(atk.ValueRO.range));
            if (rangeTiles == int.MaxValue) rangeTiles = 1; // AttackState 없는 보스뿐 — 인접 폴백
            rangeTiles = math.max(1, rangeTiles);

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

            int discArea = (2 * rangeTiles + 1) * (2 * rangeTiles + 1);
            var sources = new NativeList<int2>(math.max(4, defenderCells.Length * discArea), Allocator.Temp);
            FlowFieldBuilder.CollectDefenderSources(field.walkMask, field.gridSize,
                defenderCells.AsArray(), rangeTiles, sources);
            FlowFieldBuilder.BuildFromSources(field.walkMask, field.gridSize,
                sources.AsArray(), field.flow, field.dist);

            defenderCells.Dispose();
            sources.Dispose();
        }
    }
}
