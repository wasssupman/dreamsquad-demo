using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Battle.Combat
{
    // Unified attacker loop — any entity with AttackState + LocalTransform (and no
    // PendingDeployment) participates as an attacker. Defender-specific behaviours
    // (attack-event enqueue, buff scaling, projectile, knockback CC) branch on
    // ComponentLookup.HasComponent so no attacker-tag filtering is needed in the query.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct AttackSystem : ISystem
    {
        private EntityQuery _attackEventsQuery;
        private EntityQuery _ccEventsQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackState>();
            _attackEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<UnitAttackVisualEventsSingleton>());
            _ccEventsQuery = state.GetEntityQuery(ComponentType.ReadWrite<Wassup.Battle.Effects.EnemyCcEventsSingleton>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Snapshot attackable targets into native arrays so the unified attacker
            // loop uses the same candidate pool and filters by AttackState.targetMask.
            // ultimate-leap unit 2 — 이탈(판 밖) 중인 유닛은 타겟 후보에서 빠진다. 화면 밖에 있는
            // 보스를 겨누면 방어유닛들이 빈 타일에 사격하고 데미지 숫자가 허공에 뜬다.
            // (LeapFlight 는 여기 **없다** — 일반 도약은 비행 중에도 계속 맞는다.)
            var targetCandidatesQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .WithNone<UltimateLeapState>()
                .Build();
            var targetEntities = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            // 니들 폴백 선정용 scratch — 예전엔 발동마다 할당/해제했다. 후보 수는
            // 스냅샷 길이로 고정이라 프레임당 1회면 충분하다.
            var needleScratch = new NativeArray<NearestTargeting.Candidate>(
                targetEntities.Length, Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var projectileRefLookup = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);
            var defenderCcLookup = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
            // boss-jjangssen unit 3 — 넉업만 여기서 게이트한다. 넉업은 CC(Stun)와 연출 신호를
            // 한 쌍으로 내보내는데, 연출은 CcApplySystem 을 거치지 않으므로 부여 거절로는 못 막는다.
            // CC 만 막으면 **보스가 떠오르는데 스턴은 안 걸리는** desync 가 된다
            // (KnockupVisualEvent 계약: durationSec == 스턴 시간이어야 착지와 해제가 맞는다).
            var bossLookup = SystemAPI.GetComponentLookup<BossTag>(isReadOnly: true);
            var defenderTagLookup = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
            // defender-directional-volley unit 3 — 배치 시 확정된 영구 공격 방향(Units
            // 소유, 읽기 전용). 보유 유닛은 최근접 타겟 선택 대신 방향 레인 게이트로 발사.
            var facingLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.DeployedFacing>(isReadOnly: true);
            // projectile-shot-sequence unit 2 — 방향 pattern 원본과 진행 인스턴스.
            // 둘 다 Combat 소유이며 Bridge가 스폰 시 사전 부착한다.
            var patternSlotLookup = SystemAPI.GetBufferLookup<PatternSlot>(isReadOnly: false);
            var emitterInstanceLookup = SystemAPI.GetBufferLookup<EmitterInstance>(isReadOnly: false);
            // bomb-thrower-defender unit 4 — 폭탄맨 발사 상태(RW: rng advance). volleyLookup 선례.
            var bombLauncherLookup = SystemAPI.GetComponentLookup<BombLauncherState>(isReadOnly: false);
            var blockingHazardCellsLookup = SystemAPI.GetBufferLookup<BlockingHazardCellsBuffer>(isReadOnly: true);
            var modifierStatsLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Effects.ModifierStats>(isReadOnly: true);
            var outputBufferLookup = SystemAPI.GetBufferLookup<AttackOutputElement>(isReadOnly: true);
            // aggro-targeting Unit 4 — enemy class filter + priority targeting.
            var defenderClassLookup = SystemAPI.GetComponentLookup<DefenderClassTag>(isReadOnly: true);
            var enemyFilterLookup = SystemAPI.GetComponentLookup<EnemyTargetFilter>(isReadOnly: true);
            // aggro-targeting Unit 5 — aggroed enemy sticky-targets its guardian.
            var aggroLookup = SystemAPI.GetComponentLookup<Aggroed>(isReadOnly: true);
            // aggro-targeting Unit 11 — guardian(AggroCapacity) aggro-aware targeting + hit emit.
            var aggroCapacityLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Effects.AggroCapacity>(isReadOnly: true);
            var aggroTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            // enemy-behavior-components Unit 3 — targetMode + FocusUntilDead lock.
            var behaviorLookup = SystemAPI.GetComponentLookup<EnemyBehavior>(isReadOnly: true);
            // enemy-ai-fsm 3a — fire 게이트용 상태 RO.
            var aiStateLookup = SystemAPI.GetComponentLookup<EnemyAiState>(isReadOnly: true);
            var focusLookup = SystemAPI.GetComponentLookup<FocusTarget>(isReadOnly: false);
            // dreamcatcher-unit-trigger unit 2 — triggered card slots (counter RW).
            var dcSlotLookup = SystemAPI.GetBufferLookup<DcTriggerSlot>(isReadOnly: false);
            // attack-mod-bounce unit 3 — always-on attack mods aggregated onto the base homing shot.
            var dcAttackModLookup = SystemAPI.GetBufferLookup<DcAttackModSlot>(isReadOnly: true);
            // content-1 ① (가시 갑옷) — double-fire charge (Units→Combat handoff), read+cleared here.
            var nextDoubleFireLookup = SystemAPI.GetComponentLookup<NextAttackDoubleFire>(isReadOnly: true);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: true);
            var deadLookup = SystemAPI.GetComponentLookup<DeadTag>(isReadOnly: true);
            // combat-action-lock — 행동불가(Sleep/Stun) 게이트용 RO CcEffect lookup.
            var ccActionLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Effects.CcEffect>(isReadOnly: true);
            // dreamcatcher-content-2 끝을 보는 눈 — per-attack frontmost lock (RW, defender-owned)
            // + PastGoal exclusion (goal-reached enemies are leak-pending, not valid frontmost).
            var frontmostLockLookup = SystemAPI.GetComponentLookup<FrontmostAttackLock>(isReadOnly: false);
            var pastGoalLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Movement.PastGoalTag>(isReadOnly: true);

            bool hasStatQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.StatModifierApplyEventsSingleton>(out var statModSingleton);
            bool hasStackQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.StackModifierApplyEventsSingleton>(out var stackModSingleton);
            bool hasFlowField = SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var flowField);
            // 그리드 폴백은 프레임 불변이라 한 번만 푼다. 예전엔 폭탄 분기·attacker
            // 루프·캐스트 드레인이 각자 삼항식을 반복했다(캐스트 쪽은 후보 루프 **안**).
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
            float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;

            // Attack-output log channel — enqueue one event per output-per-target fired.
            NativeQueue<AttackOutputLogEvent>.ParallelWriter? attackOutputLogWriter = null;
            if (SystemAPI.TryGetSingletonRW<AttackOutputLogEventsSingleton>(out var attackOutputLogSingleton))
                attackOutputLogWriter = attackOutputLogSingleton.ValueRW.queue.AsParallelWriter();

            // aggro-targeting Unit 11 — 가디언 명중 → Effects 로 넘길 히트 채널 writer.
            NativeQueue<Wassup.Battle.Effects.AggroHitEvent>.ParallelWriter? aggroHitWriter = null;
            if (SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.AggroHitEventsSingleton>(out var aggroHitSingleton))
                aggroHitWriter = aggroHitSingleton.ValueRW.queue.AsParallelWriter();

            // nightmare-catcher unit 1 — 보스 위협 귀속 채널 + 게이트 lookup.
            // enqueue 는 피격자가 ThreatEntry 버퍼 보유(보스 베이크) && 공격자가
            // defender 일 때만 — defender 피격/일반 적 경로 무영향(회귀 격리).
            // 직접 큐 핸들 사용은 statModSingleton 선례(메인스레드 foreach).
            bool hasThreatQ = SystemAPI.TryGetSingletonRW<ThreatHitEventsSingleton>(out var threatHitSingleton);
            NativeQueue<ThreatHitEvent> threatQueue = hasThreatQ ? threatHitSingleton.ValueRW.queue : default;
            var threatLookup = SystemAPI.GetBufferLookup<ThreatEntry>(isReadOnly: true);

            // Hoist attack-event singleton writer — every attacker (defender or
            // enemy) enqueues one event per fire so SpineUnitPool can trigger the
            // attack animation and facing flip uniformly. Defender-specific
            // side effects (cast/attack VFX prefabs) are filtered downstream in
            // BattleBridge by looking up DefenderUnitData for the attacker.
            NativeQueue<UnitAttackVisualEvent>.ParallelWriter? attackWriter = null;
            if (!_attackEventsQuery.IsEmpty)
            {
                var singleton = _attackEventsQuery.GetSingletonRW<UnitAttackVisualEventsSingleton>();
                attackWriter = singleton.ValueRW.queue.AsParallelWriter();
            }

            NativeQueue<Wassup.Battle.Effects.EnemyCcEvent>.ParallelWriter? ccWriter = null;
            if (!_ccEventsQuery.IsEmpty)
            {
                var ccSingleton = _ccEventsQuery.GetSingletonRW<Wassup.Battle.Effects.EnemyCcEventsSingleton>();
                ccWriter = ccSingleton.ValueRW.queue.AsParallelWriter();
            }

            // knockup-fighter-defender unit 3 — 넉업 띄우기 연출 신호(Combat→Bridge).
            NativeQueue<KnockupVisualEvent>.ParallelWriter? knockupVisualWriter = null;
            if (SystemAPI.TryGetSingletonRW<KnockupVisualEventsSingleton>(out var knockupVisualEvents))
                knockupVisualWriter = knockupVisualEvents.ValueRW.queue.AsParallelWriter();

            // use-flow unit 3 — 부착 카드 발동 신호(Combat→Bridge). 발동 = 카운터 소비 성사
            // 프레임이며 payload arm/대상 유무와 무관하게 신호한다(카운트 소비가 곧 사건).
            NativeQueue<DcTriggerFiredEvent>.ParallelWriter? dcFiredWriter = null;
            if (SystemAPI.TryGetSingletonRW<DcTriggerFiredEventsSingleton>(out var dcFiredEvents))
                dcFiredWriter = dcFiredEvents.ValueRW.queue.AsParallelWriter();

            // ── attack-decoupling unit 4 — 캐스트 사건 드레인 (Effects→Combat) ──
            // attacker foreach **앞**에서 처리한다: ① 후보 스냅샷/ecb 를 그대로
            // 재사용하고 ② 카운터 변경이 루프 바깥에서 끝나 HeavyStrike pre-scan
            // 합성 불변식(spec 계약 1)에 영향이 없다. 신규 시스템 0.
            // HazardCastSystem 이 [UpdateBefore(AttackSystem)] 이라 같은 프레임 소비.
            // 계약 2(host 당 사건 지점 1개)를 **코드로** 보장한다. 이전에는 캐스터의
            // attackRange 가 0 인 덕에 RESOLVE 에 못 가는 것으로 우연히 성립했는데,
            // 유닛 스탯 시트가 캐스터 attackRange 를 3 으로 확정하면서 그 우연이 깨졌다
            // (캐스트 + RESOLVE 로 2카운트 → AttackN 카드 발동 주기가 절반). 시트가 정본이므로
            // 데이터 모양에 의존하지 않도록 여기서 막는다: 이번 프레임에 캐스트로 카운트한
            // host 는 아래 RESOLVE 카운팅 블록을 건너뛴다. 피해·다른 arm 은 영향 없다.
            var castCountedHosts = new NativeHashSet<Entity>(8, Allocator.Temp);
            if (SystemAPI.TryGetSingletonRW<CastEventsSingleton>(out var castEvents))
            {
                var castQueue = castEvents.ValueRW.queue;
                while (castQueue.TryDequeue(out var castEvt))
                {
                    // stale 드롭 — enqueue 후 드레인 전에 캐스터가 죽는 창이 있다.
                    if (!dcSlotLookup.HasBuffer(castEvt.caster)) continue;
                    // 캐스트가 이 host 의 이번 프레임 "공격 사건" 이다(캐스트 우선).
                    castCountedHosts.Add(castEvt.caster);

                    var castSlots = dcSlotLookup[castEvt.caster];
                    for (int si = 0; si < castSlots.Length; si++)
                    {
                        var slot = castSlots[si];
                        if (slot.trigger != Wassup.Data.DcTriggerKind.AttackN) continue;
                        ushort cc2 = slot.counter;
                        bool fired = DcTrigger.Tick(ref cc2, slot.period);
                        slot.counter = cc2;
                        castSlots[si] = slot;
                        if (!fired) continue;
                        if (dcFiredWriter.HasValue) // use-flow unit 3 — 발동 신호
                            dcFiredWriter.Value.Enqueue(new DcTriggerFiredEvent { host = castEvt.caster });
                        // 발동했는데 arm 이 없으면 loud fail — 조용히 카운트만 태우는 것이
                        // 이 spec 이 없애려는 병이다(RESOLVE 의 unhandled 규율과 대칭).
                        if (slot.payload != Wassup.Data.DcPayloadKind.ProjectileToTarget)
                        {
                            UnityEngine.Debug.LogWarning("[AttackSystem] cast-event dc slot fired with a payload that has no arm here — count consumed with no effect.");
                            continue;
                        }

                        int2 casterCell = GridMath.WorldToCell(castEvt.casterPos, tileSize, gridSize, origin: ffOrigin);
                        int pick = PickFallbackTarget(needleScratch,
                            targetEntities, targetTransforms, targetFactions, pastGoalLookup,
                            castEvt.caster, castEvt.casterPos, casterCell,
                            tileSize, gridSize, ffOrigin, slot.tileRange);
                        // pick < 0 = 반경 안에 적이 없다. 카운트는 이미 소비됐다(계약 5).
                        if (pick >= 0)
                            SpawnNeedleCarrier(ref ecb, slot, castEvt.caster, castEvt.casterPos,
                                targetEntities[pick], targetTransforms[pick].Position,
                                attackOutputLogWriter.HasValue,
                                attackOutputLogWriter.HasValue ? attackOutputLogWriter.Value : default);
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────────────
            // Unified attacker loop — defenders and enemies share this single query.
            // Defender-specific branches guard on defenderTagLookup / HasComponent.
            // ─────────────────────────────────────────────────────────────────────
            // leap-flight-state unit 0 — LeapFlight(도약 비행 중)는 공격 START 를 막는다.
            // 위 targetCandidatesQuery 에는 **넣지 않는다** — 비행 중에도 피격은 가능하다(계약 2).
            foreach (var (attack, transform, attackerEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithNone<PendingDeployment>()
                              .WithNone<LeapFlight>()
                              .WithEntityAccess())
            {
                // Tick cooldown first.
                if (attack.ValueRO.cooldownRemaining > 0f)
                {
                    attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);
                }

                // combat-action-lock — Sleep/Stun: 공격 START 금지(쿨다운 틱은 위에서 유지 →
                // wake 시 즉시 공격). 이미 시작된 스윙(hitDelayRemaining>0)의 RESOLVE 는 완료.
                bool actionLocked = ccActionLookup.HasBuffer(attackerEntity)
                    && Wassup.Battle.Effects.CcActionLock.IsLocked(ccActionLookup[attackerEntity]);

                // bomb-thrower-defender unit 4 — 폭탄맨은 타겟 없이 쿨다운마다 방향×N 칸에
                // 폭탄을 굴려 발사(blind bombardment, 계약 2). 타겟팅/일반 공격 경로를 타지
                // 않으므로 여기서 처리하고 continue. CC(action-lock)는 일반 공격과 동일하게
                // 발사 START 를 막는다(폭탄 = 이 유닛의 공격, shield/hazard 급 예외 아님).
                if (bombLauncherLookup.HasComponent(attackerEntity))
                {
                    if (!actionLocked && attack.ValueRO.cooldownRemaining <= 0f
                        && facingLookup.HasComponent(attackerEntity)
                        && projectileRefLookup.HasComponent(attackerEntity))
                    {
                        var bomb = bombLauncherLookup[attackerEntity];
                        var bProjRef = projectileRefLookup[attackerEntity];
                        float3 bPos = transform.ValueRO.Position;
                        int2 bCasterCell = GridMath.WorldToCell(bPos, tileSize, gridSize, origin: ffOrigin);
                        BombLanding.ResolveCell(bCasterCell, facingLookup[attackerEntity].value,
                            bomb.landingTiles, gridSize, out int2 landCell, out bool landValid);
                        if (landValid)
                        {
                            float3 landWorld = GridMath.CellToWorldCenter(landCell, tileSize, 0f, origin: ffOrigin);
                            // 3종 랜덤(균등 1/3): 0 데미지 / 1 수면 / 2 스턴. 캐스터별 rng advance.
                            int bombType = bomb.rng.NextInt(0, 3);
                            bombLauncherLookup[attackerEntity] = bomb; // rng 상태 저장
                            float bDamage = 0f; byte bCcKind = 0; float bCcDur = 0f;
                            if (bombType == 0) bDamage = bomb.dmgBombDamage;
                            else if (bombType == 1) { bCcKind = (byte)Wassup.Battle.Effects.CcKind.Sleep; bCcDur = bomb.sleepSec; }
                            else { bCcKind = (byte)Wassup.Battle.Effects.CcKind.Stun; bCcDur = bomb.stunSec; }
                            ecb.AddComponent(attackerEntity, new ProjectileSpawnRequest
                            {
                                movement = MovementKind.GrenadeToCell,
                                payload = PayloadKind.TileAoe,
                                origin = bPos,
                                impact = landWorld,
                                impactTileRange = bomb.aoeTileRange,
                                aoeTargetCap = bomb.aoeTargetCap,
                                flightTime = bomb.travelSec,   // travel n (request-carried 고정)
                                fuseSec = bomb.fuseSec,         // fuse m
                                arcHeight = bomb.arcHeight,
                                damage = bDamage,
                                ccKind = bCcKind,
                                ccDuration = bCcDur,
                                bombType = (byte)bombType,
                                dataIndex = bProjRef.dataIndex,
                                visualScale = bProjRef.visualScale,
                                owner = attackerEntity,
                                targetFaction = ProjectileTargetFaction.Enemy,
                            });
                            // unit 7 — 던지기 공격 애니 + facing(착지셀 방향). 정상 경로와
                            // 동형이나 폭탄 분기는 continue 라 여기서 별도 enqueue.
                            if (attackWriter.HasValue)
                                attackWriter.Value.Enqueue(new UnitAttackVisualEvent
                                {
                                    attacker = attackerEntity,
                                    targetWorld = landWorld,
                                    attackAnimPeriod = attack.ValueRO.cooldownDuration,
                                });

                            // ── attack-decoupling unit 3 — 폭탄맨 사건 지점 ──
                            // 폭탄이 **실제로 손을 떠난** 프레임만 1카운트다(landValid 안).
                            // off-grid 로 쿨다운만 도는 프레임은 세지 않는다 — spec 계약 2.
                            // RESOLVE 는 손대지 않는다(계약 1): 이 host 는 아래에서
                            // continue 하므로 여기가 유일한 사건 지점이다.
                            if (dcSlotLookup.HasBuffer(attackerEntity))
                            {
                                var bombSlots = dcSlotLookup[attackerEntity];
                                for (int si = 0; si < bombSlots.Length; si++)
                                {
                                    var slot = bombSlots[si];
                                    if (slot.trigger != Wassup.Data.DcTriggerKind.AttackN) continue;
                                    ushort bc = slot.counter;
                                    bool fired = DcTrigger.Tick(ref bc, slot.period);
                                    slot.counter = bc;
                                    bombSlots[si] = slot;
                                    if (!fired) continue;
                                    if (dcFiredWriter.HasValue) // use-flow unit 3 — 발동 신호
                                        dcFiredWriter.Value.Enqueue(new DcTriggerFiredEvent { host = attackerEntity });
                                    // 발동했는데 arm 이 없으면 loud fail (RESOLVE 규율과 대칭).
                                    if (slot.payload != Wassup.Data.DcPayloadKind.ProjectileToTarget)
                                    {
                                        UnityEngine.Debug.LogWarning("[AttackSystem] bomb-throw dc slot fired with a payload that has no arm here — count consumed with no effect.");
                                        continue;
                                    }

                                    // host 가 대상을 안 주므로 스스로 고른다(unit 2 폴백).
                                    // 진영 Enemy 고정 + PastGoal 제외는 헬퍼가 보장한다.
                                    int pick = PickFallbackTarget(needleScratch,
                                        targetEntities, targetTransforms, targetFactions, pastGoalLookup,
                                        attackerEntity, bPos, bCasterCell,
                                        tileSize, gridSize, ffOrigin, slot.tileRange);
                                    // pick < 0 = 반경 안에 적이 없다. 카운트는 이미 소비됐다(계약 5).
                                    if (pick >= 0)
                                        SpawnNeedleCarrier(ref ecb, slot, attackerEntity, bPos,
                                            targetEntities[pick], targetTransforms[pick].Position,
                                            attackOutputLogWriter.HasValue,
                                            attackOutputLogWriter.HasValue ? attackOutputLogWriter.Value : default);
                                }
                            }
                        }
                        // 발사 성사/off-grid 무관 쿨다운 리셋(blind bombardment, 재스캔 스팸 방지).
                        attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration;
                    }
                    continue;
                }

                // Find nearest in-range target allowed by this attacker's mask.
                float3 atkPos = transform.ValueRO.Position;
                int tileRange = GridMath.RangeToTiles(attack.ValueRO.range);
                int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = default;
                int mask = attack.ValueRO.targetMask;
                // healer-lowest-health-targeting — an ally-targeting defender (targetAllies →
                // mask == Defender, baked in BattleBridge) heals the most-hurt ally, not the
                // nearest. Gated on DefenderUnitTag so a taunted enemy (also mask == Defender
                // via TauntAttackGrantSystem) keeps nearest targeting. Same candidate set as
                // the nearest scan — only the ranking criterion changes (distance → HP ratio).
                bool rankByHealth = mask == (int)Faction.Defender
                                    && defenderTagLookup.HasComponent(attackerEntity);
                // aggro-targeting Unit 4 — enemy class filter + priority. Defenders have
                // no EnemyTargetFilter → filterMask -1 / prioClass -1 = legacy nearest.
                bool hasFilter = enemyFilterLookup.HasComponent(attackerEntity);
                int filterMask = hasFilter ? enemyFilterLookup[attackerEntity].classMask : -1;
                int prioClass = hasFilter ? enemyFilterLookup[attackerEntity].priorityClass : -1;
                float bestSqPrio = float.MaxValue;
                Entity bestTargetPrio = Entity.Null;
                float3 bestTargetPosPrio = default;
                // dreamcatcher-content-2 끝을 보는 눈 — single-pass frontmost tracking (contract 1):
                // reuse this same candidate loop, no second global query. Only defenders carrying
                // the lock participate; the lock may linger without a slot after revoke, so also
                // require a live FrontmostTarget slot. frontmostMul = product of active slots.
                bool wantFrontmost = defenderTagLookup.HasComponent(attackerEntity)
                                     && frontmostLockLookup.HasComponent(attackerEntity);
                float frontmostMul = 1f;
                if (wantFrontmost)
                {
                    bool hasSlot = false;
                    if (dcAttackModLookup.HasBuffer(attackerEntity))
                    {
                        var fmods = dcAttackModLookup[attackerEntity];
                        for (int di = 0; di < fmods.Length; di++)
                            if (fmods[di].kind == Wassup.Data.DcAttackModKind.FrontmostTarget)
                            { frontmostMul *= fmods[di].damageMul; hasSlot = true; }
                    }
                    wantFrontmost = hasSlot;
                }
                // defender-directional-volley unit 3 — facing 유닛은 "레인에 적이 있으면
                // 쏜다"가 타겟팅 규칙 전부다. 후보 루프를 공유해 레인 최근접 1기를
                // witness 로 잡는다(단일 패스 — frontmost 선례). witness 는 데미지 대상이
                // 아니라 발사 게이트/조준 시각의 근거 — 레인은 facing 축 직선이라 그
                // 위치를 바라보면 곧 facing 방향을 바라보는 것과 같다.
                bool hasFacing = facingLookup.HasComponent(attackerEntity);
                int2 facing = hasFacing ? facingLookup[attackerEntity].value : default;
                // projectile-shot-sequence unit 2 — facing Direction 탄은 START 때 레인
                // witness가 발사 허가만 한다. 이후 궤적은 targetless이므로 wind-up 중
                // witness가 죽거나 레인 밖으로 나가도 RESOLVE 자체를 취소하면 안 된다.
                bool isDirectionalProjectile = projectileRefLookup.HasComponent(attackerEntity)
                    && projectileRefLookup[attackerEntity].movement == MovementKind.DirectionalLinear;
                bool isFacingDirectional = hasFacing && isDirectionalProjectile;
                float2 committedDirection = attack.ValueRO.committedDirection;
                bool hasCommittedDirection = attack.ValueRO.hasCommittedDirection != 0;
                Entity laneWitness = Entity.Null;
                float3 laneWitnessPos = default;
                float laneBestSq = float.MaxValue;

                bool fmHasBest = false;
                bool fmChosenIsPriority = false;
                FrontmostTargeting.Candidate fmBest = default;
                Entity fmBestEntity = Entity.Null;
                float3 fmBestPos = default;
                // healer-lowest-health-targeting — most-hurt ally tracked in the same pass.
                bool healHasBest = false;
                LowestHealthTargeting.Candidate healBest = default;
                Entity healBestEntity = Entity.Null;
                float3 healBestPos = default;
                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (targetEntities[i] == attackerEntity) continue;
                    int cclass = defenderClassLookup.HasComponent(targetEntities[i])
                        ? (int)defenderClassLookup[targetEntities[i]].value : -1;
                    if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue; // class not allowed
                    float3 targetPos = targetTransforms[i].Position;
                    int2 tgtCell = GridMath.WorldToCell(targetPos, tileSize, gridSize, origin: ffOrigin);
                    int tileDist = math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y));
                    if (tileDist > tileRange) continue;
                    float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetPos, blockingHazardCellsLookup, hasFlowField, flowField, out var nearestPos);
                    if (d2 < bestSq)
                    {
                        bestSq = d2;
                        bestTarget = targetEntities[i];
                        bestTargetPos = nearestPos;
                    }
                    // healer-lowest-health-targeting — rank in-range allies by HP ratio.
                    // Candidates come from a query that requires Health, so direct index is safe.
                    if (rankByHealth)
                    {
                        var h = healthLookup[targetEntities[i]];
                        var hc = new LowestHealthTargeting.Candidate
                        {
                            hpRatio = Wassup.Battle.Units.Health.ComputeRatio(h.value, h.max),
                            sqDist = d2,
                            entityIndex = targetEntities[i].Index,
                            entityVersion = targetEntities[i].Version,
                        };
                        if (!healHasBest || LowestHealthTargeting.RanksBefore(hc, healBest))
                        {
                            healBest = hc; healBestEntity = targetEntities[i]; healBestPos = nearestPos; healHasBest = true;
                        }
                    }
                    if (prioClass >= 0 && cclass == prioClass && d2 < bestSqPrio)
                    {
                        bestSqPrio = d2;
                        bestTargetPrio = targetEntities[i];
                        bestTargetPosPrio = nearestPos;
                    }
                    // 레인 witness — facing 축 1타일 폭 × [1..tileRange]. 위 Chebyshev
                    // 사거리 필터를 이미 통과했으므로 레인은 그 부분집합이다.
                    if (hasFacing && LaneMath.IsInLane(atkCell, facing, tileRange, tgtCell) && d2 < laneBestSq)
                    {
                        laneBestSq = d2;
                        laneWitness = targetEntities[i];
                        laneWitnessPos = nearestPos;
                    }
                    // frontmost tracking — rank in-range candidates by FlowField remaining
                    // distance, excluding PastGoal (leak-pending) and unreachable cells.
                    if (wantFrontmost && !pastGoalLookup.HasComponent(targetEntities[i]))
                    {
                        int fdist = FrontmostTargeting.UnreachableDist;
                        if (hasFlowField
                            && tgtCell.x >= 0 && tgtCell.x < gridSize.x
                            && tgtCell.y >= 0 && tgtCell.y < gridSize.y)
                        {
                            fdist = flowField.dist[GridMath.CellIndex(tgtCell, gridSize)];
                        }
                        if (fdist != FrontmostTargeting.UnreachableDist)
                        {
                            var fc = new FrontmostTargeting.Candidate
                            {
                                flowDist = fdist,
                                sqDist = d2,
                                entityIndex = targetEntities[i].Index,
                                entityVersion = targetEntities[i].Version,
                            };
                            if (!fmHasBest || FrontmostTargeting.RanksBefore(fc, fmBest))
                            {
                                fmBest = fc; fmBestEntity = targetEntities[i]; fmBestPos = nearestPos; fmHasBest = true;
                            }
                        }
                    }
                }
                // healer-lowest-health-targeting — override nearest with the most-hurt ally.
                // healHasBest is true iff an in-range candidate existed (same filters as the
                // nearest scan), so this only re-ranks; it never targets when nearest wouldn't.
                // priority/focus/aggro below are enemy-only (skipped for defenders). frontmost
                // (FrontmostAttackLock) and facing (DeployedFacing) ARE defender-gated and would
                // re-override this pick — but a plain healer carries neither; a healer that also
                // holds a frontmost/facing card is an out-of-scope aggregate (see spec follow-up).
                if (rankByHealth && healHasBest)
                {
                    bestTarget = healBestEntity;
                    bestTargetPos = healBestPos;
                }

                // priority override — prefer a priority-class target if any is in range.
                if (prioClass >= 0 && bestTargetPrio != Entity.Null)
                {
                    bestTarget = bestTargetPrio;
                    bestTargetPos = bestTargetPosPrio;
                }

                // enemy-behavior-components Unit 3 — FocusUntilDead lock (below aggro,
                // above nearest/priority). Keeps the locked target until it dies/despawns;
                // range only gates firing, not the lock (fire path has no range check).
                if (behaviorLookup.HasComponent(attackerEntity)
                    && behaviorLookup[attackerEntity].targetMode == Wassup.Data.EnemyTargetMode.FocusUntilDead
                    && focusLookup.HasComponent(attackerEntity))
                {
                    Entity cur = focusLookup[attackerEntity].current;
                    bool curValid = cur != Entity.Null
                        && healthLookup.HasComponent(cur) && healthLookup[cur].value > 0f
                        && !deadLookup.HasComponent(cur);
                    if (curValid)
                    {
                        float3 cPos = aggroTransformLookup.HasComponent(cur)
                            ? aggroTransformLookup[cur].Position : bestTargetPos;
                        int2 cCell = GridMath.WorldToCell(cPos, tileSize, gridSize, origin: ffOrigin);
                        int cDist = math.max(math.abs(cCell.x - atkCell.x), math.abs(cCell.y - atkCell.y));
                        if (cDist <= tileRange) { bestTarget = cur; bestTargetPos = cPos; }
                        else bestTarget = Entity.Null; // out of range → hold fire, keep lock
                        focusLookup[attackerEntity] = new FocusTarget { current = cur };
                    }
                    else
                    {
                        // invalid lock → adopt the already-computed nearest+filter result (may be Null)
                        focusLookup[attackerEntity] = new FocusTarget { current = bestTarget };
                    }
                }

                // aggro-targeting Unit 5 — sticky override: an aggroed enemy ignores
                // filter/priority/nearest/focus and targets ONLY its guardian, and only when
                // in range (otherwise it holds fire while walking toward the anchor).
                if (aggroLookup.HasComponent(attackerEntity))
                {
                    bestTarget = Entity.Null;
                    var g = aggroLookup[attackerEntity].guardian;
                    if (g != Entity.Null && aggroTransformLookup.HasComponent(g))
                    {
                        float3 gPos = aggroTransformLookup[g].Position;
                        int2 gCell = GridMath.WorldToCell(gPos, tileSize, gridSize, origin: ffOrigin);
                        int gDist = math.max(math.abs(gCell.x - atkCell.x), math.abs(gCell.y - atkCell.y));
                        if (gDist <= tileRange)
                        {
                            bestTarget = g;
                            bestTargetPos = gPos;
                        }
                    }
                }

                // dreamcatcher-content-2 끝을 보는 눈 — frontmost lock decision (contract 2/3, strict lapse).
                // Defender-only; runs after the enemy focus/aggro blocks (which never touch defenders).
                if (wantFrontmost)
                {
                    var fmLock = frontmostLockLookup[attackerEntity];
                    bool midAttack = attack.ValueRO.hitDelayRemaining > 0f && fmLock.active;
                    if (midAttack)
                    {
                        // Hold the START-locked identity through wind-up. Validate: alive, not
                        // PastGoal, in range. Any failure = strict lapse (no reselect on
                        // death/despawn/out-of-range/PastGoal).
                        Entity lt = fmLock.target;
                        bool ltValid = lt != Entity.Null
                            && healthLookup.HasComponent(lt) && healthLookup[lt].value > 0f
                            && !deadLookup.HasComponent(lt)
                            && !pastGoalLookup.HasComponent(lt);
                        if (ltValid)
                        {
                            float3 ltPos = aggroTransformLookup.HasComponent(lt)
                                ? aggroTransformLookup[lt].Position : bestTargetPos;
                            int2 ltCell = GridMath.WorldToCell(ltPos, tileSize, gridSize, origin: ffOrigin);
                            int ltDist = math.max(math.abs(ltCell.x - atkCell.x), math.abs(ltCell.y - atkCell.y));
                            if (ltDist <= tileRange) { bestTarget = lt; bestTargetPos = ltPos; }
                            else bestTarget = Entity.Null; // out of range → lapse
                        }
                        else bestTarget = Entity.Null; // dead/despawn/PastGoal → lapse
                    }
                    else
                    {
                        // Not mid-attack: pick the current frontmost for a possible START this frame.
                        // No reachable frontmost → keep the nearest fallback (non-priority, contract 3).
                        if (fmHasBest) { bestTarget = fmBestEntity; bestTargetPos = fmBestPos; fmChosenIsPriority = true; }
                        else fmChosenIsPriority = false;
                    }
                }

                // defender-directional-volley unit 3 — facing 최종 오버라이드. 방향 고정
                // 유닛에게는 레인 밖 적이 존재하지 않는 것과 같다 — 최근접/우선순위/
                // frontmost/aggro 가 무엇을 골랐든 레인 witness 로 덮는다(레인이 곧
                // 타겟팅 규칙 전부). 레인이 비었으면 새 START는 하지 않는다(탄 낭비 방지).
                // 단 이미 START된 targetless Direction 탄은 아래 RESOLVE 예외로 완주한다.
                if (hasFacing)
                {
                    bestTarget = laneWitness;
                    bestTargetPos = laneWitnessPos;
                    // witness 는 "최전방"이 아니라 "최근접"이다 — frontmost 보너스를 여기에
                    // 실으면 카드가 약속한 대상(최전방)이 아닌 적이 +20% 를 받는다. 방향
                    // 유닛은 레인이 타겟팅 규칙 전부이므로 보너스를 포기한다(ecs-review L1).
                    fmChosenIsPriority = false;
                }

                // attack-hit-delay — fire 를 START(공격 시작) / RESOLVE(타격 판정) 로 분리.
                // 지연 중이면 tick → 만료한 프레임에 RESOLVE(재판정된 bestTarget, Direction은
                // START 허가 유지). 아니면 쿨다운+타겟 조건 시 START.
                bool doResolve = false;
                if (attack.ValueRO.hitDelayRemaining > 0f)
                {
                    float rem = attack.ValueRO.hitDelayRemaining - dt;
                    attack.ValueRW.hitDelayRemaining = math.max(0f, rem);
                    if (rem <= 0f) doResolve = true;   // 지연 만료 → 이번 프레임 타격
                    // 지연 중엔 새 공격 START 안 함
                }
                else if (!actionLocked && bestTarget != Entity.Null && attack.ValueRO.cooldownRemaining <= 0f)
                {
                    // ── START ── 애니메이션 + 쿨다운 리셋 + 지연 세팅 (타격은 RESOLVE).
                    bool isDefenderStart = defenderTagLookup.HasComponent(attackerEntity);

                    // enemy-ai-fsm 3a — 적은 Engaging|Standoff 에서만 fire. 디펜더는 상태머신 대상 아님(항상 fire).
                    bool stateAllowsFire = true;
                    if (!isDefenderStart && aiStateLookup.HasComponent(attackerEntity))
                    {
                        var ai = aiStateLookup[attackerEntity].value;
                        stateAllowsFire = ai == AiState.Engaging || ai == AiState.Standoff;
                    }

                    if (stateAllowsFire)
                    {
                        // projectile-shot-sequence unit 5 — 일반 타겟팅 Direction 탄은
                        // Archer/Ranger처럼 nearest target으로 START하되, wind-up 뒤의
                        // 재판정이 이번 발사 기준축을 바꾸거나 취소하지 못하게 방향만
                        // Combat 상태에 스냅샷한다. 즉시 RESOLVE도 아래 로컬 값을 쓴다.
                        if (!hasFacing && isDirectionalProjectile)
                        {
                            float2 toTarget = (bestTargetPos - atkPos).xz;
                            committedDirection = math.lengthsq(toTarget) > 1e-6f
                                ? math.normalize(toTarget)
                                : new float2(0f, 1f);
                            hasCommittedDirection = true;
                            attack.ValueRW.committedDirection = committedDirection;
                            attack.ValueRW.hasCommittedDirection = 1;
                        }

                        float attackSpeedMul = modifierStatsLookup.HasComponent(attackerEntity)
                            ? modifierStatsLookup[attackerEntity].attackSpeedMul
                            : 1f;
                        float effectiveCooldownMul = attackSpeedMul > 0f ? 1f / attackSpeedMul : 1f;
                        // attack-anim-speed-match unit 1 — 정상 간격(초). double-fire 로 cooldownRemaining
                        // 을 0 화하기 전의 값이라 애니는 정상속도 유지.
                        float attackInterval = attack.ValueRO.cooldownDuration * effectiveCooldownMul;
                        // 실제 발사 주기 = max(간격, hitDelay). hitDelayRemaining>0 동안 다음 START 가 막히므로
                        // (윗줄 hitDelay tick 분기), hitDelaySec>interval 이면 실주기는 hitDelaySec. 애니를 이
                        // 주기에 맞춰야 실발사보다 빨리 끝나지 않는다(critic MEDIUM #1).
                        float attackAnimPeriod = math.max(attackInterval, attack.ValueRO.hitDelaySec);

                        // Unified visual trigger — 공격 시작 시 애니메이션/facing.
                        if (attackWriter.HasValue)
                        {
                            attackWriter.Value.Enqueue(new UnitAttackVisualEvent
                            {
                                attacker = attackerEntity,
                                targetWorld = bestTargetPos,
                                attackAnimPeriod = attackAnimPeriod,
                                target = bestTarget,
                            });
                        }

                        attack.ValueRW.cooldownRemaining = attackInterval;

                        // content-1 ① (가시 갑옷) — double-fire charge: zero this attack's
                        // cooldown so the unit immediately attacks again (2연발), then
                        // consume the charge (ONE bonus shot). Each shot is a full normal
                        // attack, so DC-tick / CC / knockback / log happen once per real
                        // shot — no in-RESOLVE duplication (avoids critic H3/H4).
                        if (isDefenderStart && nextDoubleFireLookup.HasComponent(attackerEntity))
                        {
                            attack.ValueRW.cooldownRemaining = 0f;
                            ecb.RemoveComponent<NextAttackDoubleFire>(attackerEntity);
                        }

                        // dreamcatcher-content-2 끝을 보는 눈 — lock this attack's frontmost pick and
                        // snapshot the damage multiplier so mid-attack card changes don't alter an
                        // in-flight attack (contract 2/4). Held through wind-up, released at RESOLVE.
                        if (wantFrontmost)
                        {
                            frontmostLockLookup[attackerEntity] = new FrontmostAttackLock
                            {
                                active = true,
                                target = bestTarget,
                                damageMulSnapshot = frontmostMul,
                                targetIsPriority = fmChosenIsPriority,
                            };
                        }

                        // 이동 정지는 MovementSystem 이 EnemyAiState 로 처리(레거시 aimMode/movePause enqueue 제거).
                        // 타격 지연: 0 이면 이번 프레임 즉시 RESOLVE, >0 이면 지연 시작.
                        if (attack.ValueRO.hitDelaySec <= 0f) doResolve = true;
                        else attack.ValueRW.hitDelayRemaining = attack.ValueRO.hitDelaySec;
                    }
                }

                // ── RESOLVE ── 일반 공격은 재판정된 bestTarget이 필요하다. 단 START가
                // 성사된 facing Direction 탄은 targetless 궤적이므로 witness 소실 뒤에도
                // 고정 facing으로 발사한다. 로그/방향 보조점은 레인 끝을 사용한다.
                bool resolveCommittedDirectionalWithoutWitness =
                    doResolve && bestTarget == Entity.Null && !hasFacing
                    && isDirectionalProjectile && hasCommittedDirection;
                bool resolveFacingDirectionalWithoutWitness =
                    doResolve && bestTarget == Entity.Null && isFacingDirectional;
                if (resolveFacingDirectionalWithoutWitness)
                {
                    bestTargetPos = atkPos + new float3(facing.x, 0f, facing.y) * (tileRange * tileSize);
                }
                else if (resolveCommittedDirectionalWithoutWitness)
                {
                    bestTargetPos = atkPos
                        + new float3(committedDirection.x, 0f, committedDirection.y)
                        * (tileRange * tileSize);
                }
                if (doResolve && (bestTarget != Entity.Null
                                  || resolveFacingDirectionalWithoutWitness
                                  || resolveCommittedDirectionalWithoutWitness))
                {
                    float damageMul = modifierStatsLookup.HasComponent(attackerEntity)
                        ? modifierStatsLookup[attackerEntity].damageMul
                        : 1f;
                    // dreamcatcher-new-abilities unit 2 — shatter_hymn: CC 걸린 적 대상
                    // 추가 배율. 공격자 stat(부재→1); 대상별 활성 CC 게이트는 각 데미지
                    // 지점에서(투사체=발사 시점 bestTarget, 멜리=hitTarget별).
                    float attackerVsCc = modifierStatsLookup.HasComponent(attackerEntity)
                        ? modifierStatsLookup[attackerEntity].damageVsCcMul
                        : 1f;
                    // dreamcatcher-content-2 끝을 보는 눈 (unit 3) — the locked primary's +20%.
                    // Only when the lock is active AND the pick was a real frontmost (not a
                    // fallback nearest). fmPrioMul stays 0 (inert) otherwise; the melee arm
                    // guards on fmPrioTarget != Null and the projectile encodes 0 = no bonus.
                    Entity fmPrioTarget = Entity.Null;
                    float fmPrioMul = 0f;
                    if (wantFrontmost)
                    {
                        var l = frontmostLockLookup[attackerEntity];
                        if (l.active && l.targetIsPriority)
                        {
                            fmPrioTarget = l.target;
                            fmPrioMul = l.damageMulSnapshot;
                        }
                    }
                    // dreamcatcher-heavy-strike unit 1 — 응축된 일격 pre-scan: is THIS attack
                    // the N-th (→ 강공)? Aggregate the mul (product over copies). Read-only
                    // peek (WouldFire) of the same pre-increment counter the dc-trigger loop
                    // below Ticks, so this prediction == that loop's dcFired (counter write
                    // ownership stays the loop). Carried on the projectile via heavyDamageMul;
                    // consumed at hit-site + melee arm in unit 2 (inert until then).
                    float heavyMul = 1f;
                    if (bestTarget != Entity.Null
                        && defenderTagLookup.HasComponent(attackerEntity)
                        && dcSlotLookup.HasBuffer(attackerEntity))
                    {
                        var heavySlots = dcSlotLookup[attackerEntity];
                        for (int hi = 0; hi < heavySlots.Length; hi++)
                        {
                            var hs = heavySlots[hi];
                            if (hs.trigger != Wassup.Data.DcTriggerKind.AttackN
                                || hs.payload != Wassup.Data.DcPayloadKind.HeavyStrike
                                || !DcTrigger.WouldFire(hs.counter, hs.period))
                                continue;
                            // trigger-gates unit 1 — 게이트 합성 불변식: pre-scan 은
                            // WouldFire ∧ GatePass, 아래 counter 루프는 if(GatePass) Tick —
                            // 같은 프레임·같은 bestTarget·pre-damage HP 라 결과가 일치한다.
                            // 게이트 실패 시 counter 도 안 오르므로(카운트 게이트) 이
                            // 공격은 강공이 아니고, 다음 게이트 통과 공격이 같은 카운트로
                            // 재도전한다.
                            if (hs.gate != Wassup.Data.DcGateKind.None)
                            {
                                if (!healthLookup.HasComponent(bestTarget)) continue;
                                var gh = healthLookup[bestTarget];
                                if (!DcTrigger.GatePass(hs.gate, hs.gateValue, gh.value, gh.max)) continue;
                            }
                            heavyMul *= hs.magnitude > 0f ? hs.magnitude : 1f;
                        }
                    }

                    // All defender/enemy hit effects come through AttackOutputElement.
                    bool hasOutputs = outputBufferLookup.HasBuffer(attackerEntity);

                    if (hasOutputs)
                    {
                        var outputs = outputBufferLookup[attackerEntity];

                        if (projectileRefLookup.HasComponent(attackerEntity))
                        {
                            var projRef = projectileRefLookup[attackerEntity];

                            // attack-mod-bounce unit 3 + 방향탄 개통 — always-on 모드를 이
                            // 공격의 산출물에 실을 값으로 집계한다(count 합 / range max /
                            // mul 곱). 분기 위에서 **한 번만** 계산한다 — homing 과 directional
                            // 이 같은 12줄을 각자 갖고 있으면 필드가 하나 늘 때 한쪽이 조용히
                            // 뒤처진다. Ballistic 은 계산만 하고 request 에 싣지 않는다
                            // (착탄 셀이 발사 시점에 고정돼 재조준할 대상이 없다 — 계약 4).
                            int dcBounceCount = 0, dcBounceRange = 0;
                            float dcBounceMul = 1f;
                            if (defenderTagLookup.HasComponent(attackerEntity) && dcAttackModLookup.HasBuffer(attackerEntity))
                            {
                                var bmods = dcAttackModLookup[attackerEntity];
                                for (int di = 0; di < bmods.Length; di++)
                                {
                                    var mod = bmods[di];
                                    if (mod.kind != Wassup.Data.DcAttackModKind.ProjectileBounce) continue;
                                    dcBounceCount += mod.count;
                                    dcBounceRange = math.max(dcBounceRange, mod.tileRange);
                                    dcBounceMul *= mod.damageMul;
                                }
                            }
                            float projectileDamage = 0f;
                            var projectileOutputs = ecb.AddBuffer<ProjectileSpawnOutputElement>(attackerEntity);
                            for (int oi = 0; oi < outputs.Length; oi++)
                            {
                                var o = outputs[oi].value;
                                if (o.kind == Wassup.Data.AttackOutputKind.Damage)
                                {
                                    float amount = o.magnitude * damageMul;
                                    // shatter_hymn — 발사 시점 의도 대상(bestTarget)이 CC 상태면
                                    // 배율(투사체 bake 경로도 포함 — 궁수 콤보 살림, critic HIGH).
                                    if (bestTarget != Entity.Null
                                        && attackerVsCc != 1f
                                        && ccActionLookup.HasBuffer(bestTarget)
                                        && AnyActiveCc(ccActionLookup[bestTarget]))
                                        amount *= attackerVsCc;
                                    o.magnitude = amount;
                                    projectileDamage += amount;
                                    if (attackOutputLogWriter.HasValue)
                                        attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                        {
                                            attacker  = attackerEntity,
                                            kind      = Wassup.Data.AttackOutputKind.Damage,
                                            magnitude = amount,
                                            duration  = 0f,
                                            sourcePos = atkPos,
                                            targetPos = bestTargetPos,
                                        });
                                }
                                projectileOutputs.Add(new ProjectileSpawnOutputElement { value = o });
                            }

                            if (projRef.movement == MovementKind.BallisticArcToPoint)
                            {
                                // Lock the target's current cell as the impact point so
                                // the shell lands there even if the target dies or moves
                                // mid-flight. Only XZ matters here — the drain sets the
                                // final Y to the spawn-height plane, so Y is a placeholder.
                                int2 impactCell = GridMath.WorldToCell(bestTargetPos, tileSize, gridSize, origin: ffOrigin);
                                float3 impactWorld = GridMath.CellToWorldCenter(impactCell, tileSize, 0f, origin: ffOrigin);
                                ecb.AddComponent(attackerEntity, new ProjectileSpawnRequest
                                {
                                    movement = MovementKind.BallisticArcToPoint,
                                    payload = projRef.payload,
                                    origin = atkPos,
                                    impact = impactWorld,
                                    damage = projectileDamage,
                                    speed = projRef.speed,
                                    visualScale = projRef.visualScale,
                                    dataIndex = projRef.dataIndex,
                                    arcHeight = projRef.arcHeight,
                                    impactTileRange = projRef.impactTileRange,
                                    owner = attackerEntity, // nightmare-catcher unit 1 — threat attribution
                                    // 끝을 보는 눈 (unit 3) — TileAoe/ballistic priority victim + mul.
                                    priorityTarget = fmPrioTarget,
                                    priorityDamageMul = fmPrioMul,
                                    // 응축된 일격 (unit 1) — 강공 전-victim 배율(unit 2 hit-site 소비, 기본 1=inert).
                                    heavyDamageMul = heavyMul,
                                });
                            }
                            else if (projRef.movement == MovementKind.DirectionalLinear)
                            {
                                // defender-directional-volley unit 3 — 방향 발사. 타겟
                                // 엔티티를 싣지 않는다: 경로에 있는 것을 맞히는 탄이라
                                // 발사 후 대상이 죽거나 비켜도 궤적은 그대로다.
                                // 방향은 facing 이 원칙이고, facing 없는 유닛이 이 SO 를
                                // 쓰면 조준 대상 쪽으로 쏜다(퇴화 벡터는 drain 이 폐기).
                                float2 fireDir = facing;
                                if (!hasFacing && hasCommittedDirection)
                                {
                                    fireDir = committedDirection;
                                }
                                else if (!hasFacing)
                                {
                                    float2 toTarget = (bestTargetPos - atkPos).xz;
                                    fireDir = math.lengthsq(toTarget) > 1e-6f ? math.normalize(toTarget) : new float2(0f, 1f);
                                }
                                // 사거리는 레인 게이트와 같은 타일 단위로 환산 — 그래야
                                // 탄이 "게이트가 인정한 마지막 칸"까지 정확히 닿는다.
                                // direction 은 확산 전 기준 방향(템플릿 원본).
                                var template = new ProjectileSpawnRequest
                                {
                                    movement = MovementKind.DirectionalLinear,
                                    payload = projRef.payload,
                                    origin = atkPos,
                                    direction = fireDir,
                                    maxDistance = tileRange * tileSize,
                                    damage = projectileDamage,
                                    speed = projRef.speed,
                                    hitThreshold = projRef.hitThreshold,
                                    visualScale = projRef.visualScale,
                                    dataIndex = projRef.dataIndex,
                                    bounceRemaining = dcBounceCount,
                                    bounceTileRange = dcBounceRange,
                                    bounceDamageMul = dcBounceMul,
                                    owner = attackerEntity, // nightmare-catcher unit 1 — threat attribution
                                    priorityTarget = fmPrioTarget,
                                    priorityDamageMul = fmPrioMul,
                                    heavyDamageMul = heavyMul,
                                };

                                // projectile-shot-sequence unit 2 — pattern defender는
                                // 직접 request를 만들지 않고 한 trigger를 instance 하나로
                                // 번역한다. emitter가 이 시스템 뒤에 돌아 첫 탄도 같은 sim
                                // frame에 carrier로 만든다.
                                bool pushedPattern = false;
                                if (patternSlotLookup.HasBuffer(attackerEntity) &&
                                    emitterInstanceLookup.HasBuffer(attackerEntity))
                                {
                                    var slots = patternSlotLookup[attackerEntity];
                                    if (slots.Length > 0 && slots[0].spec.shots.Length > 0)
                                    {
                                        var slot = slots[0];
                                        var spec = slot.spec;
                                        // defender damage는 output/modifier가 결정한다. pattern
                                        // SO의 damage는 boss/skill 경로용이며 여기서는 trigger
                                        // 시점 실효값으로 덮어 전탄에 스냅샷한다.
                                        spec.damage = projectileDamage;
                                        // unit 5 — 랜덤 패턴도 instance가 완성된 runtime
                                        // shot 목록을 소유한다. 같은 host의 연속 trigger와
                                        // 여러 host가 같은 시퀀스를 반복하지 않되 결정론은 유지.
                                        PatternShotRandomizer.Apply(
                                            ref spec,
                                            math.hash(new int2(attackerEntity.Index, slot.fireCountBase)));

                                        // barrel 기반 template이 가진 effect/targetFaction은
                                        // 보존하고, 이번 공격에만 결정되는 값은 RESOLVE에서
                                        // 스냅샷한다.
                                        var patternTemplate = slot.template;
                                        patternTemplate.origin = template.origin;
                                        patternTemplate.direction = template.direction;
                                        patternTemplate.maxDistance = template.maxDistance;
                                        patternTemplate.damage = projectileDamage;
                                        patternTemplate.bounceRemaining = template.bounceRemaining;
                                        patternTemplate.bounceTileRange = template.bounceTileRange;
                                        patternTemplate.bounceDamageMul = template.bounceDamageMul;
                                        patternTemplate.owner = attackerEntity;
                                        patternTemplate.priorityTarget = template.priorityTarget;
                                        patternTemplate.priorityDamageMul = template.priorityDamageMul;
                                        patternTemplate.heavyDamageMul = template.heavyDamageMul;

                                        var instance = new EmitterInstance
                                        {
                                            spec = spec,
                                            template = patternTemplate,
                                            lockedTarget = Entity.Null,
                                        };
                                        EmitterTick.Begin(ref instance.runtime, spec, slot.fireCountBase);
                                        emitterInstanceLookup[attackerEntity].Add(instance);

                                        slot.fireCountBase += spec.shots.Length;
                                        slots[0] = slot;
                                        // 다음 트리거는 버스트가 끝난 뒤부터 기다린다(계약 8).
                                        attack.ValueRW.cooldownRemaining += EmitterTick.TotalDuration(spec);
                                        pushedPattern = true;
                                    }
                                }

                                // pattern 없는 방향 단발(적 또는 legacy authoring)은 기존
                                // 요청 경로를 유지한다.
                                if (!pushedPattern)
                                    ecb.AddComponent(attackerEntity, template);
                            }
                            else
                            {
                                ecb.AddComponent(attackerEntity, new ProjectileSpawnRequest
                                {
                                    movement = MovementKind.HomingToEntity,
                                    payload = projRef.payload,
                                    target = bestTarget,
                                    origin = atkPos,
                                    damage = projectileDamage,
                                    speed = projRef.speed,
                                    hitThreshold = projRef.hitThreshold,
                                    visualScale = projRef.visualScale,
                                    dataIndex = projRef.dataIndex,
                                    onHitEffect = projRef.onHitEffect,
                                    splashRadius = projRef.splashRadius,
                                    splashDamageMul = projRef.splashDamageMul,
                                    bounceRemaining = dcBounceCount,
                                    bounceTileRange = dcBounceRange,
                                    bounceDamageMul = dcBounceMul,
                                    owner = attackerEntity, // nightmare-catcher unit 1 — threat attribution
                                    // 끝을 보는 눈 (unit 3) — homing direct-victim priority + mul.
                                    priorityTarget = fmPrioTarget,
                                    priorityDamageMul = fmPrioMul,
                                    // 응축된 일격 (unit 1) — 강공 전-victim 배율(unit 2 hit-site 소비, 기본 1=inert).
                                    heavyDamageMul = heavyMul,
                                });
                            }
                        }
                        else
                        {
                            // ── Outputs path ────────────────────────────────────────────────
                            // Collect hit targets (same AoE logic as legacy melee path).
                            // aggro-targeting Unit 8 — sticky enemies hit only the guardian:
                            // force single-target so the AoE follow-up can't pull in other defenders.
                            int desiredCount = aggroLookup.HasComponent(attackerEntity)
                                ? 1
                                : math.max(1, attack.ValueRO.attackTargetCount);
                            var hitTargets = new NativeArray<Entity>(desiredCount, Allocator.Temp);
                            int hitCount = 0;

                            // aggro-targeting Unit 11 — 가디언(AggroCapacity)이면 정의 계층
                            // AggroTargeting.SelectTargets 로 aggro-aware 선정: 여유가 있으면
                            // 비-어그로 최근접 우선(신규 팩 흡수), 상한 차면 겹친 팩 정리.
                            bool isGuardian = aggroCapacityLookup.HasComponent(attackerEntity);
                            if (isGuardian)
                            {
                                var cap = aggroCapacityLookup[attackerEntity];
                                var cands = new NativeArray<AggroCandidate>(targetEntities.Length, Allocator.Temp);
                                var candIdx = new NativeArray<int>(targetEntities.Length, Allocator.Temp);
                                int nc = 0;
                                for (int i = 0; i < targetEntities.Length; i++)
                                {
                                    if (((int)targetFactions[i].value & mask) == 0) continue;
                                    if (targetEntities[i] == attackerEntity) continue;
                                    float3 tp = targetTransforms[i].Position;
                                    cands[nc] = new AggroCandidate
                                    {
                                        cell = GridMath.WorldToCell(tp, tileSize, gridSize, origin: ffOrigin),
                                        pos = tp,
                                        aggroed = aggroLookup.HasComponent(targetEntities[i]),
                                    };
                                    candIdx[nc] = i;
                                    nc++;
                                }
                                var outIdx = new NativeArray<int>(desiredCount, Allocator.Temp);
                                int sel = AggroTargeting.SelectTargets(
                                    atkCell, atkPos, tileRange, cap.held, cap.max,
                                    cands.GetSubArray(0, nc), outIdx);
                                for (int s = 0; s < sel; s++)
                                    hitTargets[hitCount++] = targetEntities[candIdx[outIdx[s]]];
                                // critic H1 — 가디언은 SelectTargets 결과가 일반 nearest(bestTarget)와
                                // 다를 수 있다. 이후 넉백 CC/DC 캐리어/AttackOutputLog 가 bestTarget/
                                // bestTargetPos 를 쓰므로, primary 를 실제 때린 적으로 정렬해 불일치를
                                // 막는다(현재 가디언 넉백=0 이라 dormant 이나 미래 가디언·DC 카드 대비).
                                if (hitCount > 0)
                                {
                                    // dreamcatcher-content-2 끝을 보는 눈 (contract 5) — a frontmost card
                                    // forces the primary to the locked frontmost instead of SelectTargets'
                                    // pick; secondaries still fill via aggro-aware selection.
                                    bool keepFrontmostPrimary = wantFrontmost
                                        && frontmostLockLookup[attackerEntity].active
                                        && bestTarget != Entity.Null;
                                    if (keepFrontmostPrimary)
                                    {
                                        // Force the locked frontmost as primary (contract 5). ecs-review
                                        // M1: if SelectTargets already ranked it as a secondary, SWAP it
                                        // to primary (no double-hit, keeps the displaced pick as a
                                        // secondary); otherwise it displaces SelectTargets' own primary.
                                        int existing = -1;
                                        for (int s = 0; s < hitCount; s++)
                                            if (hitTargets[s] == bestTarget) { existing = s; break; }
                                        if (existing >= 0) hitTargets[existing] = hitTargets[0];
                                        hitTargets[0] = bestTarget;
                                    }
                                    else
                                    {
                                        int primaryI = candIdx[outIdx[0]];
                                        bestTarget = targetEntities[primaryI];
                                        bestTargetPos = targetTransforms[primaryI].Position;
                                    }
                                }
                                cands.Dispose();
                                candIdx.Dispose();
                                outIdx.Dispose();
                            }
                            else
                            {
                                hitTargets[hitCount++] = bestTarget;
                                if (desiredCount > 1)
                                {
                                    var hitMaskO = new NativeArray<bool>(targetEntities.Length, Allocator.Temp);
                                    int seedIdx = -1;
                                    for (int i = 0; i < targetEntities.Length; i++)
                                    {
                                        if (targetEntities[i] == bestTarget) { seedIdx = i; break; }
                                    }
                                    if (seedIdx >= 0) hitMaskO[seedIdx] = true;

                                    for (int pass = 1; pass < desiredCount && hitCount < desiredCount; pass++)
                                    {
                                        float passSq = float.MaxValue;
                                        int passIdx = -1;
                                        // healer-lowest-health-targeting — secondaries also by HP ratio.
                                        bool passHasBest = false;
                                        LowestHealthTargeting.Candidate passBest = default;
                                        for (int i = 0; i < targetEntities.Length; i++)
                                        {
                                            if (hitMaskO[i]) continue;
                                            if (((int)targetFactions[i].value & mask) == 0) continue;
                                            if (targetEntities[i] == attackerEntity) continue;
                                            int2 tgtCellAoE = GridMath.WorldToCell(targetTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                                            int tileDistAoE = math.max(math.abs(tgtCellAoE.x - atkCell.x), math.abs(tgtCellAoE.y - atkCell.y));
                                            if (tileDistAoE > tileRange) continue;
                                            float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position, blockingHazardCellsLookup, hasFlowField, flowField, out _);
                                            if (rankByHealth)
                                            {
                                                var h = healthLookup[targetEntities[i]];
                                                var hc = new LowestHealthTargeting.Candidate
                                                {
                                                    hpRatio = Wassup.Battle.Units.Health.ComputeRatio(h.value, h.max),
                                                    sqDist = d2,
                                                    entityIndex = targetEntities[i].Index,
                                                    entityVersion = targetEntities[i].Version,
                                                };
                                                if (!passHasBest || LowestHealthTargeting.RanksBefore(hc, passBest))
                                                {
                                                    passBest = hc; passIdx = i; passHasBest = true;
                                                }
                                            }
                                            else if (d2 < passSq)
                                            {
                                                passSq = d2;
                                                passIdx = i;
                                            }
                                        }
                                        if (passIdx < 0) break;
                                        hitMaskO[passIdx] = true;
                                        hitTargets[hitCount++] = targetEntities[passIdx];
                                    }
                                    hitMaskO.Dispose();
                                }
                            }

                            // nightmare-catcher unit 1 — 공격 단위 불변식 hoist:
                            // 위협 credit 여부는 공격자에만 의존(피격자별 버퍼 체크는
                            // TryCredit 내부). ProjectileHitSystem 의 creditThreat 대칭.
                            bool creditThreat = hasThreatQ && defenderTagLookup.HasComponent(attackerEntity);
                            for (int ti = 0; ti < hitCount; ti++)
                            {
                                Entity hitTarget = hitTargets[ti];
                                for (int oi = 0; oi < outputs.Length; oi++)
                                {
                                    var o = outputs[oi].value;
                                    switch (o.kind)
                                    {
                                        case Wassup.Data.AttackOutputKind.Damage:
                                        {
                                            // shatter_hymn — 이 hitTarget 이 CC 상태면 배율(멜리/AoE 는
                                            // 즉시 해결이라 대상별 현재 CC 로 판정).
                                            float dmg = o.magnitude * damageMul;
                                            if (attackerVsCc != 1f && ccActionLookup.HasBuffer(hitTarget) && AnyActiveCc(ccActionLookup[hitTarget]))
                                                dmg *= attackerVsCc;
                                            // 끝을 보는 눈 (unit 3) — only the locked primary victim takes
                                            // +20%; secondaries/AoE stay base. Same dmg feeds IncomingDamage
                                            // AND ThreatTable.TryCredit below (no threat desync, HIGH 5).
                                            if (fmPrioTarget != Entity.Null && hitTarget == fmPrioTarget)
                                                dmg *= fmPrioMul;
                                            // 응축된 일격 (unit 2) — 멜리 cleave 전 대상에 강공 배율(전 victim).
                                            // heavyMul=1 이면 무영향. dmg 가 IncomingDamage+TryCredit 공통 →
                                            // threat 동기. 이 공격의 heavyMul 은 pre-scan(unit 1)이 이미 산출.
                                            dmg *= heavyMul;
                                            ecb.AppendToBuffer(hitTarget, new IncomingDamage { amount = dmg, source = attackerEntity });
                                            ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup,
                                                hitTarget, attackerEntity, dmg);
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker  = attackerEntity,
                                                    kind      = Wassup.Data.AttackOutputKind.Damage,
                                                    magnitude = dmg,
                                                    duration  = 0f,
                                                    sourcePos = atkPos,
                                                    targetPos = bestTargetPos,
                                                });
                                            break;
                                        }

                                        case Wassup.Data.AttackOutputKind.Heal:
                                            ecb.AppendToBuffer(hitTarget, new Wassup.Battle.Units.IncomingHeal { amount = o.magnitude });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker  = attackerEntity,
                                                    kind      = Wassup.Data.AttackOutputKind.Heal,
                                                    magnitude = o.magnitude,
                                                    duration  = 0f,
                                                    sourcePos = atkPos,
                                                    targetPos = bestTargetPos,
                                                });
                                            break;

                                        case Wassup.Data.AttackOutputKind.ApplyStat:
                                            if (hasStatQ)
                                                statModSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Effects.StatModifierApplyEvent
                                                {
                                                    target    = hitTarget,
                                                    stat      = o.stat,
                                                    op        = o.op,
                                                    magnitude = o.magnitude,
                                                    duration  = o.duration,
                                                    source    = attackerEntity,
                                                    stackId   = 0,
                                                    origin    = Wassup.Battle.Effects.ModifierOrigin.OnHit,
                                                });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker  = attackerEntity,
                                                    kind      = Wassup.Data.AttackOutputKind.ApplyStat,
                                                    magnitude = o.magnitude,
                                                    stat      = o.stat,
                                                    duration  = o.duration,
                                                    sourcePos = atkPos,
                                                    targetPos = bestTargetPos,
                                                });
                                            break;

                                        case Wassup.Data.AttackOutputKind.ApplyStack:
                                            if (hasStackQ)
                                                stackModSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Effects.StackModifierApplyEvent
                                                {
                                                    target         = hitTarget,
                                                    kind           = o.stackKind,
                                                    countDelta     = (byte)math.max(1f, o.magnitude),
                                                    maxStack       = o.stackMaxStack > 0 ? o.stackMaxStack : Wassup.Data.StackModifierSO.DefaultMaxStack,
                                                    perAppDuration = o.duration,
                                                    source         = attackerEntity,
                                                });
                                            if (attackOutputLogWriter.HasValue)
                                                attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                                {
                                                    attacker   = attackerEntity,
                                                    kind       = Wassup.Data.AttackOutputKind.ApplyStack,
                                                    magnitude  = o.magnitude,
                                                    stackKind  = o.stackKind,
                                                    duration   = o.duration,
                                                    sourcePos  = atkPos,
                                                    targetPos  = bestTargetPos,
                                                });
                                            break;
                                    }
                                }
                            }

                            // aggro-targeting Unit 11 — 가디언 명중분을 Effects 로 넘긴다.
                            // Aggroed 부착/capacity 게이트/선점은 AggroStateSystem(Effects)이
                            // 드레인 시 판정 — Combat 은 "때렸다" 사실만 전달(맥락 경계).
                            if (isGuardian && aggroHitWriter.HasValue)
                            {
                                for (int ti = 0; ti < hitCount; ti++)
                                    aggroHitWriter.Value.Enqueue(new Wassup.Battle.Effects.AggroHitEvent
                                    {
                                        guardian = attackerEntity,
                                        enemy = hitTargets[ti],
                                    });
                            }

                            // knockup-fighter-defender unit 0 — 공중 띄우기 = 히트한 **전 대상**에
                            // 짧은 Stun. 아래 knockback/sleep 블록(주 타겟 1체)과 달리 여기 있는
                            // 이유는 스코프가 hitTargets 전원이기 때문 — 어그로 enqueue 와 같은 자리다.
                            // 심에 "공중" 개념은 없다. 떠오르는 연출은 뷰가 따로 재생한다(unit 3).
                            if (ccWriter.HasValue && defenderCcLookup.HasComponent(attackerEntity))
                            {
                                var kd = defenderCcLookup[attackerEntity];
                                if (kd.knockupOnHitSec > 0f)
                                {
                                    for (int ti = 0; ti < hitCount; ti++)
                                    {
                                        // boss-jjangssen unit 3 — 보스는 넉업 면역. CC 와 연출을
                                        // **함께** 건너뛴다: 연출만 나가면 떠오르는데 스턴은 안 걸린다.
                                        // 판정은 하드코딩하지 않고 면역 술어 단일 소스를 부른다 —
                                        // 나중에 면역 범위를 좁히면 이 지점도 자동으로 따라온다.
                                        if (bossLookup.HasComponent(hitTargets[ti])
                                            && Wassup.Battle.Effects.CcActionLock.IsBossImmune(
                                                Wassup.Battle.Effects.CcKind.Stun)) continue;
                                        ccWriter.Value.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                                        {
                                            target = hitTargets[ti],
                                            effect = new Wassup.Battle.Effects.CcEffect
                                            {
                                                kind          = Wassup.Battle.Effects.CcKind.Stun,
                                                remainingTime = kd.knockupOnHitSec,
                                            },
                                        });
                                        // 연출 신호는 **띄운 쪽**이 보낸다 — 뷰가 CcEffect(Stun)를 보고
                                        // 판단하면 일반 스턴까지 떠오른다(계약 4). unit 3.
                                        if (knockupVisualWriter.HasValue)
                                            knockupVisualWriter.Value.Enqueue(new KnockupVisualEvent
                                            {
                                                target      = hitTargets[ti],
                                                durationSec = kd.knockupOnHitSec,
                                                height      = kd.knockupVisualHeight,
                                            });
                                    }
                                }
                            }
                            hitTargets.Dispose();
                        }
                    }

                    // [Defender only] Knockback CC — enemies do not carry DefenderCcData. (RESOLVE 시점)
                    if (bestTarget != Entity.Null
                        && ccWriter.HasValue
                        && defenderCcLookup.HasComponent(attackerEntity))
                    {
                        var ccData = defenderCcLookup[attackerEntity];
                        if (ccData.knockbackDistance > 0f && ccData.knockbackDuration > 0f)
                        {
                            // Physical collision direction:
                            //   D = projectile travel (defender→enemy)
                            //   E = enemy travel (flow at enemy cell)
                            //   dir = normalize(D - E)  ← relative-velocity impulse direction
                            // Falls back to D when flow field unavailable (tests).
                            float3 D = math.normalizesafe(bestTargetPos - atkPos);
                            D.y = 0f;
                            // Guard: attacker colocated with target → D≈0 → no meaningful direction.
                            // Skip impulse (otherwise dir would degenerate to -E and push the enemy
                            // backward along its flow path, an unintended side effect).
                            if (math.lengthsq(D) > 1e-6f)
                            {
                                float3 dir;
                                if (SystemAPI.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var ff))
                                {
                                    var targetCell = GridMath.WorldToCell(bestTargetPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    int fIdx = GridMath.CellIndex(targetCell, ff.gridSize);
                                    float2 flowDir = ff.flow[fIdx];
                                    float3 E = math.normalizesafe(new float3(flowDir.x, 0, flowDir.y));
                                    dir = math.normalizesafe(D - E);
                                    if (math.lengthsq(dir) < 1e-6f)
                                        dir = D; // fallback when D == E (hit from behind)
                                }
                                else
                                {
                                    dir = D;
                                }
                                float speed = ccData.knockbackDistance / ccData.knockbackDuration;
                                ccWriter.Value.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                                {
                                    target = bestTarget,
                                    effect = new Wassup.Battle.Effects.CcEffect
                                    {
                                        kind = Wassup.Battle.Effects.CcKind.Impulse,
                                        vector = dir * speed,
                                        remainingTime = ccData.knockbackDuration,
                                    },
                                });
                            }
                        }

                        // sleep-fighter-defender — Sleep on hit: 주 타겟(bestTarget 1체)만,
                        // 넉백과 동일 스코프. 병합/해제(wake-on-hit)/게이트는 CcApply·
                        // CcClear·CcActionLock 기존 계약이 처리. 자기 히트가 자기 Sleep 을
                        // 깨우지 않는 것은 시스템 순서(damage 프레임 N, Sleep 적용 N+1)가 보장.
                        if (ccData.sleepOnHitSec > 0f)
                        {
                            ccWriter.Value.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                            {
                                target = bestTarget,
                                effect = new Wassup.Battle.Effects.CcEffect
                                {
                                    kind = Wassup.Battle.Effects.CcKind.Sleep,
                                    remainingTime = ccData.sleepOnHitSec,
                                },
                            });
                        }
                    }

                    // [Defender only] dreamcatcher-unit-trigger unit 2 — triggered card
                    // slots count once per attack RESOLVE (multi-output attacks still
                    // count 1; a resolve that lapsed with no valid target counts 0).
                    // bestTarget is guaranteed alive here: RESOLVE applies damage via
                    // the deferred IncomingDamage buffer, so nothing in this block can
                    // have destroyed it.
                    // 계약 2 — 이번 프레임에 캐스트로 이미 카운트한 host 는 제외(위 드레인 주석).
                    if (bestTarget != Entity.Null
                        && defenderTagLookup.HasComponent(attackerEntity)
                        && dcSlotLookup.HasBuffer(attackerEntity)
                        && !castCountedHosts.Contains(attackerEntity))
                    {
                        var dcSlots = dcSlotLookup[attackerEntity];
                        for (int si = 0; si < dcSlots.Length; si++)
                        {
                            var slot = dcSlots[si];
                            if (slot.trigger != Wassup.Data.DcTriggerKind.AttackN) continue;
                            // trigger-gates unit 1 — 게이트: 통과 사건만 카운트
                            // (if(GatePass){Tick} 조립). EventTarget=bestTarget 의
                            // pre-damage HP — 위 heavy pre-scan 과 동일 입력(합성 불변식).
                            if (slot.gate != Wassup.Data.DcGateKind.None)
                            {
                                if (!healthLookup.HasComponent(bestTarget)) continue;
                                var gh = healthLookup[bestTarget];
                                if (!DcTrigger.GatePass(slot.gate, slot.gateValue, gh.value, gh.max)) continue;
                            }
                            ushort dcCounter = slot.counter;
                            bool dcFired = DcTrigger.Tick(ref dcCounter, slot.period);
                            slot.counter = dcCounter;
                            dcSlots[si] = slot;
                            if (!dcFired) continue;
                            if (dcFiredWriter.HasValue) // use-flow unit 3 — 발동 신호
                                dcFiredWriter.Value.Enqueue(new DcTriggerFiredEvent { host = attackerEntity });

                            // dreamcatcher-new-abilities unit 1 — payload 디스패치. AttackN
                            // 슬롯이 발동하면 kind 별로 carrier(투사체)/CC/스택 중 하나를 실행.
                            if (slot.payload == Wassup.Data.DcPayloadKind.ProjectileToTarget)
                            {
                                // Dedicated request-carrier entity: the shooter's own
                                // attack may stage a ProjectileSpawnRequest this same
                                // frame and the request is a single IComponentData.
                                // ECB deferred creation is required — a direct
                                // EntityManager.CreateEntity inside this query foreach
                                // would throw. The carrier materializes at ecb.Playback
                                // below, before BattleBridge's drain, and the drain
                                // destroys it after spawning the projectile.
                                // owner = 부착된 디펜더(캐리어 아님) — 위협 귀속.
                                SpawnNeedleCarrier(ref ecb, slot, attackerEntity, atkPos,
                                    bestTarget, bestTargetPos,
                                    attackOutputLogWriter.HasValue,
                                    attackOutputLogWriter.HasValue ? attackOutputLogWriter.Value : default);
                            }
                            else if (slot.payload == Wassup.Data.DcPayloadKind.ApplyCcToTarget)
                            {
                                // frost_arrow — 맞은 적에게 CcEffect(번역된 ccKind). Stun 은
                                // remainingTime 만, Impulse 는 넉백 벡터(발사 시점 방향)도.
                                // 판정 대상 = 발사 시점 의도 대상 bestTarget(homing 명중 대상
                                // 불일치는 허용 — spec 계약 6).
                                if (ccWriter.HasValue)
                                {
                                    var cc = new Wassup.Battle.Effects.CcEffect
                                    {
                                        kind = slot.ccKind,
                                        remainingTime = slot.duration,
                                    };
                                    bool emit = true;
                                    if (slot.ccKind == Wassup.Battle.Effects.CcKind.Impulse)
                                    {
                                        // review B LOW1 — 공격자·대상 동일 셀이면 방향 0 →
                                        // phantom impulse(방향 없는 CC) 방출 방지(기존 넉백 가드 대칭).
                                        float3 kd = bestTargetPos - atkPos;
                                        kd.y = 0f;
                                        if (math.lengthsq(kd) > 1e-6f) cc.vector = math.normalize(kd) * slot.magnitude;
                                        else emit = false;
                                    }
                                    if (emit)
                                        ccWriter.Value.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                                        {
                                            target = bestTarget,
                                            effect = cc,
                                        });
                                }
                            }
                            else if (slot.payload == Wassup.Data.DcPayloadKind.ApplyStackToTarget)
                            {
                                // ember_bite — 맞은 적에게 원소 스택(번역된 stackKind).
                                // 스택→DoT/기타는 StackModifierTickSystem 이 ThresholdRule 로 처리.
                                if (hasStackQ)
                                    stackModSingleton.ValueRW.queue.Enqueue(new Wassup.Battle.Effects.StackModifierApplyEvent
                                    {
                                        target         = bestTarget,
                                        kind           = slot.stackKind,
                                        // review B MED2 — 상한 clamp(무경계 (byte) 캐스트는 256→0 wrap
                                        // = silent no-op). review B MED1 — maxStack 은 카드 authorable
                                        // (slot.tileRange), 미설정(0) 시에만 기존 producer 선례 5.
                                        countDelta     = (byte)math.clamp(slot.magnitude, 1f, 255f),
                                        maxStack       = slot.tileRange > 0 ? (byte)math.min(slot.tileRange, 255) : Wassup.Data.StackModifierSO.DefaultMaxStack,
                                        perAppDuration = slot.duration,
                                        source         = attackerEntity,
                                    });
                            }
                            else if (slot.payload == Wassup.Data.DcPayloadKind.HeavyStrike)
                            {
                                // dreamcatcher-heavy-strike unit 1 — 강공은 pre-scan(RESOLVE 상단)
                                // 에서 이미 heavyMul 로 산출·공격 출력에 실렸다(투사체 캐리어 /
                                // melee 곱은 unit 2). 여기서 발사할 carrier 없음. 이 케이스는
                                // 발동 슬롯이 아래 unhandled 경고에 걸리지 않게 하기 위함 —
                                // 루프의 HeavyStrike 역할은 위 Tick(카운터 소유)뿐.
                            }
                            else
                            {
                                // 발동했는데 payload arm 이 없음 = 통합 버그(신규 kind 가 arm
                                // 없이 착지). 조용히 소모하지 말고 loud fail.
                                UnityEngine.Debug.LogWarning("[AttackSystem] DcTriggerSlot fired with unhandled payload kind.");
                            }
                        }
                    }
                }

                // dreamcatcher-content-2 끝을 보는 눈 — every resolved attack (hit or strict lapse)
                // releases the lock so the next attack re-selects the current frontmost (contract 2).
                if (doResolve && wantFrontmost)
                {
                    frontmostLockLookup[attackerEntity] = new FrontmostAttackLock
                    {
                        active = false, target = Entity.Null, damageMulSnapshot = 1f, targetIsPriority = false,
                    };
                }
                if (doResolve && hasCommittedDirection)
                {
                    attack.ValueRW.committedDirection = default;
                    attack.ValueRW.hasCommittedDirection = 0;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            needleScratch.Dispose();
            targetEntities.Dispose();
            targetTransforms.Dispose();
            castCountedHosts.Dispose();
            targetFactions.Dispose();
        }

        // dreamcatcher-new-abilities unit 2 — shatter_hymn 게이트: 대상에 활성 CcEffect
        // (Stun/Sleep/Impulse/DoT, remaining>0)가 하나라도 있는가. frost(Stun)·
        // ember(Bleed→DoT) 가 건 CC 를 감지. Slow 는 CcEffect 가 아니라 여기 해당 없음.
        // ── attack-decoupling 리팩토링 — 니들 발사의 단일 창구 ──
        // 캐리어 스폰이 세 곳(RESOLVE / 폭탄 발사 / 캐스트 드레인)에 복붙돼 있었다.
        // ProjectileSpawnRequest 는 필드가 10개라, 방향탄 bounce 개통처럼 필드가
        // 하나 늘 때 사본들이 조용히 뒤처진다 — 이 spec 이 없애려던 병의 재발이다.
        // ⚠ 호출처 3곳(RESOLVE / 폭탄 발사 / 캐스트 드레인)은 전부 defender 게이트 안이라
        // 니들의 재조준 후보 풀(AttackUnitTag = 적 전용)과 진영이 맞는다. 적이 니들을 쏘게
        // 되는 날 이 전제가 깨지면 아군 오사가 되므로, 그때 후보 풀에 진영 축을 넣어야 한다.
        private static void SpawnNeedleCarrier(
            ref EntityCommandBuffer ecb, in DcTriggerSlot slot,
            Entity owner, float3 origin, Entity target, float3 targetPos,
            bool hasLog, NativeQueue<AttackOutputLogEvent>.ParallelWriter log)
        {
            var carrier = ecb.CreateEntity();
            ecb.AddComponent(carrier, new ProjectileSpawnRequest
            {
                movement = MovementKind.HomingToEntity,
                payload = PayloadKind.SingleSplash,
                target = target,
                origin = origin,
                damage = slot.magnitude, // flat — 계약 7(공격자 damageMul 미적용)
                speed = slot.speed,
                hitThreshold = slot.hitThreshold,
                visualScale = slot.visualScale,
                dataIndex = slot.projectileDataIndex,
                owner = owner,
                // 대상이 맞기 전에 죽으면 같은 반경 안에서 다시 겨눈다. 니들은 5회에
                // 한 번 나오는 자원이라 허공에 사라지면 그 주기가 통째로 버려진다.
                retargetTileRange = slot.tileRange,
            });
            ecb.AddComponent<ProjectileRequestCarrier>(carrier);
            if (hasLog)
                log.Enqueue(new AttackOutputLogEvent
                {
                    attacker  = owner,
                    kind      = Wassup.Data.AttackOutputKind.Damage,
                    magnitude = slot.magnitude,
                    duration  = 0f,
                    sourcePos = origin,
                    targetPos = targetPos,
                });
        }

        // host 가 대상을 확정해 주지 않는 아키타입(폭탄맨·캐스터)의 폴백 선정.
        // 후보 조립이 두 곳에 복붙돼 있었고, 정작 실수하기 쉬운 부분(진영 마스크·
        // 자기 제외·그리드 변환·PastGoal)이 테스트 밖에 남아 있었다 — 실제로
        // PastGoalTag 제외가 두 곳 모두에서 누락됐다(NearestTargeting 의 caller
        // 계약과 README 계약 3 을 코드가 위반한 상태였다).
        private static int PickFallbackTarget(
            NativeArray<NearestTargeting.Candidate> scratch,
            NativeArray<Entity> ents, NativeArray<LocalTransform> xf, NativeArray<FactionTag> fac,
            ComponentLookup<Wassup.Battle.Movement.PastGoalTag> pastGoal,
            Entity self, float3 selfPos, int2 selfCell,
            float tileSize, int2 gridSize, float3 gridOrigin, int tileRange)
        {
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                bool eligible = e != self
                    && ((int)fac[i].value & (int)Faction.Enemy) != 0
                    && !pastGoal.HasComponent(e); // 유출 대기 적에 니들을 낭비하지 않는다
                float3 p = xf[i].Position;
                int2 c = GridMath.WorldToCell(p, tileSize, gridSize, origin: gridOrigin);
                scratch[i] = new NearestTargeting.Candidate
                {
                    eligible = eligible,
                    tileDist = math.max(math.abs(c.x - selfCell.x), math.abs(c.y - selfCell.y)),
                    sqDist = math.distancesq(selfPos, p),
                    entityIndex = e.Index,
                    entityVersion = e.Version,
                };
            }
            return NearestTargeting.SelectNearest(scratch, tileRange);
        }

        private static bool AnyActiveCc(in DynamicBuffer<Wassup.Battle.Effects.CcEffect> buf)
        {
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].remainingTime > 0f) return true;
            return false;
        }

        private static float DistanceSqToTarget(
            float3 attackerPos,
            Entity target,
            float3 fallbackTargetPos,
            BufferLookup<BlockingHazardCellsBuffer> hazardCellsLookup,
            bool hasFlowField,
            Wassup.Battle.Effects.FlowFieldSingleton flowField,
            out float3 nearestTargetPos)
        {
            nearestTargetPos = fallbackTargetPos;
            float3 diff = fallbackTargetPos - attackerPos;
            float bestSq = diff.x * diff.x + diff.z * diff.z;

            if (!hasFlowField || !hazardCellsLookup.HasBuffer(target))
                return bestSq;

            var cells = hazardCellsLookup[target];
            for (int i = 0; i < cells.Length; i++)
            {
                float3 cellWorld = GridMath.CellToWorldCenter(cells[i].cell, flowField.tileSize, fallbackTargetPos.y, origin: flowField.origin);
                diff = cellWorld - attackerPos;
                float d2 = diff.x * diff.x + diff.z * diff.z;
                if (d2 >= bestSq) continue;
                bestSq = d2;
                nearestTargetPos = cellWorld;
            }

            return bestSq;
        }
    }
}
