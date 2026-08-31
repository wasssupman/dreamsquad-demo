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
        // distance-based-range unit 4a — 대상의 몸 반경(타일). 없으면 0 = 점.
        //
        // ⚠ **`OnUpdate` 안의 `SystemAPI.GetComponentLookup` 지역 변수로 쓰지 말 것.**
        // 그렇게 했다가 이 시스템의 EditMode 전반이
        // `ObjectDisposedException: EntityTypeHandle ... invalidated by a structural change`
        // 로 무너졌다(실측). 같은 파일의 다른 lookup 들이 그 형태로 멀쩡히 도는 것이 함정이다.
        // 명시 필드 + `OnCreate` + `Update(ref state)` 가 Entities 정본 형태이고, 소스 생성기의
        // 핸들 갱신 순서에 기대지 않는다.
        private ComponentLookup<Wassup.Battle.Units.HitRadius> _bodyRadiusLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _bodyRadiusLookup = state.GetComponentLookup<Wassup.Battle.Units.HitRadius>(isReadOnly: true);
            state.RequireForUpdate<EnemyAiState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bodyRadiusLookup.Update(ref state);
            bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
            float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;

            // 타겟(디펜더) 후보 스냅샷 — AttackSystem 과 동일 후보 풀.
            var candQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment, DeadTag>()
                // heart-stress-axis unit 6 — AttackSystem 후보 쿼리의 미러. 한쪽에만 넣으면
                // AI 상태(Engaging/Marching)와 실제 사격 대상이 갈린다.
                //
                // ⚠ 이 미러는 **완전하지 않다**(ECS 리뷰 2026-08-24 발견, 선재 결함):
                // AttackSystem 은 `.WithNone<UltimateLeapState>()` 도 거는데 여기엔 없다.
                // 그래서 이탈(판 밖) 중인 보스를 이쪽은 유효 타겟으로 보고 Engaging 을 주는데
                // AttackSystem 은 건너뛰어 **멈춰 서서 안 쏘는** 적이 생긴다.
                // 이 spec 범위 밖이라 고치지 않고 기록만 한다 — README 후속 후보 참조.
                .WithNone<CoreShielded>()
                .Build();
            var candEntities = candQuery.ToEntityArray(Allocator.Temp);
            var candTransforms = candQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var candFactions = candQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            var candPathLookup = SystemAPI.GetComponentLookup<PathFollowState>(true);
            var candTraversalLayers = new NativeArray<byte>(candEntities.Length, Allocator.Temp);
            for (int i = 0; i < candEntities.Length; i++)
                if (candPathLookup.HasComponent(candEntities[i]))
                    candTraversalLayers[i] = candPathLookup[candEntities[i]].traversalLayers;

            var attackLookup = SystemAPI.GetComponentLookup<AttackState>(true);
            var aggroLookup = SystemAPI.GetComponentLookup<Aggroed>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var classLookup = SystemAPI.GetComponentLookup<DefenderClassTag>(true);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(true);
            var focusLookup = SystemAPI.GetComponentLookup<FocusTarget>(true);
            var filterLookup = SystemAPI.GetComponentLookup<EnemyTargetFilter>(true);
            var behaviorLookup = SystemAPI.GetComponentLookup<EnemyBehavior>(true);

            foreach (var (aiState, transform, enemyEntity) in
                     SystemAPI.Query<RefRW<EnemyAiState>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float3 atkPos = transform.ValueRO.Position;
                int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);
                bool hasAttack = attackLookup.HasComponent(enemyEntity);
                int tileRange = hasAttack ? GridMath.RangeToTiles(attackLookup[enemyEntity].range) : 0;
                int mask = hasAttack ? attackLookup[enemyEntity].targetMask : 0;
                byte targetTraversalLayers = hasAttack
                    ? attackLookup[enemyEntity].targetTraversalLayers
                    : (byte)0;

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
                            float3 gPos = transformLookup[g].Position;
                            int2 gCell = GridMath.WorldToCell(gPos, tileSize, gridSize, origin: ffOrigin);
                            // distance-based-range unit 1 — 「멈춰도 되나」도 **같은 술어**를 지난다.
                            // 셀만 보면 자를 바꾸는 순간 이 한 곳만 옛 답을 내고, 그게 정확히
                            // 「멈추는 근거」와 「쏘는 근거」가 갈리는 교착이다(AttackReach 헤더).
                            guardianInRange = AttackReach.InReach(atkPos, gPos, tileRange, tileSize, BodyRadiusOf(g, _bodyRadiusLookup));
                        }
                    }
                }
                else if (hasAttack)
                {
                    hasFireTarget = HasFireTarget(enemyEntity, atkCell, atkPos, tileRange, mask,
                        targetTraversalLayers,
                        candEntities, candTransforms, candFactions, candTraversalLayers,
                        tileSize, gridSize, ffOrigin,
                        classLookup, transformLookup, healthLookup, deadLookup, focusLookup, filterLookup, behaviorLookup,
                        candPathLookup, _bodyRadiusLookup);
                }

                aiState.ValueRW.value = Evaluate(aggroed, guardianInRange, hasFireTarget);
            }

            candEntities.Dispose();
            candTransforms.Dispose();
            candFactions.Dispose();
            candTraversalLayers.Dispose();
        }

        // 순수 전이 함수. aggro 우선, 비-aggro 는 "AttackSystem 이 fire 할 타겟 존재" 로 Engaging/Marching.
        public static AiState Evaluate(bool aggroed, bool guardianInRange, bool hasFireTarget)
        {
            if (aggroed) return guardianInRange ? AiState.Standoff : AiState.Chasing;
            return hasFireTarget ? AiState.Engaging : AiState.Marching;
        }

        // AttackSystem fire 조건 미러. 타겟 **선정** 로직은 여전히 손으로 맞춰야 하지만,
        // 락 **유지** 판정만은 target-persistence unit 1 이 TargetPersistence.KeepsLock 으로
        // 단일화했다 — 그 축의 드리프트는 이제 구조로 막힌다.
        // FocusUntilDead 락은 대상이 살아 있고 사거리 안일 때만 유지되며, 유지 중에는 그
        // 대상만 fire 가능하다(그때만 Engaging).
        // 대상의 몸 반경(타일). 컴포넌트가 없으면 0 = 점(오늘의 저작 전부).
        static float BodyRadiusOf(Entity e, in ComponentLookup<Wassup.Battle.Units.HitRadius> l)
            => l.HasComponent(e) ? l[e].value : 0f;

        static bool HasFireTarget(
            Entity attacker, int2 atkCell, float3 atkPos, int tileRange, int mask,
            byte attackTargetLayers,
            in NativeArray<Entity> candEntities,
            in NativeArray<LocalTransform> candTransforms,
            in NativeArray<FactionTag> candFactions,
            in NativeArray<byte> candTraversalLayers,
            float tileSize, int2 gridSize, float3 ffOrigin,
            in ComponentLookup<DefenderClassTag> classLookup,
            in ComponentLookup<LocalTransform> transformLookup,
            in ComponentLookup<Health> healthLookup,
            in ComponentLookup<DeadTag> deadLookup,
            in ComponentLookup<FocusTarget> focusLookup,
            in ComponentLookup<EnemyTargetFilter> filterLookup,
            in ComponentLookup<EnemyBehavior> behaviorLookup,
            in ComponentLookup<PathFollowState> pathLookup,
            in ComponentLookup<Wassup.Battle.Units.HitRadius> bodyRadiusLookup)
        {
            // 락 미러: 락 타겟만 fire 가능.
            // target-persistence unit 3 — 게이트가 `!= None` 이다(구 `== FocusUntilDead`).
            // **AttackSystem 의 락 블록 게이트와 항상 같아야 한다**(계약 4) — 갈리면
            // "락은 있는데 FSM 은 Marching" 데드락이 재발한다. 그게 B2 의 절반이었다.
            // CC 중 비움(D5)은 여기서 하지 않는다: focus 의 writer 는 AttackSystem 단독이고
            // 이 미러는 읽기 전용이다. 비워진 값이 그대로 보여 nearest 경로로 흐른다.
            if (behaviorLookup.HasComponent(attacker)
                && behaviorLookup[attacker].targetMode != EnemyTargetMode.None
                && focusLookup.HasComponent(attacker))
            {
                Entity cur = focusLookup[attacker].current;
                bool curStillCandidate = false;
                for (int i = 0; i < candEntities.Length; i++)
                {
                    if (candEntities[i] != cur) continue;
                    curStillCandidate = ((int)candFactions[i].value & mask) != 0
                        && PlacementLayers.CanTarget(
                            attackTargetLayers, candTraversalLayers[i]);
                    break;
                }
                bool curValid = cur != Entity.Null
                    && curStillCandidate
                    && healthLookup.HasComponent(cur) && healthLookup[cur].value > 0f
                    && !deadLookup.HasComponent(cur);
                if (curValid && transformLookup.HasComponent(cur))
                {
                    float3 curPos = transformLookup[cur].Position;
                    int2 cCell = GridMath.WorldToCell(curPos, tileSize, gridSize, origin: ffOrigin);
                    int cDist = GridMath.ChebyshevDistance(atkCell, cCell);   // unit 1 수렴
                    // 정지 판정은 공격 판정과 **같은 술어**여야 한다(AttackReach 주석 — 갈리면 교착).
                    bool curReach = AttackReach.InReach(atkPos, curPos, tileRange, tileSize, BodyRadiusOf(cur, bodyRadiusLookup));
                    // target-persistence unit 1·2 — 유지 판정은 AttackSystem 과 **같은 함수**다.
                    if (curReach && TargetPersistence.KeepsLock(true, cDist, tileRange)) return true;
                    // 사거리 이탈 → 락 해제(D2). 예전엔 여기서 false 를 반환해 Marching 이 됐고,
                    // 그게 "옆에 방어유닛을 두고 골로 걸어가는" B2 의 절반이었다.
                }
                // 락을 놓았거나(사망·이탈) 애초에 무효 → nearest/filter 경로로 진행
            }

            bool hasFilter = filterLookup.HasComponent(attacker);
            int filterMask = hasFilter ? filterLookup[attacker].classMask : -1;

            for (int i = 0; i < candEntities.Length; i++)
            {
                if (((int)candFactions[i].value & mask) == 0) continue;
                if (!PlacementLayers.CanTarget(
                        attackTargetLayers, candTraversalLayers[i])) continue;
                if (candEntities[i] == attacker) continue;
                int cclass = classLookup.HasComponent(candEntities[i]) ? (int)classLookup[candEntities[i]].value : -1;
                if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue;
                float3 tgtPos = candTransforms[i].Position;
                int2 tgtCell = GridMath.WorldToCell(tgtPos, tileSize, gridSize, origin: ffOrigin);
                // 같은 술어(AttackReach) — AttackSystem·PatrolAreaMath 와 한 몸이어야 한다.
                if (AttackReach.InReach(atkPos, tgtPos, tileRange, tileSize, BodyRadiusOf(candEntities[i], bodyRadiusLookup)))
                    return true;
            }
            return false;
        }
    }
}
