using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
        private EntityQuery _pastGoalQuery;
        private EntityQuery _deadQuery;
        private EntityQuery _defenderDeadQuery;

        public void OnCreate(ref SystemState state)
        {
            _singletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<GoalReachedEventsSingleton>());
            _defenderDeathSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<DefenderDeathEventsSingleton>());
            _hazardDestroyedSingletonQuery = state.GetEntityQuery(ComponentType.ReadWrite<HazardDestroyedEventsSingleton>());
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
            // 예외: 마음을 때릴 수 없는 적은 골에 붙어도 아무것도 못 하면서 "필드에 적 0기"
            // 웨이브 판정만 영구히 막는다. 그들만 기존대로 파괴하고, 마음 직격은 브리지가
            // 타워 버퍼로 넣는다(canSiege=false).
            // AttackState 는 Combat 소유지만 읽기만 한다(맥락 간 RO 읽기 허용 — 아래 DcTriggerSlot 선례).
            //
            // heart-stress-axis unit 7 — 판정을 **정밀화했다.** 예전엔 「AttackState 가 있나」
            // 였는데, 실제로 물어야 할 것은 「이 적이 **마음을** 공성할 수 있나」다.
            //
            // 돌격형(Runner·Swift)에게 일반 공격을 주면서 이 둘이 갈렸다. 그들의 마스크는
            // 21(방어유닛·방벽·본능)이라 **마음만 빠져 있다** — 방어유닛을 패고 도발도 걸리지만
            // 마음은 못 때린다. 예전 판정이면 마음 앞에 눌러앉아 영원히 아무것도 안 하면서
            // 「필드에 적 0기」 웨이브 판정만 막았을 것이다. 지금은 도달하면 산화한다(현행 유지).
            // ⚠ 기존 적 전원은 마스크에 DefenderCore 를 갖고 있어(28·29 둘 다 포함) 무회귀다.
            bool hasSink = _singletonQuery.CalculateEntityCount() == 1;
            var attackStateLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Combat.AttackState>(isReadOnly: true);
            foreach (var (_, transform, entity) in
                     SystemAPI.Query<RefRO<PastGoalTag>, RefRO<Unity.Transforms.LocalTransform>>()
                              .WithAll<AttackUnitTag>()
                              .WithNone<GoalReachedMarker>()
                              .WithEntityAccess())
            {
                bool canSiege = attackStateLookup.HasComponent(entity)
                    && (attackStateLookup[entity].targetMask & (int)Faction.DefenderCore) != 0;
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
            // skill-layer-migration unit 3d″ — 자기 죽음 seam 의 생산자.
            bool hasSkillQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Skills.SkillFiredEventsSingleton>(
                out var skillFiredSingleton);
            foreach (var (tile, entity) in
                     SystemAPI.Query<RefRO<DefenderTile>>()
                              .WithAll<DeadTag, DefenderUnitTag>()
                              .WithEntityAccess())
            {
                // ⚠ **라우팅이 레거시 스탬프보다 앞이다.** 뒤에 두면 이전한 카드가
                // 여전히 arm 을 타는데 arm 이 잘 돌아 그물이 전부 초록이 된다.
                //
                // ⚠ **여기가 자기 죽음의 유일한 합류점이다.** 피해·치명 타이머·순찰 수명이
                // 전부 `DeadTag` 로 모여 여기서 파괴된다. 앞당겨 잡으면 경로 하나만 보게 된다.
                //
                // ⚠ 아래 seam(`SkillDispatchLifecycleSystem`)은 이 시스템 **뒤**라 드레인
                // 시점엔 이 엔티티가 없다. 그래서 자리·층을 **지금** 싣는다.
                if (hasSkillQ && dcSlotLookup.HasBuffer(entity))
                {
                    var routeSlots = dcSlotLookup[entity];
                    int firedMask = 0;
                    for (int s = 0; s < routeSlots.Length; s++)
                    {
                        var rs = routeSlots[s];
                        if (rs.trigger != DcTriggerKind.OnDeath) continue;
                        if (rs.skillId == Wassup.Skills.SkillRegistry.LegacyArmId) continue;
                        // 같은 스킬은 죽음당 한 번만(레거시도 첫 매칭만 스탬프했다).
                        if (rs.skillId >= 0 && rs.skillId < 32)
                        {
                            int bit = 1 << rs.skillId;
                            if ((firedMask & bit) != 0) continue;
                            firedMask |= bit;
                        }
                        var deathPos = SystemAPI.HasComponent<Unity.Transforms.LocalTransform>(entity)
                            ? SystemAPI.GetComponent<Unity.Transforms.LocalTransform>(entity).Position
                            : float3.zero;
                        skillFiredSingleton.ValueRW.queue.Enqueue(
                            new Wassup.Battle.Skills.SkillFiredEvent
                        {
                            Seam = Wassup.Battle.Skills.SkillSeam.Lifecycle,   // 이 드레인 지점이 실행한다
                            Caster = entity,          // 드레인 때는 이미 파괴된 핸들이다
                            SkillId = rs.skillId,
                            SlotIndex = s,
                            FiredPosition = deathPos,
                            Target = Entity.Null,
                            TargetPosition = deathPos,   // 내가 쓰러진 자리
                            Magnitude = rs.magnitude,
                            Duration = rs.duration,
                            TileRange = rs.tileRange,
                            Period = rs.period,
                            DataIndex = rs.projectileDataIndex,
                            Selector = (int)rs.ccKind,
                            StatSelector = (int)rs.buffStat,
                            StackSelector = (int)rs.stackKind,
                            ProjectileMovement = (int)rs.projectileMovement,
                            ProjectilePayload = (int)rs.projectilePayload,
                            HazardDataIndex = rs.hazardDataIndex,
                            PatternIndex = rs.patternIndex,
                            Speed = rs.speed,
                            HitThreshold = rs.hitThreshold,
                            SlamDamage = rs.slamDamage,
                            SlamTileRange = rs.slamTileRange,
                            StackId = rs.statBuffStackId,
                            // ⚠ **저작을 읽는다**(2026-08-26 사용자 결정) — 시체폭발과 같은 탄이다.
                            VisualScale = rs.visualScale,
                            // ⚠ 방어유닛의 작별 선물이다 — 레거시는 층을 안 실었다(무제한).
                            TargetTraversalLayers = 0,
                        });
                    }
                }

                if (hasDefenderSink)
                {
                    // skill-layer-migration unit 3g — **OnDeath 폭발 스탬프는 은퇴했다.**
                    // 작별 선물이 concrete 로 갔고 자기 죽음 seam 이 실행한다. 이 이벤트에
                    // 남은 것은 「어느 칸이 비었나」뿐이다(타일 반납·시너지 재계산·연출).
                    var evt = new DefenderDeathEvent { cell = tile.ValueRO.cell };
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

            // battle-structures unit 0 — goal-stability 의 «골 사망» 루프를 제거했다.
            // GoalPoint 엔티티는 어떤 맵에서도 스폰되지 않아 이 루프는 한 번도 발화한 적이
            // 없고(라이브 골 타워는 아래 일반 루프에서 파괴된다), 그 결과
            // GoalCollapsedEventsSingleton 은 생산자 없는 채널이었다. 채널 타입과 Bridge
            // 소비 측은 그대로 둔다 — 거점 단위 붕괴를 짓는 unit 4 가 페이로드를 새로 정한다.

            // General dead loop: attackers + any defender that somehow lacks
            // DefenderTile (should not happen in Phase 4, but keeps the system
            // safe). WithNone<DefenderTile> prevents double-destroy of the
            // defender-dead loop above, and WithNone<BlockingHazard> prevents
            // double-destroy after hazard event enqueue.
            // battle-structures unit 0 — WithNone<GoalPoint> 는 제거했다: 짝이던 골 사망
            // 루프가 사라져 이중 파괴 위험이 없고, 라이브 골 타워는 원래부터 이 루프가 파괴한다.
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<DeadTag>>()
                              .WithNone<DefenderTile>()
                              .WithNone<BlockingHazard>()
                              .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
