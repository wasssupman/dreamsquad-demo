using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Data;

namespace Wassup.Battle.Units
{
    // Owns entity lifecycle for units. Destroys units carrying DeadTag, and PastGoalTag units
    // or DeadTag (health dropped to zero). Emits GoalReachedEvent when an attack unit reaches the goal.
    // goal-tower-siege unit 1 — 골 도달 적은 더 이상 파괴되지 않는다(공격 수단 없는 돌격형만 파괴).
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    public partial struct UnitLifecycleSystem : ISystem
    {
        private EntityQuery _singletonQuery;
        private EntityQuery _defenderDeathSingletonQuery;
        private EntityQuery _hazardDestroyedSingletonQuery;
        private EntityQuery _goalCollapsedSingletonQuery;
        private EntityQuery _pastGoalQuery;
        private EntityQuery _deadQuery;
        private EntityQuery _defenderDeadQuery;

        public void OnCreate(ref SystemState state)
        {
            _singletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            _defenderDeathSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<DefenderDeathEventsSingleton>());
            _hazardDestroyedSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<HazardDestroyedEventsSingleton>());
            _goalCollapsedSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<GoalCollapsedEventsSingleton>());
            _pastGoalQuery = state.GetEntityQuery(ComponentType.ReadOnly<PastGoalTag>(), ComponentType.ReadOnly<AttackUnitTag>());
            _deadQuery = state.GetEntityQuery(ComponentType.ReadOnly<DeadTag>());
            _defenderDeadQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<DeadTag>(),
                ComponentType.ReadOnly<DefenderUnitTag>(),
                ComponentType.ReadOnly<DefenderTile>());
            // RequireAnyForUpdate takes a params array and isn't Burst-friendly in OnCreate;
            // keep this method non-Burst. OnUpdate remains [BurstCompile].
            state.RequireAnyForUpdate(_pastGoalQuery, _deadQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Goal-reached: attack units that walked past the last waypoint.
            // Emit GoalReachedEvent when the sink singleton is present (fail-open otherwise).
            //
            // goal-tower-siege unit 1 — **도달은 더 이상 죽음이 아니다.** 공격 수단이 있는 적은
            // 골에 남아 타워를 때린다(파괴하지 않는다). 마커로 발화를 1회로 고정하고, 쿼리의
            // WithNone 으로 걸러 공성 인구를 매 프레임 순회하지 않는다.
            //
            // 예외: AttackState 가 없는 적(Runner·Swift 같은 attackMethod None 돌격형)은 골에
            // 붙어도 아무것도 못 하면서 "필드에 적 0기" 웨이브 판정만 영구히 막는다. 그들만
            // 기존대로 파괴하고, 안정도 피해는 브리지가 타워 버퍼로 넣는다(canSiege=false).
            // AttackState 는 Combat 소유지만 읽기만 한다(맥락 간 RO 읽기 허용 — 아래 DcTriggerSlot 선례).
            bool hasSink = _singletonQuery.CalculateEntityCount() == 1;
            var attackStateLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.AttackState>(isReadOnly: true);
            foreach (var (_, transform, entity) in
                     SystemAPI.Query<RefRO<PastGoalTag>, RefRO<Unity.Transforms.LocalTransform>>()
                              .WithAll<AttackUnitTag>()
                              .WithNone<GoalReachedMarker>()
                              .WithEntityAccess())
            {
                bool canSiege = attackStateLookup.HasComponent(entity);
                if (hasSink)
                {
                    var singleton = _singletonQuery.GetSingletonRW<GoalReachedEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(new GoalReachedEvent
                    {
                        entity = entity,
                        canSiege = canSiege,
                        position = transform.ValueRO.Position,
                    });
                }
                // 싱크가 없으면 **마커도 붙이지 않는다.** 마커는 쿼리에서 빼는 필터라, 이벤트
                // 없이 붙이면 그 적은 두 번 다시 평가되지 않는다 — 스트레스도 안 오르고
                // AttackUnitTag 는 유지돼 웨이브 전멸 판정을 그 판 내내 막는 유령이 된다.
                // (원저자의 "fail-open otherwise" 를 fail-closed 로 뒤집지 않기 위한 가드)
                if (!hasSink)
                {
                    if (!canSiege) ecb.DestroyEntity(entity);
                    continue;
                }
                if (canSiege) ecb.AddComponent<GoalReachedMarker>(entity);
                else ecb.DestroyEntity(entity);
            }

            // Defender deaths: emit DefenderDeathEvent (carrying tile) then destroy.
            // Enqueue happens before DestroyEntity so BattleBridge sees the tile
            // coordinate before the entity is gone.
            bool hasDefenderSink = _defenderDeathSingletonQuery.CalculateEntityCount() == 1;
            // content-1 ② — read the dying defender's OnDeath×SelfTileAoe slot (RO,
            // cross-context read of a Combat buffer is allowed) and bake it into the
            // event BEFORE ecb destroys the entity.
            var dcSlotLookup = SystemAPI.GetBufferLookup<DcTriggerSlot>(isReadOnly: true);
            foreach (var (tile, entity) in
                     SystemAPI.Query<RefRO<DefenderTile>>()
                              .WithAll<DeadTag, DefenderUnitTag>()
                              .WithEntityAccess())
            {
                if (hasDefenderSink)
                {
                    var evt = new DefenderDeathEvent { cell = tile.ValueRO.cell };
                    if (dcSlotLookup.HasBuffer(entity))
                    {
                        var slots = dcSlotLookup[entity];
                        for (int s = 0; s < slots.Length; s++)
                        {
                            if (slots[s].trigger == DcTriggerKind.OnDeath &&
                                slots[s].payload == DcPayloadKind.SelfTileAoe)
                            {
                                evt.hasOnDeathAoe = true;
                                evt.aoeDamage = slots[s].magnitude;
                                evt.aoeTileRange = slots[s].tileRange;
                                evt.aoeDataIndex = slots[s].projectileDataIndex;
                                break; // first OnDeath slot only (v1)
                            }
                        }
                    }
                    var singleton = _defenderDeathSingletonQuery.GetSingletonRW<DefenderDeathEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(evt);
                }
                ecb.DestroyEntity(entity);
            }

            bool hasHazardSink = _hazardDestroyedSingletonQuery.CalculateEntityCount() == 1;
            foreach (var (hazard, obstacle, transform, entity) in
                     SystemAPI.Query<RefRO<BlockingHazard>, RefRO<Obstacle>, RefRO<LocalTransform>>()
                              .WithAll<DeadTag>()
                              .WithEntityAccess())
            {
                if (hasHazardSink)
                {
                    var singleton = _hazardDestroyedSingletonQuery.GetSingletonRW<HazardDestroyedEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(new HazardDestroyedEvent
                    {
                        hazardEntity = entity,
                        hazardSoIndex = hazard.ValueRO.hazardSoIndex,
                        worldPosition = transform.ValueRO.Position,
                        centerCell = obstacle.ValueRO.cell,
                    });
                }
                ecb.DestroyEntity(entity);
            }

            // goal-stability unit 4 — 골 붕괴: 이벤트 enqueue 후 destroy (hazard-dead 동형,
            // enqueue 는 반드시 destroy 앞 — Bridge 가 cell/goalIndex 를 엔티티 소멸 전에 본다).
            // 유출 전환은 이 이벤트와 무관 — 엔티티 부재로 공성 게이트가 이미 열린다.
            bool hasGoalSink = _goalCollapsedSingletonQuery.CalculateEntityCount() == 1;
            foreach (var (goalPoint, transform, entity) in
                     SystemAPI.Query<RefRO<GoalPoint>, RefRO<LocalTransform>>()
                              .WithAll<DeadTag>()
                              .WithEntityAccess())
            {
                if (hasGoalSink)
                {
                    var singleton = _goalCollapsedSingletonQuery.GetSingletonRW<GoalCollapsedEventsSingleton>();
                    singleton.ValueRW.queue.Enqueue(new GoalCollapsedEvent
                    {
                        entity = entity,
                        cell = goalPoint.ValueRO.cell,
                        goalIndex = goalPoint.ValueRO.goalIndex,
                        worldPosition = transform.ValueRO.Position,
                    });
                }
                ecb.DestroyEntity(entity);
            }

            // General dead loop: attackers + any defender that somehow lacks
            // DefenderTile (should not happen in Phase 4, but keeps the system
            // safe). WithNone<DefenderTile> prevents double-destroy of the
            // defender-dead loop above, and WithNone<BlockingHazard> prevents
            // double-destroy after hazard event enqueue. WithNone<GoalPoint> —
            // goal-dead 루프(위)와의 이중 파괴/이벤트 유실 방지 (goal-stability 리뷰 M1).
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<DeadTag>>()
                              .WithNone<DefenderTile>()
                              .WithNone<BlockingHazard>()
                              .WithNone<GoalPoint>()
                              .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
