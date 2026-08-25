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
    // with a PeriodicTimer slot ticks here.
    //
    // ⚠ dreamcatcher-content-4 unit 0 — **방어유닛 카드 슬롯도 여기서 돈다.** 예전 주석은
    // "디펜더 카드는 periodSeconds 가 0 이라 건너뛴다" 였는데, 그건 카드 bake 가 그 값을
    // 안 실어 보내서 생긴 **우연**이었다(조용한 무발동). 이제 bake 가 싣고 <=0 은 loud
    // 거절한다 — 이 시스템의 진영 중립성이 «설계» 에서 «실제» 가 된 지점이다.
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
    // on-place-skill-rework unit 0 — 두 번째 트리거 축: `OnPlace`(방어유닛 배치). 브리지가
    // 배치 확정 시 `JustDeployed` 태그를 붙이고 여기서 그 프레임에 슬롯을 발화·태그 제거한다.
    // payload arm 은 PeriodicTimer 와 **공유**한다 — 그게 이 시스템에 얹은 이유다(브리지에
    // 실행부를 두면 `EmitProjectilePattern` arm 의 세 번째 사본이 된다).
    //
    // ⚠ 순서 계약: `ProjectileEmitterSystem` 이 `[UpdateAfter(this)]` 라 패턴은 같은 프레임에
    // 나가고, `AreaTaunt`(unit 4)가 같은 틱에 어그로를 붙이려면 `AggroStateSystem` 보다 앞이어야
    // 한다. 속성이 없으면 1프레임 지연이 빌드마다 달라진다.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    // battle-sim-extraction M0 unit 0 — 순서 박제. **현행 유효 순서를 고정할 뿐 고치지 않는다**
    //   (재배치 판단은 M1 설계의 몫). 근거: docs/spec/battle-sim-extraction/order-capture.md
    //   모디파이어 이벤트 생산자 중 소비자보다 **앞**에 있는 셋 중 하나(같은 프레임 반영).
    [UpdateBefore(typeof(Wassup.Battle.Effects.ModifierApplySystem))]
    [UpdateBefore(typeof(Wassup.Battle.Effects.AggroStateSystem))]
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
            // dreamcatcher-content-4 unit 3 — 궤도 화염구가 캐리어 entity 를 만든다(구조 변경).
            // 이 시스템의 다른 arm 들은 큐/버퍼만 만져서 여태 ECB 가 없었다. 슬롯 루프 도중
            // 구조 변경을 즉시 하면 순회 중인 버퍼 뷰가 무효화되므로 ECB 로 미룬다.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

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

            // on-place-skill-rework unit 4 — 범위 도발 seam. 어그로 상태는 Effects 소유라
            // 여기선 **획득 요청만** 넣는다(히트 획득이 AttackSystem 에서 같은 큐를 쓰는 것과
            // 같은 형태). 게이트 판정은 전부 AggroStateSystem 이 한다.
            bool hasAcquireQ = SystemAPI.TryGetSingletonRW<AggroAcquireEventsSingleton>(out var acquireRW);
            NativeQueue<AggroAcquireEvent> acquireQueue = hasAcquireQ ? acquireRW.ValueRW.queue : default;
            // 가디언 표식(존재 자체가 가디언) + 통행 층 게이트용 RO lookup.
            // skill-layer-foundation unit 2b — caster 의 진영을 읽는 lookup 3종.
            // `FactionQuery` 가 「이 엔티티가 어느 진영인가」의 단일 답이다(오늘 이 질문에
            // 답이 둘이었다 — 태그 존재 vs FactionTag).
            var factionLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.FactionTag>(isReadOnly: true);
            var enemyTagLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.AttackUnitTag>(isReadOnly: true);
            var defTagLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Units.DefenderUnitTag>(isReadOnly: true);

            var capacityLookup = SystemAPI.GetComponentLookup<AggroCapacity>(isReadOnly: true);
            var pathFollowLookup = SystemAPI.GetComponentLookup<PathFollowState>(isReadOnly: true);

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
                // on-place-skill-rework unit 0 — 배치 사건은 엔티티 단위라 슬롯 루프 밖에서
                // 1회만 묻는다. 태그 제거는 아래 별도 패스가 ECB 로 한다(버퍼 순회 중 구조 변경 금지).
                bool justDeployed = SystemAPI.HasComponent<JustDeployed>(entity);
                for (int si = 0; si < slots.Length; si++)
                {
                    var slot = slots[si];

                    // 트리거별 발화 판정. **payload arm 은 아래에서 공유**한다 — 트리거는
                    // "언제" 만 답하고 "무엇을" 은 payload 소유다.
                    bool fired;
                    if (slot.trigger == Wassup.Data.DcTriggerKind.PeriodicTimer)
                    {
                        float elapsed = slot.elapsed;
                        fired = DcTrigger.PeriodicTick(ref elapsed, dt, slot.periodSeconds);
                        slot.elapsed = elapsed;
                    }
                    else if (slot.trigger == Wassup.Data.DcTriggerKind.OnPlace)
                    {
                        // 1회성. 재무장은 브리지가 태그를 다시 붙일 때만(재배치).
                        fired = justDeployed;
                    }
                    else continue;

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
                                // unit 2b — 풀을 «같은 편» 로 고른다. 예전엔 `hostIsEnemy ? A : B` 였는데
                                // 그건 **누구**를 고르는지만 말하고 **왜**는 안 말한다. 스킬이 host 를
                                // 가리지 않으려면 이 자리가 caster 상대적이어야 한다.
                                var hostFaction = Wassup.Battle.Units.FactionQuery.Of(
                                    entity, in factionLookup, in enemyTagLookup, in defTagLookup);
                                var wanted = Wassup.Battle.Units.FactionRelation.AllyUnitsOf(hostFaction);
                                bool useEnemyPool = wanted == Wassup.Battle.Units.Faction.EnemyUnit;
                                bool useDefPool = wanted == Wassup.Battle.Units.Faction.DefenderUnit;
                                if (useEnemyPool && !enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                if (useDefPool && !defEntitiesBuilt)
                                {
                                    // cells = defCells (동일 쿼리 스냅샷) — entities 만 보충.
                                    defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    defEntitiesBuilt = true;
                                }
                                if (useEnemyPool || useDefPool) // 진영 불명 host = no-op
                                {
                                    var poolEntities = useEnemyPool ? enemyEntities : defEntities;
                                    var poolCells = useEnemyPool ? enemyCells : defCells;
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
                                // unit 2b — 풀을 «상대 진영» 로 고른다. 예전엔 `hostIsEnemy ? A : B` 였는데
                                // 그건 **누구**를 고르는지만 말하고 **왜**는 안 말한다. 스킬이 host 를
                                // 가리지 않으려면 이 자리가 caster 상대적이어야 한다.
                                var hostFaction = Wassup.Battle.Units.FactionQuery.Of(
                                    entity, in factionLookup, in enemyTagLookup, in defTagLookup);
                                var wanted = Wassup.Battle.Units.FactionRelation.OpponentUnitsOf(hostFaction);
                                bool useEnemyPool = wanted == Wassup.Battle.Units.Faction.EnemyUnit;
                                bool useDefPool = wanted == Wassup.Battle.Units.Faction.DefenderUnit;
                                // 대상 = 반대 진영. 진영 축은 **유닛 태그**다 — FactionTag 을 쓰면
                                // battle-structures 이후 진영 비트가 거점(마음·본능)을 포함하는데
                                // 거점엔 CcEffect 버퍼가 없다(CcApplySystem 이 skip 하지만, 애초에
                                // 후보에 넣으면 cap 자리를 유령이 차지해 실제 대상이 줄어든다).
                                if (useDefPool && !defEntitiesBuilt)
                                {
                                    defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    defEntitiesBuilt = true;
                                }
                                if (useEnemyPool && !enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                if (useEnemyPool || useDefPool)
                                {
                                    var poolEntities = useEnemyPool ? enemyEntities : defEntities;
                                    var poolCells = useEnemyPool ? enemyCells : defCells;
                                    var poolTransforms = useEnemyPool ? enemyTransformsPool : defTransforms;
                                    float3 hostPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                    int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);

                                    // **전 범위**를 후보로 잡는다. 제외는 «내가 지금 때릴 대상»
                                    // 뿐이고, 그건 아래 cap 선별 뒤 rank 로 뺀다.
                                    //
                                    // ⚠ 여기를 도넛(안쪽 반지름 = 사거리)으로 만들면 **능력이 죽는다.**
                                    // 실측(12초 조우, 방어유닛 4기): 보스는 사냥해서 **붙기 때문에**
                                    // 조우의 대부분을 사거리 안에서 보내고(268프레임) 도넛은 접근
                                    // 중에만 점유된다(85프레임) → 자장가가 3.5초 주기인데도
                                    // **조우당 1회**밖에 안 터졌다. 사용자 보고 "재우는 효과가
                                    // 발생하지 않는다" 의 실체가 이것이다.
                                    //
                                    // 원래 걱정(자기 평타가 재운 유닛을 깨운다)은 사실이지만 규모가
                                    // 다르다 — `attackTargetCount` 는 1 이라 **한 번에 1기만** 깨우고
                                    // 나머지는 계속 잔다. 링 전체를 빼는 건 1/3 낭비를 막으려다
                                    // 발동 자체를 없앤 과잉이었다.
                                    AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, ref pulseTargets);

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
                                    // **「내가 때릴 대상」만 rank 로 뺀다.**
                                    // host 가 이번 공격에 때릴 수 있는 수 = attackTargetCount 이고,
                                    // AttackSystem 은 사거리 안 **가까운 순**으로 고른다. 그래서 거리
                                    // 오름차순 정렬의 **앞에서부터 그 수만큼**, 그리고 **사거리 안일
                                    // 때만** 건너뛰면 «재우자마자 자기가 깨우는» 자리만 정확히 빠진다.
                                    // 링 전체를 빼면 붙은 보스의 후보가 통째로 마른다(위 주석).
                                    int skipCount = SystemAPI.HasComponent<AttackState>(entity)
                                        ? math.max(0, SystemAPI.GetComponent<AttackState>(entity).attackTargetCount)
                                        : 0;
                                    int attackTiles = SystemAPI.HasComponent<AttackState>(entity)
                                        ? GridMath.RangeToTiles(SystemAPI.GetComponent<AttackState>(entity).range)
                                        : -1;
                                    // 뺄 만큼 더 뽑아야 실제 재우는 수가 cap 을 유지한다.
                                    AoeTargetCap.SelectNearest(pulseDistSq.AsArray(), cap + skipCount, ref pulseTargets);

                                    int slept = 0, skipped = 0;
                                    for (int ti = 0; ti < pulseTargets.Length && slept < cap; ti++)
                                    {
                                        int pick = pulsePicked[pulseTargets[ti]];
                                        if (skipped < skipCount)
                                        {
                                            int cheb = GridMath.ChebyshevDistance(poolCells[pick], hostCell);
                                            if (cheb <= attackTiles) { skipped++; continue; }
                                        }
                                        ccRW.ValueRW.queue.Enqueue(new EnemyCcEvent
                                        {
                                            target = poolEntities[pick],
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
                                // unit 2b — 풀을 «같은 편» 로 고른다. 예전엔 `hostIsEnemy ? A : B` 였는데
                                // 그건 **누구**를 고르는지만 말하고 **왜**는 안 말한다. 스킬이 host 를
                                // 가리지 않으려면 이 자리가 caster 상대적이어야 한다.
                                var hostFaction = Wassup.Battle.Units.FactionQuery.Of(
                                    entity, in factionLookup, in enemyTagLookup, in defTagLookup);
                                var wanted = Wassup.Battle.Units.FactionRelation.AllyUnitsOf(hostFaction);
                                bool useEnemyPool = wanted == Wassup.Battle.Units.Faction.EnemyUnit;
                                bool useDefPool = wanted == Wassup.Battle.Units.Faction.DefenderUnit;
                                if (useEnemyPool && !enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                if (useDefPool && !defEntitiesBuilt)
                                {
                                    defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    defEntitiesBuilt = true;
                                }
                                if (useEnemyPool || useDefPool)
                                {
                                    var poolEntities = useEnemyPool ? enemyEntities : defEntities;
                                    var poolCells = useEnemyPool ? enemyCells : defCells;
                                    var poolTransforms = useEnemyPool ? enemyTransformsPool : defTransforms;
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
                                    var template = pat.template;

                                    // on-place-shuttle-shotgun unit 1 — **방향 바인딩 탄은 여기서
                                    // 조준을 확정해야 한다.** 원점·방향·최대거리는 «발사 시점의 값»
                                    // 이라 bake 가 템플릿에 채울 수 없고(평타는 AttackSystem 의
                                    // RESOLVE 가 같은 일을 한다), 안 채우면 방향 (0,0) 인 탄이 나간다.
                                    // 캐논은 적 조준(SkyFallOnEntity)이라 이 축을 밟지 않았다.
                                    //
                                    // ⚠ **이미 조준된 템플릿은 건드리지 않는다.** 이 arm 은 방향
                                    // 스냅샷을 미리 실어 보내는 소비자와 공유된다(무타겟 방향 패턴 —
                                    // `ProjectileEmitterIntegrationTests.DirectionPattern_FiresWithoutTargets…`
                                    // 가 "host 현재 위치로 snapshot 원점을 덮으면 안 된다" 로 고정).
                                    // 그쪽은 후보가 0이어도 발사한다. 그래서 «방향이 비어 있다» 를
                                    // 아직 조준되지 않은 템플릿의 표식으로 쓴다 — 유닛 능력 bake 는
                                    // origin·direction·maxDistance 를 하나도 채우지 않는다.
                                    bool fire = true;
                                    bool needsAim = math.lengthsq(template.direction)
                                                    < Projectile.Emission.OnPlaceFireAim.AimEpsilonSq
                                        && Projectile.Emission.MovementBinding.Of(template.movement)
                                            == Projectile.Emission.BindingClass.Direction;
                                    // ⚠ 위치를 모르면 **조준도 못 하므로 쏘지 않는다.** 이 조건을
                                    // 위 `if` 에 AND 로 매달면 위치 없는 host 가 조준 단계를 통째로
                                    // 건너뛰고 방향 (0,0) 탄을 내보낸다 — 이 unit 이 없애려던 바로
                                    // 그 증상이다(리뷰 L2). 오늘 모든 방어유닛이 LocalTransform 을
                                    // 갖지만, 가드는 우연이 아니라 규칙으로 닫는다.
                                    if (needsAim && !SystemAPI.HasComponent<LocalTransform>(entity))
                                        fire = false;
                                    else if (needsAim)
                                    {
                                        float3 hostPos3 = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                        float2 hostXZ = new float2(hostPos3.x, hostPos3.z);

                                        // 조준은 Units 소유(`DeployedFacing`, 배치 확정 1회 기록 후
                                        // 불변) — Combat 은 **읽기만** 한다. **퇴화(값 0)는 조준이
                                        // 아니다** — 여기서 함께 판정해야 순수 함수의 «조준 없음 →
                                        // 최근접» 폴백이 실제로 도달 가능해진다(리뷰 M3: 컴포넌트
                                        // 유무만 보면 후보 배열이 비어 폴백이 취소로 흐른다).
                                        float2 aim = float2.zero;
                                        if (SystemAPI.HasComponent<Wassup.Battle.Units.DeployedFacing>(entity))
                                        {
                                            var f = SystemAPI.GetComponent<Wassup.Battle.Units.DeployedFacing>(entity).value;
                                            aim = new float2(f.x, f.y);
                                        }
                                        bool hasAim = math.lengthsq(aim) > Projectile.Emission.OnPlaceFireAim.AimEpsilonSq;

                                        // 조준이 없을 때만 후보를 본다 — 조준이 방향을 이미 정했으면
                                        // 풀을 만들 이유가 없다(지연 빌드 플래그는 arm 들이 공유).
                                        var aimCandidates = new NativeArray<float2>(0, Allocator.Temp);
                                        int candidateCount = 0;
                                        if (!hasAim)
                                        {
                                            // ⚠ **후보 풀은 진영을 본다**(리뷰 H1). 형제 arm 셋이 전부
                                            // host 진영으로 풀을 가르는데 여기만 «적 풀» 로 고정하면,
                                            // 보스가 방향 탄을 쓰는 날 **자기편 중 최근접**을 조준하고
                                            // 탄의 targetFaction(=Defender)과 갈린다. 보스는 이미 이 arm 을
                                            // 타고 있고(지금은 barrel 이 Direction 이 아닐 뿐이다).
                                            // unit 2b — 조준 후보는 «상대 진영» 이다.
                                            var aimWanted = Wassup.Battle.Units.FactionRelation.OpponentUnitsOf(
                                                Wassup.Battle.Units.FactionQuery.Of(
                                                    entity, in factionLookup, in enemyTagLookup, in defTagLookup));
                                            // ⚠ **진영 미상이면 후보를 하나도 모으지 않는다.**
                                            // 이항 bool 하나로 접으면 None 이 한쪽으로 접혀 «미상 host 가
                                            // 늘 한쪽을 조준하는» 조용한 오폭이 된다(투트랙 리뷰 M1/M2 —
                                            // 내가 상대화하면서 폴백 방향을 뒤집었다). 형제 arm 셋도
                                            // `useEnemyPool || useDefPool` 로 같은 가드를 쓴다.
                                            // 후보 0 이면 아래 `TryResolve` 가 false 를 내 자연히 불발된다.
                                            if (aimWanted != Wassup.Battle.Units.Faction.None)
                                            {
                                            bool aimEnemyPool = aimWanted == Wassup.Battle.Units.Faction.EnemyUnit;
                                            if (!aimEnemyPool && !defEntitiesBuilt)
                                            {
                                                defEntities = defQuery.ToEntityArray(Allocator.Temp);
                                                defEntitiesBuilt = true;
                                            }
                                            if (aimEnemyPool && !enemyPoolBuilt)
                                            {
                                                BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                                enemyPoolBuilt = true;
                                            }
                                            var poolEntities = aimEnemyPool ? enemyEntities : defEntities;
                                            var poolCells = aimEnemyPool ? enemyCells : defCells;
                                            var poolTransforms = aimEnemyPool ? enemyTransformsPool : defTransforms;

                                            // ⚠ 공유 풀에는 **필터가 하나도 없다**. 안 거르면 시체나
                                            // «내가 못 때리는 층» 의 후보가 총구를 가져가고, 그 탄은
                                            // 통행 층 게이트에 막혀 아무도 못 맞힌다. 도발 arm 과 같은 게이트다.
                                            int2 hostCell = GridMath.WorldToCell(hostPos3, ff.tileSize, ff.gridSize, origin: ff.origin);
                                            AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, ref pulseTargets);
                                            byte hostLayers = SystemAPI.HasComponent<AttackState>(entity)
                                                ? SystemAPI.GetComponent<AttackState>(entity).targetTraversalLayers
                                                : (byte)0;
                                            // ⚠ **탄이 닿는 거리로 한 번 더 거른다**(리뷰 M1). 위 선별기는
                                            // 셀 체비셰프이고 탄 사거리는 월드 유클리드라, 대각선 끝 칸의
                                            // 적은 «후보» 이면서 사거리 밖이다(3칸 → 실거리 4.24 > 3.0).
                                            // 그 적이 유일 후보면 조준은 성립하고 탄은 도중에 소멸해
                                            // **발사 연출만 나가고 아무도 안 맞는다** — 이 unit 이
                                            // 없애려던 조용한 no-op 그 자체다. 두 자를 같은 자로 맞춘다.
                                            float maxDist = slot.tileRange * ff.tileSize;
                                            float maxDistSq = maxDist * maxDist;
                                            aimCandidates.Dispose();
                                            aimCandidates = new NativeArray<float2>(pulseTargets.Length, Allocator.Temp);
                                            for (int ti = 0; ti < pulseTargets.Length; ti++)
                                            {
                                                var cand = poolEntities[pulseTargets[ti]];
                                                if (SystemAPI.HasComponent<Wassup.Battle.Units.DeadTag>(cand)) continue;
                                                if (SystemAPI.HasComponent<UltimateLeapState>(cand)) continue;
                                                byte candLayers = pathFollowLookup.HasComponent(cand)
                                                    ? pathFollowLookup[cand].traversalLayers
                                                    : (byte)0;
                                                if (!Wassup.Data.PlacementLayers.CanTarget(hostLayers, candLayers))
                                                    continue;
                                                var p = poolTransforms[pulseTargets[ti]].Position;
                                                float2 candXZ = new float2(p.x, p.z);
                                                if (math.distancesq(candXZ, hostXZ) > maxDistSq) continue;
                                                aimCandidates[candidateCount++] = candXZ;
                                            }
                                            }   // 진영 미상 가드
                                        }

                                        fire = Projectile.Emission.OnPlaceFireAim.TryResolve(
                                            hostXZ, hasAim, aim,
                                            aimCandidates.GetSubArray(0, candidateCount),
                                            out float2 dir, out _);
                                        if (fire)
                                        {
                                            template.origin = hostPos3;
                                            template.direction = dir;
                                            // 사거리는 payload 저작값 — 평타(`tileRange * tileSize`)와
                                            // 같은 월드 단위다. `damage` 는 채우지 않는다: emitter 가
                                            // 명령값(`order.damage` = 패턴 SO)으로 항상 덮는다.
                                            template.maxDistance = slot.tileRange * ff.tileSize;
                                        }
                                        aimCandidates.Dispose();
                                    }

                                    // 조준도 합법 후보도 없으면 **발사하지 않는다** — 방향 (0,0) 인
                                    // 탄을 내보내는 대신 사건을 없던 것으로 한다. 발사 카운터도
                                    // 전진시키지 않아 다음 발동이 같은 위상에서 시작한다.
                                    // ⚠ `break`/`continue` 로 빠져나가지 말 것: 이 루프 끝의
                                    // `slots[si] = slot` write-back(트리거 상태 영속)을 건너뛰고
                                    // 뒤 슬롯도 통째로 잃는다.
                                    if (fire)
                                    {
                                        var inst = new Projectile.Emission.EmitterInstance
                                        {
                                            spec = pat.spec,
                                            template = template,
                                            lockedTarget = Entity.Null,
                                        };
                                        Projectile.Emission.EmitterTick.Begin(ref inst.runtime, inst.spec, pat.fireCountBase);
                                        pat.fireCountBase += pat.spec.shots.Length;
                                        pats[slot.patternIndex] = pat;
                                        instanceLookup[entity].Add(inst);
                                    }
                                }
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.AreaTaunt)
                        {
                            // on-place-skill-rework unit 4 — 범위 도발. host 반경 안 적 전원을
                            // duration 초 어그로시킨다.
                            //
                            // **게이트를 복제하지 않는다.** 보스 면역 · 유닛 미조준 적 · 공격 수단
                            // 부재 · 도달 불가 판정은 전부 AggroStateSystem(Effects) 소유다. 여기서
                            // 미리 걸러도 같은 판정이 두 곳에 생기고, 둘이 갈리는 순간 한쪽만
                            // 고쳐진다(defender-on-place-skills unit 4 의 후보 집합 결함과 같은 형태).
                            // 저작 검증(duration/tileRange/가디언 여부)은 bake 가 loud 로 한다 —
                            // 이 시스템은 [BurstCompile] 이라 여기선 로그를 낼 수 없다.
                            if (slot.duration > 0f && slot.tileRange > 0 && hasAcquireQ
                                && capacityLookup.HasComponent(entity)
                                && SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                if (!enemyPoolBuilt)
                                {
                                    BuildEnemyPool(ref state, ff, ref enemyEntities, ref enemyTransformsPool, ref enemyCells);
                                    enemyPoolBuilt = true;
                                }
                                float3 hostPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                                AuraPulse.SelectTargets(enemyCells, hostCell, slot.tileRange, ref pulseTargets);
                                // ⚠ **통행 층 게이트는 여기서 건다.** 브리지 헬퍼
                                // (CollectEnemiesInTileRange → CanDefenderTargetMover)를 못 쓰므로
                                // baked 마스크로 같은 판정을 한다. 빼면 **근접 가디언이 하늘의 적을
                                // 끌어온다**(배스티온 attackTargetLayers 는 지상만이다).
                                byte hostLayers = SystemAPI.HasComponent<AttackState>(entity)
                                    ? SystemAPI.GetComponent<AttackState>(entity).targetTraversalLayers
                                    : (byte)0;
                                for (int ti = 0; ti < pulseTargets.Length; ti++)
                                {
                                    var victim = enemyEntities[pulseTargets[ti]];
                                    // README 계약 9 — 「이번 프레임 합법 후보」만 본다.
                                    // `BuildEnemyPool` 은 arm 셋이 공유하는 헬퍼라 쿼리를 바꾸지
                                    // 않고 여기서 거른다(아래 통행 층 게이트와 같은 방식).
                                    // ⚠ `AggroStateSystem` 드레인에는 `UltimateLeapState` 게이트가
                                    // **없다** — 오늘은 보스 면역이 우연히 가려 줄 뿐이라,
                                    // 엘리트에 궁극기 도약이 열리면 그대로 구멍이 된다.
                                    if (SystemAPI.HasComponent<Wassup.Battle.Units.DeadTag>(victim)) continue;
                                    if (SystemAPI.HasComponent<UltimateLeapState>(victim)) continue;
                                    byte victimLayers = pathFollowLookup.HasComponent(victim)
                                        ? pathFollowLookup[victim].traversalLayers
                                        : (byte)0;
                                    if (!Wassup.Data.PlacementLayers.CanTarget(hostLayers, victimLayers))
                                        continue;
                                    acquireQueue.Enqueue(new AggroAcquireEvent
                                    {
                                        guardian = entity,
                                        enemy = victim,
                                        kind = AggroAcquireKind.Taunt,
                                        durationSec = slot.duration,
                                    });
                                }
                            }
                        }
                        else if (slot.payload == Wassup.Data.DcPayloadKind.SelfOrbitProjectile)
                        {
                            // dreamcatcher-content-4 unit 3 (불꽃 팽이) — host 셀 중심을 도는
                            // 화염구 하나를 duration 초 동안 띄운다. 캐리어 entity 로
                            // ProjectileSpawnRequest 스테이징(진동갑주 SelfTileAoe 선례) —
                            // 브리지 드레인이 스폰 후 캐리어를 파괴한다.
                            //
                            // ⚠ **이 arm 은 ISystem 이라 SO 를 읽을 수 없다.** 선속도·피격 반경은
                            // bake 가 탄 SO 에서 슬롯에 구워 놨고(unit 0), 재타격 쿨타임은 아예
                            // 싣지 않는다 — 드레인이 dataIndex 로 SO 를 해석해 채운다.
                            if (slot.tileRange > 0 && slot.duration > 0f && slot.speed > 0f
                                && SystemAPI.HasComponent<LocalTransform>(entity))
                            {
                                float radius = slot.tileRange * ff.tileSize;
                                var center = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                // content-4 unit 8 — 구슬 개수. bake 가 `period` 슬롯에 구웠다
                                // (PeriodicTimer 에게 그 필드는 AttackN 전용이라 비어 있다).
                                // 균등 배치는 **위상**으로 한다 — 같은 궤도·같은 수명·같은 각속도로
                                // 돌면서 시작 각도만 2π/n 씩 어긋난다. 캐리어는 개수만큼 나간다.
                                int orbCount = slot.period > 0 ? slot.period : 1;
                                for (int oi = 0; oi < orbCount; oi++)
                                {
                                var carrier = ecb.CreateEntity();
                                ecb.AddComponent(carrier, new Projectile.ProjectileSpawnRequest
                                {
                                    orbitPhase = Projectile.Orbit.PhaseOf(oi, orbCount),
                                    movement = Projectile.MovementKind.OrbitAroundPoint,
                                    payload  = Projectile.PayloadKind.PathHit,
                                    origin   = center,          // 궤도 중심(발사 시점 고정)
                                    impact   = center,
                                    damage   = slot.magnitude,  // flat — attacker damageMul 미적용(계약 10)
                                    maxDistance = radius,       // 궤도 반경
                                    // **각속도 = 선속도 ÷ 반경.** 슬롯의 speed 는 탄 SO 의 월드 속도라
                                    // 반경을 키워도 «도는 체감»이 유지된다(각속도를 직접 저작하면
                                    // 큰 원에서 갑자기 빨라진다). radius>0 은 위 가드가 보장.
                                    speed    = slot.speed / radius,
                                    flightTime = slot.duration, // 지속 초 → 수명
                                    hitThreshold = slot.hitThreshold, // 피격 반경(궤도 반경과 다른 축)
                                    dataIndex = slot.projectileDataIndex,
                                    visualScale = slot.visualScale > 0f ? slot.visualScale : 1f,
                                    owner = entity,             // 위협 귀속
                                    // targetFaction 은 싣지 않는다 — PathHit 의 후보 풀은
                                    // AttackUnitTag 하드코딩이라 이 페이로드에 진영 축이 없다.
                                    //
                                    // ⚠ **통행 층은 host 사양을 따른다**(ECS 리뷰 M2). 안 실으면
                                    // 0 = 무제한이라(`PlacementLayers.CanTarget` 이 0 을 무조건
                                    // 통과시킨다) **지상만 때리는 유닛에 이 카드를 붙이면 그 유닛이
                                    // 못 때리는 비행 적을 화염구는 때린다** — 카드가 유닛의 근본
                                    // 제약을 우회하는 뒷문이 된다. 방어유닛 발 투사체가 전부
                                    // AttackState.targetTraversalLayers 를 싣는 것과 같은 규약.
                                    targetTraversalLayers =
                                        SystemAPI.HasComponent<AttackState>(entity)
                                            ? SystemAPI.GetComponent<AttackState>(entity).targetTraversalLayers
                                            : (byte)0,
                                });
                                ecb.AddComponent<Projectile.ProjectileRequestCarrier>(carrier);
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
                            UnityEngine.Debug.LogWarning("[BossPeriodicTrigger] slot fired with unhandled payload kind.");
                        }
                    }
                    slots[si] = slot;
                }
            }

            // on-place-skill-rework unit 0 — 배치 태그는 **1프레임**이다. 슬롯 유무와 무관하게
            // 이번 업데이트에서 전부 걷는다(슬롯이 없는 유닛에 남으면 다음 배치 사건과 섞인다).
            // 브리지는 `DcTriggerSlot` 버퍼가 있는 유닛에만 태그를 붙이므로, 태그가 존재하는
            // 프레임엔 `RequireForUpdate<DcTriggerSlot>` 이 항상 만족돼 이 패스가 반드시 돈다.
            // ⚠ ECB 인 이유: 위 foreach 가 아직 살아 있는 쿼리 이터레이션이다.
            // (태그는 zero-size 라 `RefRO<JustDeployed>` 로 순회할 수 없다 — 쿼리 일괄 제거.)
            var justDeployedQuery = SystemAPI.QueryBuilder().WithAll<JustDeployed>().Build();
            if (!justDeployedQuery.IsEmpty)
                ecb.RemoveComponent<JustDeployed>(justDeployedQuery, EntityQueryCaptureMode.AtPlayback);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

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
