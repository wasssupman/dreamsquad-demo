using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Units
{
    // Drains IncomingDamage and IncomingHeal buffers into Health each frame.
    // Also applies RegenPerSec from ModifierStats directly (not via IncomingHeal).
    // When health crosses zero the entity gets a DeadTag so UnitLifecycleSystem can destroy it.
    [BurstCompile]
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(AttackSystem))]
    public partial struct DamageApplicationSystem : ISystem
    {
        private ComponentLookup<ModifierStats> _buffStatsLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<AttackUnitTag> _attackTagLookup;
        private BufferLookup<IncomingHeal> _healBufferLookup;
        // content-1 ① (가시 갑옷) — count defender damage-taken (DamagedCounter is Units-owned).
        private ComponentLookup<DefenderUnitTag> _defenderTagLookup;
        private BufferLookup<DamagedCounter> _damagedCounterLookup;
        // dreamcatcher-awakening-hand unit 1 — per-enemy awakening grant baked at spawn.
        private ComponentLookup<AwakeningReward> _awakeningRewardLookup;
        // combat-action-lock unit 3 — wake-on-hit: 피격 시 Sleep 보유 여부 RO 판정용.
        private BufferLookup<CcEffect> _ccLookup;
        // dreamcatcher-kill-and-threshold unit 2 — killer 의 OnKill×SelfStatBuff 슬롯 RO 판정용.
        private BufferLookup<DcTriggerSlot> _dcTriggerSlotLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<IncomingDamage>();
            _buffStatsLookup  = state.GetComponentLookup<ModifierStats>(isReadOnly: true);
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _attackTagLookup  = state.GetComponentLookup<AttackUnitTag>(isReadOnly: true);
            _healBufferLookup = state.GetBufferLookup<IncomingHeal>(isReadOnly: false);
            _defenderTagLookup     = state.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
            _damagedCounterLookup  = state.GetBufferLookup<DamagedCounter>(isReadOnly: false);
            _awakeningRewardLookup = state.GetComponentLookup<AwakeningReward>(isReadOnly: true);
            _ccLookup = state.GetBufferLookup<CcEffect>(isReadOnly: true);
            _dcTriggerSlotLookup = state.GetBufferLookup<DcTriggerSlot>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            _buffStatsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _attackTagLookup.Update(ref state);
            _healBufferLookup.Update(ref state);
            _defenderTagLookup.Update(ref state);
            _damagedCounterLookup.Update(ref state);
            _awakeningRewardLookup.Update(ref state);
            bool hasHealAppliedQueue = SystemAPI.TryGetSingletonRW<HealAppliedEventsSingleton>(out var healAppliedSingleton);
            bool hasDamageNumberQueue = SystemAPI.TryGetSingletonRW<DamageNumberEventsSingleton>(out var damageNumberSingleton);
            bool hasEnemyKilledQueue = SystemAPI.TryGetSingletonRW<EnemyKilledEventsSingleton>(out var enemyKilledSingleton);
            bool hasCcClearQueue = SystemAPI.TryGetSingletonRW<CcClearRequestsSingleton>(out var ccClearSingleton);
            // dreamcatcher-kill-and-threshold unit 2 — OnKill(devouring) self-buff 채널.
            bool hasStatModQueue = SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>(out var statModSingleton);
            _ccLookup.Update(ref state);
            _dcTriggerSlotLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (health, damageBuffer, entity) in
                     SystemAPI.Query<RefRW<Health>, DynamicBuffer<IncomingDamage>>()
                              .WithNone<DeadTag>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                // ── ModifierStats lookup (read-only, defaults safe when absent) ────────
                bool hasModifierStats = _buffStatsLookup.HasComponent(entity);
                float dmgTakenMul = hasModifierStats ? _buffStatsLookup[entity].dmgTakenMul : 1f;
                float regenPerSec = hasModifierStats ? _buffStatsLookup[entity].regenPerSec  : 0f;

                // ── IncomingDamage drain ─────────────────────────────────────────
                // Sum for the Health update, but keep the buffer intact until the
                // per-hit damage numbers below are enqueued (each entry = one font,
                // not the frame's sum — a projectile hit and a same-frame dreamcatcher
                // hit must show as two separate numbers).
                float totalDamage = 0f;
                // dreamcatcher-kill-and-threshold unit 2 — 킬 귀속: 이 프레임 IncomingDamage
                // 중 source 非Null 최대 amount entry 의 source 가 killer (contract 4).
                // DoT/on-place/환경(source=Null)은 미귀속 → OnKill 미발동(의도).
                Entity killerSource = Entity.Null;
                float killerAmount = 0f;
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    var entry = damageBuffer[i];
                    totalDamage += entry.amount;
                    KillAttribution.Consider(entry.amount, entry.source, ref killerSource, ref killerAmount);
                }
                totalDamage *= dmgTakenMul;

                // ── IncomingHeal drain (pulse channel — must Clear each frame) ───
                float pulseHeal = 0f;
                bool hasPulse = false;
                if (_healBufferLookup.HasBuffer(entity))
                {
                    var hBuf = _healBufferLookup[entity];
                    hasPulse = hBuf.Length > 0;
                    for (int i = 0; i < hBuf.Length; i++)
                        pulseHeal += hBuf[i].amount;
                    hBuf.Clear();
                }

                // ── RegenPerSec — direct per-frame addition, bypasses IncomingHeal
                float totalHeal = pulseHeal + regenPerSec * dt;

                // ── Health update with clamp ─────────────────────────────────────
                float maxHp = health.ValueRO.max;
                float newHp = math.min(maxHp, health.ValueRO.value - totalDamage + totalHeal);
                health.ValueRW.value = newHp;

                // Enemy-only floating damage number + hit micro-bar. Filter to
                // AttackUnitTag so defender hits produce no popup (per spec scope).
                // One font PER hit (buffer entry), not the frame's sum: each entry's
                // post-mitigation amount is its own number. hpRatio is the settled
                // ratio AFTER the whole frame (0 on the killing blow) — every font
                // this frame drives the micro-bar to that same final ratio.
                if (hasDamageNumberQueue
                    && _attackTagLookup.HasComponent(entity)
                    && _transformLookup.HasComponent(entity))
                {
                    float3 pos = _transformLookup[entity].Position;
                    float settledRatio = Health.ComputeRatio(newHp, maxHp);
                    for (int i = 0; i < damageBuffer.Length; i++)
                    {
                        float hitAmount = damageBuffer[i].amount * dmgTakenMul;
                        if (hitAmount <= 0f) continue;
                        damageNumberSingleton.ValueRW.queue.Enqueue(new DamageNumberEvent
                        {
                            position = pos,
                            amount = hitAmount,
                            entity = entity,
                            hpRatio = settledRatio,
                        });
                    }
                }

                // Buffer consumed — clear after per-hit fonts are read.
                damageBuffer.Clear();

                // Only enqueue VFX for IncomingHeal pulses (hasPulse + positive amount).
                // RegenPerSec is excluded to avoid spamming VFX every frame.
                if (hasHealAppliedQueue && hasPulse && pulseHeal > 0f && _transformLookup.HasComponent(entity))
                {
                    healAppliedSingleton.ValueRW.queue.Enqueue(new HealAppliedEvent
                    {
                        position = _transformLookup[entity].Position,
                        amount = pulseHeal,
                    });
                }
                // combat-action-lock unit 3 — wake-on-hit: 실제 피격(totalDamage>0) 시 Sleep 해제
                // 요청. Units 는 CcEffect 직접 못 지움 → 이벤트로 Effects(CcClearSystem)에 위임.
                // Stun 은 wake 대상 아님. lethal 포함(CcClearSystem 이 Exists 가드).
                if (hasCcClearQueue && totalDamage > 0f && _ccLookup.HasBuffer(entity))
                {
                    var ccBuf = _ccLookup[entity];
                    for (int i = 0; i < ccBuf.Length; i++)
                        if (ccBuf[i].kind == CcKind.Sleep)
                        {
                            ccClearSingleton.ValueRW.queue.Enqueue(new CcClearRequest { entity = entity, kind = CcKind.Sleep });
                            break;
                        }
                }

                // content-1 ① (가시 갑옷) — N회 피격 카운트(프레임당 피격=1). 발동 시
                // 더블파이어 charge 를 Combat 으로 넘긴다(NextAttackDoubleFire). 카운터
                // write 는 Units 안에서만(DamagedCounter=Units 소유), Combat 은 charge 만 read.
                if (totalDamage > 0f && newHp > 0f
                    && _defenderTagLookup.HasComponent(entity)
                    && _damagedCounterLookup.HasBuffer(entity))
                {
                    var counters = _damagedCounterLookup[entity];
                    bool fired = false;
                    for (int c = 0; c < counters.Length; c++)
                    {
                        var slot = counters[c];
                        ushort cnt = slot.counter;
                        if (DcTrigger.Tick(ref cnt, slot.period)) fired = true;
                        slot.counter = cnt;
                        counters[c] = slot;
                    }
                    if (fired) ecb.AddComponent(entity, new NextAttackDoubleFire { charges = 1 });
                }

                if (newHp <= 0f)
                {
                    ecb.AddComponent<DeadTag>(entity);

                    // Enemy killed by damage → bump live score. Only AttackUnitTag
                    // (enemies); goal-reach removal goes through UnitLifecycleSystem
                    // and never reaches this HP<=0 branch.
                    if (hasEnemyKilledQueue
                        && _attackTagLookup.HasComponent(entity)
                        && _transformLookup.HasComponent(entity))
                    {
                        enemyKilledSingleton.ValueRW.queue.Enqueue(new EnemyKilledEvent
                        {
                            position = _transformLookup[entity].Position,
                            // dreamcatcher-awakening-hand unit 1 — copy the baked
                            // grant now; the entity is gone before the bridge drains.
                            awakeningReward = _awakeningRewardLookup.HasComponent(entity)
                                ? _awakeningRewardLookup[entity].value : 0,
                        });
                    }

                    // devouring_craving — killer 의 OnKill×SelfStatBuff 슬롯을 매 킬 발동.
                    // EnemyKilled 큐 재소비가 아니라, killing entry 의 source(killer)로 killer
                    // 의 DcTriggerSlot(Combat) RO 읽어 self 에 StatModifier 채널(Effects)
                    // enqueue — 맥락 경계(읽기만·쓰기는 채널, contract 3). victim 진영 무관
                    // (faction-neutral): killer 가 OnKill 슬롯을 가졌으면 발동.
                    if (hasStatModQueue && killerSource != Entity.Null
                        && _dcTriggerSlotLookup.HasBuffer(killerSource))
                    {
                        var kSlots = _dcTriggerSlotLookup[killerSource];
                        for (int s = 0; s < kSlots.Length; s++)
                        {
                            var ks = kSlots[s];
                            if (ks.trigger != Wassup.Data.DcTriggerKind.OnKill ||
                                ks.payload != Wassup.Data.DcPayloadKind.SelfStatBuff) continue;
                            // 비스택 refresh: 슬롯 고정 stackId 로 재부여 → 지속만 갱신.
                            // duration<=0 = 영구(Infinity, HealthThresholdSystem 과 동일 컨벤션).
                            // op/magnitude 는 FromMultiplier 로 분류 → +% 는 Additive 버킷(squad/
                            // on-place %-buff 와 동일 스택 규칙, modifier-additive-authoring).
                            float ttl = ks.duration > 0f ? ks.duration : float.PositiveInfinity;
                            ModifierAuthoring.FromMultiplier(ks.magnitude, out var buffOp, out var buffMag);
                            statModSingleton.ValueRW.queue.Enqueue(new StatModifierApplyEvent
                            {
                                target = killerSource,
                                stat = ks.buffStat,
                                op = buffOp,
                                magnitude = buffMag,
                                duration = ttl,
                                source = killerSource,
                                stackId = ks.statBuffStackId,
                                origin = Wassup.Battle.Effects.ModifierOrigin.Dreamcatcher,
                            });
                        }
                    }
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
