using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Bridge
{
    // dreamcatcher-bridge-partial-cleanup unit 0 — the dreamcatcher card
    // translator half of BattleBridge: definition-layer cards (DreamcatcherCard /
    // DcMechanic, ECS-free) baked into unmanaged slots and StatModifier enqueues.
    // Pure move from BattleBridge.cs — one class, two files; BattleBridge stays
    // the sole MonoBehaviour↔ECS gateway (TRD constraint unchanged).
    public partial class BattleBridge
    {
        // ingame-dreamcatcher Unit 2 — dreamcatcher card effects are match-long and
        // apply to current + future matching defenders. stackId starts at 100 to
        // avoid colliding with onplace/skill (0) and synergy (1) on the same stat.
        private struct ActiveDcEffect
        {
            public Wassup.Data.CardTargetAxis axis;
            public Wassup.Battle.Effects.StatKind stat;
            public float mult;
            public ushort stackId;
            // awakening-hand unit 9 — revocation group. handle ≥1 = hosted squad
            // apply, revoked on host death. handle 0 = non-revocable match-long apply
            // (드림스톤 로드아웃, ApplyPendingDreamstones — 설계상 영구). 드림캐쳐의
            // hostless 영속 apply 는 subconscious-unit unit 3 에서 은퇴.
            public int handle;
            // dreamcatcher-empower-aura unit 1 — 이 효과의 출처. _activeDcEffects 는 드림캐쳐
            // 카드와 드림스톤을 함께 담으므로, 신규 배치 유닛 상속(ApplyActiveDcEffectsTo)이
            // 각 효과를 올바른 origin 으로 재적용하려면 항목마다 출처를 기억해야 한다.
            public Wassup.Battle.Effects.ModifierOrigin origin;
        }
        private readonly System.Collections.Generic.List<ActiveDcEffect> _activeDcEffects =
            new System.Collections.Generic.List<ActiveDcEffect>();
        private ushort _dcStackCounter = 100;
        // unit 9 — hosted-apply revocation handles. review L1 — 앱 수명 monotonic:
        // BeginPlacement 에서 의도적으로 리셋하지 않는다(등록 레지스트리는 매치마다 clear 돼
        // stale handle 이 live 엔트리로 해석될 수 없고, 리셋하면 생존한 stale 맵과 alias 위험).
        private int _dcHandleCounter = 1;
        private const float DcDuration = 1e9f;

        // dreamcatcher-awakening-hand unit 1 — death-drain relays for the awakening
        // economy. Fired from the existing drains (no new queue/context); absent
        // subscribers no-op. EnemyKilledAwakening carries the kill's baked grant.
        // DefenderDied carries the dead entity (unit-card recovery key) + its SO
        // (subscriber reads data.awakeningReward).
        public event System.Action<int> EnemyKilledAwakening;
        public event System.Action<Entity, Wassup.Data.DefenderUnitData> DefenderDied;

        // Applies one card to all currently-placed matching defenders and records
        // it so future placements (ApplyActiveDcEffectsTo) inherit it.
        // combat-action-lock — placement-aura 가 신규 배치 유닛에 걸 Sleep(초) 등록부.
        // _activeDcEffects 를 미러 → 미래 배치 유닛이 상속. BeginPlacement 에서 clear.
        // (구 dreamcatcher-squad-warmup 레지스트리; warmup → Sleep 승격으로 개명.)
        private readonly System.Collections.Generic.List<(int handle, Wassup.Data.CardTargetAxis axis, float sec)> _activePlacementSleeps =
            new System.Collections.Generic.List<(int, Wassup.Data.CardTargetAxis, float)>();

        // dreamcatcher-taxonomy-cleanup unit 1 — single attach entry point for the
        // production caller (controller). Scope keys on CardType: Unit = host-only
        // mechanics/attackMods, else (Squad) = axis-set stat buff anchored at host.
        // Returns the int handle convention (<0 fail / 0 no-revoke / >0 revoke
        // handle). The two apply machines below stay distinct (different lifecycles)
        // and remain public so tests can exercise each directly.
        public int ApplyDreamcatcherCard(Entity host, Wassup.Data.DreamcatcherCard card)
        {
            if (card == null) return -1;
            return card.type == Wassup.Data.CardType.Unit
                ? ApplyDreamcatcherCardToUnit(host, card)
                : ApplyDreamcatcherCardHosted(card);
        }

        // awakening-hand unit 9 — host-bound squad apply. Same squad-wide effect
        // (current + future matching defenders), but the effects belong to a
        // revocation group; the controller revokes it when the host dies.
        // Returns -1 when the card contributed nothing (no spend at the caller).
        public int ApplyDreamcatcherCardHosted(Wassup.Data.DreamcatcherCard card)
        {
            int before = _activeDcEffects.Count + _activePlacementSleeps.Count;
            int handle = _dcHandleCounter++;
            ApplyDreamcatcherCardInternal(card, handle);
            return (_activeDcEffects.Count + _activePlacementSleeps.Count) > before ? handle : -1;
        }

        private void ApplyDreamcatcherCardInternal(Wassup.Data.DreamcatcherCard card, int handle)
        {
            if (card == null) return;
            if (card.effects != null)
            {
                foreach (var eff in card.effects)
                {
                    if (!MapDcEffect(eff, out var stat, out var mult)) continue;
                    ushort sid = _dcStackCounter++;
                    _activeDcEffects.Add(new ActiveDcEffect { axis = card.axis, stat = stat, mult = mult, stackId = sid, handle = handle, origin = Wassup.Battle.Effects.ModifierOrigin.Dreamcatcher });
                    foreach (var kv in _defenderByTile)
                    {
                        var data = kv.Value.data;
                        var entity = kv.Value.entity;
                        if (data != null && _em.Exists(entity) && MatchesDcAxis(data, card.axis))
                            EnqueueStatModifier(entity, stat, mult, DcDuration, sid, Wassup.Battle.Effects.ModifierOrigin.Dreamcatcher);
                    }
                }
            }
            // combat-action-lock — 구 Squad warmup(idle) 경로 은퇴. warmup 개념은
            // placement-aura(PlacementAura payload)가 Sleep 으로만 부여한다(RegisterPlacementAura).
            // dreamcatcher-taxonomy-cleanup unit 2 — 잔재 placementWarmupSec SO 필드도 제거됨.
        }

        // awakening-hand unit 9 — end a hosted squad card's effects (host died).
        // Revocation = NEUTRALIZE, not remove: the modifier merge rule
        // ((source,stat,op,stackId) → magnitude=new, remaining=max) lets us
        // re-apply magnitude 1.0 on the same stackId — the slot stays but the
        // multiplier becomes the identity. No Effects-context change, no new
        // channel. Registry entries are removed so future placements stop
        // inheriting the effect/warmup.
        public void RevokeDreamcatcherEffects(int handle)
        {
            if (handle <= 0) return;
            bool live = HasLiveEntityManager();
            for (int i = _activeDcEffects.Count - 1; i >= 0; i--)
            {
                var e = _activeDcEffects[i];
                if (e.handle != handle) continue;
                if (live)
                {
                    foreach (var kv in _defenderByTile)
                    {
                        var data = kv.Value.data;
                        var entity = kv.Value.entity;
                        if (data != null && _em.Exists(entity) && MatchesDcAxis(data, e.axis))
                        {
                            // 원본 op 와 동일한 identity 로 중립화해야 머지 키(source,stat,op,stackId)가 일치해
                            // 기존 슬롯이 갱신된다. 1f 를 EnqueueStatModifier 로 보내면 FromMultiplier 가 Additive+0
                            // 으로 분류 → 원본이 Multiplicative(감소형 버프, 예 DmgTakenMul 0.87)면 op 불일치로
                            // 중립화 실패(버프 잔존 + 오라 잔존). 원본 op 를 재도출해 그 op 의 항등을 emit.
                            Wassup.Battle.Effects.ModifierAuthoring.FromMultiplier(e.mult, out var op, out _);
                            float idMag = op == Wassup.Battle.Effects.CombineOp.Multiplicative ? 1f : 0f;
                            EnqueueStatModifierRaw(entity, e.stat, op, idMag, DcDuration, e.stackId, e.origin);
                        }
                    }
                }
                _activeDcEffects.RemoveAt(i);
            }
            for (int i = _activePlacementSleeps.Count - 1; i >= 0; i--)
                if (_activePlacementSleeps[i].handle == handle) _activePlacementSleeps.RemoveAt(i);
        }

        private void ApplyActiveDcEffectsTo(Entity entity, DefenderUnitData data)
        {
            if (data == null || !_em.Exists(entity)) return;
            for (int i = 0; i < _activeDcEffects.Count; i++)
            {
                var e = _activeDcEffects[i];
                if (MatchesDcAxis(data, e.axis))
                    EnqueueStatModifier(entity, e.stat, e.mult, DcDuration, e.stackId, e.origin);
            }
            // combat-action-lock — 신규 배치 유닛이 활성 placement-aura Sleep 을 상속.
            for (int i = 0; i < _activePlacementSleeps.Count; i++)
            {
                var w = _activePlacementSleeps[i];
                if (MatchesDcAxis(data, w.axis))
                    ApplyPlacementSleep(entity, w.sec);
            }
        }

        // combat-action-lock unit 4 — 배치 유닛에 Sleep(sec 초) 부여. 구 warmup(cooldownRemaining
        // 직접쓰기) 은퇴 = 층위 비대칭 해소. 잠 = 공격+이동 정지 + 피격 시 해제(wake-on-hit).
        // CcEffect 는 소유 맥락(Effects)이 CcDecay 로 만료·소비. defender 는 unit 2 로 버퍼 보유.
        private void ApplyPlacementSleep(Entity e, float sec)
        {
            if (sec <= 0f || !_em.Exists(e) || !_em.HasBuffer<Wassup.Battle.Effects.CcEffect>(e)) return;
            Wassup.Battle.Effects.EffectSpawner.ApplyCc(_em, e, new Wassup.Battle.Effects.CcEffect
            {
                kind = Wassup.Battle.Effects.CcKind.Sleep,
                remainingTime = sec, // 무한 = float.PositiveInfinity
            });
        }

        // dreamcatcher-unit-trigger Unit 1 — unit-bound card attach: bakes each
        // DcMechanic (definition layer, ECS-free) into a DcTriggerSlot on the
        // defender entity. Translator role: an architecture swap rewrites this,
        // never the definitions. Recall registry (card↔unit↔instanceId, death
        // reclaim) is a follow-up spec — attach only for now.
        private int _dcInstanceCounter;

        // dreamcatcher-placement-aura — 반환 규약: <0 실패(무차감) / 0 성공·회수불필요
        // (엔티티 부착형: 슬롯이 엔티티와 함께 소멸) / >0 성공·회수핸들(host 사망 시 revoke).
        public int ApplyDreamcatcherCardToUnit(Entity defender, Wassup.Data.DreamcatcherCard card)
        {
            if (card == null || card.type != Wassup.Data.CardType.Unit) return -1;
            bool hasMechanics = card.mechanics != null && card.mechanics.Length > 0;
            bool hasAttackMods = card.attackMods != null && card.attackMods.Length > 0;
            if (!hasMechanics && !hasAttackMods) return -1;
            if (!HasLiveEntityManager() || !_em.Exists(defender))
            {
                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCardToUnit('{card?.id}'): ECS not ready or defender entity gone — card not attached.");
                return -1;
            }
            // Contract 2: slots live on defenders only. AttackSystem's RESOLVE arm
            // counts defender attacks, and teardown reaches the buffer through the
            // defender entity — a non-defender attach would silently never fire and
            // could orphan the buffer across matches.
            if (!_em.HasComponent<DefenderUnitTag>(defender))
            {
                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCardToUnit('{card.id}'): target entity is not a defender — card not attached.");
                return -1;
            }

            int attached = 0;
            int auraHandle = 0; // >0 if a revocable placement-aura was registered
            int mechanicsLen = hasMechanics ? card.mechanics.Length : 0;
            for (int i = 0; i < mechanicsLen; i++) // bake-time only read (managed array)
            {
                var m = card.mechanics[i];

                // content-1 ③ (마지막 불꽃) — instant SelfBuffLethal (trigger=None, no
                // slot). Handled BEFORE the trigger guard, which rejects trigger==None.
                if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfBuffLethal)
                {
                    if (m.payload.magnitude <= 0f || m.payload.duration <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfBuffLethal non-positive magnitude/duration — skipped.");
                        continue;
                    }
                    // 공속 +magnitude% for `duration` 초 (기존 StatModifier) + 만료 시 자폭.
                    EnqueueAttackSpeedMul(defender, 1f + m.payload.magnitude / 100f, m.payload.duration, Wassup.Battle.Effects.ModifierOrigin.Dreamcatcher);
                    _em.AddComponentData(defender, new Wassup.Battle.Units.LethalTimer { remaining = m.payload.duration });
                    attached++; // 즉발 branch 도 성공 시 카운트 (critic M2)
                    continue;
                }

                // dreamcatcher-placement-aura — host-bound future-only 스폰 오라(trigger=None).
                // host·기존 유닛 미적용; host 생존 중 axis 매칭 신규 배치 유닛에 부여. host-bound
                // handle 을 반환값으로 올려 host 사망 시 RevokeDreamcatcherEffects 로 회수.
                if (m.payload.kind == Wassup.Data.DcPayloadKind.PlacementAura)
                {
                    if (m.payload.magnitude <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: PlacementAura non-positive magnitude — skipped.");
                        continue;
                    }
                    // review M1 — 카드당 PlacementAura 는 1개만. 두 번째는 등록하면 핸들이
                    // 덮어써져 첫 오라가 host 사망 후에도 영구 누수 → 스킵(등록 안 함).
                    if (auraHandle != 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 카드당 PlacementAura 는 1개만 지원 — 추가 오라 스킵.");
                        continue;
                    }
                    auraHandle = RegisterPlacementAura(card.axis, m.payload.magnitude, m.payload.duration);
                    attached++;
                    continue;
                }

                if (m.trigger.kind == Wassup.Data.DcTriggerKind.None ||
                    m.payload.kind == Wassup.Data.DcPayloadKind.None ||
                    (m.trigger.kind == Wassup.Data.DcTriggerKind.AttackN && m.trigger.period <= 0))
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: None kind or non-positive period — skipped.");
                    continue;
                }

                // content-1 ① (가시 갑옷) — OnDamagedN×NextAttackDoubleFire bakes into
                // DamagedCounter (Units-owned buffer), NOT DcTriggerSlot (Combat): the
                // count is written where the defender takes damage (DamageApplicationSystem,
                // Units). Buffer element → same card twice = independent counters.
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.OnDamagedN &&
                    m.payload.kind == Wassup.Data.DcPayloadKind.NextAttackDoubleFire)
                {
                    if (m.trigger.period <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: OnDamagedN non-positive period — skipped.");
                        continue;
                    }
                    var dbuf = _em.HasBuffer<Wassup.Battle.Units.DamagedCounter>(defender)
                        ? _em.GetBuffer<Wassup.Battle.Units.DamagedCounter>(defender)
                        : _em.AddBuffer<Wassup.Battle.Units.DamagedCounter>(defender);
                    dbuf.Add(new Wassup.Battle.Units.DamagedCounter
                    {
                        instanceId = _dcInstanceCounter++,
                        period = (ushort)math.clamp(m.trigger.period, 0, ushort.MaxValue),
                        counter = 0,
                    });
                    attached++;
                    continue;
                }

                var slot = new DcTriggerSlot
                {
                    instanceId = _dcInstanceCounter++,
                    trigger = m.trigger.kind,
                    period = (ushort)math.clamp(m.trigger.period, 0, ushort.MaxValue),
                    counter = 0,
                    payload = m.payload.kind,
                    magnitude = m.payload.magnitude,
                    projectileDataIndex = -1,
                };
                if (m.payload.kind == Wassup.Data.DcPayloadKind.ProjectileToTarget)
                {
                    if (m.payload.projectile == null)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: ProjectileToTarget without ProjectileData — skipped.");
                        continue;
                    }
                    if (m.payload.magnitude <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: non-positive magnitude — skipped.");
                        continue;
                    }
                    slot.projectileDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                    slot.speed = m.payload.projectile.speed;
                    slot.hitThreshold = m.payload.projectile.hitThreshold;
                    slot.visualScale = m.payload.projectile.visualScale;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                {
                    // content-1 ② — OnDeath explosion. Needs an AOE-view ProjectileData
                    // (impact crater VFX) + positive damage + tileRange.
                    if (m.payload.projectile == null)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfTileAoe without ProjectileData (AOE view) — skipped.");
                        continue;
                    }
                    if (m.payload.magnitude <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: non-positive magnitude — skipped.");
                        continue;
                    }
                    slot.projectileDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                    slot.tileRange = math.max(0, m.payload.tileRange);
                    slot.visualScale = m.payload.projectile.visualScale;
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.ApplyCcToTarget)
                {
                    // dreamcatcher-new-abilities unit 1 — 온-히트 CC(frost_arrow=Stun).
                    // 투사체 불요. duration 초 만큼 적에 CcEffect(번역된 ccKind) 부여.
                    // Impulse 는 magnitude(넉백 속도)도 사용. duration<=0 = 무의미 → skip.
                    if (m.payload.duration <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: ApplyCcToTarget non-positive duration — skipped.");
                        continue;
                    }
                    slot.duration = m.payload.duration;
                    slot.ccKind = MapDcCc(m.payload.ccKind);
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.ApplyStackToTarget)
                {
                    // dreamcatcher-new-abilities unit 1 — 온-히트 스택(ember_bite=Bleed).
                    // magnitude=스택 수(floor,>=1), duration=스택당 지속. 스택→DoT 는
                    // StackModifierTickSystem 이 GetStackThresholds(kind) 규칙으로 처리.
                    if (m.payload.magnitude < 1f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: ApplyStackToTarget magnitude<1 (no stacks) — skipped.");
                        continue;
                    }
                    slot.duration = m.payload.duration;
                    slot.stackKind = MapDcStack(m.payload.stackKind);
                    // review B MED1 — maxStack 상한을 카드에서 authoring(tileRange 재사용).
                    // 0 = 미설정 → fire 에서 기존 producer 선례 5 로 폴백.
                    slot.tileRange = math.max(0, m.payload.tileRange);
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfStatBuff)
                {
                    // dreamcatcher-kill-and-threshold unit 1 — last_stand(HealthThreshold)/
                    // devouring(OnKill). buffStat→StatKind 번역 후 배율/TTL 을 slot 에 baked.
                    // 발동: HealthThreshold=HealthThresholdSystem(unit 1),
                    // OnKill=DamageApplicationSystem(unit 2). 배율은 magnitude(flat 필드
                    // 재사용), TTL 은 duration(<=0 = 영구, arm 이 Infinity 로 해석).
                    if (!MapDcBuff(m.payload.buffStat, m.payload.magnitude, out var buffStat, out var buffMult))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfStatBuff unmappable buffStat ({m.payload.buffStat}) — skipped.");
                        continue;
                    }
                    slot.buffStat = buffStat;
                    slot.magnitude = buffMult;
                    slot.duration = m.payload.duration;
                    // stackId 는 StatModifier 네임스페이스의 단일 할당자에서(squad 이펙트와
                    // 공유) — instanceId 잘라쓰기(네임스페이스 오염) 대신. 슬롯당 고정 →
                    // 매 킬/틱 refresh.
                    slot.statBuffStackId = _dcStackCounter++;
                    if (m.trigger.kind == Wassup.Data.DcTriggerKind.HealthThreshold)
                    {
                        // 디펜더 임계 상태 bake — 스폰 maxHp 스냅샷·경계 간격·래치 k=1.
                        if (m.trigger.fraction <= 0f)
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: HealthThreshold non-positive fraction — skipped.");
                            continue;
                        }
                        if (!_em.HasComponent<Wassup.Battle.Units.Health>(defender))
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: HealthThreshold target has no Health — skipped.");
                            continue;
                        }
                        slot.fraction = m.trigger.fraction;
                        slot.maxHpRef = _em.GetComponentData<Wassup.Battle.Units.Health>(defender).max;
                        slot.nextBoundaryIndex = 1;
                    }
                    // OnKill: 추가 슬롯 상태 없음 — 매 킬 DamageApplicationSystem 에서 발동(unit 2).
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.HeavyStrike)
                {
                    // dreamcatcher-heavy-strike unit 1 — 응축된 일격. AttackN 전용 강공:
                    // N회째 공격의 출력 데미지를 magnitude 배(2.0=×2). 다른 트리거로는
                    // 무의미 → AttackN 강제. 배율<=1 은 강공이 아니므로(1=평타, <1=약화)
                    // 거절. host 는 곱할 Damage output 이 있어야 함(eye 선례 재사용 —
                    // 힐러/output 없는 caster 거절). slot.magnitude 는 이미 배율(위 generic bake).
                    if (m.trigger.kind != Wassup.Data.DcTriggerKind.AttackN)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: HeavyStrike requires AttackN trigger — skipped.");
                        continue;
                    }
                    if (m.payload.magnitude <= 1f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: HeavyStrike magnitude<=1 (not a heavy hit) — skipped.");
                        continue;
                    }
                    if (!HasPositiveDamageOutput(defender))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: HeavyStrike on a unit with no positive Damage output — skipped.");
                        continue;
                    }
                }
                // Immediate (non-ECB) AddBuffer — same technique as ModifierApplySystem's
                // bufferless path: several attaches in one frame must all land; a
                // deferred AddBuffer would keep only the last. (Ownership is a separate
                // question: bridge-side attach writes follow the existing spawn-time
                // precedent — ProjectileRef/AttackState/AttackOutputElement.)
                var buf = _em.HasBuffer<DcTriggerSlot>(defender)
                    ? _em.GetBuffer<DcTriggerSlot>(defender)
                    : _em.AddBuffer<DcTriggerSlot>(defender);
                buf.Add(slot);
                attached++;
            }

            // dreamcatcher-attack-mod-bounce unit 3 — always-on attack-output
            // modifiers (card class c). AttackSystem aggregates these onto the base
            // attack's Homing request at spawn time. Trigger-less; no counter.
            int modsLen = hasAttackMods ? card.attackMods.Length : 0;
            for (int i = 0; i < modsLen; i++)
            {
                var m = card.attackMods[i];
                // dreamcatcher-content-2 unit 1 — per-kind validation. FrontmostTarget
                // does not use `count`, so the legacy global `count > 0` guard would
                // wrongly reject it. damageMul > 0 is the one shared requirement.
                if (m.kind == Wassup.Data.DcAttackModKind.None || m.damageMul <= 0f)
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: None kind / non-positive damageMul — skipped.");
                    continue;
                }
                if (m.kind == Wassup.Data.DcAttackModKind.ProjectileBounce)
                {
                    // Contract 4 — ProjectileBounce needs a positive bounce count and a
                    // HomingToEntity×SingleSplash projectile to modify. Melee (no
                    // ProjectileRef) has none; skip that mod only (same-card mechanics
                    // may still apply).
                    if (m.count <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: ProjectileBounce non-positive count — skipped.");
                        continue;
                    }
                    if (!_em.HasComponent<ProjectileRef>(defender))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: ProjectileBounce on a non-projectile (melee) unit — skipped.");
                        continue;
                    }
                }
                else if (m.kind == Wassup.Data.DcAttackModKind.FrontmostTarget)
                {
                    // 끝을 보는 눈 — boosts the primary target's direct Damage, so it needs
                    // a positive Damage-kind output to boost. Heal-only / output-less
                    // support units would consume the card with zero effect → reject.
                    // `count`/`tileRange` are ignored (uses the base attack range).
                    if (!HasPositiveDamageOutput(defender))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: FrontmostTarget on a unit with no positive Damage output — skipped.");
                        continue;
                    }
                    // Combat-owned per-attack lock; add once, idempotent across copies.
                    if (!_em.HasComponent<FrontmostAttackLock>(defender))
                        _em.AddComponentData(defender, new FrontmostAttackLock
                        {
                            active = false,
                            target = Entity.Null,
                            damageMulSnapshot = 1f,
                            targetIsPriority = false,
                        });
                }
                var modBuf = _em.HasBuffer<DcAttackModSlot>(defender)
                    ? _em.GetBuffer<DcAttackModSlot>(defender)
                    : _em.AddBuffer<DcAttackModSlot>(defender);
                modBuf.Add(new DcAttackModSlot
                {
                    instanceId = _dcInstanceCounter++,
                    kind = m.kind,
                    count = m.count,
                    tileRange = m.tileRange,
                    damageMul = m.damageMul,
                });
                attached++;
            }
            if (attached == 0) return -1;
            return auraHandle;
        }

        // dreamcatcher-content-2 unit 1 — does this defender emit at least one positive
        // Damage-kind attack output (vs heal-only / output-less)? Gates FrontmostTarget
        // attach so 끝을 보는 눈 can't be spent inertly on a support unit.
        private bool HasPositiveDamageOutput(Entity defender)
        {
            if (!_em.HasBuffer<AttackOutputElement>(defender)) return false;
            var outs = _em.GetBuffer<AttackOutputElement>(defender);
            for (int k = 0; k < outs.Length; k++)
                if (outs[k].value.kind == Wassup.Data.AttackOutputKind.Damage && outs[k].value.magnitude > 0f)
                    return true;
            return false;
        }

        // dreamcatcher-placement-aura — host-bound future-only aura. _defenderByTile
        // 루프 없음 → 현재 유닛/host 미적용, ApplyActiveDcEffectsTo(신규 배치)에서만 상속.
        // revocable handle 반환(host 사망 시 RevokeDreamcatcherEffects 로 전 수혜 유닛 회수).
        private int RegisterPlacementAura(Wassup.Data.CardTargetAxis axis, float asPercent, float warmupSec)
        {
            int handle = _dcHandleCounter++;
            if (asPercent > 0f)
            {
                ushort sid = _dcStackCounter++;
                _activeDcEffects.Add(new ActiveDcEffect
                {
                    axis = axis,
                    stat = Wassup.Battle.Effects.StatKind.AttackSpeedMul,
                    mult = 1f + asPercent / 100f,
                    stackId = sid,
                    handle = handle,
                    origin = Wassup.Battle.Effects.ModifierOrigin.Dreamcatcher,
                });
            }
            if (warmupSec > 0f) _activePlacementSleeps.Add((handle, axis, warmupSec));
            return handle;
        }

        private static bool MapDcEffect(Wassup.Data.CardEffect eff, out Wassup.Battle.Effects.StatKind stat, out float mult)
            => MapDcBuff(eff.kind, eff.percent, out stat, out mult);

        // dreamcatcher-kill-and-threshold unit 1 — CardBuffKind→(StatKind,배율) 순수 번역.
        // MapDcEffect(CardEffect)가 위임하고, SelfStatBuff bake 도 재사용(정의 계층이
        // Battle.StatKind 를 모르게 유지하는 유일한 번역 지점).
        private static bool MapDcBuff(Wassup.Data.CardBuffKind kind, float percent, out Wassup.Battle.Effects.StatKind stat, out float mult)
        {
            switch (kind)
            {
                case Wassup.Data.CardBuffKind.AttackDamage:
                    stat = Wassup.Battle.Effects.StatKind.DamageMul; mult = 1f + percent / 100f; return true;
                case Wassup.Data.CardBuffKind.AttackSpeed:
                    stat = Wassup.Battle.Effects.StatKind.AttackSpeedMul; mult = 1f + percent / 100f; return true;
                case Wassup.Data.CardBuffKind.EffectiveHealth:
                    // HP% proxy: less damage taken = higher effective health (max-HP unchanged).
                    stat = Wassup.Battle.Effects.StatKind.DmgTakenMul; mult = 1f / (1f + percent / 100f); return true;
                case Wassup.Data.CardBuffKind.MoveSpeed:
                    stat = Wassup.Battle.Effects.StatKind.MoveSpeedMul; mult = 1f + percent / 100f; return true;
                // dreamcatcher-new-abilities unit 2 — shatter_hymn: CC 걸린 적 대상 데미지 +percent%.
                case Wassup.Data.CardBuffKind.DamageVsCc:
                    stat = Wassup.Battle.Effects.StatKind.DamageVsCcMul; mult = 1f + percent / 100f; return true;
                // dreamstone-loadout Unit 6 — CardBuffKind.CostRate has no entity/ECS
                // stat (it scales CostRuntime.RegenRateMultiplier, a MonoBehaviour-side
                // resource, not a StatModifier channel). It falls through to this
                // default branch on purpose: GameManager.ResolveEquippedStones already
                // filters CostRate stones out of the list handed to SetDreamstones, so
                // this is a defensive no-op, never an expected live path.
                default:
                    stat = Wassup.Battle.Effects.StatKind.DamageMul; mult = 1f; return false;
            }
        }

        // dreamcatcher-new-abilities unit 1 — 데이터 계층 CC/스택 선택자 → Battle enum
        // 번역(정의 계층이 Battle 타입을 모르게 유지; MapDcEffect 와 동일 번역자 역할).
        private static Wassup.Battle.Effects.CcKind MapDcCc(Wassup.Data.DcCcKind kind)
        {
            switch (kind)
            {
                case Wassup.Data.DcCcKind.Stun: return Wassup.Battle.Effects.CcKind.Stun;
                case Wassup.Data.DcCcKind.Impulse: return Wassup.Battle.Effects.CcKind.Impulse;
                default: return Wassup.Battle.Effects.CcKind.Stun;
            }
        }

        private static Wassup.Battle.Effects.StackKind MapDcStack(Wassup.Data.DcStackKind kind)
        {
            switch (kind)
            {
                case Wassup.Data.DcStackKind.Fire: return Wassup.Battle.Effects.StackKind.Fire;
                case Wassup.Data.DcStackKind.Ice: return Wassup.Battle.Effects.StackKind.Ice;
                case Wassup.Data.DcStackKind.Bleed: return Wassup.Battle.Effects.StackKind.Bleed;
                case Wassup.Data.DcStackKind.Poison: return Wassup.Battle.Effects.StackKind.Poison;
                default: return Wassup.Battle.Effects.StackKind.Bleed;
            }
        }

        private static bool MatchesDcAxis(DefenderUnitData data, Wassup.Data.CardTargetAxis axis)
        {
            switch (axis)
            {
                case Wassup.Data.CardTargetAxis.ClassRanger: return data.role == Wassup.Data.DefenderClass.Ranger;
                case Wassup.Data.CardTargetAxis.ClassGuardian: return data.role == Wassup.Data.DefenderClass.Guardian;
                case Wassup.Data.CardTargetAxis.Cost1: return data.cost == 1;
                // dreamstone-loadout Unit 3 — All must be explicit. Falling to default
                // (false) would silently no-op every equipped stone with no error.
                case Wassup.Data.CardTargetAxis.All: return true;
                default: return false;
            }
        }
    }
}
