using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 캡처 #34. 구 `DamageApplicationSystem` 이식.
    ///
    /// **한 프레임의 피해·회복·실드를 정산하고 그 결과에서 파생되는 사건을 전부 낸다.** 이 시스템이
    /// 큰 이유는 책임이 여럿이어서가 아니라 **정산 순서 자체가 계약**이기 때문이다 — 실드는
    /// `dmgTakenMul` 뒤에 흡수하고, 폰트는 버퍼를 비우기 전에 읽고, 파열은 사망과 독립으로 발동한다.
    /// 쪼개면 그 순서가 호출자에게 흩어진다.
    ///
    /// **소유**: `Health.value` 쓰기 · `DamagedCounter.counter` 쓰기 · `ShieldSlot` 쓰기 ·
    /// `DeadTag` 마킹. 파괴는 하지 않는다(P12 의 몫 — 사망 릴레이의 1틱 창).
    ///
    /// **읽기만 하는 타 맥락 상태**: `ModifierStats`·`CcEffect`(Effects), `DcTriggerSlot`·
    /// `UltimateLeapState`(Combat). 쓰기가 필요한 곳은 전부 채널로 나간다 —
    /// Sleep 해제는 <see cref="SimChannels.CcClear"/>, OnKill 자기버프는
    /// <see cref="SimChannels.StatApply"/>, 폭발/파열은 <see cref="SimChannels.ShieldBreak"/>.
    ///
    /// ⚠ 구 sim 의 `RequireForUpdate&lt;IncomingDamage&gt;`(분류 D — 작업 존재)는 여기서 증발한다.
    /// 대상이 없으면 루프가 0회 돌 뿐이고, 그건 게이트가 막던 것과 같은 결과다.
    /// </summary>
    public sealed class DamageApplicationSystem
    {
        private readonly SimChannels _channels;
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        public DamageApplicationSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;

            foreach (var entity in world.WithBuffer<IncomingDamage>())
            {
                if (world.Has<DeadTag>(entity)) continue;
                if (world.Has<PendingDeployment>(entity)) continue;
                if (!world.TryGet<Health>(entity, out var health)) continue;

                var damageBuffer = world.GetBuffer<IncomingDamage>(entity);

                // ⚠ **이탈 중이면 버퍼를 비우고 넘어간다 — 쿼리에서 빼면 안 된다.**
                // 빼면 예고 시간 동안 피해가 적립됐다가 착지 프레임에 통째로 터진다
                // (무적이 아니라 지연 폭탄이 된다). DoT 틱과 잔여 투사체 히트도 같은 버퍼로
                // 들어오므로 이 한 지점 드랍이 전부를 덮는다.
                // 따름정리: **공중 사망이 없다 = 착지가 보장된다**(도약 시퀀스의 전제).
                if (world.Has<UltimateLeapState>(entity))
                {
                    damageBuffer.Clear();
                    continue;
                }

                bool hasModifierStats = world.TryGet<ModifierStats>(entity, out var stats);
                float dmgTakenMul = hasModifierStats ? stats.dmgTakenMul : 1f;
                float regenPerSec = hasModifierStats ? stats.regenPerSec : 0f;

                // ── 피해 합산 + 킬 귀속 ──────────────────────────────────────
                // ⚠ 합만 구하고 **버퍼는 아직 비우지 않는다** — 아래 히트당 폰트가 원본 엔트리를
                // 다시 읽는다(프레임 합 하나가 아니라 히트 수만큼의 숫자가 떠야 한다).
                float totalDamage = 0f;
                var killerSource = SimEntityId.Null;
                float killerAmount = 0f;
                for (int i = 0; i < damageBuffer.Count; i++)
                {
                    var entry = damageBuffer[i];
                    totalDamage += entry.amount;
                    KillAttribution.Consider(entry.amount, entry.source, ref killerSource, ref killerAmount);
                }
                totalDamage *= dmgTakenMul;

                // ── 실드 병합·흡수 ───────────────────────────────────────────
                // 부여 드레인은 **피해 유무와 무관하게** 매 프레임(안 그러면 무피격 프레임의 부여가
                // 유실된다). 흡수는 `dmgTakenMul` **뒤**다 — 표시 데미지 = 흡수량이 계약이라서다.
                // 이후 분기 전부가 관통분(갱신된 totalDamage)을 보므로
                // "완전 흡수 히트 = 피격 아님" 은 조건식을 하나도 안 고치고 성립한다.
                float preShieldDamage = totalDamage;
                bool shieldBrokeByHit = false;
                var shieldSlots = world.GetBuffer<ShieldSlot>(entity);
                if (shieldSlots != null)
                {
                    var grants = world.GetBuffer<IncomingShield>(entity);
                    if (grants != null)
                    {
                        for (int i = 0; i < grants.Count; i++)
                            ShieldMath.Merge(shieldSlots, grants[i].source, grants[i].amount);
                        grants.Clear();
                    }
                    if (totalDamage > 0f)
                    {
                        // 파열 감지는 **피격 경로 전용**이다 — 시간 만료는 이 자리를 지나지 않으므로
                        // 조건이 아니라 호출 지점이 배제를 보장한다.
                        float preShieldSum = ShieldMath.Sum(shieldSlots);
                        totalDamage = ShieldMath.Absorb(shieldSlots, totalDamage);
                        if (preShieldSum > 0f && ShieldMath.Sum(shieldSlots) <= 0f)
                            shieldBrokeByHit = true;
                    }
                }

                // ── 회복 펄스 드레인 (매 프레임 Clear 필수) ─────────────────
                float pulseHeal = 0f;
                bool hasPulse = false;
                var healBuffer = world.GetBuffer<IncomingHeal>(entity);
                if (healBuffer != null)
                {
                    hasPulse = healBuffer.Count > 0;
                    for (int i = 0; i < healBuffer.Count; i++)
                        pulseHeal += healBuffer[i].amount;
                    healBuffer.Clear();
                }

                // 재생은 버퍼를 거치지 않고 직접 더해진다(그래서 아래 VFX 에서 빠진다).
                float totalHeal = pulseHeal + regenPerSec * dt;

                float maxHp = health.max;
                float newHp = SimMath.Min(maxHp, health.value - totalDamage + totalHeal);
                health.value = newHp;
                world.Set(entity, health);

                // ── 피격 숫자 (적 전용) ──────────────────────────────────────
                // 엔트리마다 하나씩 — 같은 프레임의 투사체 히트와 드림캐쳐 히트가 두 숫자로 보인다.
                // `hpRatio` 는 **정산 후** 값이라 치명타 프레임엔 0 이고, 이 프레임의 모든 폰트가
                // 같은 최종 비율을 실어 마이크로바를 한 번만 움직인다.
                if (world.Has<AttackUnitTag>(entity) && world.TryGet<SimTransform>(entity, out var tr))
                {
                    float settledRatio = Health.ComputeRatio(newHp, maxHp);
                    // 관통분 비례 배분 — 완전 흡수 프레임은 전 폰트가 스킵된다.
                    // (현재 팝업=적 전용·실드=방어유닛 전용이라 교차가 없지만, 적측 실드가 열리는
                    //  날 수치가 조용히 틀리지 않도록 계약을 지금 고정한다.)
                    float pierceRatio = preShieldDamage > 0f ? totalDamage / preShieldDamage : 1f;
                    for (int i = 0; i < damageBuffer.Count; i++)
                    {
                        float hitAmount = damageBuffer[i].amount * dmgTakenMul * pierceRatio;
                        if (hitAmount <= 0f) continue;
                        _channels.DamageNumber.Enqueue(new DamageNumberEvent
                        {
                            position = tr.Position,
                            amount = hitAmount,
                            entity = entity,
                            hpRatio = settledRatio,
                        });
                    }
                }

                // 폰트를 다 읽었으니 이제 소비.
                damageBuffer.Clear();

                // 펄스만 연출한다(재생 제외 — 매 프레임 도배 방지).
                if (hasPulse && pulseHeal > 0f && world.TryGet<SimTransform>(entity, out var healTr))
                {
                    _channels.HealApplied.Enqueue(new HealAppliedEvent
                    {
                        position = healTr.Position,
                        amount = pulseHeal,
                    });
                }

                // ── wake-on-hit ──────────────────────────────────────────────
                // 실제 피격(관통분 > 0)이면 Sleep 해제를 **요청**한다. Units 는 `CcEffect` 를 직접
                // 못 지우므로 Effects 에 위임한다. Stun 은 대상이 아니다. 치명타 프레임에도 보낸다
                // (소비자가 생존을 확인한다).
                if (totalDamage > 0f)
                {
                    var ccBuffer = world.GetBuffer<CcEffect>(entity);
                    if (ccBuffer != null)
                    {
                        for (int i = 0; i < ccBuffer.Count; i++)
                            if (ccBuffer[i].kind == CcKind.Sleep)
                            {
                                _channels.CcClear.Enqueue(new CcClearRequest { entity = entity, kind = CcKind.Sleep });
                                break;
                            }
                    }
                }

                // ── OnDamagedN (가시 갑옷 계열) ──────────────────────────────
                // **프레임당 피격 = 1** 로 센다. 카운터 쓰기는 Units 안에서만 일어나고, Combat 은
                // 아래 charge 만 읽는다.
                if (totalDamage > 0f && newHp > 0f
                    && world.Has<DefenderUnitTag>(entity)
                    && world.HasBuffer<DamagedCounter>(entity))
                {
                    var counters = world.GetBuffer<DamagedCounter>(entity);
                    bool grantDoubleFire = false;
                    for (int c = 0; c < counters.Count; c++)
                    {
                        var slot = counters[c];
                        // Self 게이트 판정은 **이 피격 적용 후**(newHp) 기준이다 —
                        // "그 이하로 만든 그 피격부터" 센다. 게이트 실패 사건은 counter 무변화.
                        if (!DcTrigger.GatePass(slot.gate, slot.gateValue, newHp, maxHp)) continue;
                        ushort cnt = slot.counter;
                        bool fired = DcTrigger.Tick(ref cnt, slot.period);
                        slot.counter = cnt;
                        counters[c] = slot;
                        if (!fired) continue;

                        if (slot.payload == DcPayloadKind.NextAttackDoubleFire)
                        {
                            grantDoubleFire = true;
                        }
                        else if (slot.payload == DcPayloadKind.SelfTileAoe)
                        {
                            // 피격 폭발 — 실드 파열과 같은 채널·같은 실행기를 재사용한다.
                            if (world.TryGet<SimTransform>(entity, out var boomTr))
                                _channels.ShieldBreak.Enqueue(new ShieldBreakEvent
                                {
                                    host = entity,
                                    position = boomTr.Position,
                                    payload = DcPayloadKind.SelfTileAoe,
                                    magnitude = slot.magnitude,
                                    tileRange = slot.tileRange,
                                    duration = 0f,
                                    aoeDataIndex = slot.aoeDataIndex,
                                    fromDamagedTrigger = true,
                                });
                        }
                        else
                        {
                            // ⚠ 발동했는데 arm 이 없으면 **소리를 낸다**. 조용히 넘기면 카드가
                            // 죽은 채로 배포된다(구 sim 의 `Debug.LogWarning` 자리).
                            _channels.Warnings.Enqueue(new SimWarning
                            {
                                code = SimWarningCode.DamagedCounterUnhandledPayload,
                                entity = entity,
                                detail = (int)slot.payload,
                            });
                        }
                    }
                    if (grantDoubleFire) _ecb.Set(entity, new NextAttackDoubleFire { charges = 1 });
                }

                // ── 실드 파열 (OnShieldBreak) ────────────────────────────────
                // ⚠ **사망 분기와 독립이다** — 관통 킬 프레임에도 파열은 발동한다.
                if (shieldBrokeByHit
                    && world.HasBuffer<DcTriggerSlot>(entity)
                    && world.TryGet<SimTransform>(entity, out var sbTr))
                {
                    var sbSlots = world.GetBuffer<DcTriggerSlot>(entity);
                    for (int s = 0; s < sbSlots.Count; s++)
                    {
                        var sbSlot = sbSlots[s];
                        if (sbSlot.trigger != DcTriggerKind.OnShieldBreak) continue;
                        _channels.ShieldBreak.Enqueue(new ShieldBreakEvent
                        {
                            host = entity,
                            position = sbTr.Position,
                            payload = sbSlot.payload,
                            magnitude = sbSlot.magnitude,
                            tileRange = sbSlot.tileRange,
                            duration = sbSlot.duration,
                            aoeDataIndex = sbSlot.payload == DcPayloadKind.SelfTileAoe
                                ? sbSlot.projectileDataIndex : -1,
                        });
                    }
                }

                if (newHp <= 0f)
                {
                    // 마킹만 한다 — 파괴는 P12 다(사망 릴레이의 1틱 창).
                    _ecb.Set(entity, new DeadTag());

                    // 적 처치만 점수를 낸다. 유출 제거는 이 분기에 오지 않는다.
                    if (world.Has<AttackUnitTag>(entity) && world.TryGet<SimTransform>(entity, out var deathTr))
                    {
                        // 시체폭발 — killer 의 OnKill×SelfTileAoe **첫 매칭** 슬롯을 스탬프한다.
                        // 드레인 시점엔 killer 의 슬롯을 못 읽으므로 값이 이벤트에 실려야 한다.
                        bool hasKillBurst = false;
                        float burstDamage = 0f;
                        int burstTileRange = 0;
                        int burstDataIndex = -1;
                        if (!killerSource.IsNull && world.HasBuffer<DcTriggerSlot>(killerSource))
                        {
                            var burstSlots = world.GetBuffer<DcTriggerSlot>(killerSource);
                            for (int s = 0; s < burstSlots.Count; s++)
                            {
                                var bs = burstSlots[s];
                                if (bs.trigger != DcTriggerKind.OnKill ||
                                    bs.payload != DcPayloadKind.SelfTileAoe) continue;
                                hasKillBurst = true;
                                burstDamage = bs.magnitude;
                                burstTileRange = bs.tileRange;
                                burstDataIndex = bs.projectileDataIndex;
                                break;
                            }
                        }
                        _channels.EnemyKilled.Enqueue(new EnemyKilledEvent
                        {
                            position = deathTr.Position,
                            awakeningReward = world.TryGet<AwakeningReward>(entity, out var aw) ? aw.value : 0,
                            entity = entity,
                            killScore = world.TryGet<KillScore>(entity, out var ks) ? ks.value : 0,
                            hasKillBurst = hasKillBurst,
                            burstDamage = burstDamage,
                            burstTileRange = burstTileRange,
                            burstDataIndex = burstDataIndex,
                            killer = killerSource,
                        });
                    }

                    // ── OnKill × SelfStatBuff (포식 계열) ────────────────────
                    // 처치 큐를 재소비하지 않는다 — killing entry 의 source 로 killer 의 슬롯을
                    // RO 로 읽어 self 에 모디파이어 채널로 보낸다(맥락 경계: 읽기만·쓰기는 채널).
                    // ⚠ **victim 진영 무관**이다 — killer 가 OnKill 슬롯을 가졌으면 발동한다.
                    if (!killerSource.IsNull && world.HasBuffer<DcTriggerSlot>(killerSource))
                    {
                        var killSlots = world.GetBuffer<DcTriggerSlot>(killerSource);
                        for (int s = 0; s < killSlots.Count; s++)
                        {
                            var kill = killSlots[s];
                            if (kill.trigger != DcTriggerKind.OnKill ||
                                kill.payload != DcPayloadKind.SelfStatBuff) continue;
                            // 비스택 refresh: 슬롯 고정 stackId 로 재부여 → 지속만 갱신.
                            // duration <= 0 = 영구(+∞).
                            float ttl = kill.duration > 0f ? kill.duration : float.PositiveInfinity;
                            ModifierAuthoring.FromMultiplier(kill.magnitude, out var buffOp, out var buffMag);
                            _channels.StatApply.Enqueue(new StatModifierApplyEvent
                            {
                                target = killerSource,
                                stat = kill.buffStat,
                                op = buffOp,
                                magnitude = buffMag,
                                duration = ttl,
                                source = killerSource,
                                stackId = kill.statBuffStackId,
                                origin = ModifierOrigin.Dreamcatcher,
                            });
                        }
                    }
                }
            }

            _ecb.Playback(world);
        }
    }
}
