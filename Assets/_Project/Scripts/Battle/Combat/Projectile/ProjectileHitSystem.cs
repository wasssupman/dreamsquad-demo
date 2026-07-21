using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile
{
    // Payload axis of the projectile pipeline: resolves a projectile once
    // ProjectileMoveSystem has flagged arrival (ProjectileState.impactReached),
    // dispatching on PayloadKind. SingleSplash applies the shooter's outputs to the
    // direct target plus the OnHitEffectType.Splash bonus to nearby enemies.
    //
    // IncomingDamage is a Units-owned buffer used as a Combat→Units event channel
    // per TRD 2.5.2 rule 2. The shooter's AttackState is not touched — cooldown
    // reset happens inside AttackSystem at launch time.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(ProjectileMoveSystem))]
    public partial struct ProjectileHitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        private const float HitFlashDuration = 0.15f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var damageBufferLookup = SystemAPI.GetBufferLookup<IncomingDamage>(isReadOnly: false);
            var healBufferLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Units.IncomingHeal>(isReadOnly: false);
            // RW: a bouncing projectile decays its outputs-buffer Damage entries in
            // place (that buffer is the damage source when present) — bounce unit 2.
            var outputLookup = SystemAPI.GetBufferLookup<AttackOutputElement>(isReadOnly: false);
            var hitFlashLookup = SystemAPI.GetComponentLookup<HitFlashTag>(isReadOnly: true);
            // defender-directional-volley unit 2 — per-projectile victim record so a
            // path sweep damages each target once (read-only: appends go through the
            // ECB alongside the damage that earned them).
            var pathHitRecordLookup = SystemAPI.GetBufferLookup<PathHitRecord>(isReadOnly: true);
            bool hasStatQ = SystemAPI.TryGetSingleton<StatModifierApplyEventsSingleton>(out var statEvents);
            bool hasStackQ = SystemAPI.TryGetSingleton<StackModifierApplyEventsSingleton>(out var stackEvents);
            // nightmare-catcher unit 1 — 보스 위협 귀속: 피격자가 ThreatEntry 버퍼
            // 보유(보스 베이크) && owner 가 defender 인 착탄만 enqueue. 스킬 투사체
            // (owner == Null, 플레이어 Meteor)와 defender 피격 경로는 무영향.
            // RW 접근 = 큐 변이 의도 명시(AttackSystem 대칭 — 렌즈 B M1).
            bool hasThreatQ = SystemAPI.TryGetSingletonRW<ThreatHitEventsSingleton>(out var threatEventsRW);
            NativeQueue<ThreatHitEvent> threatQueue = hasThreatQ ? threatEventsRW.ValueRW.queue : default;
            var threatLookup = SystemAPI.GetBufferLookup<ThreatEntry>(isReadOnly: true);
            var defenderTagLookup = SystemAPI.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
            // bomb-thrower-defender unit 2 — Combat→Effects CC 채널(수면/스턴탄).
            // 수면파이터/존 CC 와 공유하는 기존 EnemyCcEvents 큐. RW 접근 = 큐 변이 의도
            // 명시(threat 큐 대칭). 테스트/초기 프레임엔 없을 수 있어 옵셔널 게이트.
            bool hasCcQ = SystemAPI.TryGetSingletonRW<EnemyCcEventsSingleton>(out var ccEventsRW);
            NativeQueue<EnemyCcEvent> ccQueue = hasCcQ ? ccEventsRW.ValueRW.queue : default;

            // Combat→Presentation: hit-VFX channel. May not exist before
            // BattleBridge.EnsureQueriesAndQueues runs (very first frames in
            // tests / dev hot-reload), so guarded by HasSingleton.
            bool hasHitChannel = SystemAPI.HasSingleton<ProjectileHitEventsSingleton>();
            NativeQueue<ProjectileHitEvent> hitQueue = default;
            if (hasHitChannel)
                hitQueue = SystemAPI.GetSingleton<ProjectileHitEventsSingleton>().queue;

            // Snapshot all living attack units up-front so a Splash hit can iterate
            // them without a nested SystemAPI.Query inside the projectile loop.
            // AttackUnitTag filter keeps non-enemy entities out of the AOE pool.
            var aoeQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
            var aoeEntities = aoeQuery.ToEntityArray(Allocator.Temp);
            var aoeTransforms = aoeQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            // Positions snapshot for the pure BounceRetarget.FindNext (architecture-
            // neutral: it takes float3, not LocalTransform/Entity) — bounce unit 2.
            var aoePositions = new NativeArray<float3>(aoeTransforms.Length, Allocator.Temp);
            for (int i = 0; i < aoeTransforms.Length; i++)
                aoePositions[i] = aoeTransforms[i].Position;

            // nightmare-catcher unit 4 — defender pool for the faction-
            // parameterized TileAoe (boss AreaBarrage hits defenders, HIGH-1).
            // Snapshot built alongside the enemy pool, same lifetime; splash and
            // bounce intentionally keep the enemy pool only.
            var defenderQuery = SystemAPI.QueryBuilder().WithAll<DefenderUnitTag, LocalTransform>().Build();
            var defenderEntities = defenderQuery.ToEntityArray(Allocator.Temp);
            var defenderTransforms = defenderQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Grid params for the TileAoe payload (impact cell + candidate cells).
            // Same source the legacy Meteor resolver used; defaults keep it safe before
            // the flow field exists (early frames / tests). Hoisted out of the loop.
            bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            int2 gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
            float3 ffOrigin = hasFlowField ? flowField.origin : float3.zero;

            foreach (var (projectile, entity) in
                     SystemAPI.Query<RefRO<ProjectileState>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                // defender-directional-volley unit 2 — PathHit has no point arrival:
                // it resolves in flight, every frame, so it must pass this gate. When
                // its move arm does set impactReached it means "reached max range" —
                // the cue to despawn after this last sweep, handled in the arm below.
                if (!projectile.ValueRO.impactReached && projectile.ValueRO.payload != PayloadKind.PathHit) continue;

                // Projectiles that live past this frame's resolution: a SingleSplash
                // that re-targeted (bounce unit 2), or a PathHit still in flight with
                // pierce budget left. Everything else is consumed below.
                bool survives = false;

                // nightmare-catcher unit 1 — per-projectile threat gate (N2).
                // A shooter that died mid-flight fails the defender-tag check
                // (despawned / version-bumped entity) and the credit is dropped —
                // harmless: Leader's alive mask excludes dead attackers anyway.
                var threatOwner = projectile.ValueRO.owner;
                bool creditThreat = hasThreatQ
                    && threatOwner != Entity.Null
                    && defenderTagLookup.HasComponent(threatOwner);

                // dreamcatcher-content-2 unit 3 (끝을 보는 눈) — only the exact victim entity
                // that equals priorityTarget takes the +20%; splash secondaries stay base.
                // prioMul resolves inert (1) unless a positive mul was carried (Null/0 default).
                var prioTarget = projectile.ValueRO.priorityTarget;
                float prioMul = projectile.ValueRO.priorityDamageMul > 0f ? projectile.ValueRO.priorityDamageMul : 1f;

                // dreamcatcher-heavy-strike unit 2 (응축된 일격) — heavy multiplies EVERY
                // Damage victim of this shot (direct/splash/bounce/TileAoe), unlike priority
                // which is one victim. Carried on state, survives bounce re-homing. Default
                // 0 → 1 (inert). Composes multiplicatively with prioMul below.
                float heavyMul = projectile.ValueRO.heavyDamageMul > 0f ? projectile.ValueRO.heavyDamageMul : 1f;

                switch (projectile.ValueRO.payload)
                {
                    case PayloadKind.SingleSplash:
                    {
                        var target = projectile.ValueRO.target;
                        if (target != Entity.Null && transformLookup.HasComponent(target))
                        {
                            float3 targetPos = transformLookup[target].Position;

                            bool handledOutputs = false;
                            if (outputLookup.HasBuffer(entity))
                            {
                                handledOutputs = true;
                                var outputs = outputLookup[entity];
                                for (int i = 0; i < outputs.Length; i++)
                                {
                                    var output = outputs[i].value;
                                    switch (output.kind)
                                    {
                                        case AttackOutputKind.Damage:
                                            if (damageBufferLookup.HasBuffer(target))
                                            {
                                                // 끝을 보는 눈 — direct victim priority (bounce direct
                                                // target changes per hop, so A→B→A re-applies to A).
                                                // 응축된 일격 — × heavyMul on top (전 victim, 여기선 direct).
                                                float dmg = (target == prioTarget ? output.magnitude * prioMul : output.magnitude) * heavyMul;
                                                ecb.AppendToBuffer(target, new IncomingDamage { amount = dmg, source = threatOwner });
                                                ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup, target, threatOwner, dmg);
                                            }
                                            break;

                                        case AttackOutputKind.Heal:
                                            if (healBufferLookup.HasBuffer(target))
                                                ecb.AppendToBuffer(target, new Wassup.Battle.Units.IncomingHeal { amount = output.magnitude });
                                            break;

                                        case AttackOutputKind.ApplyStat:
                                            if (hasStatQ)
                                                statEvents.queue.Enqueue(new StatModifierApplyEvent
                                                {
                                                    target = target,
                                                    stat = output.stat,
                                                    op = output.op,
                                                    magnitude = output.magnitude,
                                                    duration = output.duration,
                                                    source = entity,
                                                    stackId = 0,
                                                    origin = ModifierOrigin.OnHit,
                                                });
                                            break;

                                        case AttackOutputKind.ApplyStack:
                                            if (hasStackQ)
                                                stackEvents.queue.Enqueue(new StackModifierApplyEvent
                                                {
                                                    target = target,
                                                    kind = output.stackKind,
                                                    countDelta = (byte)math.max(1f, output.magnitude),
                                                    maxStack = output.stackMaxStack > 0 ? output.stackMaxStack : (byte)5,
                                                    perAppDuration = output.duration,
                                                    source = entity,
                                                });
                                            break;
                                    }
                                }
                            }

                            if (!handledOutputs && damageBufferLookup.HasBuffer(target))
                            {
                                float dmg = (target == prioTarget ? projectile.ValueRO.damage * prioMul : projectile.ValueRO.damage) * heavyMul;
                                ecb.AppendToBuffer(target, new IncomingDamage { amount = dmg, source = threatOwner });
                                ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup, target, threatOwner, dmg);
                            }

                            // Combat→Presentation: one hit event per direct target —
                            // splash secondary damage gets no extra VFX (intentional).
                            if (hasHitChannel)
                                hitQueue.Enqueue(new ProjectileHitEvent
                                {
                                    position = targetPos,
                                    dataIndex = projectile.ValueRO.dataIndex,
                                    payload = PayloadKind.SingleSplash,
                                    source = entity,
                                });

                            // Splash AOE: reduced damage to every other AttackUnit within
                            // splashRadius of the direct target (direct target skipped to
                            // avoid double-damage).
                            if (projectile.ValueRO.onHitEffect == OnHitEffectType.Splash &&
                                projectile.ValueRO.splashRadius > 0f)
                            {
                                float3 aoeCenter = targetPos;
                                float splashRadiusSq = projectile.ValueRO.splashRadius * projectile.ValueRO.splashRadius;
                                // 응축된 일격 — splash secondaries도 강공 배율(한 방 통째, 전 victim).
                                float splashDamage = projectile.ValueRO.damage * projectile.ValueRO.splashDamageMul * heavyMul;
                                for (int i = 0; i < aoeEntities.Length; i++)
                                {
                                    var candidate = aoeEntities[i];
                                    if (candidate == target) continue;
                                    float dx = aoeTransforms[i].Position.x - aoeCenter.x;
                                    float dz = aoeTransforms[i].Position.z - aoeCenter.z;
                                    if (dx * dx + dz * dz > splashRadiusSq) continue;
                                    if (damageBufferLookup.HasBuffer(candidate))
                                    {
                                        ecb.AppendToBuffer(candidate, new IncomingDamage { amount = splashDamage, source = threatOwner });
                                        ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup, candidate, threatOwner, splashDamage);
                                    }
                                }
                            }

                            // Visual feedback: pulse the target briefly. Refresh the
                            // timer on back-to-back hits rather than overwriting scale.
                            if (hitFlashLookup.HasComponent(target))
                                ecb.SetComponent(target, new HitFlashTag
                                {
                                    remaining = HitFlashDuration,
                                    duration = HitFlashDuration,
                                    originalScale = hitFlashLookup[target].originalScale,
                                });
                            else
                                ecb.AddComponent(target, new HitFlashTag
                                {
                                    remaining = HitFlashDuration,
                                    duration = HitFlashDuration,
                                    originalScale = transformLookup[target].Scale,
                                });

                            // bounce unit 2 — post-resolution survival. The resolution
                            // above (damage/VFX/flash) ran unchanged; now, if charges
                            // remain and a retarget exists, re-home the SAME entity
                            // instead of destroying it (view/trail continuity is free).
                            if (projectile.ValueRO.bounceRemaining > 0)
                            {
                                // Exclude the just-hit target (its index in the snapshot);
                                // damage is deferred (IncomingDamage) so it is still alive here.
                                int excludeIdx = -1;
                                for (int i = 0; i < aoeEntities.Length; i++)
                                    if (aoeEntities[i] == target) { excludeIdx = i; break; }

                                int nextIdx = BounceRetarget.FindNext(
                                    targetPos, excludeIdx, aoePositions,
                                    projectile.ValueRO.bounceTileRange, tileSize, gridSize, ffOrigin);

                                if (nextIdx >= 0)
                                {
                                    float mul = projectile.ValueRO.bounceDamageMul;
                                    var next = projectile.ValueRO;
                                    next.target = aoeEntities[nextIdx];
                                    next.impactReached = false;
                                    next.bounceRemaining = projectile.ValueRO.bounceRemaining - 1;
                                    next.damage = projectile.ValueRO.damage * mul;
                                    ecb.SetComponent(entity, next);

                                    // Decay the outputs-buffer Damage entries too — that
                                    // buffer, not state.damage, is the source when present.
                                    if (mul != 1f && outputLookup.HasBuffer(entity))
                                    {
                                        var buf = outputLookup[entity];
                                        for (int oi = 0; oi < buf.Length; oi++)
                                        {
                                            var e = buf[oi];
                                            if (e.value.kind == AttackOutputKind.Damage)
                                            {
                                                e.value.magnitude *= mul;
                                                buf[oi] = e;
                                            }
                                        }
                                    }
                                    survives = true;
                                }
                            }
                        }
                        break;
                    }

                    case PayloadKind.PathHit:
                    {
                        // defender-directional-volley unit 2 — in-flight sweep: damage
                        // every victim the prevPos→Position segment crossed this frame,
                        // each at most once (PathHitRecord), until the pierce budget is
                        // spent. Enemy pool only (splash/bounce precedent); damage is
                        // the pre-summed Damage total on state, so non-Damage outputs
                        // are a follow-up exactly as with TileAoe (v1 is Damage-only).
                        if (!transformLookup.HasComponent(entity)) break;

                        float2 prev = projectile.ValueRO.prevPos.xz;
                        float2 curr = transformLookup[entity].Position.xz;
                        float2 dir = projectile.ValueRO.direction;
                        float radius = projectile.ValueRO.hitThreshold;
                        int budget = projectile.ValueRO.pierceRemaining;
                        float dmg = projectile.ValueRO.damage;

                        var sweptIdx = new NativeList<int>(Allocator.Temp);
                        var sweptDist = new NativeList<float>(Allocator.Temp);
                        for (int i = 0; i < aoeEntities.Length; i++)
                        {
                            float2 victimPos = aoePositions[i].xz;
                            if (!SweepHitMath.SegmentHits(prev, curr, victimPos, radius)) continue;
                            if (pathHitRecordLookup.HasBuffer(entity) &&
                                PathHitRecord.Contains(pathHitRecordLookup[entity], aoeEntities[i])) continue;
                            sweptIdx.Add(i);
                            sweptDist.Add(math.dot(victimPos - prev, dir));
                        }

                        // Front-most first: a 1-pierce shot must stop at the nearest
                        // enemy it crossed, and snapshot order carries no meaning.
                        while (budget > 0 && sweptIdx.Length > 0)
                        {
                            int nearest = 0;
                            for (int k = 1; k < sweptIdx.Length; k++)
                                if (sweptDist[k] < sweptDist[nearest]) nearest = k;

                            var victim = aoeEntities[sweptIdx[nearest]];
                            float3 victimPos = aoePositions[sweptIdx[nearest]];
                            if (damageBufferLookup.HasBuffer(victim))
                            {
                                float vdmg = (victim == prioTarget ? dmg * prioMul : dmg) * heavyMul;
                                ecb.AppendToBuffer(victim, new IncomingDamage { amount = vdmg, source = threatOwner });
                                ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup, victim, threatOwner, vdmg);
                            }
                            ecb.AppendToBuffer(entity, new PathHitRecord { value = victim });

                            if (hasHitChannel)
                                hitQueue.Enqueue(new ProjectileHitEvent
                                {
                                    position = victimPos,
                                    dataIndex = projectile.ValueRO.dataIndex,
                                    payload = PayloadKind.PathHit,
                                    source = entity,
                                });

                            if (hitFlashLookup.HasComponent(victim))
                                ecb.SetComponent(victim, new HitFlashTag
                                {
                                    remaining = HitFlashDuration,
                                    duration = HitFlashDuration,
                                    originalScale = hitFlashLookup[victim].originalScale,
                                });
                            else if (transformLookup.HasComponent(victim))
                                ecb.AddComponent(victim, new HitFlashTag
                                {
                                    remaining = HitFlashDuration,
                                    duration = HitFlashDuration,
                                    originalScale = transformLookup[victim].Scale,
                                });

                            budget--;
                            sweptIdx.RemoveAtSwapBack(nearest);
                            sweptDist.RemoveAtSwapBack(nearest);
                        }
                        sweptIdx.Dispose();
                        sweptDist.Dispose();

                        if (budget != projectile.ValueRO.pierceRemaining)
                        {
                            var next = projectile.ValueRO;
                            next.pierceRemaining = budget;
                            ecb.SetComponent(entity, next);
                        }
                        // Out of budget = spent; impactReached = flew its full range.
                        survives = budget > 0 && !projectile.ValueRO.impactReached;
                        break;
                    }

                    case PayloadKind.TileAoe:
                    {
                        // Flat AOE to every enemy within impactTileRange of the
                        // cell-locked impact — no direct target, no falloff (shares
                        // the tile-membership rule with the legacy Meteor resolver).
                        // Damage source depends on the spawner: defender-fired = the
                        // pre-summed Damage-output total; skill-fired (Meteor) =
                        // SkillData.magnitude — both snapshotted into state.damage
                        // (contract: no new field). non-Damage outputs are a
                        // follow-up (v1 is Damage-only).
                        float3 impactWorld = projectile.ValueRO.impact;
                        int2 centerCell = GridMath.WorldToCell(impactWorld, tileSize, gridSize, origin: ffOrigin);
                        int tileRange = projectile.ValueRO.impactTileRange;
                        float dmg = projectile.ValueRO.damage;
                        // nightmare-catcher unit 4 — victim pool by targetFaction.
                        // Enemy(0) = legacy pool so player Meteor / defender
                        // ballistic are byte-identical to before (N3); the boss
                        // AreaBarrage arm is the only Defender setter (unit 2).
                        bool hitsDefenders = projectile.ValueRO.targetFaction == ProjectileTargetFaction.Defender;
                        var victims = hitsDefenders ? defenderEntities : aoeEntities;
                        var victimTransforms = hitsDefenders ? defenderTransforms : aoeTransforms;

                        // bomb-thrower-defender unit 2 — 범위 내 victim 인덱스 + impact
                        // 중심 거리²를 모아 가까운 순 aoeTargetCap 개로 절단(0 = 무제한 =
                        // 레거시 메테오/스킬/보스 경로, byte-identical). 데미지와 CC 를
                        // 같은 capped 집합에 적용. 비폭탄 spawn 은 cap 0·ccKind 0 → 무회귀.
                        var inRange = new NativeList<int>(Allocator.Temp);
                        var inRangeDistSq = new NativeList<float>(Allocator.Temp);
                        for (int i = 0; i < victims.Length; i++)
                        {
                            int2 cell = GridMath.WorldToCell(victimTransforms[i].Position, tileSize, gridSize, origin: ffOrigin);
                            if (!TileAoe.IsInTileRange(cell, centerCell, tileRange)) continue;
                            inRange.Add(i);
                            float ddx = victimTransforms[i].Position.x - impactWorld.x;
                            float ddz = victimTransforms[i].Position.z - impactWorld.z;
                            inRangeDistSq.Add(ddx * ddx + ddz * ddz);
                        }
                        var selectedAoe = new NativeList<int>(Allocator.Temp);
                        AoeTargetCap.SelectNearest(inRangeDistSq.AsArray(), projectile.ValueRO.aoeTargetCap, ref selectedAoe);

                        byte bombCc = projectile.ValueRO.ccKind;
                        float bombCcDur = projectile.ValueRO.ccDuration;
                        for (int s = 0; s < selectedAoe.Length; s++)
                        {
                            var victim = victims[inRange[selectedAoe[s]]];
                            // 데미지탄만 dmg>0 — 수면/스턴탄(dmg 0)은 데미지 append 스킵.
                            if (dmg > 0f && damageBufferLookup.HasBuffer(victim))
                            {
                                // 끝을 보는 눈 — priority victim +mul; 응축된 일격 — 전 victim ×heavyMul.
                                float vdmg = (victim == prioTarget ? dmg * prioMul : dmg) * heavyMul;
                                ecb.AppendToBuffer(victim, new IncomingDamage { amount = vdmg, source = threatOwner });
                                ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup, victim, threatOwner, vdmg);
                            }
                            // bomb-thrower-defender unit 2 — 수면/스턴탄 CC (Combat→Effects
                            // 기존 채널). ccKind 0 = None(데미지탄) → enqueue 없음.
                            if (bombCc != 0 && hasCcQ)
                                ccQueue.Enqueue(new EnemyCcEvent
                                {
                                    target = victim,
                                    effect = new CcEffect { kind = (CcKind)bombCc, remainingTime = bombCcDur },
                                });
                        }
                        inRange.Dispose();
                        inRangeDistSq.Dispose();
                        selectedAoe.Dispose();

                        // Impact-crater VFX at the cell (not a target position). No
                        // per-target HitFlash: an AOE strike flashing N enemies is
                        // visual noise — matches the Meteor precedent. radiusWorld
                        // snapshots the per-cast AOE radius for the burst visual.
                        if (hasHitChannel)
                            hitQueue.Enqueue(new ProjectileHitEvent
                            {
                                position = impactWorld,
                                dataIndex = projectile.ValueRO.dataIndex,
                                payload = PayloadKind.TileAoe,
                                radiusWorld = tileRange * tileSize,
                                source = entity,
                            });
                        break;
                    }

                    default:
                        // Unknown payload: no resolution. Unlike MoveSystem's default,
                        // this can't leak — the projectile is consumed unconditionally
                        // just below. Present for parity / intent when a future arm lands.
                        break;
                }

                // A re-targeted bounce or an in-flight PathHit survives; the destroy
                // stays unconditional for TileAoe/default and non-bouncing hits.
                if (!survives)
                    ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            aoeEntities.Dispose();
            aoeTransforms.Dispose();
            aoePositions.Dispose();
            defenderEntities.Dispose();
            defenderTransforms.Dispose();
        }
    }
}
