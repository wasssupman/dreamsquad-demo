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
    // battle-sim-extraction unit 0 — 모디파이어 enqueue 의 같은-프레임 적용(캡처 순서)을 선언으로 고정.
    [UpdateBefore(typeof(ModifierApplySystem))]
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

            var whipTargets = new NativeList<int>(Allocator.Temp);
            NativeArray<Entity> whipEnemyEntities = default, whipDefEntities = default;
            NativeArray<int2> whipEnemyCells = default;
            bool whipEnemyPoolBuilt = false, whipDefEntitiesBuilt = false;

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
                                if (hostIsEnemy && !whipEnemyPoolBuilt)
                                {
                                    var enemyQuery = SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build();
                                    whipEnemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
                                    var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                                    whipEnemyCells = new NativeArray<int2>(enemyTransforms.Length, Allocator.Temp);
                                    for (int i = 0; i < enemyTransforms.Length; i++)
                                        whipEnemyCells[i] = GridMath.WorldToCell(enemyTransforms[i].Position, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    enemyTransforms.Dispose();
                                    whipEnemyPoolBuilt = true;
                                }
                                if (hostIsDefender && !whipDefEntitiesBuilt)
                                {
                                    // cells = defCells (동일 쿼리 스냅샷) — entities 만 보충.
                                    whipDefEntities = defQuery.ToEntityArray(Allocator.Temp);
                                    whipDefEntitiesBuilt = true;
                                }
                                if (hostIsEnemy || hostIsDefender) // 진영 불명 host = no-op
                                {
                                    var poolEntities = hostIsEnemy ? whipEnemyEntities : whipDefEntities;
                                    var poolCells = hostIsEnemy ? whipEnemyCells : defCells;
                                    float3 hostPos = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                                    int2 hostCell = GridMath.WorldToCell(hostPos, ff.tileSize, ff.gridSize, origin: ff.origin);
                                    AuraPulse.SelectTargets(poolCells, hostCell, slot.tileRange, ref whipTargets);
                                    float mul = 1f + slot.magnitude / 100f;
                                    int buffed = 0;
                                    for (int ti = 0; ti < whipTargets.Length; ti++)
                                    {
                                        var target = poolEntities[whipTargets[ti]];
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
            whipTargets.Dispose();
            if (whipEnemyPoolBuilt) { whipEnemyEntities.Dispose(); whipEnemyCells.Dispose(); }
            if (whipDefEntitiesBuilt) whipDefEntities.Dispose();
        }
    }
}
