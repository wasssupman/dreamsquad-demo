using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Effects
{
    // boss-defender-field unit 1/5 — 살아있는 방어유닛들을 "공격 가능한" walkable 셀
    // (Chebyshev ≤ 헌터 사거리 min)을 소스로 multi-source BFS 를 매 프레임 재빌드.
    // 그리드가 작아 배치/사망 이벤트 훅·dirty 추적 없이 매 프레임이 가장 단순(계약 4).
    // 방어유닛 0 → BuildFromSources 가 전 셀 int.MaxValue 로 리셋 → goal-fallback(계약 5).
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

            // continuous-agent-movement unit 1 — 정적 walk 마스크는 goal field 가 단독 소유한다
            // (사본을 들면 double dispose + 벽 정의 이원화). 실운영에선 SimFieldInstaller 가 두
            // 필드를 항상 함께 세우므로 동시 존재이지만, 합성 테스트 월드는 한쪽만 만들 수 있다.
            //
            // 이 의존을 RequireForUpdate 에 올리지 않은 것은 의도다 — 올리면 DefenderField 만
            // 세우는 테스트 월드에서 시스템이 아예 안 돌아 기존 픽스처가 조용히 바뀐다. 대신
            // 조기 return 을 택했고, 그 대가로 그런 월드에선 매 프레임 OnUpdate 진입 후 즉시
            // 빠져나오는 비용이 있다(ecs-review T3). 프로덕션엔 해당 없음.
            if (!SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var goalField)) return;
            var walkMask = goalField.walkMask;
            if (!walkMask.IsCreated) return;

            // unit 5 — 필드 소비자는 헌터뿐. 헌터 부재 시 재빌드 skip(헌터 스폰 프레임엔
            // 이 시스템이 Movement 앞에서 다시 돌아 신선한 필드 보장).
            // bonus-wave-pull unit 0 — 게이트가 BossTag 에서 DefenderHunterTag 로 바뀌었다.
            // 보스는 여전히 그 태그를 받으므로(tier == Boss) 이 시스템 관점에선 무회귀다.
            // R = 동시 헌터 사거리(타일)의 **min fold** — 소스는 "모든 헌터가 발사 가능한 셀"
            // 이어야 사거리 짧은 헌터가 dist-0 셀에서 발사 불가로 서버리는 스톨이 구조적으로
            // 불가능하다. 사거리 긴 헌터는 FSM 이 소스 도달 전에 Engaging 으로 먼저 멈춘다.
            // ⚠ **이질 사거리 헌터가 동시에 살아 있으면 R 이 짧은 쪽으로 내려간다.** 보너스
            // 당기기의 근접 잡몹 10기와 원거리 보스가 겹치는 구간이 정확히 그 조건이고,
            // 그동안 보스는 「긴 사거리로만 닿던 심층 배치」의 소스를 잃어 hunt-dist 가
            // MaxValue 가 되고 **사냥을 멈추고 골로 향한다**(스톨이 아니라 전략 퇴행).
            // 잡몹이 정리되면 R 이 복원돼 보스가 사냥을 재개한다. 해소는 R-별 필드 분리뿐 —
            // bonus-wave-pull / boss-defender-field README 후속 후보.
            var hunterQuery = SystemAPI.QueryBuilder().WithAll<Wassup.Battle.Combat.DefenderHunterTag>().Build();
            if (hunterQuery.IsEmpty) return;
            int rangeTiles = int.MaxValue;
            foreach (var atk in SystemAPI.Query<RefRO<Wassup.Battle.Combat.AttackState>>()
                                         .WithAll<Wassup.Battle.Combat.DefenderHunterTag>())
                rangeTiles = math.min(rangeTiles, GridMath.RangeToTiles(atk.ValueRO.range));
            if (rangeTiles == int.MaxValue) rangeTiles = 1; // AttackState 없는 헌터뿐 — 인접 폴백
            rangeTiles = math.max(1, rangeTiles);

            // 방어유닛 스냅샷 — FSM 후보 풀(EnemyAiStateSystem)과 동일 조건 + faction 필터.
            var defenderCells = new NativeList<int2>(16, Allocator.Temp);
            foreach (var (faction, transform) in
                     SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>>()
                              .WithAll<Health>()
                              .WithNone<PendingDeployment, DeadTag>())
            {
                // battle-structures unit 0 — DefenderUnit 단독. 예전엔 Faction.Defender 였고
                // 골 타워가 그 비트를 달아 **보스 사냥 필드의 방어유닛 소스로 계수됐다**.
                if (((int)faction.ValueRO.value & (int)Faction.DefenderUnit) == 0) continue;
                defenderCells.Add(GridMath.WorldToCell(
                    transform.ValueRO.Position, field.tileSize, field.gridSize, origin: field.origin));
            }

            int discArea = (2 * rangeTiles + 1) * (2 * rangeTiles + 1);
            var sources = new NativeList<int2>(math.max(4, defenderCells.Length * discArea), Allocator.Temp);
            FlowFieldBuilder.CollectDefenderSources(walkMask, field.gridSize,
                defenderCells.AsArray(), rangeTiles, sources);
            FlowFieldBuilder.BuildFromSources(walkMask, field.gridSize,
                sources.AsArray(), field.flow, field.dist);

            defenderCells.Dispose();
            sources.Dispose();
        }
    }
}
