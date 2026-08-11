using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Battle.Combat
{
    // nightmare-catcher unit 2 — PeriodicTimer × AreaBarrage arm. Gated on
    // DcTriggerSlot buffer presence only (faction-neutral by construction —
    // no BossTag/DefenderUnitTag in the gate, spec unit 4): any slot carrier
    // with a PeriodicTimer slot ticks here; defender card slots are skipped by
    // trigger-kind dispatch (their periodSeconds is 0 anyway — 계약 9 guard).
    //
    // Fire = one SkyFall×TileAoe carrier request into the existing projectile
    // drain (dc-trigger contract 6: the slot owner's own attack may stage a
    // request the same frame, so a dedicated carrier entity is required).
    // Orthogonal to the basic attack by construction: nothing here touches
    // AttackState / AiState / movement (계약 4).
    //
    // nightmare-whip-aura unit 1 — second payload arm on the same tick:
    // AllyMoveSpeedAura pulses a MoveSpeedMul modifier (TTL) onto same-faction
    // units in range via StatModifierApplyEvents; release is TTL expiry only.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial struct BossPeriodicTriggerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DcTriggerSlot>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // BattleSimGroup dt — TimeManager Battle-domain scaled (slomo 포함).
            float dt = SystemAPI.Time.DeltaTime;
            var ff = SystemAPI.GetSingleton<FlowFieldSingleton>();

            // 방어유닛 셀 스냅샷 — whip 오라의 defender-host 경로가 쓴다(entities 는
            // 아래에서 보충). projectile-emission-pattern unit 4 로 융단폭격 진앙이
            // emitter 로 이관돼, 이제 이 배열의 유일한 소비자는 whip 이다.
            var defQuery = SystemAPI.QueryBuilder().WithAll<DefenderUnitTag, LocalTransform>().Build();
            var defTransforms = defQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var defCells = new NativeArray<int2>(defTransforms.Length, Allocator.Temp);
            for (int i = 0; i < defTransforms.Length; i++)
                defCells[i] = GridMath.WorldToCell(defTransforms[i].Position, ff.tileSize, ff.gridSize, origin: ff.origin);

            // nightmare-whip-aura unit 1 — whip pulse state: Effects channel ref
            // (RW — queue mutation intent) + lazy same-faction pools, built at
            // most once per frame and only when a whip slot actually fires
            // (unlike the eager defender cell snapshot above, which carries no
            // entities).
            bool hasStatEvents = SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>(out var statEventsRef);
            // unit 3 — 펄스 연출: 버프가 실제로 나간 펄스만 host 위치에 hit-VFX
            // 1회 재생 (blink 퍼프 선례 — Combat→Presentation 기존 채널).
            bool hasHitQ = SystemAPI.TryGetSingletonRW<ProjectileHitEventsSingleton>(out var hitRW);
            NativeQueue<ProjectileHitEvent> hitQueue = hasHitQ ? hitRW.ValueRW.queue : default;
            // projectile-emission-pattern unit 3 — 패턴 push seam. arm 이 하는 일은
            // 인스턴스 하나를 host 버퍼에 넣는 것뿐이고, 발사 전개는 emitter 소유다.
            var patternLookup = SystemAPI.GetBufferLookup<Projectile.Emission.PatternSlot>(isReadOnly: false);
            var instanceLookup = SystemAPI.GetBufferLookup<Projectile.Emission.EmitterInstance>(isReadOnly: false);

            // boss-mamemo unit 1 — 자장가(AreaSleep) seam. 수면은 대상 진영이 반대일 뿐
            // 부여 경로는 기존 CC 파이프라인 그대로다 — CcApplySystem 에 대상 진영 게이트가
            // 없고(거점 skip · 버퍼 부재 skip · 보스 면역만 판정), 방어유닛은 이미 CcEffect
            // 버퍼를 갖고 AttackSystem 이 공격자의 IsLocked 를 본다. 신규 채널 0.
            // ⚠ 기존 AreaSleep 실행기(BattleBridge.DrainShieldBreakEvents)는 **쓰지 않는다** —
            // 그쪽 대상 풀이 AttackUnitTag 하드코딩이라 손대면 실드파열 카드가 깨진다.
            // payload kind 만 공유하고 실행 경로는 별개다.
            bool hasCcQ = SystemAPI.TryGetSingletonRW<EnemyCcEventsSingleton>(out var ccRW);
            // boss-mamemo unit 3 — 악몽의 가호 seam. 가디언 전용 생산자(ShieldCastSystem)를
            // 재사용하지 않고 그 아래층(Units 소유 IncomingShield 버퍼)에 append 한다.
            var incomingShieldLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Units.IncomingShield>(isReadOnly: false);
            var shieldSlotLookup = SystemAPI.GetBufferLookup<Wassup.Battle.Units.ShieldSlot>(isReadOnly: true);
            // 실드 부여 원샷 연출 — 가디언이 이미 쓰는 채널(→ VfxSpawner.SpawnShieldGranted).
            bool hasShieldVfxQ = SystemAPI.TryGetSingletonRW<ShieldGrantedEventsSingleton>(out var shieldVfxRW);

            var pulseTargets = new NativeList<int>(Allocator.Temp);
            var pulseDistSq = new NativeList<float>(Allocator.Temp);
            var pulsePicked = new NativeList<int>(Allocator.Temp);
            // 진영별 후보 풀 — 원래 whip(같은 진영) 전용이었으나 자장가가 **반대 진영**을
            // 쓰면서 둘 다 어느 payload 에서든 채워질 수 있다. 그래서 이름에서 whip 을 뗀다.
            NativeArray<Entity> enemyEntities = default, defEntities = default;
            NativeArray<int2> enemyCells = default;
            // 자장가의 "가까운 M명" 은 셀이 아니라 **월드 거리²** 로 고른다. 셀 거리는 동률이
            // 흔하고 그 동률을 쿼리 인덱스 순서가 가르기 때문이다(형제 경로인 실드파열 AreaSleep
            // 도 월드 거리를 쓴다 — 같은 payload 는 같은 선별 규칙이어야 한다).
            NativeArray<LocalTransform> enemyTransformsPool = default;
            bool enemyPoolBuilt = false, defEntitiesBuilt = false;

            foreach (var (slotsRef, entity) in
                     SystemAPI.Query<DynamicBuffer<DcTriggerSlot>>()
                              .WithNone<Wassup.Battle.Units.DeadTag>()
                              .WithEntityAccess())
            {
                // 죽은 유닛은 새 발동을 시작하지 않는다. DeadTag 는 DamageApplicationSystem 이
                // 붙이고 UnitLifecycleSystem 이 같은 프레임에 파괴하지만, 그 사이에 이 시스템이
                // 끼면 시체가 한 번 더 스킬을 쓴다. 시스템 순서(UpdateAfter)로 가리는 대신
                // 규칙으로 표현한다 — 이미 시작된 버스트는 emitter 가 완주시킨다
                // (combat-action-lock 의 "START 는 막고 RESOLVE 는 완료" 와 같은 결).
                // foreach 변수는 readonly — DynamicBuffer 는 뷰 struct 라 로컬
                // 복사가 같은 버퍼 메모리를 가리킨다(CS1654 회피 관용구).
                var slots = slotsRef;
                for (int si = 0; si < slots.Length; si++)
                {
                    var slot = slots[si];
                    if (slot.trigger != Wassup.Data.DcTriggerKind.PeriodicTimer) continue;

                    float elapsed = slot.elapsed;
                    bool fired = DcTrigger.PeriodicTick(ref elapsed, dt, slot.periodSeconds);
                    slot.elapsed = elapsed;
                    if (fired)
                    {
                        if (slot.payload == Wassup.Data.DcPayloadKind.AllyMoveSpeedAura)
                        {
                            // nightmare-whip-aura unit 1 — pulse: same-faction
                            // units within Chebyshev tileRange of the host get a
                            // MoveSpeedMul modifier (TTL=duration) through the
                            // existing Combat→Effects channel. Range exit / host
                            // death release by TTL expiry alone — no revoke
                            // (계약 5). Degenerate authoring (mul 1.0 / no TTL)
                            // consumes the fire quietly (계약 6).
                            if (slot.magnitude != 0f && slot.duration > 0f && hasStatEvents &&
                                SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                bool hostIsEnemy = SystemAPI.HasComponent<AttackUnitTag>(entity);
                                bool hostIsDefender = !hostIsEnemy && SystemAPI.HasComponent<DefenderUnitTag>(entity);
                                if (hostIsEnemy && !enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                if (hostIsDefender && !defEntitiesBuilt)
                                {
                                    // cells = defCells (동일 쿼리 스냅샷) — entities 만 보충.
                                    defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    defEntitiesBuilt = true;
                                }
                                if (hostIsEnemy || hostIsDefender) // 진영 불명 host = no-op
                                {
                                    var poolEntities = hostIsEnemy ? enemyEntities : defEntities;
                                    var poolCells = hostIsEnemy ? enemyCells : defCells;
                                    float3 hostPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                    int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, ref pulseTargets);
                                    float mul = 1f + slot.magnitude / 100f;
                                    int buffed = 0;
                                    for (int ti = 0; ti < pulseTargets.Length; ti++)
                                    {
                                        var target = poolEntities[pulseTargets[ti]];
                                        if (target == entity) continue; // host 자신 제외 — entity 비교 (계약 3)
                                        statEventsRef.ValueRW.queue.Enqueue(new StatModifierApplyEvent
                                        {
                                            target = target,
                                            stat = StatKind.MoveSpeedMul,
                                            op = CombineOp.Multiplicative,
                                            magnitude = mul,
                                            duration = slot.duration,
                                            source = entity,
                                            stackId = 0,
                                            origin = ModifierOrigin.Boss,
                                        });
                                        buffed++;
                                    }
                                    // unit 3 — 효과 없는 연출 금지: 버프 ≥1 펄스만
                                    // 재생. dataIndex < 0 = 무연출 authoring (blink 선례).
                                    if (buffed > 0 && hasHitQ && slot.projectileDataIndex >= 0)
                                    {
                                        hitQueue.Enqueue(new ProjectileHitEvent
                                        {
                                            position = hostPos,
                                            dataIndex = slot.projectileDataIndex,
                                            payload = PayloadKind.SingleSplash,
                                            source = entity,
                                        });
                                    }
                                }
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.AreaSleep)
                        {
                            // boss-mamemo unit 1 — 자장가. host 의 **반대 진영** 유닛 중 가까운
                            // magnitude 명을 duration 초 재운다. whip 오라와 같은 arm·같은 후보
                            // 풀을 쓰되 진영이 반대이고, 결과가 스탯이 아니라 CC 라는 것만 다르다.
                            int cap = (int)slot.magnitude;
                            if (cap >= 1 && slot.duration > 0f && hasCcQ
                                && SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                bool hostIsEnemy = SystemAPI.HasComponent<AttackUnitTag>(entity);
                                bool hostIsDefender = !hostIsEnemy && SystemAPI.HasComponent<DefenderUnitTag>(entity);
                                // 대상 = 반대 진영. 진영 축은 **유닛 태그**다 — FactionTag 을 쓰면
                                // battle-structures 이후 진영 비트가 거점(마음·본능)을 포함하는데
                                // 거점엔 CcEffect 버퍼가 없다(CcApplySystem 이 skip 하지만, 애초에
                                // 후보에 넣으면 cap 자리를 유령이 차지해 실제 대상이 줄어든다).
                                if (hostIsEnemy && !defEntitiesBuilt)
                                {
                                    defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    defEntitiesBuilt = true;
                                }
                                if (hostIsDefender && !enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                if (hostIsEnemy || hostIsDefender)
                                {
                                    var poolEntities = hostIsEnemy ? defEntities : enemyEntities;
                                    var poolCells = hostIsEnemy ? defCells : enemyCells;
                                    var poolTransforms = hostIsEnemy ? defTransforms : enemyTransformsPool;
                                    float3 hostPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                    int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);

                                    // 도넛의 안쪽 반지름 = host 사거리 **+1**. 사거리 안은 어차피
                                    // 자기 평타가 깨우므로 재우면 자기무효화다(AuraPulse 주석).
                                    // FSM·공격이 "때릴 수 있나" 를 재는 것과 **같은 변환**
                                    // (GridMath.RangeToTiles)을 쓰되, 그 둘은 `<=` 판정이라
                                    // **경계 링 자체가 사거리 안**이다(AttackSystem: `tileDist >
                                    // tileRange` 면 skip). SelectRing 의 min 은 inclusive 이므로
                                    // +1 을 해야 «때릴 수 있는 칸» 과 «재우는 칸» 이 겹치지 않는다.
                                    // 그리고 마메모는 방어유닛을 사냥해 **붙는** 보스라 대상이 스스로
                                    // 경계 링으로 들어온다 — 여기서 1 을 빠뜨리면 최빈 케이스가 곧
                                    // 자기무효화가 된다.
                                    int attackTiles = SystemAPI.HasComponent<AttackState>(entity)
                                        ? GridMath.RangeToTiles(SystemAPI.GetComponent<AttackState>(entity).range)
                                        : -1;
                                    AuraPulse.SelectRing(poolCells, hostCell, attackTiles + 1, slot.tileRange, ref pulseTargets);

                                    // 후보를 거리²로 좁힌 뒤 cap 적용 — 형제 경로(실드파열
                                    // AreaSleep)와 같은 선별기. 배제는 여기서 끝내야 cap 자리를
                                    // 죽은/배치중 유닛이 차지하지 않는다.
                                    pulseDistSq.Clear();
                                    pulsePicked.Clear();
                                    for (int ti = 0; ti < pulseTargets.Length; ti++)
                                    {
                                        var cand = poolEntities[pulseTargets[ti]];
                                        if (cand == entity) continue;
                                        if (SystemAPI.HasComponent<Wassup.Battle.Units.DeadTag>(cand)) continue;
                                        if (SystemAPI.HasComponent<Wassup.Battle.Units.PendingDeployment>(cand)) continue;
                                        pulsePicked.Add(pulseTargets[ti]);
                                        pulseDistSq.Add(math.distancesq(poolTransforms[pulseTargets[ti]].Position, hostPos));
                                    }
                                    // SelectNearest 가 results 를 비우고 채운다 — pulseTargets 는
                                    // 이미 pulsePicked 로 복사됐으므로 출력으로 재사용해도 안전하다.
                                    AoeTargetCap.SelectNearest(pulseDistSq.AsArray(), cap, ref pulseTargets);

                                    int slept = 0;
                                    for (int ti = 0; ti < pulseTargets.Length; ti++)
                                    {
                                        ccRW.ValueRW.queue.Enqueue(new EnemyCcEvent
                                        {
                                            target = poolEntities[pulsePicked[pulseTargets[ti]]],
                                            effect = new CcEffect
                                            {
                                                kind = CcKind.Sleep,
                                                remainingTime = slot.duration,
                                            },
                                        });
                                        slept++;
                                    }
                                    // whip 선례 — 실제로 잰 펄스만 연출한다(효과 없는 연출 금지).
                                    if (slept > 0 && hasHitQ && slot.projectileDataIndex >= 0)
                                    {
                                        hitQueue.Enqueue(new ProjectileHitEvent
                                        {
                                            position = hostPos,
                                            dataIndex = slot.projectileDataIndex,
                                            payload = PayloadKind.SingleSplash,
                                            source = entity,
                                        });
                                    }
                                }
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.GrantShield)
                        {
                            // boss-mamemo unit 3 — 악몽의 가호. host **와 같은 진영** 유닛
                            // (host 제외)에게 실드를 나눠준다. 이 arm 은 반경 확산만 배선한다 —
                            // 자기 실드는 경계 arm 의 꿈의 장막이 소유한다(bake 가 조합을 가른다).
                            //
                            // host 제외가 계약인 이유: ShieldMath 는 source 를 병합 키로 쓰므로
                            // 두 능력이 같은 host 에서 나와 자기 자신에게 겹치면 **한 슬롯을
                            // 공유**하고, 이쪽이 매 주기 그 잔량을 max 로 재충전해 「경계에 생기는
                            // 벽」이 「상시 실드」로 붕괴한다.
                            if (slot.magnitude > 0f && slot.tileRange > 0
                                && SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                bool hostIsEnemy = SystemAPI.HasComponent<AttackUnitTag>(entity);
                                bool hostIsDefender = !hostIsEnemy && SystemAPI.HasComponent<DefenderUnitTag>(entity);
                                if (hostIsEnemy && !enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                if (hostIsDefender && !defEntitiesBuilt)
                                {
                                    defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    defEntitiesBuilt = true;
                                }
                                if (hostIsEnemy || hostIsDefender)
                                {
                                    var poolEntities = hostIsEnemy ? enemyEntities : defEntities;
                                    var poolCells = hostIsEnemy ? enemyCells : defCells;
                                    var poolTransforms = hostIsEnemy ? enemyTransformsPool : defTransforms;
                                    float3 hostPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                    int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, ref pulseTargets);

                                    int granted = 0;
                                    for (int ti = 0; ti < pulseTargets.Length; ti++)
                                    {
                                        var target = poolEntities[pulseTargets[ti]];
                                        if (target == entity) continue;              // host 제외 (위 계약)
                                        if (SystemAPI.HasComponent<Wassup.Battle.Units.DeadTag>(target)) continue;
                                        if (!shieldSlotLookup.HasBuffer(target)) continue;
                                        if (!incomingShieldLookup.HasBuffer(target)) continue;
                                        // 만충이면 Merge 가 max 로 no-op 이라 헛 VFX 만 남는다
                                        // (가디언 unit 4 선례).
                                        if (Wassup.Battle.Units.ShieldMath.ValueFromSource(
                                                shieldSlotLookup[target], entity) >= slot.magnitude) continue;
                                        incomingShieldLookup[target].Add(new Wassup.Battle.Units.IncomingShield
                                        {
                                            source = entity,   // 같은 출처 = max 갱신 → 깎인 만큼만 다시 찬다
                                            amount = slot.magnitude,
                                        });
                                        // boss-mamemo unit 4 — 가디언과 같은 실드 부여 채널(저작 0).
                                        // **대상 위치에 대상 수만큼** 쏜다 — 가디언(ShieldCastSystem)이
                                        // 그렇게 한다. host 에서 한 번만 쏘면 "보스가 반짝하고 호위 실드는
                                        // 소리 없이 생긴다" 가 되어, 같은 채널을 재사용한 이유("같은 사건은
                                        // 같은 그림")가 정작 깨진다.
                                        if (hasShieldVfxQ)
                                            shieldVfxRW.ValueRW.queue.Enqueue(new ShieldGrantedEvent
                                            {
                                                position = poolTransforms[pulseTargets[ti]].Position,
                                            });
                                        granted++;
                                    }
                                }
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.EmitProjectilePattern)
                        {
                            // 발사 명세를 트리거한다. spec/template 을 **값으로 복사**하므로
                            // 발사 도중 무엇이 바뀌어도 이미 시작된 버스트는 불변이다(계약 8).
                            // 영속시켜야 하는 것은 발사 카운터 하나뿐이고, 그것만 durable
                            // 소유자(PatternSlot)에 남아 다음 발화가 이어받는다 —
                            // 안 그러면 선택 규칙이 고정된다(spec-review C2).
                            if (slot.patternIndex >= 0
                                && patternLookup.HasBuffer(entity) && instanceLookup.HasBuffer(entity))
                            {
                                var pats = patternLookup[entity];
                                if (slot.patternIndex < pats.Length)
                                {
                                    var pat = pats[slot.patternIndex];
                                    var inst = new Projectile.Emission.EmitterInstance
                                    {
                                        spec = pat.spec,
                                        template = pat.template,
                                        lockedTarget = Entity.Null,
                                    };
                                    Projectile.Emission.EmitterTick.Begin(ref inst.runtime, inst.spec, pat.fireCountBase);
                                    pat.fireCountBase += pat.spec.shots.Length;
                                    pats[slot.patternIndex] = pat;
                                    instanceLookup[entity].Add(inst);
                                }
                            }
                        }
                        else
                        {
                            // Payload landed without its arm — fail loudly instead
                            // of silently consuming the fire (dc-trigger 선례).
                            // projectile-emission-pattern unit 4 — AreaBarrage arm 은
                            // 제거됐다(융단폭격은 EmitProjectilePattern 으로 이관). enum
                            // 값은 append-only 계약상 남아 있고 bake 가 loud 거절한다.
                            UnityEngine.Debug.LogWarning("[BossPeriodicTrigger] PeriodicTimer slot fired with unhandled payload kind.");
                        }
                    }
                    slots[si] = slot;
                }
            }

            defTransforms.Dispose();
            defCells.Dispose();
            pulseTargets.Dispose();
            pulseDistSq.Dispose();
            pulsePicked.Dispose();
            if (enemyPoolBuilt) { enemyEntities.Dispose(); enemyCells.Dispose(); enemyTransformsPool.Dispose(); }
            if (defEntitiesBuilt) defEntities.Dispose();
        }

        // 적 후보 풀 스냅샷 — whip(같은 진영)과 자장가(반대 진영) 두 호출처가 공유한다.
        // transforms 를 **버리지 않는다**: 자장가의 cap 선별이 월드 거리²를 쓴다.
        private static void BuildEnemyPool(ref SystemState state, in FlowFieldSingleton ff,
            ref NativeArray<Entity> entities, ref NativeArray<LocalTransform> transforms,
            ref NativeArray<int2> cells)
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AttackUnitTag, LocalTransform>().Build(ref state);
            entities = query.ToEntityArray(Allocator.Temp);
            transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            cells = new NativeArray<int2>(transforms.Length, Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
                cells[i] = GridMath.WorldToCell(transforms[i].Position, ff.tileSize, ff.gridSize, origin: ff.origin);
        }
    }
}
