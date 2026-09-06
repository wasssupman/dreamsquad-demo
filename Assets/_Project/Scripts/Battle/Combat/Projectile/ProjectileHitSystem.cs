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
            var pathFollowLookup = SystemAPI.GetComponentLookup<PathFollowState>(isReadOnly: true);
            // defender-knockback-on-impact unit 1 — 사수의 넉백 저작(Combat 소유, RO).
            // 넉백은 **유닛의 성질**이지 화살의 성질이 아니라 탄 SO 로 옮기지 않는다 —
            // 같은 화살을 쓰는 다른 유닛(마크스맨)에 새지 않게 하려는 것이기도 하다.
            var defenderCcLookup = SystemAPI.GetComponentLookup<DefenderCcData>(isReadOnly: true);
            // defender-directional-volley unit 2 — per-projectile victim record so a
            // path sweep damages each target once.
            // RW (dreamcatcher-content-4 unit 2): a rehit cooldown has to *rewrite* the
            // victim's slot, and the ECB has no "modify buffer element N" operation —
            // only AppendToBuffer and SetBuffer (whole-buffer replace). So the record
            // write left the ECB entirely and both the add and the update are direct.
            // Same shape as the outputs decay above; this is a main-thread
            // ISystem.OnUpdate and the buffer belongs to the projectile currently being
            // iterated, so it never aliases the query.
            var pathHitRecordLookup = SystemAPI.GetBufferLookup<PathHitRecord>(isReadOnly: false);
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
            // unit 8 — splash·bounce 의 진영 판정용. 「누가 쐈나」를 알아야 「누구를
            // 때려도 되나」가 나온다.
            var attackTagLookup = SystemAPI.GetComponentLookup<AttackUnitTag>(isReadOnly: true);
            var factionTagLookup = SystemAPI.GetComponentLookup<FactionTag>(isReadOnly: true);
            // bomb-thrower-defender unit 2 — Combat→Effects CC 채널(수면/스턴탄).
            // 수면파이터/존 CC 와 공유하는 기존 EnemyCcEvents 큐. RW 접근 = 큐 변이 의도
            // 명시(threat 큐 대칭). 테스트/초기 프레임엔 없을 수 있어 옵셔널 게이트.
            bool hasCcQ = SystemAPI.TryGetSingletonRW<EnemyCcEventsSingleton>(out var ccEventsRW);
            NativeQueue<EnemyCcEvent> ccQueue = hasCcQ ? ccEventsRW.ValueRW.queue : default;
            // bomb-barrel-on-place unit 2 — Combat→Bridge 설치물 스폰 요청. 해저드 캐스트가
            // 쓰는 **바로 그 채널**이라 신규 채널 0. 맥락 간 통신을 큐로 하는 공인 경로다.
            bool hasHazardSpawnQ = SystemAPI.TryGetSingletonRW<HazardSpawnRequestsSingleton>(out var hazardSpawnRW);
            NativeQueue<HazardSpawnRequest> hazardSpawnQueue = hasHazardSpawnQ ? hazardSpawnRW.ValueRW.queue : default;

            // Combat→Presentation: hit-VFX channel. May not exist before
            // BattleBridge.EnsureQueriesAndQueues runs (very first frames in
            // tests / dev hot-reload), so guarded by HasSingleton.
            bool hasHitChannel = SystemAPI.HasSingleton<ProjectileHitEventsSingleton>();
            NativeQueue<ProjectileHitEvent> hitQueue = default;
            if (hasHitChannel)
                hitQueue = SystemAPI.GetSingleton<ProjectileHitEventsSingleton>().queue;

            // unit 8 리뷰 H-1 — **적 전용 풀(`aoeQuery`)이 은퇴했다.** splash·bounce·PathHit
            // 스윕이 전부 아래 `victimQuery`(양 진영 + 거점)를 쓰고 주인의 상대 진영으로
            // 거른다. 옛 풀이 `AttackUnitTag` 하드코딩이라 「적이 쏘면 자기편을 때린다」가
            // 구조적으로 가능했고, 그걸 막던 것이 브리지의 payload 별 거절 가드였다.
            //
            // ⚠ **이탈(판 밖) 중인 대상은 제외한다** — splash/TileAoe 피해자도, bounce 재조준
            // 후보도 아니다(ultimate-leap unit 2). 직격 호밍은 target 을 이미 들고 있어 여기로
            // 안 걸러지지만, 그 피해는 `DamageApplicationSystem` 의 버퍼 드랍이 잡는다.
            //
            // ⚠ 왜 값 필터인가: ECS 쿼리는 `FactionTag` **값**으로 필터할 수 없어(shared
            // component 가 아니다) 한 벌을 뽑아 놓고 소비 지점에서 마스크로 가른다.
            var victimQuery = SystemAPI.QueryBuilder()
                .WithAny<AttackUnitTag, DefenderUnitTag, StructureTag>()
                .WithAll<LocalTransform, FactionTag>()
                .WithNone<UltimateLeapState>().Build();
            var victimEntities = victimQuery.ToEntityArray(Allocator.Temp);
            var victimTransforms = victimQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var victimFactions = victimQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
            var victimTraversalLayers = new NativeArray<byte>(victimEntities.Length, Allocator.Temp);
            // 순수 함수(`BounceRetarget`)가 인덱스를 돌려주므로 위치·진영도 같은 정렬로 편다.
            var victimPositions = new NativeArray<float3>(victimEntities.Length, Allocator.Temp);
            var victimFactionMasks = new NativeArray<int>(victimEntities.Length, Allocator.Temp);
            // distance-based-range unit 3 — 대상의 몸 반경. 없으면 0 = 점(오늘과 동일).
            var victimBodyRadii = new NativeArray<float>(victimEntities.Length, Allocator.Temp);
            var hitRadiusLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.HitRadius>(isReadOnly: true);
            for (int i = 0; i < victimEntities.Length; i++)
            {
                if (pathFollowLookup.HasComponent(victimEntities[i]))
                    victimTraversalLayers[i] = pathFollowLookup[victimEntities[i]].traversalLayers;
                victimPositions[i] = victimTransforms[i].Position;
                victimFactionMasks[i] = (int)victimFactions[i].value;
                victimBodyRadii[i] = hitRadiusLookup.HasComponent(victimEntities[i])
                    ? hitRadiusLookup[victimEntities[i]].value : 0f;
            }

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
                        byte directTargetLayers = target != Entity.Null
                            && pathFollowLookup.HasComponent(target)
                            ? pathFollowLookup[target].traversalLayers
                            : (byte)0;
                        if (target != Entity.Null
                            && transformLookup.HasComponent(target)
                            && PlacementLayers.CanTarget(
                                projectile.ValueRO.targetTraversalLayers,
                                directTargetLayers))
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
                                                    // ⚠ 아래 ApplyStack 과 달리 여기는 **투사체 엔티티**가 source 다.
                                                    // StatModifierSlot 의 병합 키도 (source, stat, op, stackId) 라
                                                    // 발사마다 새 슬롯이 생겨 곱연산이 누적된다(Enemy_Debuffer
                                                    // DamageMul ×0.6 → 0.6ⁿ). 지금 고치지 않는 이유는 라이브
                                                    // 밸런스가 바뀌기 때문 — 현재는 ModifierMath 의 클램프
                                                    // [0.2, 5](modifier-stacking-policy)가 병리를 경계하고 있다.
                                                    // 수치 재조정과 한 묶음으로 별도 처리:
                                                    // docs/spec/enemy-fire-stack-shooter/README.md 후속 후보.
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
                                                    maxStack = output.stackMaxStack > 0 ? output.stackMaxStack : StackModifierSO.DefaultMaxStack,
                                                    perAppDuration = output.duration,
                                                    // enemy-fire-stack-shooter unit 0 — 병합 키는 (source, kind)
                                                    // 다(ModifierApplySystem). 투사체는 발사마다 새 엔티티라 그걸
                                                    // 실으면 매 히트가 새 슬롯을 만들어 stackCount 가 영원히 1이고
                                                    // 임계(5스택)에 절대 도달하지 못한다. 근접 경로
                                                    // (AttackSystem, source = attackerEntity)와 같은 규약 = 사수.
                                                    // Null 폴백은 bridge-cast 투사체(owner 없음)용 현행 동작 보존.
                                                    source = threatOwner != Entity.Null ? threatOwner : entity,
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

                            // defender-knockback-on-impact unit 1 — 유닛 넉백은 **맞는 순간** 난다.
                            // 발사 시점의 AttackSystem 은 이 payload 에서 넉백을 걸지 않고 여기로
                            // 넘긴다(그쪽 `knockbackAtImpact`). 사거리 4·탄속 12 면 즉시 걸 때
                            // 0.33초 먼저 밀려, 0.2초마다 쏘는 유닛에서는 한 발치를 넘는 어긋남이었다.
                            //
                            // 방향 = **피격자가 가던 방향의 반대** 하나다(사용자 결정 B, 2026-08-17).
                            // ⚠ 탄의 진행 방향을 쓸 수 없다 — 유도탄은 도착할 때 좌표가 대상과
                            // 정확히 같아져서(`ProjectileMoveSystem`: dist <= step → newPos = targetPos)
                            // 진행 벡터가 0 이 된다. 훑는 탄(PathHit)의 넉백이 스윕 방향을 쓰는 것과
                            // 규칙이 다른 것은 의도다 — 그쪽은 「지나가며 밀어낸다」가 곧 그 능력이다.
                            //
                            // 사수가 비행 중에 죽으면 넉백도 없다 — 위협 귀속과 같은 규약.
                            // 방향이 없는 대상(스폰 직후·구조물)은 밀지 않는다.
                            if (hasCcQ
                                && damageBufferLookup.HasBuffer(target)
                                && defenderCcLookup.HasComponent(threatOwner)
                                && pathFollowLookup.HasComponent(target))
                            {
                                var ownerCc = defenderCcLookup[threatOwner];
                                float2 travel = pathFollowLookup[target].lastMoveDir;
                                if (ownerCc.knockbackDistance > 0f
                                    && ownerCc.knockbackDuration > 0f
                                    && math.lengthsq(travel) > 1e-6f)
                                {
                                    float2 kb = -math.normalize(travel)
                                                * (ownerCc.knockbackDistance / ownerCc.knockbackDuration);
                                    ccQueue.Enqueue(new EnemyCcEvent
                                    {
                                        target = target,
                                        effect = new CcEffect
                                        {
                                            kind = CcKind.Impulse,
                                            vector = new float3(kb.x, 0f, kb.y),
                                            remainingTime = ownerCc.knockbackDuration,
                                        },
                                    });
                                }
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
                                // skill-layer-migration unit 8 — **양 진영 풀 + 주인의 상대**.
                                // 예전엔 적 전용 풀이라 적이 쏜 splash 가 자기편을 태웠다.
                                int splashWanted = OpponentMaskOfOwner(
                                    projectile.ValueRO.owner, ref factionTagLookup,
                                    ref attackTagLookup, ref defenderTagLookup);
                                for (int i = 0; i < victimEntities.Length; i++)
                                {
                                    var candidate = victimEntities[i];
                                    if (candidate == target) continue;
                                    // 마스크는 이제 절대 0 이 아니다(미상은 플레이어로 접힌다).
                                    if ((victimFactionMasks[i] & splashWanted) == 0) continue;
                                    if (!PlacementLayers.CanTarget(
                                            projectile.ValueRO.targetTraversalLayers,
                                            victimTraversalLayers[i])) continue;
                                    float dx = victimTransforms[i].Position.x - aoeCenter.x;
                                    float dz = victimTransforms[i].Position.z - aoeCenter.z;
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
                                for (int i = 0; i < victimEntities.Length; i++)
                                    if (victimEntities[i] == target) { excludeIdx = i; break; }

                                // unit 8 — splash 와 같은 이유로 양 진영 풀 + 주인의 상대.
                                // 튕김이 자기편으로 넘어가면 「내 탄이 나를 사냥하는」 그림이 된다.
                                int bounceWanted = OpponentMaskOfOwner(
                                    projectile.ValueRO.owner, ref factionTagLookup,
                                    ref attackTagLookup, ref defenderTagLookup);
                                int nextIdx = BounceRetarget.FindNext(
                                    targetPos, excludeIdx, victimPositions, victimTraversalLayers,
                                    projectile.ValueRO.targetTraversalLayers,
                                    victimFactionMasks, bounceWanted,
                                    projectile.ValueRO.bounceTileRange, tileSize, gridSize, ffOrigin,
                                    victimBodyRadii);

                                if (nextIdx >= 0)
                                {
                                    float mul = projectile.ValueRO.bounceDamageMul;
                                    var next = projectile.ValueRO;
                                    next.target = victimEntities[nextIdx];
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
                        //
                        // dreamcatcher-content-4 unit 2 — "at most once" is now "at most
                        // once per rehit window"; rehitCooldownSec 0 keeps it literal.
                        if (!transformLookup.HasComponent(entity)) break;

                        float2 prev = projectile.ValueRO.prevPos.xz;
                        float2 curr = transformLookup[entity].Position.xz;
                        float2 dir = projectile.ValueRO.direction;
                        float radius = projectile.ValueRO.hitThreshold;
                        int budget = projectile.ValueRO.pierceRemaining;
                        float dmg = projectile.ValueRO.damage;

                        // dreamcatcher-content-4 unit 2 — 재타격 쿨타임(계약 3). >0 이면
                        // 같은 적을 쿨타임마다 다시 때리고 **관통 예산을 소모하지 않는다**:
                        // 궤도 화염구의 유일한 종료 조건은 수명이라, 예산을 깎으면 몇 명 스치고
                        // N초를 못 채운 채 사라진다. 예산을 **읽지도** 않는 이유도 같다 —
                        // pierceCount 0 으로 저작된 탄이 조용히 아무도 못 때리거나 1 이면
                        // 프레임당 한 명으로 잘려, 「수명이 유일한 종료 조건」이 거짓이 된다.
                        // 0 = 기존 방향탄(샷건너·머신거너) 그대로.
                        float rehitCooldown = projectile.ValueRO.rehitCooldownSec;
                        // 시계는 투사체 자기 시계(elapsed) — 이동 arm 이 굴린다. 궤도가 굴리고
                        // DirectionalLinear 는 굴리지 않으므로, 방향탄에 이 값을 켜면 첫 타 뒤
                        // 창이 영영 안 열려 **기존 1회 동작으로 안전하게 퇴화**한다(오작동 아님).
                        float now = projectile.ValueRO.elapsed;

                        // 프로덕션에선 브리지 드레인이 PathHit 스폰마다 이 버퍼를 붙인다
                        // (BattleBridge). 없는 채로 도는 탄이 있어도 기록만 건너뛰고 굴러간다 —
                        // ECB append 시절엔 그런 탄이 **플레이백을 통째로 끊어** 뒤따르는
                        // SetComponent/DestroyEntity 까지 날렸다(직접 쓰기의 부수 효과).
                        bool hasRecords = pathHitRecordLookup.HasBuffer(entity);
                        // ⚠ **기록 없이는 재타격을 켜지 않는다**(ECS 리뷰 M1). 재타격 레짐에서
                        // 기록은 «장식» 이 아니라 **유일한 방어선**이다 — 관통 예산도 안 깎으므로,
                        // 버퍼가 없으면 스윕 안의 적 전원을 **프레임마다** 때린다(60fps·3초면
                        // 의도의 ~30배). 지금은 PathHit 스폰 seam 이 하나뿐이고 거기서 무조건
                        // 버퍼를 붙여 도달 불가지만, 조용한 fail-open 을 남겨두지 않는다.
                        // 이 한 줄로 버퍼 없는 탄은 «적당 1회» 로 안전 퇴화한다.
                        bool rehits = rehitCooldown > 0f && hasRecords;

                        // dreamcatcher-content-5 unit 2 — 스친 적을 **그 프레임 진행 방향**으로
                        // 민다. 값은 탄 SO 소유이고 0 = 꺼짐이라 기존 관통탄은 전부 무변화.
                        //
                        // 방향을 `dir`(= state.direction)이 아니라 **스윕 벡터**에서 뽑는 이유:
                        // 왕복(부메랑)에서 그 필드는 **발사 축이라 돌아올 때도 안 뒤집힌다**
                        // (뒤집으면 궤적이 깨진다 — MovementKind.BoomerangReturn 주석).
                        // 실제로 움직인 방향만이 「맞은 순간의 진행 방향」이고, 왕복이 두 다리에서
                        // 반대 힘이 되는 것은 그 결과다(계약 5 — 다리 상태를 어디에도 안 둔다).
                        //
                        // 퇴화(변위 0)면 쏘지 않는다 — 방향 없는 넉백 방출은 근접 경로도 막는다.
                        float kbSpeed = projectile.ValueRO.knockbackSpeed;
                        float2 kbSweep = curr - prev;
                        bool knocks = kbSpeed > 0f
                                      && projectile.ValueRO.knockbackDuration > 0f
                                      && hasCcQ
                                      && math.lengthsq(kbSweep) > 1e-8f;
                        float2 kbDir = knocks ? math.normalize(kbSweep) : float2.zero;

                        // 방향탄 bounce — 관통을 다 쓴 지점(마지막 victim)에서 튕긴다.
                        int lastVictimIdx = -1;
                        float3 lastVictimPos = default;

                        var sweptIdx = new NativeList<int>(Allocator.Temp);
                        var sweptDist = new NativeList<float>(Allocator.Temp);
                        // 후보의 기록 슬롯(-1 = 미기록)을 같이 들고 간다 — 실제로 때릴 때 두 번째
                        // 스캔 없이 그 슬롯을 갱신하려는 것. 아래 루프가 버퍼에 하는 일은 append
                        // 와 제자리 덮어쓰기뿐이라 **원소가 이동하지 않는다** — 그래서 여기서
                        // 잡아 둔 인덱스가 프레임 끝까지 유효하다.
                        var sweptRec = new NativeList<int>(Allocator.Temp);
                        // unit 8 리뷰 H-1 — splash·bounce 와 **같은 이유로** 양 진영 풀 +
                        // 주인의 상대. 여기가 적 전용으로 남아 있어서 「PathHit 하드코딩이
                        // 사라졌다」는 내 은퇴 근거가 거짓이었다(궤도 화염구가 이 경로다).
                        int pathWanted = OpponentMaskOfOwner(
                            projectile.ValueRO.owner, ref factionTagLookup,
                            ref attackTagLookup, ref defenderTagLookup);
                        for (int i = 0; i < victimEntities.Length; i++)
                        {
                            if ((victimFactionMasks[i] & pathWanted) == 0) continue;
                            if (!PlacementLayers.CanTarget(
                                    projectile.ValueRO.targetTraversalLayers,
                                    victimTraversalLayers[i])) continue;
                            float2 victimPos = victimPositions[i].xz;
                            // ⚠ **유효 반경 = 투사체 피격 반경 + 대상 몸 반경.** 두 값은 뜻이 다르다 —
                            // `radius`(= `hitThreshold`)는 «이 탄이 얼마나 관대하게 맞히나», 몸 반경은
                            // «이 대상이 얼마나 큰가». 큰 몸이 큰 표적인 것은 물성이고, 탄의 관대함을
                            // 전 대상에 균일하게 올려 흉내 내던 것이 `MachineGunBullet` 의 0.4→0.7 완화다.
                            // ⚠ `radius`(=hitThreshold)는 **월드**, `victimBodyRadii` 는 **타일**이다 —
                            // 환산 없이 더하면 tileSize ≠ 1 에서 조용히 틀어진다.
                            if (!SweepHitMath.SegmentHits(prev, curr, victimPos,
                                    radius + victimBodyRadii[i] * tileSize)) continue;
                            int recIdx = -1;
                            if (hasRecords && !PathHitRecord.CanHit(
                                    pathHitRecordLookup[entity], victimEntities[i],
                                    now, rehitCooldown, out recIdx)) continue;
                            sweptIdx.Add(i);
                            // content-5 — front-most 정렬 기준을 **그 프레임의 실제 이동 방향**으로
                            // 바꾼다. `dir`(= state.direction)은 왕복에서 **발사 축이라 귀환 다리에서도
                            // 안 뒤집힌다** → 「가장 앞」이 실제로는 「가장 늦게 닿는」 적이 됐다.
                            // DirectionalLinear 은 두 값이 같아 무회귀. 넉백이 같은 이유로 이미
                            // 스윕에서 방향을 뽑는데(아래) 정렬만 옛 기준으로 남아 있었다.
                            sweptDist.Add(math.dot(victimPos - prev, math.normalizesafe(curr - prev, dir)));
                            sweptRec.Add(recIdx);
                        }

                        // Front-most first: a 1-pierce shot must stop at the nearest
                        // enemy it crossed, and snapshot order carries no meaning.
                        while ((rehits || budget > 0) && sweptIdx.Length > 0)
                        {
                            int nearest = 0;
                            for (int k = 1; k < sweptIdx.Length; k++)
                                if (sweptDist[k] < sweptDist[nearest]) nearest = k;

                            var victim = victimEntities[sweptIdx[nearest]];
                            float3 victimPos = victimPositions[sweptIdx[nearest]];
                            if (damageBufferLookup.HasBuffer(victim))
                            {
                                float vdmg = (victim == prioTarget ? dmg * prioMul : dmg) * heavyMul;
                                ecb.AppendToBuffer(victim, new IncomingDamage { amount = vdmg, source = threatOwner });
                                ThreatTable.TryCredit(threatQueue, creditThreat, threatLookup, victim, threatOwner, vdmg);

                                // content-5 unit 2 — 넉백은 **피해가 실제로 들어간 그 순간에만** 나간다.
                                // ⚠ 그래서 이 블록 **안**에 있다 — 밖에 두면 피해 버퍼가 없는 대상을
                                // 밀어서 주석과 코드가 갈린다(리뷰 L3).
                                // 재타격 쿨타임에 막혀 이 루프에 못 들어온 프레임은 밀지도 않는다 —
                                // 안 그러면 스치는 내내 매 프레임 밀려 적이 날아간다.
                                // ⚠ 병합 키가 kind 하나라 피해자당 Impulse 슬롯은 **게임 전체에 하나**다:
                                // 남은 시간은 max, 방향은 나중 것이 덮어쓴다(CcEffectMerge).
                                if (knocks)
                                    ccQueue.Enqueue(new EnemyCcEvent
                                    {
                                        target = victim,
                                        effect = new CcEffect
                                        {
                                            kind = CcKind.Impulse,
                                            vector = new float3(kbDir.x, 0f, kbDir.y) * kbSpeed,
                                            remainingTime = projectile.ValueRO.knockbackDuration,
                                        },
                                    });
                            }
                            // 기록은 **갱신이지 추가가 아니다** — 매 바퀴 append 하면 궤도
                            // 화염구의 버퍼가 수명 내내 자란다. ECB 를 못 쓰는 이유는 lookup
                            // 선언부 주석 참조(원소 수정 오퍼레이션이 없다).
                            if (hasRecords)
                            {
                                // cooldown 0 이면 nextHitAt 은 읽히지 않는 값이라 분기 불요.
                                var record = new PathHitRecord { value = victim, nextHitAt = now + rehitCooldown };
                                var records = pathHitRecordLookup[entity];
                                int recIdx = sweptRec[nearest];
                                if (recIdx >= 0) records[recIdx] = record;
                                else records.Add(record);
                            }


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

                            lastVictimIdx = sweptIdx[nearest];
                            lastVictimPos = victimPos;

                            if (!rehits) budget--;
                            sweptIdx.RemoveAtSwapBack(nearest);
                            sweptDist.RemoveAtSwapBack(nearest);
                            sweptRec.RemoveAtSwapBack(nearest);
                        }
                        sweptIdx.Dispose();
                        sweptDist.Dispose();
                        sweptRec.Dispose();

                        // Out of budget = spent; impactReached = flew its full range.
                        // 재타격 탄은 예산을 안 깎으므로 예산 게이트도 안 본다(계약 3) —
                        // 남은 종료 조건은 수명뿐이다.
                        var next = projectile.ValueRO;
                        next.pierceRemaining = budget;
                        bool dirty = budget != projectile.ValueRO.pierceRemaining;
                        survives = (rehits || budget > 0) && !projectile.ValueRO.impactReached;

                        // ── 통통구슬 × 방향탄 (defender-directional-volley 후속 결정) ──
                        // 이 탄이 더 뚫을 수 없게 된 순간(예산 소진 또는 사거리 끝) bounce 가
                        // 남아 있으면, 마지막으로 맞힌 적에서 다음 적으로 **호밍 전환**해
                        // 재비행한다. 같은 엔티티를 유지하므로 뷰/트레일이 그대로 이어진다
                        // (SingleSplash bounce 와 동일 원리 — ViewPool 은 Homing/Directional 을
                        // 특수 처리하지 않아 전환 프레임에 끊기지 않는다).
                        //
                        // ⚠ 머신건 탄은 `pierceCount: 1` 이라 **관통이 없다** — 첫 적을
                        // 맞히는 즉시 예산이 0 이 되고 곧바로 튕긴다. 즉 실사용 형태는
                        // "관통하다 튕김"이 아니라 "**맞히고 튕김**"이다. pierce > 1 인 탄이
                        // 생기면 같은 코드가 "다 뚫고 나서 튕김"이 된다.
                        //
                        // 계약 2가지:
                        //  · **바운스는 마지막 히트 프레임에서만** 발생한다. 아무도 못 맞히고
                        //    사거리 끝에 도달하면 튕길 기준점이 없으므로 그대로 소멸한다
                        //    (프레임을 넘겨 lastVictim 을 기억하는 상태는 만들지 않는다).
                        //  · **PathHitRecord 를 승계하지 않는다.** 전환 후엔 SingleSplash 라
                        //    그 버퍼를 읽지 않으므로, pierce>1 탄이 A→B 를 뚫고 B 에서 A 로
                        //    다시 튕길 수 있다 — SingleSplash 바운스의 A→B→A 선례와 같다.
                        if (!survives && next.bounceRemaining > 0 && lastVictimIdx >= 0)
                        {
                            // unit 8 리뷰 H-1 — 같은 이유로 양 진영 풀 + 주인의 상대.
                            int dirWanted = OpponentMaskOfOwner(
                                projectile.ValueRO.owner, ref factionTagLookup,
                                ref attackTagLookup, ref defenderTagLookup);
                            int nextIdx = BounceRetarget.FindNext(
                                lastVictimPos, lastVictimIdx, victimPositions, victimTraversalLayers,
                                next.targetTraversalLayers,
                                victimFactionMasks, dirWanted,
                                next.bounceTileRange, tileSize, gridSize, ffOrigin);
                            if (nextIdx >= 0)
                            {
                                next.movement = MovementKind.HomingToEntity; // 방향 → 호밍
                                next.payload = PayloadKind.SingleSplash;     // 스윕 → 단일 착탄
                                next.target = victimEntities[nextIdx];
                                next.impactReached = false;
                                next.bounceRemaining -= 1;
                                next.damage *= next.bounceDamageMul;

                                // outputs 스냅샷을 떼어 **Damage-only 계약을 유지**한다.
                                // PathHit arm 은 state.damage 하나만 쓰지만 SingleSplash arm 은
                                // outputs 가 있으면 전 kind(Stat/Stack/Heal)를 디스패치한다 —
                                // 그대로 두면 "경로 히트엔 안 걸리던 슬로우가 바운스 홉에만
                                // 걸리는" 비대칭이 생긴다. 기획이 방향탄에 상태이상 output 을
                                // 붙이는 순간 코드 변경 없이 열리는 구멍이라 여기서 닫는다.
                                // (버퍼가 없으면 SingleSplash 는 state.damage 폴백을 탄다.)
                                if (outputLookup.HasBuffer(entity))
                                    ecb.RemoveComponent<AttackOutputElement>(entity);

                                dirty = true;
                                survives = true;
                            }
                        }
                        if (dirty) ecb.SetComponent(entity, next);
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
                        int tileRange = projectile.ValueRO.impactTileRange;
                        // unit 23b — 0 = 이 자리에 주인이 없다(진짜 날아온 탄) → 칸 반폭.
                        float originBodyR = projectile.ValueRO.originBodyRadius;
                        float dmg = projectile.ValueRO.damage;
                        // nightmare-catcher unit 4 — victim pool by targetFaction.
                        // Enemy(0) = legacy pool so player Meteor / defender
                        // ballistic are byte-identical to before (N3); the boss
                        // AreaBarrage arm is the only Defender setter (unit 2).
                        bool hitsDefenders = projectile.ValueRO.targetFaction == ProjectileTargetFaction.Defender;
                        // battle-structures unit 9 — 풀 선택이 아니라 **진영 마스크 선택**이다.
                        // 유닛·거점을 구분하지 않는다: 「상대 진영에 속한 것」이 곧 피해자다.
                        int wantMask = hitsDefenders
                            ? Wassup.Battle.Units.Factions.AnyDefender
                            : Wassup.Battle.Units.Factions.AnyEnemy;

                        // bomb-thrower-defender unit 2 — 범위 내 victim 인덱스 + impact
                        // 중심 거리²를 모아 가까운 순 aoeTargetCap 개로 절단(0 = 무제한 =
                        // 레거시 메테오/스킬/보스 경로, byte-identical). 데미지와 CC 를
                        // 같은 capped 집합에 적용. 비폭탄 spawn 은 cap 0·ccKind 0 → 무회귀.
                        // battle-structures unit 0 — 후보를 엔티티 리스트로 수집해 cap 선정에 태운다.
                        // goal-stability 의 별도 «골 풀» 합류는 제거했다: 풀이 한 벌이라
                        // 골이 그 안에 있고, 두 풀을 이어 붙이면 중복 제거가 없어 광역 1발이
                        // 골을 2번 때렸다(unit 9 에서 풀이 한 벌로 합쳐져 이 위험이 구조적으로 사라졌다).
                        // on-place-skill-rework unit 11 — **이 팔은 `target` 을 읽지 않는다.**
                        // 「그 칸에 있는 것을 때린다」 한 문장이 이 페이로드의 전부다.
                        //
                        // unit 8 이 여기에 임자(`target`) 게이트를 넣었다가 unit 11 에서 걷어냈다.
                        // 목적은 「반경 안 적 전원에게 1발씩」이었는데, 셀 낙하탄에 적 조준을 얹으면
                        // **한 탄에 조준이 둘**이 된다: 궤적은 발사 시점의 칸에 고정되고(다시 조준하지
                        // 않는 것이 예고의 사양이다) 페이로드는 착탄 시점의 적을 본다. 실측 예고
                        // 0.40s × 적 속도 2.00 = 0.80타일 이동인데 칸 소속 유지 폭은 ±0.50타일 →
                        // 최소 예고에도 벗어나 **피해 0**(뒤 슬롯 0.72s=1.44타일은 전원 헛방).
                        // 게이트 이전엔 그 칸에 **누가 있든** 때려서 행군하는 뒤 적이 빈 칸을 채웠고,
                        // 조준이 낡았다는 사실이 그렇게 가려져 있었다.
                        //
                        // 지금 「적 단위 폭격」은 `SkyFallOnEntity`(하늘낙하 × 적 조준) + `SingleSplash`
                        // 가 담당한다 — 조준이 하나라 어긋날 것이 없다. ⚠ 그러니 **이 팔에 `target`
                        // 을 다시 들이지 말 것.** 대상 하나를 때리고 싶으면 페이로드를 바꾸는 것이
                        // 맞고, 광역을 단일 대상으로 좁히는 게이트는 같은 결함을 다시 만든다.
                        var inRangeEnts = new NativeList<Entity>(Allocator.Temp);
                        var inRangeDistSq = new NativeList<float>(Allocator.Temp);
                        for (int i = 0; i < victimEntities.Length; i++)
                        {
                            // unit 9 — 상대 진영만. 이 한 줄이 «자기편 오폭» 을 막는다.
                            if (((int)victimFactions[i].value & wantMask) == 0) continue;
                            if (!PlacementLayers.CanTarget(
                                    projectile.ValueRO.targetTraversalLayers,
                                    victimTraversalLayers[i])) continue;
                            float3 vpos = victimTransforms[i].Position;
                            // unit 18 — 피해자 **위치** 기준(셀 양자화 제거). 자는 unit 4b 그대로
                            // (원 = 반경 + 칸 반폭 + 대상 몸), 입력만 연속이 됐다.
                            float aoeInvT = tileSize > 1e-6f ? 1f / tileSize : 1f;
                            // unit 23b — **자리에 «주인» 이 있으면 그 몸이 원점 항이다.**
                            // 자기 자리 폭발(`flightTime = 0` 즉발: 자폭·시체폭발·사망폭발·
                            // 도약 슬램)은 폭심이 트리거 대상의 몸 중심이라 칸 반폭이 틀리다.
                            // 진짜 날아온 탄의 착탄점은 주인이 없어(0) 종전대로 자리형이다.
                            if (!Wassup.Skills.SkillMath.ReachFromImpact(
                                    (vpos.x - impactWorld.x) * aoeInvT,
                                    (vpos.z - impactWorld.z) * aoeInvT,
                                    tileRange, originBodyR, victimBodyRadii[i])) continue;
                            inRangeEnts.Add(victimEntities[i]);
                            float dx = vpos.x - impactWorld.x;
                            float dz = vpos.z - impactWorld.z;
                            inRangeDistSq.Add(dx * dx + dz * dz);
                        }
                        var selectedAoe = new NativeList<int>(Allocator.Temp);
                        AoeTargetCap.SelectNearest(inRangeDistSq.AsArray(), projectile.ValueRO.aoeTargetCap, ref selectedAoe);

                        byte bombCc = projectile.ValueRO.ccKind;
                        float bombCcDur = projectile.ValueRO.ccDuration;
                        for (int s = 0; s < selectedAoe.Length; s++)
                        {
                            var victim = inRangeEnts[selectedAoe[s]];
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
                        inRangeEnts.Dispose();
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

                    // bomb-barrel-on-place unit 2 — 착탄 지점 칸에 길막 설치물을 세운다.
                    // **피해 0**: 배럴은 폭탄이 아니라 물건이고, 터지는 것은 부서질 때다(unit 0).
                    // 실제 스폰은 브리지 드레인이 하고 여기선 기존 요청 채널에 넣기만 한다
                    // (신규 채널 0). 캐스트 해저드가 쓰는 그 채널 그대로다.
                    case PayloadKind.SpawnBlocker:
                    {
                        if (projectile.ValueRO.blockerDataIndex < 0)
                        {
                            // 조용한 무발동 금지 — 저작이 빠지면 「던졌는데 아무것도 안 선다」가
                            // 되고, 탄은 아래에서 그대로 소모되어 단서가 남지 않는다.
                            UnityEngine.Debug.LogWarning(
                                "[ProjectileHitSystem] SpawnBlocker payload with no blockerDataIndex — nothing will be built.");
                            break;
                        }
                        if (!hasHazardSpawnQ) break;
                        int2 blockerCell = GridMath.WorldToCell(
                            projectile.ValueRO.impact, tileSize, gridSize, origin: ffOrigin);
                        hazardSpawnQueue.Enqueue(new HazardSpawnRequest
                        {
                            kind = HazardCastKind.Blocking,
                            dataIndex = projectile.ValueRO.blockerDataIndex,
                            centerCell = blockerCell,
                            width = 1,
                            height = 1,
                            // 설치물 자체는 시전자를 안 쓴다(모양·체력·수명이 전부 SO 다).
                            // 드레인의 시전자 생존 검사는 길막 종류에서 걷어냈다 — 비행 중
                            // 폭탄맨이 죽어도 배럴은 서야 한다(spec 계약 7).
                            caster = projectile.ValueRO.owner,
                            target = Entity.Null,
                            targetTraversalLayers = projectile.ValueRO.targetTraversalLayers,
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
            victimPositions.Dispose();
            victimFactionMasks.Dispose();
            victimBodyRadii.Dispose();
            victimEntities.Dispose();
            victimTransforms.Dispose();
            victimFactions.Dispose();
            victimTraversalLayers.Dispose();
        }
    
        // skill-layer-migration unit 8 — 투사체 주인의 **상대 진영 마스크**.
        //
        // ⚠ **0 은 「진영을 안 본다」다.** 주인이 없거나(플레이어 액티브 스킬탄) 이미
        // 파괴됐으면 진영을 물을 수 없고, 그때는 옛 동작(풀 전체)을 유지한다 —
        // 여기서 fail-closed 로 뒤집으면 플레이어 스킬탄의 splash 가 통째로 사라진다.
        // 판정 자체는 `FactionRelation` 이 소유한다(복제하면 조용히 갈린다).
        private static int OpponentMaskOfOwner(
            Entity owner,
            ref ComponentLookup<FactionTag> factionTags,
            ref ComponentLookup<AttackUnitTag> attackTags,
            ref ComponentLookup<DefenderUnitTag> defenderTags)
        {
            // ⚠ **미상은 0(필터 없음)이 아니다**(unit 8 리뷰 M-4). 후보 풀이 양 진영 +
            // 거점으로 넓어졌기 때문에, 여기서 0 을 돌려주면 「옛 동작」이 아니라
            // **아군과 골 타워까지 때리는** 동작이 된다. 주인이 없다 = 플레이어 액티브
            // 스킬탄이므로 디스패처가 시전자 없는 발동을 접는 것과 **같은 값**으로 접는다
            // (`BuildCaster` → `CasterRef.Player(DefenderUnit)`). 두 자리가 갈리면
            // 같은 탄이 누가 쐈느냐에 따라 다른 편을 때린다.
            if (owner == Entity.Null || !factionTags.HasComponent(owner)
                && !attackTags.HasComponent(owner) && !defenderTags.HasComponent(owner))
                return (int)Wassup.Battle.Units.FactionRelation.OpponentUnitsOf(
                    Wassup.Battle.Units.Faction.DefenderUnit);
            bool hasTag = factionTags.HasComponent(owner);
            var faction = Wassup.Battle.Units.FactionRelation.Resolve(
                hasTag,
                hasTag ? factionTags[owner].value : Wassup.Battle.Units.Faction.None,
                attackTags.HasComponent(owner),
                defenderTags.HasComponent(owner));
            return (int)Wassup.Battle.Units.FactionRelation.OpponentUnitsOf(faction);
        }
}
}
