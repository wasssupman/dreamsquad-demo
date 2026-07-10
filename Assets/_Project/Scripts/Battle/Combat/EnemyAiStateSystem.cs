using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // enemy-ai-fsm Unit 1 — 적 FSM 전이 평가. Combat 소유, EnemyAiState 의 유일한 writer.
    // 전이 트리거(타겟·사거리·aggro)를 평가해 매 틱 상태를 set. Movement/Attack 은 RO 소비.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(TauntAttackGrantSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct EnemyAiStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyAiState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
            float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;

            // 타겟(디펜더) 후보 스냅샷 — AttackSystem 과 동일 후보 풀.
            var candQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment, DeadTag>()
                .Build();
            var candEntities = candQuery.ToEntityArray(Allocator.Temp);
            var candTransforms = candQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var candFactions = candQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            var attackLookup = SystemAPI.GetComponentLookup<AttackState>(true);
            var aggroLookup = SystemAPI.GetComponentLookup<Aggroed>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var classLookup = SystemAPI.GetComponentLookup<DefenderClassTag>(true);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(true);
            var focusLookup = SystemAPI.GetComponentLookup<FocusTarget>(true);
            var filterLookup = SystemAPI.GetComponentLookup<EnemyTargetFilter>(true);
            var behaviorLookup = SystemAPI.GetComponentLookup<EnemyBehavior>(true);
            // enemy-hunter-targeting unit 1 — 헌터(보스) 게이트 + 추격 대상 write.
            var bossLookup = SystemAPI.GetComponentLookup<BossTag>(true);
            var huntLookup = SystemAPI.GetComponentLookup<HuntTarget>(false);

            foreach (var (aiState, transform, enemyEntity) in
                     SystemAPI.Query<RefRW<EnemyAiState>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float3 atkPos = transform.ValueRO.Position;
                int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);
                bool hasAttack = attackLookup.HasComponent(enemyEntity);
                int tileRange = hasAttack ? GridMath.RangeToTiles(attackLookup[enemyEntity].range) : 0;
                int mask = hasAttack ? attackLookup[enemyEntity].targetMask : 0;

                bool aggroed = aggroLookup.HasComponent(enemyEntity);
                bool guardianInRange = false;
                bool hasFireTarget = false;

                if (aggroed)
                {
                    // 가디언 사거리 판정엔 AttackState.range 필요. 없으면 Chasing 고착(M5).
                    if (hasAttack)
                    {
                        var g = aggroLookup[enemyEntity].guardian;
                        if (g != Entity.Null && transformLookup.HasComponent(g))
                        {
                            int2 gCell = GridMath.WorldToCell(transformLookup[g].Position, tileSize, gridSize, origin: ffOrigin);
                            int gDist = math.max(math.abs(gCell.x - atkCell.x), math.abs(gCell.y - atkCell.y));
                            guardianInRange = gDist <= tileRange;
                        }
                    }
                }
                else if (hasAttack)
                {
                    hasFireTarget = HasFireTarget(enemyEntity, atkCell, tileRange, mask,
                        candEntities, candTransforms, candFactions, tileSize, gridSize, ffOrigin,
                        classLookup, transformLookup, healthLookup, deadLookup, focusLookup, filterLookup, behaviorLookup);
                }

                // enemy-hunter-targeting unit 1 — 헌터(보스)는 비-aggro·사거리 밖일 때
                // 최근접 방어유닛을 추격 대상으로 잡는다. HuntTarget 은 스폰 베이크된
                // 보스만 보유(HasComponent 가드) — 비-보스는 이 블록 전부 no-op.
                bool isHunter = bossLookup.HasComponent(enemyEntity);
                bool hasHuntTarget = false;
                if (isHunter && huntLookup.HasComponent(enemyEntity))
                {
                    Entity target = Entity.Null;
                    if (!aggroed && hasAttack && !hasFireTarget)
                    {
                        int idx = SelectNearestTarget(enemyEntity, atkCell, mask,
                            candEntities, candTransforms, candFactions, tileSize, gridSize, ffOrigin,
                            classLookup, filterLookup);
                        if (idx >= 0) { target = candEntities[idx]; hasHuntTarget = true; }
                    }
                    // 사거리 내 타겟(Engaging) / aggro / 방어유닛 0 → 추격 잔상 클리어.
                    huntLookup[enemyEntity] = new HuntTarget { value = target };
                }

                aiState.ValueRW.value = Evaluate(aggroed, guardianInRange, hasFireTarget, isHunter, hasHuntTarget);
            }

            candEntities.Dispose();
            candTransforms.Dispose();
            candFactions.Dispose();
        }

        // 순수 전이 함수. aggro 우선, 비-aggro 는 "AttackSystem 이 fire 할 타겟 존재" 로 Engaging/Marching.
        // 비-헌터 오버로드 — enemy-hunter-targeting 이전 계약을 그대로 보존(무회귀 고정).
        public static AiState Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget)
            => Evaluate(aggroed, guardianInRange, hasFireTarget, isHunter: false, hasHuntTarget: false);

        // enemy-hunter-targeting unit 0 — 헌터(보스) 축 추가. 비-aggro 헌터가 사거리
        // 내 타겟이 없어도 추격 대상(HuntTarget)이 있으면 Chasing(goal 대신 추격) —
        // 새 AiState 없이 기존 Chasing 재사용(계약 2). isHunter=false 면 기존과 동일.
        public static AiState Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget,
                                       bool isHunter, bool hasHuntTarget)
        {
            if (aggroed) return guardianInRange ? AiState.Standoff : AiState.Chasing;
            if (hasFireTarget) return AiState.Engaging;
            if (isHunter && hasHuntTarget) return AiState.Chasing;
            return AiState.Marching;
        }

        // enemy-hunter-targeting unit 1 — 후보 중 atkCell 최근접 방어유닛의 candEntities
        // index(사거리 조건 없음 — 추격 대상). eligibility 필터(faction mask + class,
        // HasFireTarget 과 동일, 사거리 게이트만 제거)는 여기서, 최근접+동점 결정은
        // 순수함수 HunterTargeting.NearestIndex 로(제약 10 — EditMode 고정).
        static int SelectNearestTarget(
            Entity attacker, int2 atkCell, int mask,
            in NativeArray<Entity> candEntities,
            in NativeArray<LocalTransform> candTransforms,
            in NativeArray<FactionTag> candFactions,
            float tileSize, int2 gridSize, float3 ffOrigin,
            in ComponentLookup<DefenderClassTag> classLookup,
            in ComponentLookup<EnemyTargetFilter> filterLookup)
        {
            bool hasFilter = filterLookup.HasComponent(attacker);
            int filterMask = hasFilter ? filterLookup[attacker].classMask : -1;

            int n = candEntities.Length;
            var elCells = new NativeArray<int2>(n, Allocator.Temp);
            var elKeys = new NativeArray<int>(n, Allocator.Temp);
            var elOrig = new NativeArray<int>(n, Allocator.Temp);
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                if (((int)candFactions[i].value & mask) == 0) continue;
                if (candEntities[i] == attacker) continue;
                int cclass = classLookup.HasComponent(candEntities[i]) ? (int)classLookup[candEntities[i]].value : -1;
                if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue;
                elCells[m] = GridMath.WorldToCell(candTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                elKeys[m] = candEntities[i].Index;
                elOrig[m] = i;
                m++;
            }

            int p = HunterTargeting.NearestIndex(atkCell, elCells.GetSubArray(0, m), elKeys.GetSubArray(0, m));
            int result = p < 0 ? -1 : elOrig[p];
            elCells.Dispose();
            elKeys.Dispose();
            elOrig.Dispose();
            return result;
        }

        // ⚠ AttackSystem fire 조건 미러 (AttackSystem.cs:131-189). 타겟 선정 로직 변경 시 동기화 필요.
        // FocusUntilDead 락이 걸린 적은 락 타겟이 사거리 내일 때만 fire → 그때만 Engaging(데드락 방지).
        static bool HasFireTarget(
            Entity attacker, int2 atkCell, int tileRange, int mask,
            in NativeArray<Entity> candEntities,
            in NativeArray<LocalTransform> candTransforms,
            in NativeArray<FactionTag> candFactions,
            float tileSize, int2 gridSize, float3 ffOrigin,
            in ComponentLookup<DefenderClassTag> classLookup,
            in ComponentLookup<LocalTransform> transformLookup,
            in ComponentLookup<Health> healthLookup,
            in ComponentLookup<DeadTag> deadLookup,
            in ComponentLookup<FocusTarget> focusLookup,
            in ComponentLookup<EnemyTargetFilter> filterLookup,
            in ComponentLookup<EnemyBehavior> behaviorLookup)
        {
            // FocusUntilDead 락 미러: 락 타겟만 fire 가능.
            if (behaviorLookup.HasComponent(attacker)
                && behaviorLookup[attacker].targetMode == EnemyTargetMode.FocusUntilDead
                && focusLookup.HasComponent(attacker))
            {
                Entity cur = focusLookup[attacker].current;
                bool curValid = cur != Entity.Null
                    && healthLookup.HasComponent(cur) && healthLookup[cur].value > 0f
                    && !deadLookup.HasComponent(cur);
                if (curValid)
                {
                    if (!transformLookup.HasComponent(cur)) return false;
                    int2 cCell = GridMath.WorldToCell(transformLookup[cur].Position, tileSize, gridSize, origin: ffOrigin);
                    int cDist = math.max(math.abs(cCell.x - atkCell.x), math.abs(cCell.y - atkCell.y));
                    return cDist <= tileRange;
                }
                // invalid 락 → nearest/filter 경로로 진행
            }

            bool hasFilter = filterLookup.HasComponent(attacker);
            int filterMask = hasFilter ? filterLookup[attacker].classMask : -1;

            for (int i = 0; i < candEntities.Length; i++)
            {
                if (((int)candFactions[i].value & mask) == 0) continue;
                if (candEntities[i] == attacker) continue;
                int cclass = classLookup.HasComponent(candEntities[i]) ? (int)classLookup[candEntities[i]].value : -1;
                if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue;
                int2 tgtCell = GridMath.WorldToCell(candTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                int tileDist = math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y));
                if (tileDist <= tileRange) return true;
            }
            return false;
        }
    }
}
