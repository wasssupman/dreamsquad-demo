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
        // heart-stress-axis unit 2 — 마음의 회복은 **원샷 VFX 를 쓰지 않는다**(아래 힐 펄스
        // 게이트). 위 `_attackTagLookup` 이 데미지 폰트를 «적 전용» 으로 거르는 것과 같은 형태다.
        private ComponentLookup<GoalTowerTag> _goalTowerLookup;
        // heart-stress-axis unit 6 — 방패 백스톱. 조준·경로 배제로 못 막는 **부수 피해**용.
        private ComponentLookup<CoreShielded> _coreShieldedLookup;
        private BufferLookup<IncomingHeal> _healBufferLookup;
        // content-1 ① (가시 갑옷) — count defender damage-taken (DamagedCounter is Units-owned).
        private ComponentLookup<DefenderUnitTag> _defenderTagLookup;
        private BufferLookup<DamagedCounter> _damagedCounterLookup;
        // dreamcatcher-awakening-hand unit 1 — per-enemy awakening grant baked at spawn.
        private ComponentLookup<AwakeningReward> _awakeningRewardLookup;
        // battle-score-formula unit 2 — final-score value stamped into EnemyKilledEvent.
        // combat-action-lock unit 3 — wake-on-hit: 피격 시 Sleep 보유 여부 RO 판정용.
        private BufferLookup<CcEffect> _ccLookup;
        // dreamcatcher-kill-and-threshold unit 2 — killer 의 OnKill×SelfStatBuff 슬롯 RO 판정용.
        private BufferLookup<DcTriggerSlot> _dcTriggerSlotLookup;
        // shield-guardian-defender unit 0 — 실드 슬롯(쓰기 단독 소유) + 부여 drain.
        private BufferLookup<ShieldSlot> _shieldSlotLookup;
        private BufferLookup<IncomingShield> _incomingShieldLookup;
        // ultimate-leap unit 2 — 이탈(판 밖) 판정. Combat 소유 컴포넌트를 Units 가 RO 로 읽는다.
        private ComponentLookup<Wassup.Battle.Combat.UltimateLeapState> _ultimateLeapLookup;
        // dreamcatcher-content-5 unit 4 — 잿불이 물려줄 통행 층(killer 사양). 위 UltimateLeapState
        // 와 같은 형태의 Combat→Units RO 읽기다.
        private ComponentLookup<Wassup.Battle.Combat.AttackState> _attackStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<IncomingDamage>();
            _buffStatsLookup  = state.GetComponentLookup<ModifierStats>(isReadOnly: true);
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _attackTagLookup  = state.GetComponentLookup<AttackUnitTag>(isReadOnly: true);
            _goalTowerLookup  = state.GetComponentLookup<GoalTowerTag>(isReadOnly: true);
            _coreShieldedLookup = state.GetComponentLookup<CoreShielded>(isReadOnly: true);
            _healBufferLookup = state.GetBufferLookup<IncomingHeal>(isReadOnly: false);
            _defenderTagLookup     = state.GetComponentLookup<DefenderUnitTag>(isReadOnly: true);
            _damagedCounterLookup  = state.GetBufferLookup<DamagedCounter>(isReadOnly: false);
            _awakeningRewardLookup = state.GetComponentLookup<AwakeningReward>(isReadOnly: true);
            _ultimateLeapLookup = state.GetComponentLookup<Wassup.Battle.Combat.UltimateLeapState>(isReadOnly: true);
            _attackStateLookup = state.GetComponentLookup<Wassup.Battle.Combat.AttackState>(isReadOnly: true);
            _ccLookup = state.GetBufferLookup<CcEffect>(isReadOnly: true);
            _dcTriggerSlotLookup = state.GetBufferLookup<DcTriggerSlot>(isReadOnly: true);
            _shieldSlotLookup = state.GetBufferLookup<ShieldSlot>(isReadOnly: false);
            _incomingShieldLookup = state.GetBufferLookup<IncomingShield>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            _buffStatsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _attackTagLookup.Update(ref state);
            _goalTowerLookup.Update(ref state);
            _healBufferLookup.Update(ref state);
            _defenderTagLookup.Update(ref state);
            _damagedCounterLookup.Update(ref state);
            _awakeningRewardLookup.Update(ref state);
            bool hasHealAppliedQueue = SystemAPI.TryGetSingletonRW<HealAppliedEventsSingleton>(out var healAppliedSingleton);
            bool hasDamageNumberQueue = SystemAPI.TryGetSingletonRW<DamageNumberEventsSingleton>(out var damageNumberSingleton);
            bool hasEnemyKilledQueue = SystemAPI.TryGetSingletonRW<EnemyKilledEventsSingleton>(out var enemyKilledSingleton);
            bool hasCcClearQueue = SystemAPI.TryGetSingletonRW<CcClearRequestsSingleton>(out var ccClearSingleton);
            // skill-layer-migration unit 3c — 죽음 seam 의 생산자.
            bool hasSkillQ = SystemAPI.TryGetSingletonRW<Wassup.Battle.Skills.SkillFiredEventsSingleton>(
                out var skillFiredSingleton);
            // dreamcatcher-kill-and-threshold unit 2 — OnKill(devouring) self-buff 채널.
            bool hasStatModQueue = SystemAPI.TryGetSingletonRW<StatModifierApplyEventsSingleton>(out var statModSingleton);
            // dreamcatcher-shield-break unit 0 — 실드 피격 파열 이벤트 채널(Units→Bridge).
            bool hasShieldBreakQueue = SystemAPI.TryGetSingletonRW<ShieldBreakEventsSingleton>(out var shieldBreakSingleton);
            _ccLookup.Update(ref state);
            _dcTriggerSlotLookup.Update(ref state);
            _shieldSlotLookup.Update(ref state);
            _incomingShieldLookup.Update(ref state);
            _ultimateLeapLookup.Update(ref state);
            _coreShieldedLookup.Update(ref state);
            _attackStateLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (health, damageBuffer, entity) in
                     SystemAPI.Query<RefRW<Health>, DynamicBuffer<IncomingDamage>>()
                              .WithNone<DeadTag>()
                              .WithNone<PendingDeployment>()
                              .WithEntityAccess())
            {
                // ultimate-leap unit 2 — 이탈 중이면 **버퍼를 비우고 넘어간다.**
                // ⚠ 쿼리에서 WithNone 으로 빼면 안 된다 — 그러면 2초 동안 피해가 버퍼에 적립됐다가
                // 착지 프레임에 통째로 터진다(무적이 아니라 지연 폭탄이 된다). DoT 틱과 잔여
                // 투사체 히트도 같은 버퍼로 들어오므로 이 한 지점 드랍이 전부를 커버한다.
                // 따름정리: 공중 사망이 없다 = 착지가 보장된다(UltimateLeapSystem 의 전제).
                if (_ultimateLeapLookup.HasComponent(entity))
                {
                    damageBuffer.Clear();
                    continue;
                }

                // heart-stress-axis unit 6 — **방패 백스톱.** 조준(AttackSystem)·경로
                // (StructureDestinationSystem) 배제는 «마음을 겨눈» 피해만 막는다. 겨누지
                // **않고** 닿는 경로가 따로 있다: 골 근처 방어유닛에 떨어진 광역이 그것이다
                // (`ProjectileHitSystem` TileAoe 의 피해자 마스크가 `Factions.AnyDefender` =
                // DefenderCore 포함, 라이브 생산자 2곳 — 보스 임계 barrage · 궁극기 슬램).
                // 그 한 발이 방패 선 마음을 0 으로 만들면 판이 끝난다.
                //
                // 생산자마다 필터를 다는 대신 여기 한 곳에서 떨어뜨린다 — 위 UltimateLeapState
                // 와 **같은 이유·같은 형태**다: 새 피해 경로(DoT·미래 페이로드)가 생겨도
                // 자동으로 덮인다. 그리고 그 주석대로 **쿼리에서 빼면 안 된다** — 그러면 방패가
                // 서 있는 동안 피해가 버퍼에 적립됐다가 해제 프레임에 통째로 터진다.
                //
                // ⚠ **힐 버퍼도 같이 비운다.** rev 1 은 「어차피 clamp 로 버려진다」며 damage 만
                // 비웠는데, clamp 는 **값**을 버리지 **버퍼**를 안 비운다 — 방패가 선 동안 킬마다
                // 엔트리가 쌓여(3분 100킬 = 100+) DynamicBuffer 가 힙으로 넘어가고, 방패가 풀리는
                // 프레임에 **전부 한꺼번에 적용**된다. 하필 그 프레임이 「쌓여 있던 적이 일제히
                // 치는」 순간이라 그 피해가 통째로 상쇄된다(코드 리뷰 발견).
                // 바로 위 UltimateLeapState 드랍이 경고한 «적립됐다 터지는» 실패 모드 그대로다.
                if (_coreShieldedLookup.HasComponent(entity))
                {
                    damageBuffer.Clear();
                    if (_healBufferLookup.HasBuffer(entity)) _healBufferLookup[entity].Clear();
                    continue;
                }

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

                // ── shield-guardian-defender unit 0 — 실드 병합·흡수 ─────────────
                // 부여 drain(출처별 max / 교차 출처 합산)은 데미지 유무와 무관하게
                // 매 프레임. 흡수는 dmgTakenMul 적용 후(계약 2 — 표시 데미지 = 흡수량).
                // 이후 분기 전부가 관통분(totalDamage 갱신값)을 판정하므로
                // "완전 흡수 히트 = 피격 아님"(계약 3)은 조건식 무변경으로 성립.
                float preShieldDamage = totalDamage;
                bool shieldBrokeByHit = false;
                if (_shieldSlotLookup.HasBuffer(entity))
                {
                    var shieldSlots = _shieldSlotLookup[entity];
                    if (_incomingShieldLookup.HasBuffer(entity))
                    {
                        var grants = _incomingShieldLookup[entity];
                        for (int i = 0; i < grants.Length; i++)
                            ShieldMath.Merge(ref shieldSlots, grants[i].source, grants[i].amount);
                        grants.Clear();
                    }
                    if (totalDamage > 0f)
                    {
                        // dreamcatcher-shield-break unit 0 — 피격으로 실드 풀이 완전 소진되는
                        // 순간(Sum>0→0) 감지. Absorb 전용이라 시간만료는 구조적 배제.
                        float preShieldSum = ShieldMath.Sum(shieldSlots);
                        totalDamage = ShieldMath.Absorb(ref shieldSlots, totalDamage);
                        if (preShieldSum > 0f && ShieldMath.Sum(shieldSlots) <= 0f)
                            shieldBrokeByHit = true;
                    }
                }

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
                    // 관통분 비례 배분(계약 3) — 완전 흡수 프레임은 전 폰트 스킵.
                    // (현재 팝업=적 전용·실드=defender 전용이라 교차 없음이지만,
                    // 적측 실드 후속이 와도 수치가 조용히 틀리지 않게 계약을 고정.)
                    float pierceRatio = preShieldDamage > 0f ? totalDamage / preShieldDamage : 1f;
                    for (int i = 0; i < damageBuffer.Length; i++)
                    {
                        float hitAmount = damageBuffer[i].amount * dmgTakenMul * pierceRatio;
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
                //
                // heart-stress-axis unit 2 — **마음은 제외한다.** 악몽 처치마다 힐 펄스가 들어오는데
                // (분당 수십 킬) 그때마다 마음 위에서 원샷 이펙트가 터지면 노이즈다. 회복 피드백은
                // 마음 프랍 틴트가 옅어지는 것 · 심박이 느려지는 것 · 포스트 비네트가 물러나는 것 ·
                // 머리 위 숫자가 내려가는 것이 담당한다 — 전부 «지속» 어휘라 킬 페이스에
                // 묻히지 않는다. (rev 1 의 「머리 위 바」는 은퇴했다.)
                if (hasHealAppliedQueue && hasPulse && pulseHeal > 0f && _transformLookup.HasComponent(entity)
                    && !_goalTowerLookup.HasComponent(entity))
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
                    bool grantDoubleFire = false;
                    for (int c = 0; c < counters.Length; c++)
                    {
                        var slot = counters[c];
                        // trigger-gates unit 1 — Self 게이트: 이 피격 적용 후(newHp) 기준
                        // ("이하 상태로 만든 그 피격부터" 카운트). 실패 사건은 counter
                        // 무변화 (카운트 게이트 — if(GatePass){Tick} 조립).
                        if (!DcTrigger.GatePass(slot.gate, slot.gateValue, newHp, maxHp)) continue;
                        ushort cnt = slot.counter;
                        bool fired = DcTrigger.Tick(ref cnt, slot.period);
                        slot.counter = cnt;
                        counters[c] = slot;
                        if (!fired) continue;

                        // trigger-gates unit 0 — payload 디스패치 (위드닝). 발동했는데
                        // arm 이 없으면 loud fail (AttackSystem unhandled 컨벤션).
                        if (slot.payload == Wassup.Data.DcPayloadKind.NextAttackDoubleFire)
                            grantDoubleFire = true;
                        else if (slot.payload == Wassup.Data.DcPayloadKind.SelfTileAoe)
                        {
                            // 피격 폭발 — OnShieldBreak 와 같은 큐/드레인 실행기 재사용.
                            if (hasShieldBreakQueue && _transformLookup.HasComponent(entity))
                                shieldBreakSingleton.ValueRW.queue.Enqueue(new ShieldBreakEvent
                                {
                                    host = entity,
                                    position = _transformLookup[entity].Position,
                                    payload = Wassup.Data.DcPayloadKind.SelfTileAoe,
                                    magnitude = slot.magnitude,
                                    tileRange = slot.tileRange,
                                    duration = 0f,
                                    aoeDataIndex = slot.aoeDataIndex,
                                    fromDamagedTrigger = true,
                                });
                        }
                        else
                            UnityEngine.Debug.LogWarning("[DamageApplication] DamagedCounter fired with unhandled payload kind.");
                    }
                    if (grantDoubleFire) ecb.AddComponent(entity, new NextAttackDoubleFire { charges = 1 });
                }

                // dreamcatcher-shield-break unit 0 — 실드가 피격으로 파열된 프레임: host 의
                // OnShieldBreak DcTriggerSlot(Combat, RO — OnKill 선례)를 읽어 페이로드
                // 파라미터를 emit. 실행(SelfTileAoe 폭발 / AreaSleep)은 BattleBridge drain.
                // death 분기와 독립 — 관통 킬 프레임에도 파열은 발동.
                if (shieldBrokeByHit && hasShieldBreakQueue
                    && _dcTriggerSlotLookup.HasBuffer(entity)
                    && _transformLookup.HasComponent(entity))
                {
                    var sbSlots = _dcTriggerSlotLookup[entity];
                    float3 sbPos = _transformLookup[entity].Position;
                    for (int s = 0; s < sbSlots.Length; s++)
                    {
                        var sbSlot = sbSlots[s];
                        if (sbSlot.trigger != Wassup.Data.DcTriggerKind.OnShieldBreak) continue;
                        shieldBreakSingleton.ValueRW.queue.Enqueue(new ShieldBreakEvent
                        {
                            host = entity,
                            position = sbPos,
                            payload = sbSlot.payload,
                            magnitude = sbSlot.magnitude,
                            tileRange = sbSlot.tileRange,
                            duration = sbSlot.duration,
                            aoeDataIndex = sbSlot.payload == Wassup.Data.DcPayloadKind.SelfTileAoe
                                ? sbSlot.projectileDataIndex : -1,
                        });
                    }
                }

                if (newHp <= 0f)
                {
                    ecb.AddComponent<DeadTag>(entity);

                    // ⚠ **라우팅은 전용 루프 하나로 한다**(skill-layer-migration unit 3d).
                    // 아래 레거시 블록 둘은 가드가 서로 **부분집합이 아니다**(하나는
                    // `hasEnemyKilledQueue`+적 태그+transform, 다른 하나는 `hasStatModQueue`).
                    // 어느 한쪽에 라우팅을 얹으면 그 조건이 안 맞는 킬에서 슬롯이 조용히
                    // 죽고, 양쪽에 얹으면 **이중 발화**한다. 그래서 둘보다 앞에 한 번만 돈다.
                    //
                    // ⚠ **드레인 시점엔 피해자가 이미 없다** — `UnitLifecycleSystem` 이
                    // 파괴한다. 그래서 killer 사양(통행 층)과 자리를 지금 싣는다.
                    // ⚠ **피해자 진영 술어를 레거시와 맞춘다**(ECS 리뷰 M-2). 레거시
                    // 시체폭발 블록은 `_attackTagLookup.HasComponent(victim)` 안에 있어
                    // **적이 죽었을 때만** 터졌다. 빼면 방어유닛이 죽어도 킬러의 폭발이
                    // 그 자리에서 터진다 — 그건 「고침」이 아니라 사양 변경이라, 하려면
                    // 별도 결정으로 한다.
                    // ⚠ **죽은 자리를 못 읽으면 아예 라우팅하지 않는다**(투트랙 리뷰 M-2).
                    // OnKill 스킬은 전부 시체 자리를 쓴다. transform 이 없을 때 0 으로
                    // 폴백하면 폭발과 장판이 **월드 원점에서** 터진다 — 조용한 오발이다.
                    // 레거시 시체폭발 블록도 `_transformLookup.HasComponent(entity)` 안에 있었다.
                    if (hasSkillQ && killerSource != Entity.Null
                        && _attackTagLookup.HasComponent(entity)
                        && _transformLookup.HasComponent(entity)
                        && _dcTriggerSlotLookup.HasBuffer(killerSource))
                    {
                        var routeSlots = _dcTriggerSlotLookup[killerSource];
                        // ⚠ **같은 스킬은 킬당 한 번만**(투트랙 리뷰 M-2). 레거시는 페이로드
                        // arm 별로 「첫 매칭 슬롯」만 스탬프했고, `skillId` 가 정확히 그
                        // (trigger × payload) 키다. 캡이 없으면 같은 카드를 두 장 붙인
                        // 유닛의 킬 한 번이 폭발을 두 번 터뜨린다.
                        int firedMask = 0;
                        for (int s = 0; s < routeSlots.Length; s++)
                        {
                            var rs = routeSlots[s];
                            if (rs.trigger != Wassup.Data.DcTriggerKind.OnKill) continue;
                            if (rs.skillId == Wassup.Skills.SkillRegistry.LegacyArmId) continue;
                            if (rs.skillId >= 0 && rs.skillId < 32)
                            {
                                int bit = 1 << rs.skillId;
                                if ((firedMask & bit) != 0) continue;
                                firedMask |= bit;
                            }
                            skillFiredSingleton.ValueRW.queue.Enqueue(
                                new Wassup.Battle.Skills.SkillFiredEvent
                            {
                                Caster = killerSource,
                                SkillId = rs.skillId,
                                SlotIndex = s,
                                FiredPosition = _transformLookup.HasComponent(killerSource)
                                    ? _transformLookup[killerSource].Position : float3.zero,
                                // 죽은 자리 — 시체폭발·장판이 여기를 쓴다.
                                Target = Entity.Null,
                                TargetPosition = _transformLookup[entity].Position,   // 위 가드가 보장
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
                                VisualScale = rs.visualScale,
                                TargetTraversalLayers = _attackStateLookup.HasComponent(killerSource)
                                    ? _attackStateLookup[killerSource].targetTraversalLayers : (byte)0,
                            });
                        }
                    }

                    // Enemy killed by damage → bump live score. Only AttackUnitTag
                    // (enemies); goal-reach removal goes through UnitLifecycleSystem
                    // and never reaches this HP<=0 branch.
                    if (hasEnemyKilledQueue
                        && _attackTagLookup.HasComponent(entity)
                        && _transformLookup.HasComponent(entity))
                    {
                        // 시체폭발 (content-3 unit 3) — killer 의 OnKill×SelfTileAoe 첫 매칭
                        // 슬롯을 이벤트에 스탬프(첫 슬롯만 — OnDeath v1 선례). 슬롯 읽기는
                        // devouring 루프와 같은 RO 읽기(contract 3).
                        bool hasKillBurst = false;
                        float burstDamage = 0f;
                        int burstTileRange = 0;
                        int burstDataIndex = -1;
                        // content-5 unit 4 (잿불) — 같은 루프에서 장판 슬롯도 본다. 두 payload 는
                        // 배타가 아니다(한 유닛이 시체폭발과 잿불을 같이 가질 수 있다).
                        bool hasKillHazard = false;
                        int hazardDataIndex = -1;
                        byte hazardTargetLayers = 0;
                        if (killerSource != Entity.Null && _dcTriggerSlotLookup.HasBuffer(killerSource))
                        {
                            var bSlots = _dcTriggerSlotLookup[killerSource];
                            for (int s = 0; s < bSlots.Length; s++)
                            {
                                var bs = bSlots[s];
                                if (bs.trigger != Wassup.Data.DcTriggerKind.OnKill) continue;
                                // 이전된 슬롯은 위 라우팅 루프가 보냈다 — 이중 발화 방지.
                                if (bs.skillId != Wassup.Skills.SkillRegistry.LegacyArmId) continue;
                                if (bs.payload == Wassup.Data.DcPayloadKind.SelfTileAoe)
                                {
                                    if (hasKillBurst) continue;   // 첫 매칭만(OnDeath v1 선례)
                                    hasKillBurst = true;
                                    burstDamage = bs.magnitude;
                                    burstTileRange = bs.tileRange;
                                    burstDataIndex = bs.projectileDataIndex;
                                }
                                else if (bs.payload == Wassup.Data.DcPayloadKind.SpawnHazard
                                         && bs.hazardDataIndex >= 0)
                                {
                                    if (hasKillHazard) continue;
                                    // ⚠ 통행 층은 **killer 가 살아 있는 지금** 읽는다. 드레인 시점엔
                                    // 파괴됐을 수 있고, 그때 0 으로 새면 무제한 통과가 되어 지상
                                    // 전용 유닛의 불씨가 비행 적을 태운다(계약: content-4 3-1 대칭).
                                    //
                                    // 사양을 모르면 **아예 안 깐다**(fail-closed). 초판은 0 을 폴백으로
                                    // 썼는데 그건 바로 위 문장이 막으려던 구멍을 그대로 여는 값이다
                                    // (`PlacementLayers.CanTarget(0, x)` 는 무조건 참) — 리뷰 M3.
                                    if (!_attackStateLookup.HasComponent(killerSource)) continue;
                                    hasKillHazard = true;
                                    hazardDataIndex = bs.hazardDataIndex;
                                    hazardTargetLayers = _attackStateLookup[killerSource].targetTraversalLayers;
                                }
                            }
                        }
                        enemyKilledSingleton.ValueRW.queue.Enqueue(new EnemyKilledEvent
                        {
                            position = _transformLookup[entity].Position,
                            // dreamcatcher-awakening-hand unit 1 — copy the baked
                            // grant now; the entity is gone before the bridge drains.
                            awakeningReward = _awakeningRewardLookup.HasComponent(entity)
                                ? _awakeningRewardLookup[entity].value : 0,
                            // subconscious-curse-expansion unit 2 — 표식 회수 귀속 키.
                            entity = entity,
                            // three-minute-kill-race unit 1 — 점수 기여분을 싣지 않는다
                            // (1킬 = 1점). 유출 경로는 이 분기에 오지 않으므로 유출된 적이
                            // 점수를 안 주는 성질은 그대로다.
                            hasKillBurst = hasKillBurst,
                            burstDamage = burstDamage,
                            burstTileRange = burstTileRange,
                            burstDataIndex = burstDataIndex,
                            killer = killerSource,
                            // content-5 unit 4 (잿불) — 시체폭발과 나란한 스탬프. 둘은 배타가
                            // 아니라 한 킬이 폭발과 불씨를 동시에 낼 수 있다.
                            hasKillHazard = hasKillHazard,
                            hazardDataIndex = hazardDataIndex,
                            hazardTargetLayers = hazardTargetLayers,
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
                            // 이전된 슬롯은 위 라우팅 루프가 이미 보냈다 — 여기서 또 처리하면 이중 발화.
                            if (ks.skillId != Wassup.Skills.SkillRegistry.LegacyArmId) continue;
                            // 슬롯 고정 stackId 로 재부여. 최대 중첩(ks.tileRange)이 0 이면 지속만
                            // 갱신되는 비스택 refresh 이고, >0 이면 매 킬마다 상한까지 누적된다
                            // (dreamcatcher-berserker unit 1 — 짱빠른/짱쎈버서커가 이 자리에 선다).
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
                                magnitudeCap = ModifierAuthoring.StackCap(ks.magnitude, ks.tileRange),
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
