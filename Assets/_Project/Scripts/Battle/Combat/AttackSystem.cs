using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat.Projectile;
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
            var targetCandidatesQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, Health, LocalTransform>()
                .WithNone<PendingDeployment>()
                .WithNone<DeadTag>()
                .Build();
            var targetEntities = targetCandidatesQuery.ToEntityArray(Allocator.Temp);
            var targetTransforms = targetCandidatesQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var targetFactions = targetCandidatesQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var projectileRefLookup = SystemAPI.GetComponentLookup<ProjectileRef>(isReadOnly: true);
            var defenderCcLookup = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
            var defenderTagLookup = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
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

            // ─────────────────────────────────────────────────────────────────────
            // Unified attacker loop — defenders and enemies share this single query.
            // Defender-specific branches guard on defenderTagLookup / HasComponent.
            // ─────────────────────────────────────────────────────────────────────
            foreach (var (attack, transform, attackerEntity) in
                     SystemAPI.Query<RefRW<AttackState>, RefRO<LocalTransform>>()
                              .WithNone<PendingDeployment>()
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

                // Find nearest in-range target allowed by this attacker's mask.
                float3 atkPos = transform.ValueRO.Position;
                float tileSize = hasFlowField ? flowField.tileSize : 1f;
                int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
                float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;
                int tileRange = GridMath.RangeToTiles(attack.ValueRO.range);
                int2 atkCell = GridMath.WorldToCell(atkPos, tileSize, gridSize, origin: ffOrigin);
                float bestSq = float.MaxValue;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = default;
                int mask = attack.ValueRO.targetMask;
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
                bool fmHasBest = false;
                bool fmChosenIsPriority = false;
                FrontmostTargeting.Candidate fmBest = default;
                Entity fmBestEntity = Entity.Null;
                float3 fmBestPos = default;
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
                    if (prioClass >= 0 && cclass == prioClass && d2 < bestSqPrio)
                    {
                        bestSqPrio = d2;
                        bestTargetPrio = targetEntities[i];
                        bestTargetPosPrio = nearestPos;
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

                // attack-hit-delay — fire 를 START(공격 시작) / RESOLVE(타격 판정) 로 분리.
                // 지연 중이면 tick → 만료한 프레임에 RESOLVE(재판정된 bestTarget). 아니면 쿨다운+타겟 조건 시 START.
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

                // ── RESOLVE ── 타격 판정/적용 (재판정된 bestTarget). 데미지/투사체/넉백.
                if (doResolve && bestTarget != Entity.Null)
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
                    if (defenderTagLookup.HasComponent(attackerEntity) && dcSlotLookup.HasBuffer(attackerEntity))
                    {
                        var heavySlots = dcSlotLookup[attackerEntity];
                        for (int hi = 0; hi < heavySlots.Length; hi++)
                        {
                            var hs = heavySlots[hi];
                            if (hs.trigger == Wassup.Data.DcTriggerKind.AttackN
                                && hs.payload == Wassup.Data.DcPayloadKind.HeavyStrike
                                && DcTrigger.WouldFire(hs.counter, hs.period))
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
                                    if (attackerVsCc != 1f && ccActionLookup.HasBuffer(bestTarget) && AnyActiveCc(ccActionLookup[bestTarget]))
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
                            else
                            {
                                // attack-mod-bounce unit 3 — aggregate always-on mods onto
                                // this base homing shot only (count sum / range max / mul
                                // product). Ballistic + dc-trigger carrier shots are
                                // excluded by construction (contract 4). Defaults 0/0/1 =
                                // no bounce.
                                int dcBounceCount = 0, dcBounceRange = 0;
                                float dcBounceMul = 1f;
                                if (defenderTagLookup.HasComponent(attackerEntity) && dcAttackModLookup.HasBuffer(attackerEntity))
                                {
                                    var mods = dcAttackModLookup[attackerEntity];
                                    for (int di = 0; di < mods.Length; di++)
                                    {
                                        var mod = mods[di];
                                        if (mod.kind != Wassup.Data.DcAttackModKind.ProjectileBounce) continue;
                                        dcBounceCount += mod.count;
                                        dcBounceRange = math.max(dcBounceRange, mod.tileRange);
                                        dcBounceMul *= mod.damageMul;
                                    }
                                }
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
                                        for (int i = 0; i < targetEntities.Length; i++)
                                        {
                                            if (hitMaskO[i]) continue;
                                            if (((int)targetFactions[i].value & mask) == 0) continue;
                                            if (targetEntities[i] == attackerEntity) continue;
                                            int2 tgtCellAoE = GridMath.WorldToCell(targetTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                                            int tileDistAoE = math.max(math.abs(tgtCellAoE.x - atkCell.x), math.abs(tgtCellAoE.y - atkCell.y));
                                            if (tileDistAoE > tileRange) continue;
                                            float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetTransforms[i].Position, blockingHazardCellsLookup, hasFlowField, flowField, out _);
                                            if (d2 < passSq)
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
                                                    maxStack       = o.stackMaxStack > 0 ? o.stackMaxStack : (byte)5,
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
                            hitTargets.Dispose();
                        }
                    }

                    // [Defender only] Knockback CC — enemies do not carry DefenderCcData. (RESOLVE 시점)
                    if (ccWriter.HasValue && defenderCcLookup.HasComponent(attackerEntity))
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
                    }

                    // [Defender only] dreamcatcher-unit-trigger unit 2 — triggered card
                    // slots count once per attack RESOLVE (multi-output attacks still
                    // count 1; a resolve that lapsed with no valid target counts 0).
                    // bestTarget is guaranteed alive here: RESOLVE applies damage via
                    // the deferred IncomingDamage buffer, so nothing in this block can
                    // have destroyed it.
                    if (defenderTagLookup.HasComponent(attackerEntity) && dcSlotLookup.HasBuffer(attackerEntity))
                    {
                        var dcSlots = dcSlotLookup[attackerEntity];
                        for (int si = 0; si < dcSlots.Length; si++)
                        {
                            var slot = dcSlots[si];
                            if (slot.trigger != Wassup.Data.DcTriggerKind.AttackN) continue;
                            ushort dcCounter = slot.counter;
                            bool dcFired = DcTrigger.Tick(ref dcCounter, slot.period);
                            slot.counter = dcCounter;
                            dcSlots[si] = slot;
                            if (!dcFired) continue;

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
                                var dcCarrier = ecb.CreateEntity();
                                ecb.AddComponent(dcCarrier, new ProjectileSpawnRequest
                                {
                                    movement = MovementKind.HomingToEntity,
                                    payload = PayloadKind.SingleSplash,
                                    target = bestTarget,
                                    origin = atkPos,
                                    damage = slot.magnitude, // flat — no damageMul (spec contract 7)
                                    speed = slot.speed,
                                    hitThreshold = slot.hitThreshold,
                                    visualScale = slot.visualScale,
                                    dataIndex = slot.projectileDataIndex,
                                    // nightmare-catcher unit 1 — card projectiles credit
                                    // the bound defender, not the carrier entity.
                                    owner = attackerEntity,
                                });
                                ecb.AddComponent<ProjectileRequestCarrier>(dcCarrier);
                                if (attackOutputLogWriter.HasValue)
                                    attackOutputLogWriter.Value.Enqueue(new AttackOutputLogEvent
                                    {
                                        attacker  = attackerEntity,
                                        kind      = Wassup.Data.AttackOutputKind.Damage,
                                        magnitude = slot.magnitude,
                                        duration  = 0f,
                                        sourcePos = atkPos,
                                        targetPos = bestTargetPos,
                                    });
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
                                        maxStack       = slot.tileRange > 0 ? (byte)math.min(slot.tileRange, 255) : (byte)5,
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
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            targetEntities.Dispose();
            targetTransforms.Dispose();
            targetFactions.Dispose();
        }

        // dreamcatcher-new-abilities unit 2 — shatter_hymn 게이트: 대상에 활성 CcEffect
        // (Stun/Sleep/Impulse/DoT, remaining>0)가 하나라도 있는가. frost(Stun)·
        // ember(Bleed→DoT) 가 건 CC 를 감지. Slow 는 CcEffect 가 아니라 여기 해당 없음.
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
