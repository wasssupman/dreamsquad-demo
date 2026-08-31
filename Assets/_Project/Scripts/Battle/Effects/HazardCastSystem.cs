using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    // attack-decoupling unit 4 — 캐스트 사건을 AttackSystem 이 **같은 프레임**에
    // 드레인하도록 순서를 명시한다. 둘 다 UpdateAfter(MovementSystem) 뿐이라
    // 상대 순서가 정렬기 tie-break 에 맡겨져 있었고, 시스템이 하나 추가되면
    // 뒤집혀 "가끔 한 프레임 늦게 나감"이 될 수 있었다.
    [UpdateBefore(typeof(Wassup.Battle.Combat.AttackSystem))]
    public partial struct HazardCastSystem : ISystem
    {
        // 캐스터가 연속 이동체인가 — 사거리 술어의 2차 게이트 인자.
        //
        // ⚠ **`OnUpdate` 안의 `var x = SystemAPI.GetComponentLookup<PathFollowState>(...)`
        // 형태로 쓰면 이 시스템에서 Burst NRE 가 난다**(실측 — `HazardCasterTests` 8건 중 6건이
        // 초기화 안 된 lookup 포인터로 죽었다). 바로 위 `simIdLookup` 은 **같은 형태인데 동작한다**
        // — 그러니 「저 형태가 여기서도 되겠지」로 되돌리지 말 것. 둘의 유일한 차이는 이 타입이
        // **이 시스템의 targetsQuery 에도 쓰인다**(`.WithAll<…, PathFollowState>()`)는 점이라
        // 그 조합을 의심하지만, **원인은 확정하지 않았다.** 확정된 것은 명시 필드 +
        // `Update(ref state)` 가 초록이라는 사실뿐이고, 그게 생성기에 안 기대는 형태다.
        private ComponentLookup<PathFollowState> _casterPathLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _casterPathLookup = state.GetComponentLookup<PathFollowState>(isReadOnly: true);
            state.RequireForUpdate<HazardCastState>();
            state.RequireForUpdate<FlowFieldSingleton>();
            state.RequireForUpdate<HazardSpawnRequestsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var flowField = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var spawnSingleton = SystemAPI.GetSingletonRW<HazardSpawnRequestsSingleton>();
            bool hasAttackVisualQueue = SystemAPI.TryGetSingletonRW<UnitAttackVisualEventsSingleton>(out var attackVisualSingleton);
            // attack-decoupling unit 4 — 캐스트 사건 채널(Effects→Combat). 큐가 아직
            // 없으면(구 세이브/테스트 월드) 조용히 건너뛴다.
            bool hasCastQueue = SystemAPI.TryGetSingletonRW<Wassup.Battle.Combat.CastEventsSingleton>(out var castSingleton);
            // skill-layer-migration unit 5a — 캐스트 seam 의 생산자.
            bool hasSkillQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Skills.SkillFiredEventsSingleton>(
                out var skillFiredRW);
            var dcSlotLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Combat.DcTriggerSlot>(isReadOnly: true);

            var targetsQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, LocalTransform, PathFollowState>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .Build();

            var targetEntities = targetsQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetsQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            var targetPathStates = targetsQuery.ToComponentDataArray<PathFollowState>(Allocator.Temp);
            // battle-sim-extraction M0 unit 1 — 최근접 선택의 동률 축. 이 시스템은 동률
            // tie-break 가 **아예 없어서** 같은 거리 두 적 중 «스냅샷에 먼저 든 쪽»이
            // 뽑혔고, 그 순서는 청크 배치(= 스폰/파괴 이력)가 정했다. 형제 타겟팅들과
            // 같은 축(낮은 SimEntityId = 먼저 스폰된 쪽)으로 맞춘다.
            var simIdLookup = SystemAPI.GetComponentLookup<SimEntityId>(isReadOnly: true);
            // 타겟 쪽은 조회하지 않는다: 아래 targetsQuery 가 `PathFollowState` 를 요구하므로
            // **정의상 전원 연속**이다 → `bothContinuous ≡ casterIsContinuous`.
            _casterPathLookup.Update(ref state);
            var targetSimIds = new NativeArray<int>(targetEntities.Length, Allocator.Temp);
            for (int i = 0; i < targetEntities.Length; i++)
                targetSimIds[i] = simIdLookup.HasComponent(targetEntities[i])
                    ? simIdLookup[targetEntities[i]].value
                    : SimEntityId.Unassigned;

            foreach (var (cast, transform, casterEntity) in
                     SystemAPI.Query<RefRW<HazardCastState>, RefRO<LocalTransform>>()
                         .WithAll<DefenderUnitTag>()
                         .WithNone<PendingDeployment>()
                         .WithNone<DeadTag>()
                         .WithEntityAccess())
            {
                if (cast.ValueRO.cooldownRemaining > 0f)
                    cast.ValueRW.cooldownRemaining = math.max(0f, cast.ValueRO.cooldownRemaining - dt);

                if (cast.ValueRO.kind == HazardCastKind.None || cast.ValueRO.dataIndex < 0)
                    continue;

                float3 casterPos = transform.ValueRO.Position;
                int2 casterCell = GridMath.WorldToCell(casterPos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                bool casterIsContinuous = _casterPathLookup.HasComponent(casterEntity);
                int tileRange = GridMath.RangeToTiles(cast.ValueRO.range);
                int mask = cast.ValueRO.targetMask;
                float bestSq = float.MaxValue;
                int bestSimId = SimEntityId.Unassigned;
                Entity bestTarget = Entity.Null;
                int2 bestTargetCell = default;

                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (targetEntities[i] == casterEntity) continue;
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (!PlacementLayers.CanTarget(
                            cast.ValueRO.targetTraversalLayers,
                            targetPathStates[i].traversalLayers)) continue;

                    float3 targetPos = targetTransforms[i].Position;
                    int2 targetCell = GridMath.WorldToCell(targetPos, flowField.tileSize, flowField.gridSize, origin: flowField.origin);
                    // distance-based-range unit 1 — 캐스트 사거리도 **같은 술어**를 지난다.
                    // 3번째 인자에 `false` 리터럴을 두지 않는 이유: 그건 「오늘 캐스터가 전부
                    // 타일 고정 방어유닛」이라는 **콘텐츠 사실**이지 이 코드의 성질이 아니다.
                    // 리터럴은 grep(「인라인 사거리 판정 0건」)에도 안 걸려 조용히 다른 자가 된다.
                    if (!AttackReach.InReach(casterCell, targetCell, tileRange,
                            casterPos, targetPos, flowField.tileSize, casterIsContinuous)) continue;

                    float distSq = math.distancesq(casterPos, targetPos);
                    if (distSq < bestSq || (distSq == bestSq && targetSimIds[i] < bestSimId))
                    {
                        bestSq = distSq;
                        bestSimId = targetSimIds[i];
                        bestTarget = targetEntities[i];
                        bestTargetCell = targetCell;
                    }
                }

                if (bestTarget == Entity.Null || cast.ValueRO.cooldownRemaining > 0f)
                    continue;

                if (hasAttackVisualQueue)
                {
                    float3 targetWorld = GridMath.CellToWorldCenter(bestTargetCell, flowField.tileSize, casterPos.y, origin: flowField.origin);
                    attackVisualSingleton.ValueRW.queue.Enqueue(new UnitAttackVisualEvent
                    {
                        attacker = casterEntity,
                        targetWorld = targetWorld,
                    });
                }

                // ⚠ **라우팅이 실행보다 앞이다**(skill-layer-migration unit 5a).
                // 이 시스템에 남는 일은 **언제·어디에**를 정하는 것뿐이다 — 그 판단이
                // 캐스터의 공격 사양(사거리·대상 마스크·통행 층·동률 축)과 얽혀 있어서
                // 스킬로 옮기면 그 사양을 복제하게 된다. 깔린 다음의 일은 전부 해저드
                // 저작이 소유하므로, 스킬이 할 일은 「저 칸에 이 에셋을」 하나다.
                bool routed = cast.ValueRO.skillId != Wassup.Skills.SkillRegistry.NotRouted;
                if (routed && hasSkillQ)
                {
                    skillFiredRW.ValueRW.queue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                    {
                        Seam = Wassup.Battle.Skills.SkillSeam.Cast,
                        Caster = casterEntity,
                        SkillId = cast.ValueRO.skillId,
                        FiredPosition = casterPos,
                        Target = bestTarget,
                        // 대상 **칸의 중심**이다 — 대상 좌표가 아니다. 장판은 칸에 깔린다.
                        TargetPosition = GridMath.CellToWorldCenter(
                            bestTargetCell, flowField.tileSize, casterPos.y, origin: flowField.origin),
                        HazardDataIndex = cast.ValueRO.dataIndex,
                        Selector = (int)cast.ValueRO.kind,
                        TargetTraversalLayers = cast.ValueRO.targetTraversalLayers,
                    });
                }
                else
                {
                spawnSingleton.ValueRW.queue.Enqueue(new HazardSpawnRequest
                {
                    kind = cast.ValueRO.kind,
                    dataIndex = cast.ValueRO.dataIndex,
                    centerCell = bestTargetCell,
                    width = 1,
                    height = 1,
                    caster = casterEntity,
                    target = bestTarget,
                    targetTraversalLayers = cast.ValueRO.targetTraversalLayers,
                });
                }

                // attack-decoupling unit 4 — 캐스트 성사 = 이 host 의 공격 사건.
                // 카운터(Combat 소유)를 여기서 쓰지 않고 큐로 넘긴다(spec 계약 7).
                // 생산자 게이트: 카드가 붙은 캐스터만 — 없으면 4초마다 이벤트가
                // 쌓이기만 한다. dcSlotLookup 은 Combat 컴포넌트 **읽기**라 경계 위반 아님.
                if (hasCastQueue && dcSlotLookup.HasBuffer(casterEntity))
                    castSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Combat.CastEvent
                    {
                        caster = casterEntity,
                        casterPos = casterPos,
                        targetTraversalLayers = cast.ValueRO.targetTraversalLayers,
                    });

                cast.ValueRW.cooldownRemaining = cast.ValueRO.cooldownDuration;
            }

            targetEntities.Dispose();
            targetTransforms.Dispose();
            targetFactions.Dispose();
            targetPathStates.Dispose();
            targetSimIds.Dispose();
        }
    }
}
