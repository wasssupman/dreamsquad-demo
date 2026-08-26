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
        // dreamcatcher-orb-dock unit 3 — 흡수 비행용으로 사망 view-space 위치를 함께 실어보낸다
        // (기존 sim 데이터 surfacing; 새 ECS write 아님). 구독자는 컨트롤러 하나뿐이라 안전.
        // unit 6 — 죽은 적 시각 데이터(ISpineUnitVisualData)를 함께 실어 피규어를 그 적 스킨으로
        // 렌더한다(등록부 조회, null 이면 대표 스킨 폴백).
        public event System.Action<int, Vector3, Wassup.Data.ISpineUnitVisualData> EnemyKilledAwakening;
        public event System.Action<Entity, Wassup.Data.DefenderUnitData, Vector3> DefenderDied;
        // defender-clock-out unit 1 — **자발적 퇴근.** DefenderDied 의 형제이고 시그니처도 같다.
        // 플래그 하나로 합치지 않은 이유: DefenderDied 의 구독자 2개가 퇴근에서 **둘 다 다르게**
        // 굴어야 한다(트레이는 쿨타임 시작이 붙고, 손패는 각성 지급이 빠진다). 합치면 두 구독자
        // 전부 if (retired) 를 쓰고 앞으로 붙는 구독자도 그걸 기억해야 한다 — 갈라 두면 잘못
        // 구독하는 것 자체가 어려워진다.
        // ⚠ Vector3(셀 view 좌표)는 **현재 소비처가 없다.** DefenderDied 와의 형제 대칭으로 남겨
        // 뒀다. unit 3 의 퇴근 아치가 이걸 쓸 예정이었으나, 실제로는 떼어낸 뷰의 자기 transform 이
        // 더 정확해서(SpineVisualOffset·넉업 hop 이 얹혀 있다) 그쪽을 쓴다. 알고 남긴 인자다.
        public event System.Action<Entity, Wassup.Data.DefenderUnitData, Vector3> DefenderRetired;
        // defender-board-limit 1 — 방어유닛이 판에 **올라온** 순간. DefenderDied 의 짝이고,
        // 둘 다 «바인딩(_defenderByTile)이 바뀌었다» 는 같은 사실을 알린다.
        //
        // 트레이 소진 표현이 이 짝을 구독한다. 예전엔 배치 쪽만 UI 이벤트
        // (DefenderDragPlacementController.PlacementCommitted)를 들었는데, 그러면 드래그를
        // 지나지 않는 배치 경로(PlaceDefenderAs 직접 호출 등)에서 트레이가 조용히 stale 해진다.
        // 사망은 브리지가, 배치는 UI 가 알리는 비대칭이 원인이었다 — 둘 다 브리지가 알린다.
        public event System.Action<Entity, Wassup.Data.DefenderUnitData> DefenderPlaced;
        // subconscious-curse-expansion unit 2 (살찌운 제물) — 표식 악몽 소멸(처치 또는
        // 유출) 알림. 컨트롤러가 카드 회수(큐 복귀)에 구독한다. 처치/유출의 보상 차이는
        // 이 이벤트가 아니라 표식 시점의 AwakeningReward 베이크가 만든다(처치=배율 보상
        // 자동 지급, 유출=지급 없음 — 두 드레인 모두 보상 경로 무수정).
        public event System.Action<Entity> EnemyGone;

        // 표식 등록부(적 entity 키). 처치/유출 드레인에서 제거+EnemyGone 발화,
        // BeginPlacement 에서 clear. 이 mark 는 적에 Dreamcatcher-origin 모디파이어를
        // 얹는 **최초 사례** — 향후 origin 기반 판정(오라/dispel/UI)을 추가할 때는
        // 반드시 진영/태그 게이트를 유지할 것 (spec critic m6).
        private readonly System.Collections.Generic.HashSet<Entity> _bountyMarked =
            new System.Collections.Generic.HashSet<Entity>();

        // 드레인 훅(BattleBridge.cs 처치/유출 드레인에서 호출) — 표식이면 회수 알림.
        internal void NotifyEnemyGoneIfMarked(Entity enemy)
        {
            if (_bountyMarked.Remove(enemy)) EnemyGone?.Invoke(enemy);
        }

        // dreamcatcher-attach-lockon — 살찌운 제물(EnemyMark) 리티클/콜아웃 유효성.
        // 이미 표식된 적은 재표식 불가(ApplyBountyMark 의 '이미 표식됨' 프리플라이트와
        // 동일 게이트). 읽기 전용.
        public bool IsEnemyMarked(Entity enemy) => _bountyMarked.Contains(enemy);

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
            if (card.type == Wassup.Data.CardType.Unit)
                return ApplyDreamcatcherCardToUnit(host, card);
            if (card.type != Wassup.Data.CardType.Squad)
                return -1;

            // dreamcatcher-attach-requirement unit 10 — Squad 의 axis 는 버프 수혜
            // 집합이고 attachType 은 전역 버프를 유지할 host 제한이다. hosted 효과 머신의
            // 첫 쓰기(_activeDcEffects.Add) 전에 검사해 거절 시 부분 적용·차감이 없다.
            if (!HasLiveEntityManager() || !_em.Exists(host))
            {
                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCard('{card.id}'): ECS not ready or defender entity gone — card not attached.");
                return -1;
            }
            if (!_em.HasComponent<DefenderUnitTag>(host))
            {
                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCard('{card.id}'): target entity is not a defender — card not attached.");
                return -1;
            }
            if (!PassesAttachRequirement(host, card))
            {
                LogAttachRequirementReject(host, card);
                return -1;
            }
            return ApplyDreamcatcherCardHosted(card);
        }

        // awakening-hand unit 9 — host-bound squad apply. Same squad-wide effect
        // (current + future matching defenders), but the effects belong to a
        // revocation group; the controller revokes it when the host dies.
        // Low-level effect machine for focused tests and pre-hosted sources: it has no
        // host and therefore does not evaluate attachType. Production hand commits must
        // enter through ApplyDreamcatcherCard(host, card), which owns the unit 10 gate.
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

            // dreamcatcher-attach-requirement unit 1 — 부착 제한 preflight. 여기까지는
            // 전부 순수 읽기라(첫 쓰기는 아래 mechanics bake 루프) 부분 적용 위험 0.
            // 거절은 카드 전체·무차감(-1 → HandController.CommitAttach 가 Spend 전 반환).
            if (!PassesAttachRequirement(defender, card))
            {
                LogAttachRequirementReject(defender, card);
                return -1;
            }

            // attack-decoupling unit 1 — host 종속 판정의 단일 입력. UI preflight
            // (WouldDreamcatcherCardApply)와 **같은 profile·같은 함수**를 쓴다.
            var hostProfile = BuildHostProfile(defender);

            // subconscious-cursed-relics unit 0 / curse-expansion unit 0 — 이중 상태는
            // 어떤 쓰기도 하기 전에 **카드 전체**를 거절한다. AddComponentData 가 기존
            // LethalTimer/DreamCocoon 을 덮어써 원래 타이머를 리셋하고 멀티-mechanic
            // 카드를 부분 적용시키기 때문. (판정은 DcApplicability 로 수렴 — 두 블록이
            // 하나가 됐다.)
            int preflightMechanicsLen = hasMechanics ? card.mechanics.Length : 0;
            for (int i = 0; i < preflightMechanicsLen; i++)
            {
                var pm = card.mechanics[i];
                if (Wassup.Core.DcApplicability.EvaluateMechanic(pm, hostProfile)
                    != Wassup.Core.DcRejectReason.DuplicateState) continue;

                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCardToUnit('{card.id}'): target already has {pm.payload.kind} state — card not attached.");
                return -1;
            }

            int attached = 0;
            // unit 4a — 부착 seam 에 실린 것이 있으면 **이 호출 안에서** 드레인한다.
            // 루프 뒤에 한 번만 도는 이유: mechanic 이 여럿이면 한 번에 소진하는 것이
            // 자연스럽고, 중간에 돌리면 같은 카드의 뒷 mechanic 이 앞 것의 결과를 본다.
            bool immediateFired = false;
            int auraHandle = 0; // >0 if a revocable placement-aura was registered
            int mechanicsLen = hasMechanics ? card.mechanics.Length : 0;
            for (int i = 0; i < mechanicsLen; i++) // bake-time only read (managed array)
            {
                var m = card.mechanics[i];

                // attack-decoupling unit 1 — host 종속 판정 단일 게이트. 이 한 줄이
                // 예전의 흩어진 가드 3종(ProjectileToTarget ally / HeavyStrike 데미지
                // output / 이중 상태)을 대체한다. 아래 kind별 블록에 남은 것은 전부
                // **카드 데이터 검증**(magnitude·duration·projectile null 등)이다.
                var hostReason = Wassup.Core.DcApplicability.EvaluateMechanic(m, hostProfile);
                if (hostReason != Wassup.Core.DcRejectReason.None)
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: {m.payload.kind} 는 이 host 에서 발동하지 않는다 ({hostReason}, archetype={hostProfile.archetype}, route={hostProfile.route}) — skipped.");
                    continue;
                }

                // content-1 ③ (마지막 불꽃) — instant SelfBuffLethal (trigger=None, no
                // slot). Handled BEFORE the trigger guard, which rejects trigger==None.
                if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfBuffLethal)
                {
                    if (m.payload.magnitude <= 0f || m.payload.duration <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfBuffLethal non-positive magnitude/duration — skipped.");
                        continue;
                    }
                    // unit 4a — 실행은 concrete 가 한다. 여기가 하는 일은 **발화 신호**와
                    // 값 스냅샷뿐이다(다른 다섯 seam 의 감지자와 같은 모양).
                    // ⚠ `% → 배율` 은 여기서 바꾼다 — 슬롯 bake 가 같은 자리에서 같은 변환을
                    // 하고(그 주석: 「bake 가 % → 배율로 이미 바꿔 실은 값」), 도메인은 저작
                    // 인코딩을 모르는 것이 계약이다.
                    int lethalSkillId = SkillIdForCardPayload(m.trigger.kind, m.payload.kind);
                    if (lethalSkillId != Wassup.Skills.SkillRegistry.LegacyArmId
                        && _skillFiredQueue.IsCreated)
                    {
                        _skillFiredQueue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                        {
                            Seam = Wassup.Battle.Skills.SkillSeam.Immediate,
                            Caster = defender,
                            SkillId = lethalSkillId,
                            SlotIndex = i,
                            Target = Entity.Null,
                            Magnitude = 1f + m.payload.magnitude / 100f,
                            Duration = m.payload.duration,
                        });
                        immediateFired = true;
                    }
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

                // subconscious-curse-expansion unit 0 (호접몽) — instant DreamCocoon
                // (trigger=None, no slot). 부착 즉시 Sleep(duration) + 완주 감시 컴포넌트.
                // 완주(무피격) 시 DreamCocoonSystem 이 self 영구 버프 부여, 피격 wake 시
                // 파탄(버프 없음) — 리스크는 기존 wake-on-hit 그 자체(신규 잠 변종 없음).
                if (m.payload.kind == Wassup.Data.DcPayloadKind.DreamCocoon)
                {
                    // Epsilon 가드: duration−Epsilon 이 0 이하면 무수면 즉시 완주 foot-gun
                    // (spec critic m3). Epsilon 은 내부 상수 — 튜닝 노브 아님.
                    if (m.payload.magnitude <= 0f || m.payload.duration <= Wassup.Battle.Effects.DreamCocoon.Epsilon)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: DreamCocoon non-positive magnitude or duration <= epsilon — skipped.");
                        continue;
                    }
                    if (!MapDcBuff(m.payload.buffStat, m.payload.magnitude, out var cocoonStat, out var cocoonMult))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: DreamCocoon unmappable buffStat ({m.payload.buffStat}) — skipped.");
                        continue;
                    }
                    if (!_em.HasBuffer<Wassup.Battle.Effects.CcEffect>(defender))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: DreamCocoon target has no CcEffect buffer — skipped.");
                        continue;
                    }
                    // unit 4b — 실행은 concrete 가 한다. 저작(kind + %)을 스탯·배율로 푸는
                    // 것과 스택 슬롯 발급은 **여기가** 한다 — 둘 다 저작 인코딩과 브리지
                    // 소유 카운터라 도메인이 알 이유가 없다.
                    int cocoonSkillId = SkillIdForCardPayload(m.trigger.kind, m.payload.kind);
                    if (cocoonSkillId != Wassup.Skills.SkillRegistry.LegacyArmId
                        && _skillFiredQueue.IsCreated)
                    {
                        _skillFiredQueue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                        {
                            Seam = Wassup.Battle.Skills.SkillSeam.Immediate,
                            Caster = defender,
                            SkillId = cocoonSkillId,
                            SlotIndex = i,
                            Target = Entity.Null,
                            Duration = m.payload.duration,
                            StatSelector = (int)cocoonStat,
                            Magnitude = cocoonMult,
                            StackId = _dcStackCounter++,
                        });
                        immediateFired = true;
                    }
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

                // dreamcatcher-retire-recall unit 0 — 손패 조작(hand op) payload.
                // 실행자가 sim 도 브리지도 아니라 DreamcatcherHandController 다 → **슬롯을 굽지
                // 않는다.** 브리지가 하는 일은 "이 선언이 유효하다"를 인정하는 것뿐이고,
                // attached++ 가 필요한 이유는 attached==0 이면 아래에서 -1(부착 거절, 무차감)이
                // 되기 때문이다 — sim 기여가 0 이어도 카드는 실제로 일한다.
                // (PlacementAura 가 엔티티에 아무것도 안 쓰고 카운트만 하는 것과 같은 모양.)
                //
                // 트리거 화이트리스트: 손패 컨트롤러가 host 귀속으로 볼 수 있는 사건은
                // DefenderRetired / DefenderDied / EnemyGone 뿐이고 지금 배선된 것은 퇴근 하나다.
                // sim 트리거(AttackN·OnKill·PeriodicTimer …)와 조합하면 슬롯도 없고 사건도 안 와서
                // **영영 안 터지는 카드**가 되므로 조용히 통과시키지 않는다(기존 bake 가드 관례).
                if (Wassup.Data.DcPayloadKinds.IsHandOp(m.payload.kind))
                {
                    if (m.trigger.kind != Wassup.Data.DcTriggerKind.OnRetire)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 손패 조작 payload({m.payload.kind}) 는 OnRetire 에만 배선돼 있다 (현재 trigger={m.trigger.kind}) — skipped.");
                        continue;
                    }
                    // 이 분기는 아래 게이트 검증 블록보다 **위**에 있어 continue 하면 그 검증을
                    // 건너뛴다. 게이트를 저작하면 조용히 무시되므로(이 파일의 관례를 깨는
                    // 유일한 경로가 된다) 여기서 직접 거절한다. GateComboSupported 는 어차피
                    // OnRetire × 모든 게이트를 미지원으로 판정한다.
                    if (m.trigger.gate != Wassup.Data.DcGateKind.None)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 손패 조작 payload 에는 게이트가 배선돼 있지 않다 (gate={m.trigger.gate}) — skipped.");
                        continue;
                    }
                    attached++;
                    continue;
                }

                // dreamcatcher-content-4 unit 0 — 주기 트리거의 방어유닛 개방.
                // BossPeriodicTriggerSystem 은 이미 진영 중립이다(게이트가 DcTriggerSlot 버퍼
                // 존재뿐). 막고 있던 것은 **이 bake 가 periodSeconds 를 안 실어 보내서**
                // 슬롯이 0(=no-fire 가드)으로 굳던 것 하나였다 — 아래 슬롯 조립에서 싣는다.
                // 값이 없는 저작은 조용한 무발동 대신 loud 거절한다(dc-trigger 관례).
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.PeriodicTimer && m.trigger.periodSeconds <= 0f)
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: PeriodicTimer non-positive periodSeconds — skipped.");
                    continue;
                }

                // dreamcatcher-content-4 unit 0 — 퇴근 트리거가 **슬롯으로** 여는 배선은
                // SelfTileAoe 한 쌍뿐이다. 퇴장 지점(RetireDefender)에는 trigger×payload
                // 디스패처가 없고 운석 cast 하나만 있으므로, 다른 payload 를 통과시키면 슬롯만
                // 붙고 아무 일도 안 하는 카드가 "부착됨"으로 집계된다(같은 함수의 트리거 축
                // 가드들과 같은 이유).
                // ⚠ 손패 조작 payload 는 이 가드에 도달하지 않는다 — 위에서 슬롯 없이 처리하고
                // continue 한다(dreamcatcher-retire-recall unit 0).
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.OnRetire
                    && m.payload.kind != Wassup.Data.DcPayloadKind.SelfTileAoe)
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: OnRetire 는 SelfTileAoe 만 배선돼 있다 (현재 payload={m.payload.kind}) — skipped.");
                    continue;
                }

                // trigger-gates unit 1 — 게이트 배선 검증. 배선 표의 단일 SoT 는
                // DcTrigger.GateComboSupported — 미배선/퇴화 조합은 조용한 무효과 대신
                // loud 거절 (기존 bake 가드 컨벤션).
                if (m.trigger.gate != Wassup.Data.DcGateKind.None)
                {
                    if (!DcTrigger.GateComboSupported(m.trigger.kind, m.trigger.gate, m.trigger.gateSubject))
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: unsupported gate combo ({m.trigger.kind}×{m.trigger.gate}/{m.trigger.gateSubject}) — skipped.");
                        continue;
                    }
                    if (m.trigger.gateValue <= 0f || m.trigger.gateValue >= 1f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: gateValue out of (0,1) — skipped.");
                        continue;
                    }
                }

                // content-1 ① (가시 갑옷) — OnDamagedN×NextAttackDoubleFire bakes into
                // DamagedCounter (Units-owned buffer), NOT DcTriggerSlot (Combat): the
                // count is written where the defender takes damage (DamageApplicationSystem,
                // Units). Buffer element → same card twice = independent counters.
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.OnDamagedN)
                {
                    // trigger-gates unit 0 — payload 개통. NextAttackDoubleFire 전용
                    // 가드 해제: SelfTileAoe(피격 폭발)도 bake. 그 외 kind 는 미지원.
                    if (m.trigger.period <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: OnDamagedN non-positive period — skipped.");
                        continue;
                    }
                    var dEntry = new Wassup.Battle.Units.DamagedCounter
                    {
                        // unit 3d‴ — 이 버퍼의 라우팅 키. 슬롯 경로와 **같은 규칙 함수**를
                        // 쓴다(버퍼가 다르다는 것이 규칙이 다르다는 뜻은 아니다).
                        skillId = SkillIdForCardPayload(m.trigger.kind, m.payload.kind),
                        instanceId = _dcInstanceCounter++,
                        period = (ushort)math.clamp(m.trigger.period, 0, ushort.MaxValue),
                        counter = 0,
                        payload = m.payload.kind,
                        aoeDataIndex = -1,
                        // trigger-gates unit 1 — OnDamagedN 게이트(Self 고정, 위 배선 검증 통과분).
                        gate = m.trigger.gate,
                        gateValue = m.trigger.gateValue,
                    };
                    if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfTileAoe)
                    {
                        // SelfTileAoe bake 규칙은 슬롯 경로와 동일 (AOE view + 양수 데미지).
                        if (m.payload.projectile == null)
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: OnDamagedN×SelfTileAoe without ProjectileData (AOE view) — skipped.");
                            continue;
                        }
                        if (m.payload.magnitude <= 0f)
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: non-positive magnitude — skipped.");
                            continue;
                        }
                        dEntry.magnitude = m.payload.magnitude;
                        dEntry.tileRange = math.max(0, m.payload.tileRange);
                        dEntry.aoeDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                        // 형제들(`DcTriggerSlot.visualScale`)과 같은 자리·같은 출처.
                        dEntry.aoeVisualScale = m.payload.projectile.visualScale;
                    }
                    else if (m.payload.kind != Wassup.Data.DcPayloadKind.NextAttackDoubleFire)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: OnDamagedN unsupported payload ({m.payload.kind}) — skipped.");
                        continue;
                    }
                    var dbuf = _em.HasBuffer<Wassup.Battle.Units.DamagedCounter>(defender)
                        ? _em.GetBuffer<Wassup.Battle.Units.DamagedCounter>(defender)
                        : _em.AddBuffer<Wassup.Battle.Units.DamagedCounter>(defender);
                    dbuf.Add(dEntry);
                    attached++;
                    continue;
                }

                var slot = new DcTriggerSlot
                {
                    // 카드 경로의 스킬 레이어 라우팅 — 규칙은 `SkillIdForCardPayload` 소유.
                    skillId = SkillIdForCardPayload(m.trigger.kind, m.payload.kind),
                    instanceId = _dcInstanceCounter++,
                    trigger = m.trigger.kind,
                    period = (ushort)math.clamp(m.trigger.period, 0, ushort.MaxValue),
                    counter = 0,
                    payload = m.payload.kind,
                    magnitude = m.payload.magnitude,
                    projectileDataIndex = -1,
                    // 보스 bake 와 같은 불변식(struct default 0 은 유효 index 다). 지금은
                    // 아래 EmitProjectilePattern 거절이 도달 경로를 막지만, 불변식 자체를
                    // 여기서 걸어두어야 다른 kind 가 patternIndex 를 쓰게 될 때 안전하다.
                    patternIndex = -1,
                    // content-5 unit 0 — 같은 불변식(0 은 유효 index 다). SpawnHazard 분기만 채운다.
                    hazardDataIndex = -1,
                    // dreamcatcher-content-4 unit 0 — 주기 트리거 개방(계약 9). 여태 보스 bake
                    // (BakeNightmareMechanics)만 실어 보내서 카드 주기 슬롯이 조용히 무발동이었다.
                    // 비-PeriodicTimer 슬롯은 0 = inert 라 기존 카드 전부 무손상.
                    periodSeconds = m.trigger.periodSeconds,
                    // **카드는 부착 즉시 첫 발동한다**(사용자 결정 2026-08-16). 누산기를 주기만큼
                    // 채워 구우면 다음 틱에 바로 터진다(PeriodicTick 은 `elapsed += dt` 뒤 임계를
                    // 보므로 첫 프레임에 넘는다. 나머지는 이월돼 이후 주기는 정확히 유지된다).
                    //
                    // 왜 필요한가: 카드는 **전투 중에** 붙는다. 보스 스킬처럼 스폰과 함께 시작하는
                    // 것과 달리, 붙이자마자 주기만큼 아무 일도 안 일어나면 플레이어에겐 «안 붙었다»
                    // 로 읽힌다(불꽃 팽이 6초). 보스 bake 는 이 줄을 타지 않으므로 무영향.
                    elapsed = m.trigger.kind == Wassup.Data.DcTriggerKind.PeriodicTimer
                        ? m.trigger.periodSeconds : 0f,
                    // trigger-gates unit 1 — 게이트 번역 (위 배선 검증 통과분만 착지).
                    gate = m.trigger.gate,
                    gateSubject = m.trigger.gateSubject,
                    gateValue = m.trigger.gateValue,
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
                    // dreamcatcher-content-5 unit 0 — **탄 에셋의 궤적을 존중한다.** 여태 발사
                    // arm(SpawnNeedleCarrier)이 (Homing, SingleSplash)를 하드코딩해 저작이
                    // 무시됐다. 번역은 ResolveProjectileAxes 단일 지점을 쓴다.
                    // 기존 카드(비수)의 탄은 Homing 이라 축이 종전과 같다 — 무회귀.
                    var dcAxes = ResolveProjectileAxes(m.payload.projectile.flightMode);
                    slot.projectileMovement = dcAxes.movement;
                    slot.projectilePayload = dcAxes.payload;
                    // ⚠ **방향 바인딩(부메랑 등)은 유효 저작의 요구가 다르다.** 호밍은 대상을
                    // 쫓아가 hitThreshold 가 «도달 판정» 이라 관대하지만, 경로를 훑는 탄에게
                    // 그 값은 **스치는 굵기**라 0 이면 «정상으로 날아갔다 돌아오는데 아무도 못
                    // 맞히는» 탄이 된다 — 로그 한 줄 없이 조용하다. 거리·속도 0 은 드레인이
                    // 잡지만 그건 **발사할 때마다** 경고라 부착 시점에 끊는 게 맞다.
                    // 형제 payload(SelfOrbitProjectile)가 같은 셋을 같은 형태로 거절한다.
                    var dcBinding = Wassup.Battle.Combat.Projectile.Emission.MovementBinding.Of(dcAxes.movement);
                    // ⚠ **셀 바인딩은 이 경로에 배선돼 있지 않다.** 발사 arm(SpawnNeedleCarrier)은
                    // `target` 은 늘 싣지만 `impact` 를 **한 번도 채우지 않는다** — 그래서
                    // SkyFall·BallisticToCell 탄으로 저작하면 착탄점이 (0,0,0) 이라 **보드 원점에
                    // 떨어진다**. 궤적을 저작에 개방한 것이 이 뒷문을 열었으므로 여기서 닫는다.
                    // (개통하려면 arm 이 대상 셀을 계산해 실어야 한다 — 별도 작업.)
                    if (dcBinding == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Cell)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 셀 바인딩 탄({m.payload.projectile.flightMode})은 이 payload 에 미배선 — 착탄점이 없어 보드 원점에 떨어진다 — skipped.");
                        continue;
                    }
                    if (dcBinding == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Direction)
                    {
                        if (m.payload.projectile.hitThreshold <= 0f)
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 경로 스윕 탄의 hitThreshold<=0 — 아무도 못 맞힌다 — skipped.");
                            continue;
                        }
                        if (m.payload.projectile.speed <= 0f)
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 경로 스윕 탄의 speed<=0 — 날아가지 못한다 — skipped.");
                            continue;
                        }
                        if (m.payload.tileRange <= 0)
                        {
                            Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 방향 바인딩에서 tileRange 는 **날아가는 거리**다 — 0 이면 발사 자체가 성립하지 않는다 — skipped.");
                            continue;
                        }
                    }
                    // attack-decoupling unit 2 — 폴백 탐색 반경(host 가 대상을 못 고를 때만
                    // 쓰인다). 0 이하는 "폴백 없음"이지 "발동 불가"가 아니다 — host 우선
                    // 경로(현재 전부)는 반경과 무관하게 정상 동작한다(spec 계약 3).
                    slot.tileRange = math.max(0, m.payload.tileRange);
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
                    // dreamcatcher-content-4 unit 0 — 낙하 예고 초(SkyFall flightTime).
                    // AreaBarrage 의 duration=텔레그래프 선례. **OnRetire(퇴근 운석)만 소비**하고
                    // 기존 SelfTileAoe 카드는 전부 duration 0 이라 즉시 착탄 그대로다.
                    slot.duration = math.max(0f, m.payload.duration);
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.SpawnHazard)
                {
                    // content-5 unit 4 — 잿불. 카드는 「어떤 불씨를」만 말하고 모양·반경·지속·
                    // 효과·틱·뷰는 전부 그 SO 소유다(계약 9). 여기서 하는 일은 인덱스 등록뿐.
                    if (m.payload.hazard == null)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SpawnHazard without HazardSO — skipped.");
                        continue;
                    }
                    // 트리거 축 가드 — 발동 지점은 킬 처리 하나뿐이다(SelfOrbitProjectile 이
                    // PeriodicTimer 만 배선한 것과 같은 이유: 조용한 무발동 금지).
                    if (m.trigger.kind != Wassup.Data.DcTriggerKind.OnKill)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SpawnHazard 는 OnKill 만 배선돼 있다 (현재 trigger={m.trigger.kind}) — skipped.");
                        continue;
                    }
                    slot.hazardDataIndex = RegisterZoneHazardSO(m.payload.hazard);
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.SelfOrbitProjectile)
                {
                    // 리뷰 M3 — **트리거 축 가드**. 발동 arm 은 BossPeriodicTriggerSystem 하나뿐이라
                    // AttackN/HealthThreshold 로 저작하면 슬롯은 붙고 발동마다 각 시스템의
                    // "unhandled payload kind" 경고만 뜨면서 카드는 "부착됨"으로 집계된다.
                    // 바로 위 OnRetire 가드가 정확히 같은 이유로 반대 방향을 막고 있다 —
                    // 한쪽만 비워두면 저작 foot-gun 이 남는다.
                    if (m.trigger.kind != Wassup.Data.DcTriggerKind.PeriodicTimer)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile 은 PeriodicTimer 만 배선돼 있다 (현재 trigger={m.trigger.kind}) — skipped.");
                        continue;
                    }
                    // dreamcatcher-content-4 unit 0 — 궤도 화염구. arm(BossPeriodicTriggerSystem)
                    // 은 ISystem 이라 SO 를 못 읽는다 → **탄 SO 의 선속도·피격 반경을 여기서
                    // 구워야 한다.** 셋(speed/hitThreshold/duration) 중 하나라도 빠지면 각각
                    // "안 도는 구슬 / 아무도 못 맞히는 구슬 / 즉시 사라지는 구슬"이 되고,
                    // 전부 조용한 실패라 눈으로 원인을 찾기 어렵다.
                    if (m.payload.projectile == null)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile without ProjectileData — skipped.");
                        continue;
                    }
                    if (m.payload.magnitude <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile non-positive magnitude — skipped.");
                        continue;
                    }
                    if (m.payload.duration <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile non-positive duration (즉시 사라지는 구슬) — skipped.");
                        continue;
                    }
                    if (m.payload.tileRange <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile tileRange<=0 (궤도 반경 0 — 각속도 산출 불가) — skipped.");
                        continue;
                    }
                    if (m.payload.projectile.speed <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile 탄 SO 의 speed<=0 (안 도는 구슬) — skipped.");
                        continue;
                    }
                    if (m.payload.projectile.hitThreshold <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile 탄 SO 의 hitThreshold<=0 (아무도 못 맞히는 구슬) — skipped.");
                        continue;
                    }
                    slot.projectileDataIndex = GetOrCreateProjectileDataIndex(m.payload.projectile);
                    slot.tileRange = m.payload.tileRange;              // 궤도 반경(타일)
                    slot.duration = m.payload.duration;                // 지속 초
                    slot.visualScale = m.payload.projectile.visualScale;
                    slot.speed = m.payload.projectile.speed;           // **선속도**(arm 이 ÷반경 → 각속도)
                    slot.hitThreshold = m.payload.projectile.hitThreshold; // 피격 반경
                    // 구슬 개수 → `period` 슬롯. PeriodicTimer 에게 그 필드는 AttackN 전용이라
                    // 비어 있다(이 struct 의 필드 재사용 규율 그대로). 0/1 = 1개.
                    slot.period = (ushort)math.clamp(
                        m.payload.orbitCount <= 0 ? 1 : m.payload.orbitCount, 1, 16);
                    // magnitude(스친 적 피해)는 위 slot 초기화에서 이미 복사됨.
                    // 재타격 쿨타임은 굽지 않는다 — 탄 SO 소유라 드레인이 dataIndex 로 해석해 채운다.

                    // 저작 경고(거절 아님): 주기가 지속보다 **짧으면** 화염구가 겹쳐 쌓인다.
                    // ⚠ 판정은 `<` 다 — 정확히 같으면(주기 5 = 지속 5) 앞 구슬이 사라지는
                    // 순간 다음이 나와 **끊김 없이 이어지는** 것이지 쌓이는 게 아니다.
                    // `<=` 로 두면 그 저작에서 매 부착마다 오경보가 뜬다(2026-08-16 실사용).
                    // 겹치기가 의도인 저작도 있을 수 있어 거절이 아니라 경고로만 남긴다
                    // (AllyMoveSpeedAura 의 반대 방향 경고와 동형).
                    if (m.trigger.kind == Wassup.Data.DcTriggerKind.PeriodicTimer
                        && m.trigger.periodSeconds < m.payload.duration)
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfOrbitProjectile periodSeconds({m.trigger.periodSeconds}) <= duration({m.payload.duration}) — 화염구가 겹쳐 쌓입니다.");
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.AreaSleep)
                {
                    // dreamcatcher-shield-break unit 1 — 실드 파열 시 N타일 내 가장 가까운 M명을
                    // L초 수면. 투사체 불요(연출은 기존 수면 표현). magnitude=M(적 수 cap, floor>=1)·
                    // tileRange=N(Chebyshev 반경)·duration=L(수면 초). 실행=DrainShieldBreakEvents(unit 2).
                    if (m.payload.magnitude < 1f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: AreaSleep magnitude<1 (no targets) — skipped.");
                        continue;
                    }
                    if (m.payload.tileRange < 1)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: AreaSleep tileRange<1 — skipped.");
                        continue;
                    }
                    if (m.payload.duration <= 0f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: AreaSleep non-positive duration — skipped.");
                        continue;
                    }
                    slot.tileRange = m.payload.tileRange;
                    slot.duration = m.payload.duration;
                    // magnitude(M) 은 slot 초기화에서 이미 복사됨.
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
                    // dreamcatcher-berserker unit 1 — 최대 중첩. >0 이면 재발동이 덮어쓰기가
                    // 아니라 누적이 된다(상한 = 1회분 × 중첩). 저작 자리로 tileRange 를 쓰는 것은
                    // ApplyStackToTarget 이 같은 칸을 최대 중첩으로 쓰는 선례와 같고, 시트
                    // DTO 에도 이미 있어 저작 경로가 공짜로 열린다. 0 = 기존 덮어쓰기.
                    //
                    // 배율 <=1 은 누적이 성립하지 않는다 — FromMultiplier 가 그 값을 **곱셈
                    // 버킷**으로 보내는데 곱셈 값을 더하면 의미가 뒤집힌다(0.9 + 0.9 = 1.8 =
                    // 오히려 강화). 조용히 이상하게 도느니 loud 로 세운다.
                    if (m.payload.tileRange > 0 && buffMult <= 1f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: SelfStatBuff 최대 중첩({m.payload.tileRange})은 배율 > 1 에서만 성립한다 (현재 배율 {buffMult}) — skipped.");
                        continue;
                    }
                    slot.buffStat = buffStat;
                    slot.magnitude = buffMult;
                    slot.duration = m.payload.duration;
                    slot.tileRange = math.max(0, m.payload.tileRange);
                    // stackId 는 StatModifier 네임스페이스의 단일 할당자에서(squad 이펙트와
                    // 공유) — instanceId 잘라쓰기(네임스페이스 오염) 대신. 슬롯당 고정 →
                    // 매 킬/틱 refresh.
                    slot.statBuffStackId = _dcStackCounter++;
                    // HealthThreshold 상태 bake 는 payload-불문 공통 블록(아래)으로 호이스팅됨
                    // (dreamcatcher-content-3 unit 4). OnKill: 추가 슬롯 상태 없음 — 매 킬
                    // DamageApplicationSystem 에서 발동(kill-and-threshold unit 2).
                }
                else if (m.payload.kind == Wassup.Data.DcPayloadKind.HeavyStrike)
                {
                    // dreamcatcher-heavy-strike unit 1 — 응축된 일격. AttackN 전용 강공:
                    // N회째 공격의 출력 데미지를 magnitude 배(2.0=×2). 다른 트리거로는
                    // 무의미 → AttackN 강제. 배율<=1 은 강공이 아니므로(1=평타, <1=약화)
                    // 거절. host 의 Damage output 요구는 위 단일 게이트가 처리한다
                    // (NeedsDamageOutput). slot.magnitude 는 이미 배율(위 generic bake).
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
                }

                // projectile-emission-pattern unit 3 — 발사 명세는 **카드 경로 미배선**이다.
                // 패턴 자료(PatternSlot 버퍼 + template 조립)는 보스 스폰 bake 에만 있고,
                // 여기서 통과시키면 슬롯이 patternIndex=0(struct default, 유효 index 처럼
                // 보이는 값)으로 붙어 아무 일도 안 하는 카드가 "부착됨"으로 집계된다 —
                // 설명 텍스트도 공란. 이 spec 이 인용해 온 "조용한 no-op 금지"(dc-trigger
                // 선례)를 지켜 loud 거절한다. 개통하려면 defender 에도 PatternSlot/
                // EmitterInstance 부착 + BuildPatternTemplate(hostIsEnemy:false) 이 필요하다.
                // dreamcatcher-content-5 unit 5 — 카드 경로 **개통**. 여태 여기서 loud 거절했고
                // (슬롯이 patternIndex=0 으로 붙어 아무 일도 안 하는 카드가 «부착됨» 이 되는 것을
                // 막으려고) 그 사이 defender 템플릿·적용성·발동 arm 은 전부 완성됐다 — 남은 것이
                // 이 bake 하나였다.
                if (m.payload.kind == Wassup.Data.DcPayloadKind.EmitProjectilePattern)
                {
                    var pattern = m.payload.pattern;
                    if (pattern == null || pattern.barrel == null)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: EmitProjectilePattern needs a pattern with a barrel — skipped.");
                        continue;
                    }
                    // ⚠ **트리거 축 가드** — 이 payload 의 발동 arm 은 BossPeriodicTriggerSystem
                    // 하나뿐이고 거기서 발화하는 트리거는 PeriodicTimer 와 OnPlace 뿐이다
                    // (OnPlace 는 카드 경로에서 아래가 따로 거절한다). 그러므로 AttackN·OnKill·
                    // HealthThreshold 로 저작하면 **슬롯은 붙고 발사는 영원히 없다** — 이번
                    // 변경이 없애려던 「조용한 no-op」 그 자체다. 형제 payload 둘(SpawnHazard →
                    // OnKill · SelfOrbitProjectile → PeriodicTimer)이 같은 가드를 갖는다.
                    //
                    // 이 가드는 **아래 패턴 슬롯 append 보다 앞에** 있어야 한다 — 뒤에 두면
                    // 거절된 mechanic 이 주인 없는 PatternSlot 을 남긴다(ECS 리뷰 M2).
                    if (m.trigger.kind != Wassup.Data.DcTriggerKind.PeriodicTimer)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: EmitProjectilePattern 은 PeriodicTimer 만 배선돼 있다 (현재 trigger={m.trigger.kind}) — skipped.");
                        continue;
                    }
                    // ⚠ **직선탄 유닛의 기본 공격이 0번 패턴 슬롯을 읽는다**(AttackSystem 의
                    // 다연발 경로). 그 유닛이 자기 패턴을 안 가진 «패턴 없는 방향 단발» 저작이면
                    // 카드 슬롯이 index 0 을 차지해 **기본 공격이 카드 패턴을 쏘게 된다.**
                    // 카드가 유닛의 공격을 바꿔치는 것은 어떤 카드의 사양도 아니므로 loud 거절한다.
                    // (유닛이 자기 패턴을 이미 가졌으면 카드는 1번 이후라 안전하다.)
                    bool hostFiresDirectional =
                        _em.HasComponent<ProjectileRef>(defender)
                        && _em.GetComponentData<ProjectileRef>(defender).movement == MovementKind.DirectionalLinear;
                    bool hostHasOwnPattern =
                        _em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender)
                        && _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender).Length > 0;
                    if (hostFiresDirectional && !hostHasOwnPattern)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: host 가 «패턴 없는 방향 단발» 이라 이 카드의 패턴이 0번 슬롯이 되어 **기본 공격을 바꿔친다** — skipped.");
                        continue;
                    }
                    // on-place-shuttle-shotgun unit 2 — 유닛 경로와 **같은 가드**(거기 주석 참조).
                    // 방향 패턴의 사거리는 payload 저작값이라 0 이면 사거리 0 인 탄이 되어 발사해도
                    // 아무 일도 안 일어난다. 한쪽 bake 에만 걸면 카드 저작이 그 구멍으로 샌다.
                    if (Wassup.Battle.Combat.Projectile.Emission.MovementBinding.Of(
                            ResolveProjectileAxes(pattern.barrel.flightMode).movement)
                            == Wassup.Battle.Combat.Projectile.Emission.BindingClass.Direction
                        && m.payload.tileRange <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: 방향 패턴인데 payload tileRange 가 0 이다 — 사거리 0 인 탄이라 발사해도 아무 일도 안 일어난다 — skipped. 사거리를 지정하라.");
                        continue;
                    }
                    if (!TryBuildPatternSlot(pattern, defender, hostIsEnemy: false,
                                             $"Card '{card.id}' mechanic {i}", out var cardPatternSlot))
                        continue;
                    // 살아 있는 엔티티라 **add-or-get** 이 필요하다(유닛 경로는 스폰 시점이라
                    // 무조건 AddBuffer 해도 됐다). 기존 슬롯을 보존해야 같은 유닛에 두 장이
                    // 붙어도 서로의 발사 카운터를 밟지 않는다.
                    // ⚠ 두 버퍼 추가는 **DcTriggerSlot 핸들을 잡기 전에** 끝난다 — 구조 변경이
                    // 기존 버퍼 핸들을 무효화하기 때문(유닛 경로 주석과 같은 경고).
                    if (!_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender))
                        _em.AddBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender);
                    if (!_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(defender))
                        _em.AddBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(defender);
                    var cardPatternSlots =
                        _em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender);
                    cardPatternSlots.Add(cardPatternSlot);
                    slot.patternIndex = cardPatternSlots.Length - 1;
                }

                // on-place-skill-rework unit 0 — 배치 트리거는 **카드가 쓸 수 없다.** 카드는 전투
                // 중 이미 판에 있는 유닛에 붙으므로, 붙는 순간 배치 사건은 이미 지나갔다. 슬롯은
                // 생기는데 `JustDeployed` 가 다시는 안 붙어 **붙는데 영영 안 터지는** 카드가 된다
                // (위 EmitProjectilePattern 거절과 같은 «조용한 no-op 금지» 선례).
                // 배치 스킬은 유닛 자기 규칙(UnitSkillAbility)으로만 선언한다.
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.OnPlace)
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' mechanic {i}: OnPlace 는 카드 경로에 쓸 수 없다(부착 시점엔 배치 사건이 이미 지났다) — skipped.");
                    continue;
                }

                // dreamcatcher-content-3 unit 4 — HealthThreshold 상태 bake 를 payload-불문
                // 공통 블록으로 호이스팅. 기존엔 SelfStatBuff 분기 안에만 있어 진동갑주
                // (HealthThreshold×SelfTileAoe)가 fraction 0(inert)으로 잠들었다. 가드·
                // 시맨틱스(스폰 maxHp 스냅샷·경계 간격·래치 k=1)는 last_stand 시절 그대로.
                if (m.trigger.kind == Wassup.Data.DcTriggerKind.HealthThreshold)
                {
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
                // attack-decoupling unit 1 — host 종속 판정 단일 게이트(메커닉과 동일).
                // ProjectileBounce 의 옛 게이트는 `ProjectileRef 유무`였는데, 그건
                // 폭탄맨(GrenadeToCell)·머신거너(DirectionalLinear)·아틸러리(Ballistic)를
                // 전부 통과시켜 "붙는데 무효"를 만들었다 — 이제 route==Homing 을 요구한다.
                var modReason = Wassup.Core.DcApplicability.EvaluateAttackMod(m.kind, hostProfile);
                if (modReason != Wassup.Core.DcRejectReason.None)
                {
                    Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: {m.kind} 는 이 host 에서 발동하지 않는다 ({modReason}, archetype={hostProfile.archetype}, route={hostProfile.route}) — skipped.");
                    continue;
                }
                if (m.kind == Wassup.Data.DcAttackModKind.ProjectileBounce)
                {
                    if (m.count <= 0)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: ProjectileBounce non-positive count — skipped.");
                        continue;
                    }
                }
                else if (m.kind == Wassup.Data.DcAttackModKind.DamageVsSleeping)
                {
                    // dreamcatcher-content-4 unit 0 — 수면 특효. 배율<=1 은 "특효"가 아니라
                    // 저작 실수일 가능성이 압도적이다(1=평타, <1=잠든 적에게 오히려 약화).
                    // HeavyStrike 가 같은 이유로 magnitude<=1 을 거절하는 선례.
                    if (m.damageMul <= 1f)
                    {
                        Debug.LogWarning($"[BattleBridge] Card '{card.id}' attackMod {i}: DamageVsSleeping damageMul<=1 (특효가 아님) — skipped.");
                        continue;
                    }
                }
                else if (m.kind == Wassup.Data.DcAttackModKind.FrontmostTarget)
                {
                    // 끝을 보는 눈 — `count`/`tileRange` 는 무시(기본 사거리 사용).
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
            // unit 4a — **여기서 끝낸다.** 부착이 동기 트랜잭션이라, 반환 전에 실행이
            // 끝나 있어야 호출자가 보는 상태와 반환값이 같은 시점을 가리킨다.
            // ⚠ `attached == 0` 거절 **뒤**다 — 거절된 카드는 아무것도 안 쓴다는 계약이
            // 이 순서에 있다(부분 적용 금지). 거절 경로에 실린 이벤트는 없다(위 분기가
            // `attached++` 와 같은 자리에서만 싣는다).
            if (immediateFired) RunImmediateSkills();
            return auraHandle;
        }

        // dreamcatcher-attach-lockon — 부착 조준 유효성 preflight(읽기 전용). "이 카드가
        // 이 유닛에 기여하는가"(= 위 apply 가 -1 이 아닌가)를 유닛-종속 게이트로 판정 →
        // base-ring/리티클/화살표 색이 커밋과 일치(통통구슬↔가디언처럼). 판정 로직은 순수
        // DreamcatcherAttachEval(EditMode 테스트)이고, 여기선 ECS 능력만 조회한다.
        // ★ 동기화: 위 apply 의 유닛-종속 skip(ProjectileBounce→ProjectileRef,
        //   FrontmostTarget·HeavyStrike→HasPositiveDamageOutput, 이중 LethalTimer/DreamCocoon)과
        //   같이 유지 — 새 유닛-게이트 kind 추가 시 eval 갱신.
        public bool WouldDreamcatcherCardApply(Entity defender, Wassup.Data.DreamcatcherCard card)
        {
            if (card == null) return false;
            bool defenderHosted = card.type == Wassup.Data.CardType.Unit
                || card.type == Wassup.Data.CardType.Squad;
            // Active 등은 defender 부착 경로 밖. profile 은 읽히지 않는다.
            if (!defenderHosted)
                return Wassup.Core.DreamcatcherAttachEval.WouldApply(card, default(Wassup.Core.DcHostProfile));
            if (!HasLiveEntityManager() || !_em.Exists(defender) || !_em.HasComponent<DefenderUnitTag>(defender))
                return false;
            // dreamcatcher-attach-requirement units 1·10 — Unit/Squad 모두 부착 제한을
            // 먼저 본다. Squad 는 host 능력 profile 이 필요 없는 축 버프라 default 로 충분하다.
            if (!PassesAttachRequirement(defender, card)) return false;
            if (card.type == Wassup.Data.CardType.Squad)
                return Wassup.Core.DreamcatcherAttachEval.WouldApply(card, default(Wassup.Core.DcHostProfile));
            return Wassup.Core.DreamcatcherAttachEval.WouldApply(card, BuildHostProfile(defender));
        }

        // subconscious-curse-expansion unit 2 (살찌운 제물) — 적 표식. 반환 규약은
        // ApplyDreamcatcherCardToUnit 과 동일: <0 실패(무차감) / 0 성공·회수불필요
        // (표식은 엔티티 수명과 함께 소멸 — 처치/유출이 곧 회수 트리거, revoke 없음).
        // 스탯 적용은 기존 StatModifier 채널(TTL=DcDuration 영구, revoke 레지스트리
        // 비등록 — 소멸은 엔티티 수명). empower aura 는 defender 한정 쿼리라 적엔 안 켜짐.
        public int ApplyBountyMark(Entity enemy, Wassup.Data.DreamcatcherCard card)
        {
            if (card == null || card.mechanics == null) return -1;
            // BountyMark 메커닉 추출. magnitude=각성 배율(>1 필수), tileRange=받는 피해
            // 감소 %(0~99 — ApplyStackToTarget 의 tileRange 재사용 선례).
            int mi = -1;
            for (int i = 0; i < card.mechanics.Length; i++)
                if (card.mechanics[i].payload.kind == Wassup.Data.DcPayloadKind.BountyMark) { mi = i; break; }
            if (mi < 0) return -1;
            var payload = card.mechanics[mi].payload;
            if (payload.magnitude <= 1f)
            {
                Debug.LogWarning($"[BattleBridge] ApplyBountyMark('{card.id}'): magnitude<=1 (현상금 없음) — not marked.");
                return -1;
            }
            if (payload.tileRange < 0 || payload.tileRange >= 100)
            {
                Debug.LogWarning($"[BattleBridge] ApplyBountyMark('{card.id}'): tileRange(피해감소%) [0,100) 밖 — not marked.");
                return -1;
            }
            if (!HasLiveEntityManager() || !_em.Exists(enemy))
            {
                Debug.LogWarning($"[BattleBridge] ApplyBountyMark('{card.id}'): ECS not ready or enemy gone — not marked.");
                return -1;
            }
            // 적 판별 — 표식은 악몽 전용(방어유닛/해저드 부착 방지).
            if (!_em.HasComponent<Wassup.Battle.Units.AttackUnitTag>(enemy))
            {
                Debug.LogWarning($"[BattleBridge] ApplyBountyMark('{card.id}'): target is not an enemy — not marked.");
                return -1;
            }
            // 이중 표식 사전검증 — AwakeningReward 이중 배율 방지(LethalTimer preflight 선례).
            if (_bountyMarked.Contains(enemy))
            {
                Debug.LogWarning($"[BattleBridge] ApplyBountyMark('{card.id}'): enemy already marked — not marked.");
                return -1;
            }

            // unit 4c — 실행은 concrete 가 한다. 저작(% → 배율)과 스택 슬롯 발급은
            // 여기가 한다(저작 인코딩과 브리지 소유 카운터).
            // ⚠ **부착 seam 이라 이 호출 안에서 끝난다.** 표식은 그 적이 죽을 때 소비되고,
            // 처치 이벤트가 enqueue 시점에 보상값을 복사하므로 늦으면 안 된다.
            int bountySkillId = SkillIdForCardPayload(
                card.mechanics[mi].trigger.kind, Wassup.Data.DcPayloadKind.BountyMark);
            if (bountySkillId != Wassup.Skills.SkillRegistry.LegacyArmId
                && _skillFiredQueue.IsCreated)
            {
                _skillFiredQueue.Enqueue(new Wassup.Battle.Skills.SkillFiredEvent
                {
                    Seam = Wassup.Battle.Skills.SkillSeam.Immediate,
                    Caster = Entity.Null,          // 플레이어가 건다 — host 가 없다
                    SkillId = bountySkillId,
                    SlotIndex = mi,
                    Target = enemy,
                    Magnitude = payload.magnitude,                    // 각성 배율
                    HitThreshold = payload.tileRange > 0
                        ? 1f - payload.tileRange / 100f : 0f,          // 받는 피해 배율
                    Duration = DcDuration,
                    StackId = _dcStackCounter++,
                });
                RunImmediateSkills();
            }
            _bountyMarked.Add(enemy);
            return 0;
        }

        // dreamcatcher-attach-requirement unit 1 — 부착 제한(정적 술어) 게이트. UI 판정
        // (WouldDreamcatcherCardApply)과 커밋 preflight(ApplyDreamcatcherCardToUnit)가
        // 이 하나를 공유하므로 리티클 색과 커밋 결과가 어긋나지 않는다.
        //
        // attachType==None 이면 host data 조회조차 하지 않는다 → 무제한 카드 경로가
        // 완전히 무변화. host 조회 실패는 fail-closed: 사망 teardown 창에서 등록부 제거와
        // 엔티티 파괴의 수명이 달라 제한 카드만 먼저 거절될 수 있다(무차감이라 실피해
        // 없음 — spec README '의도된 동작' 계약. 버그로 오인 금지).
        private bool PassesAttachRequirement(Entity defender, Wassup.Data.DreamcatcherCard card)
        {
            if (card == null) return false;
            if (card.attachType == Wassup.Data.DcAttachType.None) return true;
            // 조회는 기존 FindDefenderData(BattleBridge.cs) 재사용 — 같은 _defenderByTile
            // 선형 스캔을 중복 구현하지 않는다(review M2).
            var data = FindDefenderData(defender);
            if (data == null) return false;
            return Wassup.Core.DreamcatcherAttachEval.MeetsAttachRequirement(card, data.role, data.id);
        }

        // units 1·10 — 실제 커밋 거절 로그. UI preflight 는 호버마다 호출되므로 로그를
        // 남기지 않고, Unit/Squad 커밋만 이 helper 를 호출해 같은 원인을 같은 문구로 알린다.
        private void LogAttachRequirementReject(Entity defender, Wassup.Data.DreamcatcherCard card)
        {
            if (Wassup.Core.DreamcatcherAttachEval.HasInvalidAttachRequirement(card))
            {
                // 데이터 실수 — 이 카드는 어떤 유닛에도 붙지 않는다(손패 슬롯 점유).
                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCard('{card.id}'): 부착 제한 설정이 무효(attachType={card.attachType}, attachValue='{card.attachValue}') — 어떤 유닛에도 부착되지 않는다. 시트 값을 확인할 것.");
                return;
            }

            string want = card.attachValue;
            var hostData = FindDefenderData(defender);
            // review M4 — 거절 사유 3종을 문구로 구분한다. 조회 실패를 '불일치'로
            // 적으면 사망 teardown 창(의도된 동작)이 데이터 문제처럼 읽힌다.
            if (hostData == null)
            {
                Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCard('{card.id}'): host 등록부 조회 실패 — 요구 {card.attachType}='{want}' 를 판정할 수 없어 fail-closed 거절(무차감). 사망 teardown 창의 의도된 동작 (spec README '의도된 동작').");
                return;
            }

            Debug.LogWarning($"[BattleBridge] ApplyDreamcatcherCard('{card.id}'): 부착 제한 불일치 — 요구 {card.attachType}='{want}', host role={hostData.role} id='{hostData.id}' — card not attached.");
        }

        // dreamcatcher-content-2 unit 1 — does this defender emit at least one positive
        // Damage-kind attack output (vs heal-only / output-less)? Gates FrontmostTarget
        // attach so 끝을 보는 눈 can't be spent inertly on a support unit.
        // attack-decoupling unit 1 — host 종속 판정의 입력. ECS 조회를 여기서 한 번에
        // 하고, 판정 자체는 순수 계층(DcApplicability)이 한다. UI preflight 와 커밋
        // bake 가 **같은 profile → 같은 함수**를 쓰므로 리티클 색과 커밋 결과가 어긋날
        // 수 없다(예전엔 두 미러를 손으로 맞췄다).
        private Wassup.Core.DcHostProfile BuildHostProfile(Entity defender)
        {
            var profile = new Wassup.Core.DcHostProfile
            {
                archetype = Wassup.Core.DcHostArchetype.Standard,
                route = Wassup.Core.DcProjectileRoute.None,
            };
            if (!HasLiveEntityManager() || !_em.Exists(defender)) return profile;

            // 판정 순서 고정: 능력 상태 컴포넌트를 먼저 걸러 낸다. HazardCast 는
            // attackRange 0 이라 RESOLVE 에 못 가는 부류.
            // (bomb-thrower-defender unit 9 로 폭탄맨은 facing 을 쓰지 않게 됐지만,
            //  BombThrow 를 먼저 보는 이 순서는 유지한다 — 어느 쪽이든 결과가 같다.)
            //
            // FacingVolley 는 `PatternSlot` **단독**으로 판별한다 — DeployedFacing 은
            // 조준 '완료 여부'라는 일시 상태라, 그걸 요구하면 같은 유닛이 조준 전후로
            // 다르게 판정된다(Play 실측: 조준 전 머신거너가 Standard 로 나왔다). 유닛
            // 정체성은 방향 pattern 보유가 나타낸다. SO 측 미러(DcApplicabilityMatrixTests
            // 의 ProfileOf)도 `DirectionalVolleyAbility` 보유로 같은 분류를 만든다.
            if (_em.HasComponent<BombLauncherState>(defender))
                profile.archetype = Wassup.Core.DcHostArchetype.BombThrow;
            else if (_em.HasComponent<Wassup.Battle.Effects.HazardCastState>(defender))
                profile.archetype = Wassup.Core.DcHostArchetype.HazardCast;
            else if (_em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender))
                profile.archetype = Wassup.Core.DcHostArchetype.FacingVolley;

            // route = host 가 **실제로** 타는 발사 경로. 폭탄맨은 ProjectileRef 가
            // 무엇을 선언하든 GrenadeToCell 하드코딩이다(spec 계약 6) — SO 의
            // flightMode 를 믿으면 Projectile_Bomb(=Homing) 때문에 오판한다.
            if (profile.archetype == Wassup.Core.DcHostArchetype.BombThrow)
                profile.route = Wassup.Core.DcProjectileRoute.Grenade;
            else if (_em.HasComponent<ProjectileRef>(defender))
            {
                switch (_em.GetComponentData<ProjectileRef>(defender).movement)
                {
                    case MovementKind.HomingToEntity: profile.route = Wassup.Core.DcProjectileRoute.Homing; break;
                    case MovementKind.BallisticArcToPoint:
                    case MovementKind.SkyFall: profile.route = Wassup.Core.DcProjectileRoute.Ballistic; break;
                    case MovementKind.DirectionalLinear: profile.route = Wassup.Core.DcProjectileRoute.Directional; break;
                    case MovementKind.GrenadeToCell: profile.route = Wassup.Core.DcProjectileRoute.Grenade; break;
                }
            }

            profile.targetsEnemies = TargetsEnemies(defender);
            profile.hasDamageOutput = HasPositiveDamageOutput(defender);
            profile.hasLethalTimer = _em.HasComponent<LethalTimer>(defender);
            profile.hasDreamCocoon = _em.HasComponent<Wassup.Battle.Effects.DreamCocoon>(defender);
            return profile;
        }

        // 이 유닛의 공격 대상이 적인가. mask 는 CreateDefenderEntity 가 targetAllies 로
        // 굽는다(Defender ↔ Enemy). 마스크 부재/0(공격 안 하는 유닛)도 false.
        private bool TargetsEnemies(Entity defender)
        {
            if (!_em.HasComponent<AttackState>(defender)) return false;
            return (_em.GetComponentData<AttackState>(defender).targetMask & Factions.AnyEnemy) != 0;
        }

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
                case Wassup.Data.DcCcKind.Sleep: return Wassup.Battle.Effects.CcKind.Sleep;
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
