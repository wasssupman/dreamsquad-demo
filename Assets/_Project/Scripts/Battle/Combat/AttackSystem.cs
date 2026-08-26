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
                // heart-stress-axis unit 6 — 본능이 살아 있는 동안 마음은 **후보가 아니다**.
                // 피해만 막으면 적이 마음 앞에 붙어 아무 일도 안 일어나는 그림이 된다(버그로 읽힌다).
                // ⚠ EnemyAiStateSystem 의 미러 쿼리에도 **같이** 넣어야 한다.
                .WithNone<CoreShielded>()
                .Build();
            var targetEntities = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            // waypoint-routing unit 4 rev 4 — Movement-owned traversal layer is read
            // once before any deferred structural changes and aligned with the existing
            // target snapshot. Targets without PathFollowState (structures/allies) use 0
            // and keep their faction-only legacy behavior.
            var targetPathLookup = SystemAPI.GetComponentLookup<PathFollowState>(isReadOnly: true);
            var targetTraversalLayers = new NativeArray<byte>(targetEntities.Length, Allocator.Temp);
            // battle-sim-extraction M0 unit 1 — 동률 해소 축. `Entity.Index/Version` 은
            // 할당기의 산물이라 신 sim 에서 재현이 불가능하다. 스냅샷과 나란한 배열로
            // 한 번만 풀어두고(랭킹 유틸은 lookup 을 모른다) 후보 조립이 그대로 읽는다.
            // 후보 아키타입(FactionTag+Health+LocalTransform)은 전부 Bridge 스폰이라
            // ID 가 붙어 있다 — Unassigned 는 그 불변식이 깨졌을 때 **맨 뒤로** 미는
            // 폴백이지 정상 경로가 아니다.
            var targetSimIds = new NativeArray<int>(targetEntities.Length, Allocator.Temp);
            var simIdLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.SimEntityId>(isReadOnly: true);
            for (int i = 0; i < targetEntities.Length; i++)
            {
                if (targetPathLookup.HasComponent(targetEntities[i]))
                    targetTraversalLayers[i] = targetPathLookup[targetEntities[i]].traversalLayers;
                targetSimIds[i] = simIdLookup.HasComponent(targetEntities[i])
                    ? simIdLookup[targetEntities[i]].value
                    : Wassup.Battle.Units.SimEntityId.Unassigned;
            }
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
            // summon-patrol-defender unit 3 — 소환사 상태(RO). current 갱신은 Bridge 드레인이 한다.
            var summonerLookup = SystemAPI.GetComponentLookup<SummonerState>(isReadOnly: true);
            var occupiedCellsLookup = SystemAPI.GetBufferLookup<OccupiedCellsBuffer>(isReadOnly: true);
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
            // leap-flight-state unit 0 rev — 도약 비행 잠금. CC 와 같은 술어에 OR 로 합류한다.
            var leapFlightLookup = SystemAPI.GetComponentLookup<LeapFlight>(isReadOnly: true);
            // dreamcatcher-content-2 끝을 보는 눈 — per-attack frontmost lock (RW, defender-owned).
            // goal-tower-siege unit 1 — PastGoal 배제가 사라져 그 lookup 도 함께 없앴다.
            var frontmostLockLookup = SystemAPI.GetComponentLookup<FrontmostAttackLock>(isReadOnly: false);
            // battle-structures unit 0 — 거점 판별 lookup 은 없다. goal-stability 의 최후순위·
            // 잠금금지 두 예외가 모두 제거되어, 거점을 타입으로 식별해 순위를 뒤집는 자리가
            // 이 시스템에 남아 있지 않다. 후보는 targetMask 로만 들어오고 거리로만 경쟁한다.

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
            NativeQueue<Wassup.Battle.Effects.AggroAcquireEvent>.ParallelWriter? aggroAcquireWriter = null;
            if (SystemAPI.TryGetSingletonRW<Wassup.Battle.Effects.AggroAcquireEventsSingleton>(out var aggroAcquireSingleton))
                aggroAcquireWriter = aggroAcquireSingleton.ValueRW.queue.AsParallelWriter();

            // nightmare-catcher unit 1 — 보스 위협 귀속 채널 + 게이트 lookup.
            // enqueue 는 피격자가 ThreatEntry 버퍼 보유(보스 베이크) && 공격자가
            // defender 일 때만 — defender 피격/일반 적 경로 무영향(회귀 격리).
            // 직접 큐 핸들 사용은 statModSingleton 선례(메인스레드 foreach).
            bool hasThreatQ = SystemAPI.TryGetSingletonRW<ThreatHitEventsSingleton>(out var threatHitSingleton);
            // skill-layer-migration unit 3a — **공격 seam 개통.** 여태 이 seam 은 자리만
            // 잡혀 있고 생산자가 0이었다(`ExecutedCountOf(Attack)` 이 항상 0).
            bool hasSkillQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Skills.SkillFiredEventsSingleton>(
                out var skillFiredSingleton);
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
                            targetEntities, targetTransforms, targetFactions, targetTraversalLayers, targetSimIds,
                            castEvt.caster, castEvt.casterPos, casterCell,
                            tileSize, gridSize, ffOrigin, slot.tileRange,
                            castEvt.targetTraversalLayers,
                            (int)Faction.EnemyUnit);
                        // pick < 0 = 반경 안에 적이 없다. 카운트는 이미 소비됐다(계약 5).
                        if (pick >= 0)
                            SpawnNeedleCarrier(ref ecb, slot, castEvt.caster, castEvt.casterPos,
                                targetEntities[pick], targetTransforms[pick].Position,
                                castEvt.targetTraversalLayers, tileSize,
                                attackOutputLogWriter.HasValue,
                                attackOutputLogWriter.HasValue ? attackOutputLogWriter.Value : default);
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────────────
            // Unified attacker loop — defenders and enemies share this single query.
            // Defender-specific branches guard on defenderTagLookup / HasComponent.
            // ─────────────────────────────────────────────────────────────────────
            foreach (var (attack, transform, attackerEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                // battle-sim-extraction M0 unit 1 — 이 공격자의 stable ID. 아래 발사 패턴
                // 난수 씨앗의 축이다(할당기 번호를 쓰던 자리).
                int attackerSimId = simIdLookup.HasComponent(attackerEntity)
                    ? simIdLookup[attackerEntity].value
                    : Wassup.Battle.Units.SimEntityId.Unassigned;

                // Tick cooldown first.
                if (attack.ValueRO.cooldownRemaining > 0f)
                {
                    attack.ValueRW.cooldownRemaining = math.max(0f, attack.ValueRO.cooldownRemaining - dt);
                }

                // combat-action-lock — Sleep/Stun: 공격 START 금지(쿨다운 틱은 위에서 유지 →
                // wake 시 즉시 공격). 이미 시작된 스윙(hitDelayRemaining>0)의 RESOLVE 는 완료.
                // leap-flight-state unit 0 rev — 도약 비행 중도 **같은 술어에 합류**한다.
                // 쿼리 `WithNone<LeapFlight>` 로 빼면 위 쿨다운 틱과 진행 중 스윙 RESOLVE 까지
                // 얼어붙어 CC 와 규약이 갈린다(CC 는 둘 다 유지하고 START 만 막는다 — 그래야
                // 깨어난 유닛이 즉시 때린다). MovementSystem 이 `locked` 한 변수에 OR 로 접은 것과
                // 같은 고도. 소비 지점이 같고 출처만 다르다.
                bool actionLocked =
                    (ccActionLookup.HasBuffer(attackerEntity)
                     && Wassup.Battle.Effects.CcActionLock.IsLocked(ccActionLookup[attackerEntity]))
                    || leapFlightLookup.HasComponent(attackerEntity);

                // bomb-thrower-defender unit 4 — 폭탄맨은 일반 타겟팅/RESOLVE 경로를 타지
                // 않으므로 여기서 처리하고 continue. CC(action-lock)는 일반 공격과 동일하게
                // 발사 START 를 막는다(폭탄 = 이 유닛의 공격, shield/hazard 급 예외 아님).
                //
                // unit 9 — 조준(DeployedFacing)과 blind bombardment 는 은퇴했다. 이제
                // **사거리 안 최근접 적이 서 있는 칸**에 던진다. 후보 선정은 니들 폴백과
                // 같은 헬퍼(NearestTargeting)라 「가까운 적」의 뜻이 한 곳에만 있다.
                // 착지 칸은 발사 시점 스냅샷이다 — 적이 걸어 나가면 빗나간다(유도 아님).
                if (bombLauncherLookup.HasComponent(attackerEntity))
                {
                    if (!actionLocked && attack.ValueRO.cooldownRemaining <= 0f
                        && projectileRefLookup.HasComponent(attackerEntity))
                    {
                        var bomb = bombLauncherLookup[attackerEntity];
                        var bProjRef = projectileRefLookup[attackerEntity];
                        float3 bPos = transform.ValueRO.Position;
                        int2 bCasterCell = GridMath.WorldToCell(bPos, tileSize, gridSize, origin: ffOrigin);
                        int bombPick = PickFallbackTarget(needleScratch,
                            targetEntities, targetTransforms, targetFactions, targetTraversalLayers, targetSimIds,
                            attackerEntity, bPos, bCasterCell,
                            tileSize, gridSize, ffOrigin,
                            GridMath.RangeToTiles(attack.ValueRO.range),
                            attack.ValueRO.targetTraversalLayers,
                            attack.ValueRO.targetMask);
                        if (bombPick >= 0)
                        {
                            int2 landCell = GridMath.WorldToCell(
                                targetTransforms[bombPick].Position, tileSize, gridSize, origin: ffOrigin);
                            float3 landWorld = GridMath.CellToWorldCenter(landCell, tileSize, 0f, origin: ffOrigin);
                            // unit 10 — 평타는 **데미지 폭탄 한 종**이다(사용자 결정 2026-08-21).
                            // 구 3종 무작위(피해/수면/기절 균등 1/3)와 그 캐스터별 rng 는 은퇴 —
                            // 무엇이 떨어질지 모르는 유닛은 플레이어가 계획을 세울 수 없었다.
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
                                damage = bomb.dmgBombDamage,
                                dataIndex = bProjRef.dataIndex,
                                visualScale = bProjRef.visualScale,
                                owner = attackerEntity,
                                targetFaction = ProjectileTargetFaction.Enemy,
                                targetTraversalLayers = attack.ValueRO.targetTraversalLayers,
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
                                    // 니들은 적 **유닛**만 찌른다 — 폭탄의 본 공격 마스크와
                                    // 별개다(그쪽은 거점도 노린다, unit 9).
                                    int pick = PickFallbackTarget(needleScratch,
                                        targetEntities, targetTransforms, targetFactions, targetTraversalLayers, targetSimIds,
                                        attackerEntity, bPos, bCasterCell,
                                        tileSize, gridSize, ffOrigin, slot.tileRange,
                                        attack.ValueRO.targetTraversalLayers,
                                        (int)Faction.EnemyUnit);
                                    // pick < 0 = 반경 안에 적이 없다. 카운트는 이미 소비됐다(계약 5).
                                    if (pick >= 0)
                                        SpawnNeedleCarrier(ref ecb, slot, attackerEntity, bPos,
                                            targetEntities[pick], targetTransforms[pick].Position,
                                            attack.ValueRO.targetTraversalLayers, tileSize,
                                            attackOutputLogWriter.HasValue,
                                            attackOutputLogWriter.HasValue ? attackOutputLogWriter.Value : default);
                                }
                            }
                            // unit 9 — 던진 프레임에만 리셋한다. 사거리에 적이 없으면 쿨다운을
                            // 만료 상태로 **대기**시켜(소환사의 닫힌 게이트와 같은 규율) 적이
                            // 들어온 프레임에 즉시 투척한다. 여기서 리셋하면 최대 한 쿨 늦는다.
                            attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration;
                        }
                    }
                    continue;
                }

                // summon-patrol-defender unit 3 — 소환사는 **타겟을 고르지 않고** 순찰병을
                // 유지한다. 폭탄맨과 같은 자리(타겟 선정 앞)에서 처리하고 continue —
                // 타겟을 요구하는 RESOLVE 에 두면 소환사 attackRange(근접 1타일) 안에 적이
                // 들어와야만 소환돼, 순찰병이 마중 나갈 시간이 없어진다.
                //
                // 단 **첫 소환에만 거점 구역 게이트**가 걸린다(계약 8) — 폭탄맨의 blind
                // bombardment 를 그대로 따르지 않는 지점이다. 상세는 아래 게이트 블록.
                if (summonerLookup.HasComponent(attackerEntity))
                {
                    if (!actionLocked && attack.ValueRO.cooldownRemaining <= 0f)
                    {
                        var summoner = summonerLookup[attackerEntity];
                        // 계약 9 — 양방향 대칭 생존 술어. `current != Entity.Null` 만 보면
                        // 파괴된 순찰병의 stale 핸들로 소환사가 영구 대기한다.
                        bool alivePatrol = summoner.current != Entity.Null
                            && SystemAPI.Exists(summoner.current)
                            && !deadLookup.HasComponent(summoner.current)
                            && healthLookup.HasComponent(summoner.current)
                            && healthLookup[summoner.current].value > 0f;

                        // 초회 게이트 — 첫 순찰병은 **담당 구역 안에 적이 있을 때만** 낸다
                        // (사용자 결정 2026-08-03). 구역 술어는 PatrolAreaMath 가 단독 소유한다.
                        //
                        // unit 9 — 중심 = 소환사 셀, 반경 = 이 유닛의 공격범위. 즉 소환사는
                        // «사거리에 적이 들면 공격(=소환)»하는 평범한 유닛이고, 게이트가 보는
                        // 박스와 순찰병이 지킬 박스와 배치 프리뷰가 칠한 박스가 **같은 하나**다.
                        float3 sPos = transform.ValueRO.Position;
                        int2 sCell = GridMath.WorldToCell(sPos, tileSize, gridSize, origin: ffOrigin);
                        int coverTiles = GridMath.RangeToTiles(attack.ValueRO.range);
                        bool gateOpen = summoner.hasSummonedOnce;
                        if (!gateOpen && !alivePatrol && summoner.patrolDataIndex >= 0)
                        {
                            for (int ti = 0; ti < targetEntities.Length && !gateOpen; ti++)
                            {
                                if (((int)targetFactions[ti].value & (int)Faction.EnemyUnit) == 0) continue;
                                if (!Wassup.Data.PlacementLayers.CanTarget(
                                        attack.ValueRO.targetTraversalLayers,
                                        targetTraversalLayers[ti])) continue;
                                // goal-tower-siege unit 1 — PastGoal 배제 제거(머지 정리).
                                // 그 태그는 이제 "유출 대기" 가 아니라 "골에 붙어 타워를 때리는 중" 이다 —
                                // 골을 두들기는 적이야말로 순찰을 부를 이유다.
                                int2 eCell = GridMath.WorldToCell(
                                    targetTransforms[ti].Position, tileSize, gridSize, origin: ffOrigin);
                                if (Wassup.Battle.Effects.PatrolAreaMath.IsInArea(eCell, sCell, coverTiles))
                                    gateOpen = true;
                            }
                        }

                        if (gateOpen && !alivePatrol && summoner.patrolDataIndex >= 0)
                        {
                            var carrier = ecb.CreateEntity();
                            ecb.AddComponent<PatrolRequestCarrier>(carrier);
                            ecb.AddComponent(carrier, new PatrolSpawnRequest
                            {
                                owner = attackerEntity,
                                ownerCell = sCell,   // 게이트 판정과 **같은 셀**이어야 한다
                                patrolDataIndex = summoner.patrolDataIndex,
                                coverTileRadius = coverTiles,
                            });
                            // 소환 = 이 유닛의 공격 사건. 애니/SFX 는 여기서 신호한다.
                            if (attackWriter.HasValue)
                                attackWriter.Value.Enqueue(new UnitAttackVisualEvent
                                {
                                    attacker = attackerEntity,
                                    targetWorld = sPos,
                                    attackAnimPeriod = attack.ValueRO.cooldownDuration,
                                });
                        }

                        // 게이트가 닫혀 있으면 **쿨다운을 리셋하지 않는다** — 만료 상태로 대기하다
                        // 적이 구역에 들어온 프레임에 즉시 소환한다("구역에 들어오면 부른다"가
                        // 규칙이므로 여기서 리셋하면 최대 한 쿨 늦게 반응한다). 그 대가로 게이트가
                        // 닫힌 소환사는 매 프레임 타겟 스냅샷을 훑는다 — 진영 미스매치를 즉시
                        // continue 하는 짧은 루프이고 소환사 수도 적어 수용한다. 게이트는 첫
                        // 소환 한 번뿐이라 이 상태가 판 내내 지속되지도 않는다.
                        //
                        // 게이트가 열렸으면 성사 여부와 무관하게 리셋한다(스냅 실패로 취소된
                        // 경우 포함) — 요청을 stage 한 프레임에 이미 리셋되므로 드레인이 한
                        // 프레임 늦어도 중복 소환이 나올 수 없다.
                        if (gateOpen) attack.ValueRW.cooldownRemaining = attack.ValueRO.cooldownDuration;
                    }
                    continue;
                }

                // Find nearest in-range target allowed by this attacker's mask.
                float3 atkPos = transform.ValueRO.Position;
                int tileRange = GridMath.RangeToTiles(attack.ValueRO.range);
                int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);
                // 사거리 2차 게이트(물리 거리)는 **둘 다 연속 이동**일 때만 — AttackReach 주석.
                bool attackerIsContinuous = targetPathLookup.HasComponent(attackerEntity);
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = default;
                int mask = attack.ValueRO.targetMask;
                // healer-lowest-health-targeting — an ally-targeting defender (targetAllies →
                // mask == Defender, baked in BattleBridge) heals the most-hurt ally, not the
                // nearest. Gated on DefenderUnitTag so a taunted enemy (also mask == Defender
                // via TauntAttackGrantSystem) keeps nearest targeting. Same candidate set as
                // the nearest scan — only the ranking criterion changes (distance → HP ratio).
                bool rankByHealth = mask == (int)Faction.DefenderUnit
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
                // target-persistence 계약 6 — 락이 **아직 합법 후보인가**를 이 루프에서 함께 본다.
                //
                // 락 이전엔 bestTarget 이 매 프레임 마스크로 재선정돼 «마스크 밖 대상을 계속
                // 때린다»가 구조적으로 불가능했다. 락이 그 재선정을 건너뛰므로 런타임 마스크
                // 변경(도발 해제의 previousTargetMask 원복)이 반영되지 않는다.
                //
                // **새 ComponentLookup 을 쓰지 않는다.** FactionTag lookup 을 추가했더니
                // AttackSystem 전체가 `ObjectDisposedException: EntityTypeHandle invalidated by
                // a structural change` 로 무너졌다(EditMode 25건). 이 루프가 **이미 마스크를
                // 거르고 있으므로** 여기서 표시하면 조회도 스캔도 추가되지 않는다.
                //
                // 후보 스냅샷은 DeadTag·PendingDeployment·UltimateLeapState 를 이미 제외한다 —
                // 그래서 이 플래그는 «마스크 통과»보다 조금 강한 «이번 프레임 합법 후보»다.
                // 이탈(판 밖) 보스를 문 락이 풀리는 것도 그 귀결이며 의도한 방향이다.
                Entity lockedNow = focusLookup.HasComponent(attackerEntity)
                    ? focusLookup[attackerEntity].current : Entity.Null;
                bool lockStillCandidate = false;

                for (int i = 0; i < targetEntities.Length; i++)
                {
                    if (((int)targetFactions[i].value & mask) == 0) continue;
                    if (!Wassup.Data.PlacementLayers.CanTarget(
                            attack.ValueRO.targetTraversalLayers,
                            targetTraversalLayers[i])) continue;
                    if (targetEntities[i] == lockedNow) lockStillCandidate = true;
                    if (targetEntities[i] == attackerEntity) continue;
                    int cclass = defenderClassLookup.HasComponent(targetEntities[i])
                        ? (int)defenderClassLookup[targetEntities[i]].value : -1;
                    if (hasFilter && cclass >= 0 && (filterMask & (1 << cclass)) == 0) continue; // class not allowed
                    float3 targetPos = targetTransforms[i].Position;
                    int2 tgtCell = GridMath.WorldToCell(targetPos, tileSize, gridSize, origin: ffOrigin);
                    if (!AttackReach.InReach(atkCell, tgtCell, tileRange, atkPos, targetPos, tileSize,
                            attackerIsContinuous && targetPathLookup.HasComponent(targetEntities[i]))) continue;
                    float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetPos, occupiedCellsLookup, hasFlowField, flowField, out var nearestPos);
                    // battle-structures unit 0 — 거점에 대한 타입 기반 특별 취급은 없다.
                    // 마스크에 들어온 후보는 종류를 묻지 않고 **거리로만** 경쟁한다.
                    // «이 공격자가 거점을 우선하나» 는 공격자 쪽 저작이 정할 문제이고
                    // (unit 1, EnemyTargetFilter), 술어가 타입을 보고 순위를 뒤집지 않는다.
                    // 방어측 지원 마스크가 DefenderUnit 단독이라 거점은 힐러·버프 후보에
                    // 애초에 들지 않는다(후보 루프 진입부의 targetMask 필터) — 버퍼 부재 예외는 그쪽에서 막힌다.
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
                            simId = targetSimIds[i],
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
                    // distance, excluding unreachable cells.
                    //
                    // goal-tower-siege unit 1 — PastGoal 배제를 **뺐다**. 그 태그는 이제
                    // "유출 대기(곧 사라짐)" 가 아니라 "골에 붙어 타워를 때리는 중" 이다 —
                    // 경로상 가장 앞선 적이 곧 frontmost 라는 정의에 정확히 부합한다.
                    if (wantFrontmost)
                    {
                        int fdist = FrontmostTargeting.UnreachableDist;
                        if (hasFlowField
                            && tgtCell.x >= 0 && tgtCell.x < gridSize.x
                            && tgtCell.y >= 0 && tgtCell.y < gridSize.y)
                        {
                            // traversal-layers unit 1a — 슬롯 뷰(직접 인덱싱 금지).
                            fdist = flowField.DistSlot(FlowFieldSingleton.PrimarySlot)[GridMath.CellIndex(tgtCell, gridSize)];
                        }
                        if (fdist != FrontmostTargeting.UnreachableDist)
                        {
                            var fc = new FrontmostTargeting.Candidate
                            {
                                flowDist = fdist,
                                sqDist = d2,
                                simId = targetSimIds[i],
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
                // above nearest/priority).
                //
                // ⚠ target-persistence unit 2 로 계약이 바뀌었다. 예전 주석은 *"죽거나 사라질
                // 때까지 유지, 사거리는 발사만 게이팅하고 락은 유지"* 였는데 **사거리 이탈도
                // 이제 해제 사유다**(D2). 예전 거동은 이탈한 적이 락을 붙든 채 발사를 보류하고
                // FSM 이 Marching 으로 떨어져 **옆에 방어유닛을 두고 골로 걸어가는** 버그였다.
                // unit 3 — 게이트가 `!= None` 이다(구 `== FocusUntilDead`). `Nearest` 4종도
                // 락을 받는다 — **보스 2종 포함**(D4: "한 놈 타겟되면 한 놈만 팬다").
                // BossTag 분기를 넣지 **않는 것**이 그 결정의 구현이다.
                // ⚠ EnemyAiStateSystem 미러의 게이트와 **항상 같아야 한다**(계약 4).
                if (behaviorLookup.HasComponent(attackerEntity)
                    && behaviorLookup[attackerEntity].targetMode != Wassup.Data.EnemyTargetMode.None
                    && focusLookup.HasComponent(attackerEntity))
                {
                    // unit 3 (D5) — 행동정지 CC 중엔 락을 비우고 **재잠금도 건너뛴다**.
                    // 깨어나는 프레임에 비어 있으므로 자연히 새로 고른다. «해제 순간»을 잡지
                    // 않는 이유: actionLocked 는 continue 하지 않아 CC 중에도 이 사슬이 돌기
                    // 때문에 전이 감지용 상태가 필요 없다. CC 중엔 START 자체가 막혀 있어
                    // 비워도 잃는 것이 없다.
                    //
                    // ⚠ **else 로 감싸는 것이 핵심이다.** 비우기만 하고 아래로 흘리면 해제
                    // 분기가 그 프레임의 최근접으로 **즉시 다시 잠근다** — 초판이 그랬고
                    // `Cc_ClearsTheLock_WhileActionLocked` 가 빨갛게 잡았다.
                    //
                    // committedTarget(한 공격 안의 커밋)은 건드리지 않는다 — 진행 중 스윙은
                    // 겨눈 대상에 꽂히고 끝난다(기존 계약). 층이 다르다.
                    if (actionLocked)
                    {
                        focusLookup[attackerEntity] = new FocusTarget { current = Entity.Null };
                    }
                    else
                    {
                        Entity cur = focusLookup[attackerEntity].current;
                        bool curValid = cur != Entity.Null
                            && lockStillCandidate          // 계약 6 — 마스크 밖으로 나가면 놓는다
                            && healthLookup.HasComponent(cur) && healthLookup[cur].value > 0f
                            && !deadLookup.HasComponent(cur);
                        // target-persistence unit 2 — 유지 여부는 TargetPersistence 가 정한다
                        // (EnemyAiStateSystem 미러와 **같은 함수**. 두 벌이면 데드락이 재발한다).
                        bool keepLock = false;
                        float3 curPos = bestTargetPos;
                        if (curValid)
                        {
                            curPos = aggroTransformLookup.HasComponent(cur)
                                ? aggroTransformLookup[cur].Position : bestTargetPos;
                            int2 cCell = GridMath.WorldToCell(curPos, tileSize, gridSize, origin: ffOrigin);
                            int cDist = math.max(math.abs(cCell.x - atkCell.x), math.abs(cCell.y - atkCell.y));
                            // 락 유지도 **선정과 같은 술어**를 지나야 한다. 셀만 보면 락을 문 뒤로는
                            // 2차 게이트가 영영 적용되지 않고, EnemyAiState 쪽 미러(같은 락 블록에
                            // AttackReach 를 건다)와 갈려 «쏘면서 골로 걸어가는» 상태가 된다.
                            keepLock = AttackReach.InReach(atkCell, cCell, tileRange, atkPos, curPos, tileSize,
                                           attackerIsContinuous && targetPathLookup.HasComponent(cur))
                                       && TargetPersistence.KeepsLock(true, cDist, tileRange);
                        }

                        if (keepLock)
                        {
                            bestTarget = cur; bestTargetPos = curPos;
                            focusLookup[attackerEntity] = new FocusTarget { current = cur };
                        }
                        else
                        {
                            // 사망/소멸 **또는 사거리 이탈**(D2) → 락 해제.
                            // 예전엔 이탈 시 bestTarget=Null 로 발사만 보류하고 락을 재저장했다.
                            // 그 결과 EnemyAiStateSystem 미러가 Marching 을 반환해 **옆에 방어유닛을
                            // 두고 골로 걸어갔다**(B2). 이제 이미 계산된 pick 을 그대로 채택한다.
                            // invalid lock → adopt the already-computed nearest+filter result (may be Null)
                            //
                            // battle-structures unit 0 — goal-stability 리뷰 M3 의 «거점은 잠금 대상이
                            // 아니다» 예외를 제거했다. 거점을 타입으로 특별 취급하는 자리를 남기지
                            // 않는다는 결정이고(2026-08-09 사용자 확정), 락 유지·해제는
                            // TargetPersistence.KeepsLock 하나가 소유한다 — 죽거나 사거리를 벗어나면
                            // 놓는다. FocusUntilDead 가 거점을 물면 그것이 죽을 때까지 유지되는 것이
                            // 그 저작 모드의 의미다.
                            focusLookup[attackerEntity] = new FocusTarget { current = bestTarget };
                        }
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
                        // goal-tower-siege unit 1 — PastGoal 은 더 이상 락 해제 사유가 아니다
                        // (골에 붙은 적은 살아서 계속 유효한 대상이다).
                        bool ltValid = lt != Entity.Null
                            && healthLookup.HasComponent(lt) && healthLookup[lt].value > 0f
                            && !deadLookup.HasComponent(lt);
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

                // ═══ target-persistence unit 4 — 방어유닛 지속 락 (원칙 1의 본체) ═══
                //
                // 방어유닛은 매 프레임 최근접을 재계산해 왔다. 자기는 고정인데 **적이 계속
                // 흘러가므로 최근접이 매 순간 바뀐다** — unit 3(적 락)이 `Halt` 적에겐 거의
                // 무효였던 것과 정반대로, 락이 실제로 일하는 자리다. 동거리 flip-flop 도
                // 여기서 함께 사라진다(매 프레임 재선정이 없어지면 진동의 원인이 없다).
                //
                // **이 자리인 이유** — frontmost 직후, unit 0 커밋 유지 **직전**:
                //   스윙 진행 중  → unit 0 커밋이 이긴다(한 공격 안에서는 안 바뀐다)
                //   스윙 사이     → 이 락이 이긴다(공격들 사이에서도 안 바뀐다)
                // spec README 표의 «facing override 직후»는 **틀렸다** — 그 자리면 wind-up
                // 커밋을 덮어써 B1(A 겨누고 B 때림)이 부분 부활한다(unit 4 문서 §정정).
                //
                // **제외 4종은 누락이 아니라 계약이다**(계약 2):
                //   facing        — 레인 witness 는 타겟이 아니라 발사 게이트다
                //   frontmost     — 「끝을 보는 눈」 카드 계약이 "매 공격마다 지금의 최전방"
                //   힐러          — lowest-health 재랭킹이 정체성
                //   가디언(D1)    — 어그로 자석이 "아직 어그로 안 걸린 적 우선"으로 신규 팩을
                //                   흡수한다. primary 를 고정하면 자석이 죽는다
                // 넷 중 하나라도 "빠뜨렸네" 하고 채우면 그 유닛의 정체성이 조용히 망가진다.
                //
                // `!EnemyBehavior` — 순찰병은 적 AI 스택을 물려받아 **unit 3 블록**이 이미
                // 처리한다. 여기서 비켜주지 않으면 한 프레임에 두 번 잠근다.
                if (defenderTagLookup.HasComponent(attackerEntity)
                    && focusLookup.HasComponent(attackerEntity)
                    && !behaviorLookup.HasComponent(attackerEntity)
                    && !hasFacing
                    && !wantFrontmost
                    && !rankByHealth
                    && !aggroCapacityLookup.HasComponent(attackerEntity))
                {
                    // D5 — CC 중엔 비우고 재잠금도 건너뛴다(unit 3 과 **같은 형태**).
                    // else 로 감싸지 않으면 해제 분기가 그 프레임 최근접으로 즉시 다시 잠근다.
                    if (actionLocked)
                    {
                        focusLookup[attackerEntity] = new FocusTarget { current = Entity.Null };
                    }
                    else
                    {
                        Entity dcur = focusLookup[attackerEntity].current;
                        bool dcurValid = dcur != Entity.Null
                            && lockStillCandidate          // 계약 6 — 적 락과 같은 규칙
                            && healthLookup.HasComponent(dcur) && healthLookup[dcur].value > 0f
                            && !deadLookup.HasComponent(dcur);
                        bool dKeep = false;
                        float3 dcurPos = bestTargetPos;
                        if (dcurValid)
                        {
                            dcurPos = aggroTransformLookup.HasComponent(dcur)
                                ? aggroTransformLookup[dcur].Position : bestTargetPos;
                            int2 dCell = GridMath.WorldToCell(dcurPos, tileSize, gridSize, origin: ffOrigin);
                            int dDist = math.max(math.abs(dCell.x - atkCell.x), math.abs(dCell.y - atkCell.y));
                            dKeep = AttackReach.InReach(atkCell, dCell, tileRange, atkPos, dcurPos, tileSize,
                                        attackerIsContinuous && targetPathLookup.HasComponent(dcur))
                                    && TargetPersistence.KeepsLock(true, dDist, tileRange);
                        }
                        if (dKeep)
                        {
                            bestTarget = dcur; bestTargetPos = dcurPos;
                            focusLookup[attackerEntity] = new FocusTarget { current = dcur };
                        }
                        else
                        {
                            // 사망 ∨ 사거리 이탈 → 해제하고 이미 계산된 pick 을 채택한다.
                            // 거점을 특별 취급하지 않는다 — battle-structures unit 0 이 그 예외를
                            // 제거했다(2026-08-09 사용자 확정). 물면 죽거나 벗어날 때까지 유지.
                            focusLookup[attackerEntity] = new FocusTarget { current = bestTarget };
                        }
                    }
                }

                // target-persistence unit 0 — 공격 1회 타겟 커밋. wind-up 중에는 START 에서
                // 겨눈 대상을 유지한다(판정은 frontmost 블록과 **같은 규칙** — 생존 + 사거리,
                // 실패면 strict lapse 로 재선정하지 않는다).
                //
                // 이 자리인 이유:
                //  · 어그로(:672~)보다 뒤 + `!aggroed` 게이트 — 사용자 원칙 2가 «어그로 끌림»을
                //    변경 사유로 명시했다. 어그로가 이겨야 한다.
                //  · frontmost(:691~)보다 뒤 + `!wantFrontmost` 게이트 — 그쪽이 이미 같은 일을
                //    한다(중복 방지).
                //  · facing override(아래)보다 **앞** — 레인 witness 는 타겟이 아니라 발사
                //    게이트라 그쪽이 이겨야 한다. 뒤에 있으므로 자동으로 그렇게 된다.
                if (!wantFrontmost
                    && !aggroLookup.HasComponent(attackerEntity)
                    && attack.ValueRO.hitDelayRemaining > 0f
                    && attack.ValueRO.hasCommittedTarget != 0)
                {
                    Entity ct = attack.ValueRO.committedTarget;
                    // PastGoalTag 는 해제 사유가 아니다 — 골에 붙은 적은 살아 있는 유효 대상
                    // (goal-tower-siege unit 1 선례, frontmost 블록과 동일).
                    bool ctValid = ct != Entity.Null
                        && healthLookup.HasComponent(ct) && healthLookup[ct].value > 0f
                        && !deadLookup.HasComponent(ct);
                    if (ctValid)
                    {
                        float3 ctPos = aggroTransformLookup.HasComponent(ct)
                            ? aggroTransformLookup[ct].Position : bestTargetPos;
                        int2 ctCell = GridMath.WorldToCell(ctPos, tileSize, gridSize, origin: ffOrigin);
                        if (AttackReach.InReach(atkCell, ctCell, tileRange, atkPos, ctPos, tileSize,
                                attackerIsContinuous && targetPathLookup.HasComponent(ct)))
                        { bestTarget = ct; bestTargetPos = ctPos; }
                        else bestTarget = Entity.Null;   // 사거리 이탈 → lapse
                    }
                    else bestTarget = Entity.Null;       // 사망/소멸 → lapse
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

                        // target-persistence unit 0 — 겨눈 **대상**도 같이 커밋한다. 방향
                        // 커밋(위)이 "이번 발사 기준축"을 지키듯 이것은 "이번 발사 대상"을
                        // 지킨다. hitDelaySec == 0(즉시 RESOLVE)이어도 저장했다가 같은
                        // 프레임에 해제한다 — 분기를 늘리지 않는다.
                        attack.ValueRW.committedTarget = bestTarget;
                        attack.ValueRW.hasCommittedTarget = 1;

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
                    // dreamcatcher-content-4 unit 4 (악몽 사냥) — 잠든 적에게만 붙는 상시 배율.
                    // 여기서는 **공격자 쪽 값만** 접는다. 실제 곱은 아래 두 피해 지점에서
                    // 그 victim 이 자고 있을 때만 일어난다(shatter_hymn 과 같은 형태) —
                    // 판정이 피해자별이라 잠든 적 옆의 깨어 있는 적은 기준값 그대로다.
                    // 복수 부착은 곱으로 중첩(ProjectileBounce 의 damageMul 집계와 같은 관례).
                    // HasBuffer 가 먼저인 이유: 이 버퍼는 카드를 실제로 부착한 방어유닛에만
                    // 존재하므로(bake 가 DefenderUnitTag 를 요구) 대부분의 공격자는 여기서
                    // 즉시 빠져 추가 비용이 0 이다 — defender 태그를 따로 볼 필요가 없다.
                    float dcSleepMul = 1f;
                    if (dcAttackModLookup.HasBuffer(attackerEntity))
                    {
                        var sleepMods = dcAttackModLookup[attackerEntity];
                        for (int si = 0; si < sleepMods.Length; si++)
                        {
                            if (sleepMods[si].kind != Wassup.Data.DcAttackModKind.DamageVsSleeping) continue;
                            dcSleepMul *= sleepMods[si].damageMul;
                        }
                    }
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

                    // defender-knockback-on-impact unit 1 — 이 공격의 넉백을 **착탄까지 미뤘나.**
                    // 직격 victim 이 있는 유도탄에서만 켜지며, 그때 실제 발동은
                    // ProjectileHitSystem 의 SingleSplash 분기가 한다.
                    bool knockbackAtImpact = false;

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
                                    // 악몽 사냥 — 같은 스냅샷 기준(발사 시점 의도 대상)이되
                                    // **Sleep 만** 본다. CC 전반으로 넓히면 기절/출혈한 적까지
                                    // 배율돼 shatter_hymn 과 구분이 사라진다.
                                    if (bestTarget != Entity.Null
                                        && dcSleepMul != 1f
                                        && ccActionLookup.HasBuffer(bestTarget)
                                        && AnyActiveSleep(ccActionLookup[bestTarget]))
                                        amount *= dcSleepMul;
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
                                    targetTraversalLayers = attack.ValueRO.targetTraversalLayers,
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
                                    targetTraversalLayers = attack.ValueRO.targetTraversalLayers,
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
                                        // battle-sim-extraction M0 unit 1 — 씨앗 축이
                                        // `attackerEntity.Index` 였다. 할당기 번호가 난수열을
                                        // 정하면 신 sim 이 같은 탄막을 못 낸다. `SimEntityId` 는
                                        // 스폰 순서라 재현된다(같은 판·같은 스폰 순서 = 같은 열).
                                        PatternShotRandomizer.Apply(
                                            ref spec,
                                            math.hash(new int2(attackerSimId, slot.fireCountBase)));

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
                                        patternTemplate.targetTraversalLayers = template.targetTraversalLayers;
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
                                // defender-knockback-on-impact unit 1 — 유도탄은 직격 victim 이
                                // 있으므로 넉백을 착탄까지 넘긴다. TileAoe 같은 직격 없는 payload 는
                                // 넘길 대상이 없어 기존대로 발사 시점에 건다(조용히 사라지지 않게).
                                if (projRef.payload == PayloadKind.SingleSplash)
                                    knockbackAtImpact = true;

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
                                    targetTraversalLayers = attack.ValueRO.targetTraversalLayers,
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
                            // elite-whirlpot unit 0 — ★**어그로는 primary 선정만 지배한다.**
                            // 예전엔 `aggro-targeting` unit 8(MEDIUM 2)이 어그로된 적의
                            // `attackTargetCount` 를 1 로 접었다. 근거는 「sticky 적은 가디언만
                            // 때린다」였지만, 계약 4 가 말한 것은 «타겟»(**단수**) = primary 이고
                            // 광역 폭까지 줄인 것은 그 확장 해석이었다 — 테스트도 없었다.
                            //
                            // 그 확장이 만든 결과는 도발과 무관했다: **어그로가 적의 공격 «형태» 를
                            // 바꿨다.** 광역 적이 붙잡히면 단일 적이 되어 안 붙잡았을 때보다 **덜**
                            // 때렸다(숨은 방어 버프). 폭은 이제 어그로와 무관하다.
                            //
                            // ⚠ **sticky primary override(unit 5)는 그대로다** — 어그로면 bestTarget
                            // = 링크 가디언이고 사거리 밖이면 미발사다. 그 배타성은 load-bearing:
                            // 「가디언 없으면 최근접」으로 풀면, 가디언에게 걸어가는 도중 옆 방어유닛이
                            // 사거리에 들어오는 순간 `EngageMovement.Halt` 로 멈춰 싸우고 가디언에
                            // **영영 도착하지 않는다.** 고정 = `AggroAoeWidthTests`.
                            int desiredCount = math.max(1, attack.ValueRO.attackTargetCount);
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
                                    if (!Wassup.Data.PlacementLayers.CanTarget(
                                            attack.ValueRO.targetTraversalLayers,
                                            targetTraversalLayers[i])) continue;
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
                                            if (!Wassup.Data.PlacementLayers.CanTarget(
                                                    attack.ValueRO.targetTraversalLayers,
                                                    targetTraversalLayers[i])) continue;
                                            if (targetEntities[i] == attackerEntity) continue;
                                            int2 tgtCellAoE = GridMath.WorldToCell(targetTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                                            int tileDistAoE = math.max(math.abs(tgtCellAoE.x - atkCell.x), math.abs(tgtCellAoE.y - atkCell.y));
                                            if (tileDistAoE > tileRange) continue;
                                            float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position, occupiedCellsLookup, hasFlowField, flowField, out _);
                                            if (rankByHealth)
                                            {
                                                var h = healthLookup[targetEntities[i]];
                                                var hc = new LowestHealthTargeting.Candidate
                                                {
                                                    hpRatio = Wassup.Battle.Units.Health.ComputeRatio(h.value, h.max),
                                                    sqDist = d2,
                                                    simId = targetSimIds[i],
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
                                            // 악몽 사냥 — 이 hitTarget 이 «자고 있을 때만». 같은
                                            // 공격의 cleave 로 함께 맞은 깨어 있는 적은 기준값
                                            // 그대로다. 이 자리가 강공(HeavyStrike, 전 victim
                                            // 배율)과 갈리는 지점이라 카드가 사양 초과가 아니다.
                                            if (dcSleepMul != 1f && ccActionLookup.HasBuffer(hitTarget) && AnyActiveSleep(ccActionLookup[hitTarget]))
                                                dmg *= dcSleepMul;
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
                            if (isGuardian && aggroAcquireWriter.HasValue)
                            {
                                for (int ti = 0; ti < hitCount; ti++)
                                    aggroAcquireWriter.Value.Enqueue(new Wassup.Battle.Effects.AggroAcquireEvent
                                    {
                                        guardian = attackerEntity,
                                        enemy = hitTargets[ti],
                                        // 명중 획득 — 상한·선점 게이트를 전부 통과해야 붙고 무기한이다.
                                        kind = Wassup.Battle.Effects.AggroAcquireKind.Hit,
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
                        if (!knockbackAtImpact
                            && ccData.knockbackDistance > 0f && ccData.knockbackDuration > 0f)
                        {
                            // defender-knockback-on-impact unit 1 (사용자 결정 B, 2026-08-17) —
                            // 미는 방향은 **적이 가던 방향의 반대** 하나다.
                            //
                            // [은퇴] 이전엔 상대속도 합이었다: D(사수→적) − E(적 진행). 그 식은
                            // 적이 사수를 **지나쳐 멀어질 때** 두 성분이 상쇄돼 무너진다 —
                            // 근처에선 옆으로 밀리고, 정확히 일직선이면 폴백이 D 로 떨어져
                            // **적의 진행 방향 = 골 쪽으로 밀어준다.** 게다가 E 를 흐름장의
                            // PrimarySlot 에서 읽어서 비행 적·추격 중인 적은 애초에 틀린 방향이었다.
                            //
                            // 디펜스에서 「밀어낸다」는 사수가 어디 서 있든 같은 뜻이어야 한다.
                            // 진행 방향은 이제 Movement 가 관측해 기록한다(PathFollowState.lastMoveDir).
                            // 방향이 없는 대상(스폰 직후·고정 구조물)은 밀지 않는다.
                            float2 travel = targetPathLookup.HasComponent(bestTarget)
                                ? targetPathLookup[bestTarget].lastMoveDir
                                : float2.zero;
                            if (math.lengthsq(travel) > 1e-6f)
                            {
                                float2 kb = -math.normalize(travel)
                                            * (ccData.knockbackDistance / ccData.knockbackDuration);
                                ccWriter.Value.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent
                                {
                                    target = bestTarget,
                                    effect = new Wassup.Battle.Effects.CcEffect
                                    {
                                        kind = Wassup.Battle.Effects.CcKind.Impulse,
                                        vector = new float3(kb.x, 0f, kb.y),
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

                    // dreamcatcher-unit-trigger unit 2 — triggered slots count once per attack
                    // RESOLVE (multi-output attacks still count 1; a resolve that lapsed with no
                    // valid target counts 0). bestTarget is guaranteed alive here: RESOLVE applies
                    // damage via the deferred IncomingDamage buffer, so nothing in this block can
                    // have destroyed it.
                    // 계약 2 — 이번 프레임에 캐스트로 이미 카운트한 host 는 제외(위 드레인 주석).
                    //
                    // elite-enemy-tier unit 3 — ★**진영 게이트를 뺐다.** 예전엔 `[Defender only]`
                    // 로 `defenderTagLookup` 을 요구해서 적은 AttackN 을 영영 못 썼다(드래곤의
                    // 3타 브레스가 이것 없이는 성립하지 않는다). 새 술어는 **버퍼 존재**뿐이다 —
                    // 슬롯이 붙은 것 자체가 «이 유닛은 트리거를 갖는다» 는 선언이고, 적에게 슬롯을
                    // 붙이는 유일한 경로는 `BakeNightmareMechanics`(= 저작된 메커닉)다.
                    // 진영별 분기를 새로 만들지 않는 것이 요점이다.
                    //
                    // ⚠ 같은 파일의 다른 `defenderTagLookup` 게이트 7곳은 **건드리지 않았다**
                    // (힐 대상 랭킹 · frontmost · 포커스 · HeavyStrike pre-scan · bounce ·
                    // 위협 귀속 · 공격 시작 타이밍). 전부 방어유닛 전용이어야 하는 기능이다.
                    if (bestTarget != Entity.Null
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
                            // ★연출 신호는 **방어유닛 한정**으로 남긴다(elite-enemy-tier unit 3).
                            // 이 큐의 드레인(BattleBridge.DrainDcTriggerFiredEvents)은 카드 행
                            // 펄스에서 끝나지 않고 `spineUnitPool.TryGet(host)` 로 뷰를 찾아
                            // `PlayPunch` + `FlashWhite` + «카드 흡수» VFX 를 낸다. 적도 같은 풀에
                            // 등록돼 있어서, 게이트를 함께 풀면 드래곤이 3타마다 방어유닛 카드
                            // 연출을 낸다(2026-08-12 코드리뷰 H3 — 「적 host 는 무해하다」가 거짓).
                            if (dcFiredWriter.HasValue && defenderTagLookup.HasComponent(attackerEntity))
                                dcFiredWriter.Value.Enqueue(new DcTriggerFiredEvent { host = attackerEntity });

                            // ⚠ **라우팅은 payload 분기들보다 앞이다.** 뒤에 두면 이전한
                            // 스킬이 여전히 legacy arm 을 타는데 legacy 가 잘 돌아서 그물이
                            // 전부 초록이 된다(`7f902e55` 가 잡은 실패 유형).
                            //
                            // skill-layer-migration unit 3a — **여기가 값 스냅샷 계약이 처음
                            // 실제로 쓰이는 자리다.** `bestTarget` 은 9단계 오버라이드의
                            // 합성물이라(최근접 → 힐러 재랭킹 → priority → 적 락 → 어그로 →
                            // frontmost → 지속 락 → 커밋 유지 → facing) **드레인 시점에
                            // 재질의하면 다른 답이 나온다.** 그래서 지금 손에 든 값을 싣는다.
                            if (slot.skillId != Wassup.Skills.SkillRegistry.LegacyArmId)
                            {
                                if (hasSkillQ)
                                {
                                    skillFiredSingleton.ValueRW.queue.Enqueue(
                                        new Wassup.Battle.Skills.SkillFiredEvent
                                    {
                                        Seam = Wassup.Battle.Skills.SkillSeam.Attack,   // 이 드레인 지점이 실행한다
                                        Caster = attackerEntity,
                                        SkillId = slot.skillId,
                                        SlotIndex = si,
                                        FiredPosition = transform.ValueRO.Position,
                                        Target = bestTarget,
                                        TargetPosition = bestTargetPos,
                                        // 넉백·브레스가 쓰는 **계산된** 방향. 대상이 host 와
                                        // 겹치면 0 이고, 그 판정은 concrete 가 한다.
                                        DirectionXZ = math.normalizesafe(
                                            (bestTargetPos - transform.ValueRO.Position).xz),
                                        // ⚠ killer 사양이다. 0 으로 새면 무제한 통과가 된다.
                                        TargetTraversalLayers = attack.ValueRO.targetTraversalLayers,
                                        Magnitude = slot.magnitude,
                                        Duration = slot.duration,
                                        TileRange = slot.tileRange,
                                        Period = slot.period,
                                        DataIndex = slot.projectileDataIndex,
                                        Selector = (int)slot.ccKind,
                                        StatSelector = (int)slot.buffStat,
                                        StackSelector = (int)slot.stackKind,
                                    ProjectileMovement = (int)slot.projectileMovement,
                                    ProjectilePayload = (int)slot.projectilePayload,
                                    HazardDataIndex = slot.hazardDataIndex,
                                        PatternIndex = slot.patternIndex,
                                        Speed = slot.speed,
                                        HitThreshold = slot.hitThreshold,
                                        SlamDamage = slot.slamDamage,
                                        SlamTileRange = slot.slamTileRange,
                                        StackId = slot.statBuffStackId,
                                        VisualScale = slot.visualScale,
                                    });
                                }

                                // ⚠ **전투 로그는 이 attack 의 것이다**(투트랙 리뷰 M-4).
                                // 레거시 비수 arm 은 `SpawnNeedleCarrier` 안에서 이 이벤트를
                                // 넣었고, arm 을 걷으면서 같이 사라졌다 — 판 리포트에서
                                // 비수·부메랑 줄이 조용히 빠진다.
                                //
                                // 스킬이 아니라 **감지자**가 넣는 이유: 이 채널이 기록하는 것은
                                // 「이 공격이 무엇을 내보냈나」이고, 「지금이 공격이다」를 아는
                                // 것은 RESOLVE 뿐이다. 도메인은 자기가 무슨 사건에 실려 왔는지
                                // 모른다(그게 계약이다).
                                //
                                // payload 를 보는 이유: 레거시에서 이 로그를 넣은 arm 이
                                // **비수 하나뿐**이었다. CC·스택 arm 은 안 넣었고, 여기서
                                // 넓히면 없던 줄이 리포트에 생긴다.
                                if (attackOutputLogWriter.HasValue
                                    && slot.payload == Wassup.Data.DcPayloadKind.ProjectileToTarget)
                                {
                                    attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                    {
                                        attacker  = attackerEntity,
                                        kind      = Wassup.Data.AttackOutputKind.Damage,
                                        magnitude = slot.magnitude,
                                        duration  = 0f,
                                        sourcePos = transform.ValueRO.Position,
                                        targetPos = bestTargetPos,
                                    });
                                }
                                continue;
                            }

                            // dreamcatcher-new-abilities unit 1 — payload 디스패치. AttackN
                            // 슬롯이 발동하면 kind 별로 carrier(투사체)/CC/스택 중 하나를 실행.
                            //
                            // skill-layer-migration unit 3g — 「적 host 는 비수를 타면 안 된다」
                            // 가드는 **사라졌다.** 그것은 arm 이 대상 진영을 방어유닛으로
                            // 하드코딩해서 생긴 제약이었고, concrete 는 caster 의 상대 진영에서
                            // 도출한다(foundation unit 2b). 막을 것이 없어졌다 — 적이 비수를
                            // 저작하면 그냥 **방어유닛을 쏜다**. 위 라우팅이 이 줄보다 앞이라
                            // 어차피 도달 불가능한 코드이기도 했다.
                            // elite-enemy-tier unit 4 — 화염 브레스. 투사체 캐리어를 만들지 않는다:
                            // 즉발이고, 이 프레임의 후보 배열이 이미 손에 있다. 순회 본문은 아래
                            // private static 으로 빼서 1974줄 시스템을 키우지 않고 단위 테스트가
                            // 가능하게 했다(SpawnNeedleCarrier 선례).
                            if (slot.payload == Wassup.Data.DcPayloadKind.AreaBreath)
                            {
                                float rangeWorld = slot.tileRange * tileSize;
                                float3 selfPos = transform.ValueRO.Position;
                                float2 breathDir = math.normalizesafe(
                                    (bestTargetPos - selfPos).xz, new float2(1f, 0f));
                                ApplyConeBreath(ref ecb, attackerEntity, selfPos.xz, breathDir,
                                    slot.coneCosSq, rangeWorld, slot.magnitude,
                                    mask, attack.ValueRO.targetTraversalLayers,
                                    targetEntities, targetTransforms, targetFactions, targetTraversalLayers);

                                // 연출 — Burst ISystem 은 VfxSpawner 를 못 부른다. 기존 채널에
                                // VFX 캐리어로 태운다(신규 큐 0). 드레인은 이 플래그를 보면 애니
                                // 재생을 건너뛴다(공격 시작 이벤트는 이미 별도로 나갔다).
                                if (attackWriter.HasValue)
                                {
                                    attackWriter.Value.Enqueue(new UnitAttackVisualEvent
                                    {
                                        attacker = attackerEntity,
                                        targetWorld = bestTargetPos,
                                        attackAnimPeriod = 0f,
                                        target = bestTarget,
                                        hasAreaBreath = true,
                                        breathDir = breathDir,
                                        breathRangeWorld = rangeWorld,
                                        breathHalfAngleDeg = slot.coneHalfAngleDeg,
                                    });
                                }
                                continue;
                            }
                            if (slot.payload == Wassup.Data.DcPayloadKind.HeavyStrike)
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
                // target-persistence unit 0 — 대상 커밋도 같은 자리에서 비운다. 다음 공격은
                // 그때의 선정 사슬로 다시 고른다(이 unit 은 공격 1회 안만 책임진다).
                if (doResolve && attack.ValueRO.hasCommittedTarget != 0)
                {
                    attack.ValueRW.committedTarget = Entity.Null;
                    attack.ValueRW.hasCommittedTarget = 0;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            needleScratch.Dispose();
            targetEntities.Dispose();
            targetTransforms.Dispose();
            castCountedHosts.Dispose();
            targetFactions.Dispose();
            targetTraversalLayers.Dispose();
            targetSimIds.Dispose();
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
        // elite-enemy-tier unit 4 — 화염 브레스의 피해 적용. plain 배열·plain 값만 받는 순수
        // static 이라 1974줄 시스템을 키우지 않고 단위 테스트가 가능하다(제약 10 형태).
        //
        // ★**세 술어는 생략 불가다.** `targetCandidatesQuery` 는 `FactionTag, Health,
        // LocalTransform` 의 **전 진영 통합 풀**이고 진영 판정은 공격자 루프 안의 `targetMask` 가
        // 한다 — 배열이 미리 걸러져 있다고 착각하면 드래곤이 같은 웨이브 동료와 적 마음을 태운다
        // (2026-08-12 리뷰가 초판 스펙의 그 거짓 전제를 잡았다).
        //   ① 진영 마스크  ② 통행층 교집합(지상 전용 공격이 Air 로 번지지 않게)  ③ 자기 제외
        //
        // `AoeTargetCap` 을 쓰지 않는다 — 부채꼴에 든 전원이 맞는 것이 이 능력의 요점이다.
        // 위협(`ThreatHitEvent`) 귀속도 하지 않는다 — 위협 테이블은 보스 전용 부속물이다.
        private static void ApplyConeBreath(
            ref EntityCommandBuffer ecb,
            Entity self, float2 selfXZ, float2 dir, float cosSq, float rangeWorld, float damage,
            int targetMask, byte selfTargetLayers,
            NativeArray<Entity> targetEntities,
            NativeArray<LocalTransform> targetTransforms,
            NativeArray<FactionTag> targetFactions,
            NativeArray<byte> targetTraversalLayers)
        {
            if (damage <= 0f) return;
            for (int i = 0; i < targetEntities.Length; i++)
            {
                if (((int)targetFactions[i].value & targetMask) == 0) continue;          // ①
                if (!Wassup.Data.PlacementLayers.CanTarget(
                        selfTargetLayers, targetTraversalLayers[i])) continue;           // ②
                if (targetEntities[i] == self) continue;                                 // ③
                if (!TileAoe.IsInCone(selfXZ, targetTransforms[i].Position.xz,
                        dir, cosSq, rangeWorld)) continue;
                ecb.AppendToBuffer(targetEntities[i], new IncomingDamage
                {
                    amount = damage,
                    source = self,
                });
            }
        }

        private static void SpawnNeedleCarrier(
            ref EntityCommandBuffer ecb, in DcTriggerSlot slot,
            Entity owner, float3 origin, Entity target, float3 targetPos,
            byte targetTraversalLayers, float tileSize,
            bool hasLog, NativeQueue<AttackOutputLogEvent>.ParallelWriter log)
        {
            // dreamcatcher-content-5 unit 3 — **탄 에셋의 궤적을 존중한다.** 여기가 여태
            // (HomingToEntity, SingleSplash)를 하드코딩해서, 저작자가 탄 SO 에 어떤 비행을
            // 골라도 유도탄으로 나갔다. 축은 bake 가 ResolveProjectileAxes 로 구워 보낸다.
            // 기본값(0,0)이 그 레거시 짝이라 기존 카드(비수)는 무변화다.
            var movement = slot.projectileMovement;
            var payload = slot.projectilePayload;

            // 방향 바인딩 궤적(왕복 = 부메랑)은 타겟 엔티티를 잡지 않는다 — 발사 시점의
            // 대상 방향을 축으로 굳히고 거리로 산다. 두 값 다 여기서 이미 손에 있다.
            bool directional = MovementBinding.Of(movement) == BindingClass.Direction;
            float2 axis = float2.zero;
            if (directional)
            {
                float3 d = targetPos - origin;
                d.y = 0f;
                // 같은 셀이면 축이 없다 — 드레인이 loud 거절하므로 여기선 0 을 그대로 보낸다
                // (조용히 임의 방향을 지어내면 저작 실수가 안 보인다).
                axis = math.lengthsq(d) > 1e-6f ? math.normalize(d.xz) : float2.zero;
            }

            var carrier = ecb.CreateEntity();
            ecb.AddComponent(carrier, new ProjectileSpawnRequest
            {
                movement = movement,
                payload = payload,
                target = target,
                origin = origin,
                damage = slot.magnitude, // flat — 계약 7(공격자 damageMul 미적용)
                speed = slot.speed,
                hitThreshold = slot.hitThreshold,
                visualScale = slot.visualScale,
                dataIndex = slot.projectileDataIndex,
                owner = owner,
                targetTraversalLayers = targetTraversalLayers,
                direction = axis,
                // 방향 바인딩에서 tileRange 는 **날아가는 거리**로 읽는다(아래 재조준 참조).
                maxDistance = directional ? slot.tileRange * tileSize : 0f,
                // 대상이 맞기 전에 죽으면 같은 반경 안에서 다시 겨눈다. 니들은 5회에
                // 한 번 나오는 자원이라 허공에 사라지면 그 주기가 통째로 버려진다.
                // ⚠ 방향 바인딩에는 **겨눌 대상 엔티티가 없어** 재조준이 성립하지 않는다.
                // 지금은 사전 스캔이 호밍으로 좁혀져 있어 실어도 무해하지만 그건 우연한
                // 무해이므로 0 으로 명시한다 — 같은 필드가 두 의미를 동시에 갖지 않는다.
                retargetTileRange = directional ? 0 : slot.tileRange,
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

        // 「반경 안 최근접 하나」 선정. 원래는 host 가 대상을 확정해 주지 않는
        // 아키타입(폭탄맨·캐스터)의 **니들 폴백** 전용이었고, 후보 조립이 두 곳에
        // 복붙돼 있었으며 실수하기 쉬운 부분(진영 마스크·자기 제외·그리드 변환)이
        // 테스트 밖에 남아 있었다.
        // bomb-thrower-defender unit 9 — 폭탄맨의 **본 공격 타겟팅**도 여기로 들어왔다.
        // `factionMask` 를 호출부가 넘기는 것이 그 귀결이다: 폴백은 적 유닛만 노리지만
        // 본 공격은 유닛의 저작 마스크(AttackState.targetMask, 적 거점 포함)를 따른다.
        // goal-tower-siege unit 1 — PastGoal 제외는 폐기됐다(골에 붙은 적도 유효 대상).
        private static int PickFallbackTarget(
            NativeArray<NearestTargeting.Candidate> scratch,
            NativeArray<Entity> ents, NativeArray<LocalTransform> xf, NativeArray<FactionTag> fac,
            NativeArray<byte> targetTraversalLayers, NativeArray<int> targetSimIds,
            Entity self, float3 selfPos, int2 selfCell,
            float tileSize, int2 gridSize, float3 gridOrigin, int tileRange,
            byte attackTargetLayers, int factionMask)
        {
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                // goal-tower-siege unit 1 — PastGoal 배제 제거. 골에 붙은 적은 살아서 타워를
                // 때리는 중이라 니들을 낭비하는 대상이 아니라 **최우선으로 지워야 할 대상**이다.
                bool eligible = e != self
                    && ((int)fac[i].value & factionMask) != 0
                    && Wassup.Data.PlacementLayers.CanTarget(
                        attackTargetLayers, targetTraversalLayers[i]);
                float3 p = xf[i].Position;
                int2 c = GridMath.WorldToCell(p, tileSize, gridSize, origin: gridOrigin);
                scratch[i] = new NearestTargeting.Candidate
                {
                    eligible = eligible,
                    tileDist = math.max(math.abs(c.x - selfCell.x), math.abs(c.y - selfCell.y)),
                    sqDist = math.distancesq(selfPos, p),
                    simId = targetSimIds[i],
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

        // dreamcatcher-content-4 unit 4 (악몽 사냥) — «이 적이 자고 있나». AnyActiveCc 를
        // kind 파라미터로 일반화하지 않는다: 호출처가 각각 2벌뿐이고, 두 술어가 각자
        // 이름을 갖는 편이 «CC 전반(shatter_hymn) vs 수면만(악몽 사냥)» 이라는 두 카드의
        // 차이를 호출 지점에서 바로 읽히게 한다(제약 8 — 소비자 없는 추상화 금지).
        // 무한 수면은 remainingTime = +∞ 로 표현되므로 > 0 이 그대로 커버한다.
        private static bool AnyActiveSleep(in DynamicBuffer<Wassup.Battle.Effects.CcEffect> buf)
        {
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].remainingTime > 0f
                    && buf[i].kind == Wassup.Battle.Effects.CcKind.Sleep) return true;
            return false;
        }

        private static float DistanceSqToTarget(
            float3 attackerPos,
            Entity target,
            float3 fallbackTargetPos,
            BufferLookup<OccupiedCellsBuffer> occupiedCellsLookup,
            bool hasFlowField,
            Wassup.Battle.Effects.FlowFieldSingleton flowField,
            out float3 nearestTargetPos)
        {
            nearestTargetPos = fallbackTargetPos;
            float3 diff = fallbackTargetPos - attackerPos;
            float bestSq = diff.x * diff.x + diff.z * diff.z;

            if (!hasFlowField || !occupiedCellsLookup.HasBuffer(target))
                return bestSq;

            var cells = occupiedCellsLookup[target];
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
