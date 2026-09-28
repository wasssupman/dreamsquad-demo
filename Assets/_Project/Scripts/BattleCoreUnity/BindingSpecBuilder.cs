using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Trigger;
using Wassup.Data;
using Wassup.Data.Authoring;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.BattleCoreUnity
{
    /// <summary>
    /// 새 저작 형식의 소유자 한 명(skill-data-table unit 4). **소유자 종류에서 파생하는 사실만** 싣는다(계약 3 — 능력 선언 칸 없음).
    /// </summary>
    internal struct RuleOwner
    {
        /// <summary>규칙 줄의 출처 꼬리표 — 카드 = `Card` · 방어유닛 · 적 = `UnitAuthored`(옛 두 빌더와 같은 값).</summary>
        public BindingOrigin Origin;
        /// <summary>진단 라벨 머리(카드 = `카드 '{id}'` · 유닛 · 적 = 에셋 이름 — 옛 굽기 스냅샷 라벨과 같은 모양).</summary>
        public string Label;
        public bool IsCard;
        /// <summary>유닛 · 적 쪽의 진영(카드는 `CardHosts` 로 숙주 종류마다 본다).</summary>
        public bool IsEnemy;
        /// <summary>어그로를 드는 몸인가(방어유닛 `aggroCapacity > 0` · 적 = 아니다 — 옛 빌더와 같다).</summary>
        public bool HostIsGuardian;
        /// <summary>카드가 붙을 수 있는 숙주 종류(U5).</summary>
        public HostKinds CardHosts;
        /// <summary>카드가 적을 겨냥하나(적 표식 카드) — 그 카드는 표식 효과만 쓴다.</summary>
        public bool CardTargetsEnemies;
        /// <summary>카드 축(배치 오라의 주어 필터).</summary>
        public CardTargetAxis Axis;
    }

    // skill-data-table unit 4 — **새 저작 형식**(효과 에셋 참조 소유 줄 `BindingSpec`)을 굽는 한 경로. 카드 · 방어유닛 · 적이 같은 함수를
    // 지나고, 소유자마다 다른 것은 `RuleOwner` 의 파생 사실뿐이다.
    //
    // 이전 과도기(옛 칸이 남아 있는 동안)의 모양: 소유 줄 하나를 **옛 메커닉 값으로 비춰**(`Legacy` — `EffectSlots.ToLegacy`) 두 빌더가 이미
    // 공유하는 잎 함수(조합 `ComboOf` · 트리거 가드 `TriggerValuesValid` · 강공 · 종류별 값 가드와 표 참조 `BindPayload` · 연출)를 그대로
    // 부른다 — 검증 사본이 갈리지 않게. 새 형식에만 있는 것(CC · 스택 새 번호 · U10 피해 · 실드 대상 셋 · 발동 상한 · 수치 방식 · 효과 id)은
    // 여기서 직접 싣는다. 옛 몸통(카드 `BakeMechanic` · 유닛 `Bake`)은 옛 칸 전용으로 남고 4-정리에서 이 함수로 합쳐진다.
    //
    // ⚠ 라이브 굽기 동치는 이전 dry-run(`EffectSlots.FromLegacy` 와 같은 표) + 굽기 스냅샷이 증명한다. 옛 몸통과 **순서가 다른 한 곳**:
    //    카드 줄도 라우팅 확인을 표 참조(`BindPayload` — 탄 · 명세 표 등록) **앞**에 한다(유닛 몸통 순서). 건너뛸 줄이 표에 탄을 등록하지 않는다.
    internal static class BindingSpecBuilder
    {
        /// <summary>소유 줄 전부를 싣는다(카드 부착 줄 · 유닛 · 적). 액티브 시전 줄은 `CardDefinitionBuilder` 가 따로 굽는다.</summary>
        internal static void Bake(BindingSpec[] bindings, in RuleOwner o, List<ProjectileData> projectiles,
                                  List<ProjectilePatternData> patterns, HazardSO[] hazards, List<BindingDef> rows,
                                  List<EffectDef> effects, List<int> mine, List<AttackModDef> mods,
                                  ref bool declaresRetireRecall, MatchViewAssets view)
        {
            bool aura = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                var s = bindings[i];
                string label = $"{o.Label} mechanic {i}";
                var e = s.effect;
                if (e == null) { Warn($"{label}: 효과 에셋이 비었다 — 건너뛴다."); continue; }
                if (e.deprecated) { Error($"{label}: 폐기된 효과 '{e.id}' 를 참조한다 — 건너뛴다."); continue; }
                var m = Legacy(in s);
                if (!BindingDefinitionBuilder.KnownKinds(in m, label)) continue;
                var v = e.values;
                var trigger = s.trigger.kind;
                var kind = v.kind;

                if (kind == EffectKind.None) { Warn($"{label}: None 종류 — 건너뛴다."); continue; }
                if (kind == EffectKind.SplitOnDeath) { Error($"{label}: 분열은 소유 줄이 아니라 적 고유 값(splitUnit · splitCount)이다 — 건너뛴다."); continue; }
                if (trigger == TriggerKind.Cast || SkillRouting.IsActiveCast(kind))
                { Warn($"{label}: 시전 × 액티브 효과는 액티브 카드의 한 줄만 — 건너뛴다."); continue; }
                if (o.IsCard && o.CardTargetsEnemies && kind != EffectKind.BountyMark)
                { Warn($"{label}: 적 표식 카드는 표식 효과만 쓴다 — {kind} 는 건너뛴다."); continue; }

                // ── 트리거 없음 = 부착되는 순간(카드만 — 유닛 · 적은 조합 검증이 「영영 안 터짐」으로 거절한다) ──
                if (trigger == TriggerKind.None && o.IsCard)
                {
                    AttachInstant(in m, in v, kind, label, e.id, o.Axis, ref aura, rows, effects, mine, view);
                    continue;
                }
                // 손패 동작(인수인계) — 규칙이 아니라 퇴근 회수 선언이다. 카드 · 퇴근 · 게이트 없음.
                if (kind == EffectKind.RecallAttachedToFront)
                {
                    if (!o.IsCard) { Warn($"{label}: 인수인계는 카드 손패 동작 — 건너뛴다."); continue; }
                    if (trigger != TriggerKind.OnRetire) { Warn($"{label}: 인수인계는 OnRetire 에만 배선돼 있다(현재 {trigger}) — 건너뛴다."); continue; }
                    if (s.trigger.gate != GateKind.None) { Warn($"{label}: 인수인계에는 게이트가 배선돼 있지 않다 — 건너뛴다."); continue; }
                    declaresRetireRecall = true;
                    continue;
                }
                if (!CombosAllow(in m, trigger, kind, v.magnitudeMode, in o, label)) continue;
                var gate = s.trigger.gate;
                var gateSubject = s.trigger.gateSubject;
                if (!BindingDefinitionBuilder.TriggerValuesValid(in m, trigger, gate, gateSubject, label)) continue;
                if (kind == EffectKind.HeavyStrike)
                {
                    if (BindingDefinitionBuilder.TryHeavyStrike(in m, trigger, gate, label, out var heavy)) mods.Add(heavy);
                    continue;
                }
                if (kind == EffectKind.AreaBarrage) { Warn($"{label}: AreaBarrage 는 EmitProjectilePattern 으로 이관됐다 — 건너뛴다."); continue; }
                var skill = SkillRouting.Resolve(trigger, kind);
                if (skill == null)
                {
                    Warn(SkillRouting.IsSkill(kind)
                        ? $"{label}: '{trigger} × {kind}' 조합에 라우팅이 없다 — 건너뛴다."
                        : $"{label}: '{kind}' 는 이 레이어의 규칙이 아니다 — 건너뛴다.");
                    continue;
                }
                if (!ValuesValid(in v, kind, label)) continue;

                var b = BindingDef.Default();
                var fx = EffectDef.Default();
                b.Label = label;
                b.Trigger = trigger;
                b.Skill = skill;
                b.Origin = o.Origin;
                b.Period = Mathf.Clamp(s.trigger.period, 0, ushort.MaxValue);
                b.PeriodSeconds = s.trigger.periodSeconds;
                b.Fraction = s.trigger.fraction;
                b.Gate = gate;
                b.GateSubject = gateSubject;
                b.GateValue = s.trigger.gateValue;
                fx.Kind = kind;
                fx.Magnitude = m.payload.magnitude;
                fx.TileRange = Mathf.Max(0, m.payload.tileRange);
                fx.Duration = Mathf.Max(0f, m.payload.duration);
                // CC · 스택은 효과 값이 **스킬 번호**를 든다(이전 스크립트가 옛 번호를 옮겼다) — 번역 없이 싣는다.
                fx.CcKind = (int)v.ccKind;
                fx.StackKind = (int)v.stackKind;
                fx.StatKind = (int)(BindingDefinitionBuilder.TryToSkillStat(v.buffStat, out var stat) ? stat : SkillStatKind.DamageMul);
                if (EffectSlots.IsLeap(kind))
                {
                    fx.Damage = Mathf.Max(0f, v.damage);
                    fx.SlamTileRange = Mathf.Max(0, v.radiusTiles);
                }
                BindingDefinitionBuilder.BakeCone(ref fx, v.coneHalfDeg);
                BindingDefinitionBuilder.ApplyAuthoredAxes(ref b, ref fx, in m);
                fx.MagnitudeMode = v.magnitudeMode;
                if (v.magnitudeMode != MagnitudeMode.Flat) { fx.BasisStat = v.basisStat; fx.Ratio = v.ratio; }
                if (kind == EffectKind.GrantShield)
                {
                    fx.ShieldFilter = (int)BindingDefinitionBuilder.ToSkillShieldFilter(v.shieldFilter);
                    fx.ShieldTargetCount = Mathf.Max(0, v.count);
                    fx.ShieldIncludesSelf = v.includesSelf;
                }

                if (!BindingDefinitionBuilder.BindPayload(ref b, ref fx, in m, label, projectiles, patterns, hazards)) continue;
                // U10 — 발사 명세 · 장판의 피해는 **효과 줄**이 든다(옛 굽기는 명세 · 길막 · 장판 SO 에서 읽었다 — 이전이 그 값을 옮겼다).
                if (kind == EffectKind.EmitProjectilePattern) fx.Damage = v.damage;
                else if (kind == EffectKind.SpawnHazard)
                {
                    if (v.damage > 0f && !HasDot(e.hazard))
                    { Error($"{label}: 피해({v.damage})가 있는데 장판 '{e.hazard.name}' 에 DoT 하위 효과가 없다 — 건너뛴다."); continue; }
                    fx.Damage = v.damage;
                }
                // U15(연출은 효과 기준 · 2026-09-29 결정) — 자리 폭발의 착탄 연출 배율 = 그 효과의 탄 배율, **누가 들든 같다**.
                // 옛 굽기는 카드만 실었다(유닛 · 적 = 0 → 뷰가 1 로 읽음). 라이브 유닛·적 자리 폭발 탄은 배율 1 이라 화면 무변 · 해시만 바뀐다.
                if (kind == EffectKind.SelfTileAoe) fx.VisualScale = e.projectile.visualScale;
                if (kind == EffectKind.SelfStatBuff)
                {
                    // 효과 값은 % 다(소유자 무관) → 배율.
                    if (!CardDefinitionBuilder.MapBuff(v.buffStat, v.percent, out var buff, out float mul)) { Warn($"{label}: SelfStatBuff 스탯 {v.buffStat} 을 옮길 수 없다 — 건너뛴다."); continue; }
                    if (v.stackCap > 0 && mul <= 1f) { Warn($"{label}: SelfStatBuff 최대 중첩은 배율 > 1 에서만 — 건너뛴다."); continue; }
                    fx.StatKind = (int)buff;
                    fx.Magnitude = mul;
                }
                // 발동 상한 = 소유 줄의 칸(옛 빌더가 종류로 박던 궁극기 1 을 옮겼다).
                b.FireCap = Mathf.Max(0, s.fireCap);
                if (kind == EffectKind.UltimateLeap && b.FireCap != 1)
                    Warn($"{label}: UltimateLeap 의 fireCap = {b.FireCap} — 「생존당 1회」는 1 이다(경계 트리거가 다회 발동한다).");

                BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                BindingDefinitionBuilder.AddRow(rows, effects, mine, b, fx, e.id);
            }
        }

        /// <summary>
        /// 소유 줄 하나를 옛 메커닉 값으로 비춘다(잎 검증 함수들이 아직 옛 칸을 읽는다 — 4-정리까지). CC · 스택은 싣지 않는다 — 잎 함수가
        /// 안 읽고, 굽기는 효과 값의 새 번호를 직접 쓴다.
        /// </summary>
        internal static DcMechanic Legacy(in BindingSpec s)
        {
            var e = s.effect;
            var p = EffectSlots.ToLegacy(in e.values);
            return new DcMechanic
            {
                trigger = s.trigger,
                payload = new DcPayloadSpec
                {
                    kind = p.Kind,
                    magnitude = p.Magnitude,
                    tileRange = p.TileRange,
                    duration = p.Duration,
                    projectile = e.projectile,
                    pattern = e.pattern,
                    hazard = e.hazard,
                    auraPrefab = e.auraPrefab,
                    auraScale = e.auraScale,
                    stackModifier = e.stackModifier,
                    buffStat = p.BuffStat,
                    slamDamage = p.SlamDamage,
                    slamTileRange = p.SlamTileRange,
                    tickIntervalSec = p.TickIntervalSec,
                    orbitCount = p.OrbitCount,
                    coneHalfAngleDeg = p.ConeHalfAngleDeg,
                    telegraph = p.Telegraph,
                },
            };
        }

        /// <summary>조합 검증 — 카드는 **켜진 숙주 종류마다**(U5 · 계약 4) 한 번씩, 유닛 · 적은 한 번. 하나라도 거절이면 거절.</summary>
        private static bool CombosAllow(in DcMechanic m, TriggerKind trigger, EffectKind kind, MagnitudeMode mode, in RuleOwner o, string label)
        {
            if (!o.IsCard)
            {
                var c = BindingDefinitionBuilder.ComboOf(in m, trigger, kind, o.IsEnemy, bindsAfterPlacement: false,
                                                        hostCannotHoldAggro: !o.HostIsGuardian);
                c.Magnitude = mode;
                return BindingDefinitionBuilder.CheckCombo(in c, label);
            }
            if (o.CardHosts == HostKinds.None) { Warn($"{label}: 카드의 숙주 종류(hostKinds)가 비었다 — 어디에도 안 붙는다. 건너뛴다."); return false; }
            // 카드는 숙주가 **놓인 뒤** 붙는다(자기 배치는 지났다). 숙주가 가디언인지는 부착 때 정해진다(부착 판정 몫).
            if ((o.CardHosts & HostKinds.Defender) != 0 && !CardCombo(in m, trigger, kind, mode, hostIsEnemy: false, label)) return false;
            if ((o.CardHosts & HostKinds.Enemy) != 0 && !CardCombo(in m, trigger, kind, mode, hostIsEnemy: true, label + " (적 숙주)")) return false;
            return true;
        }

        private static bool CardCombo(in DcMechanic m, TriggerKind trigger, EffectKind kind, MagnitudeMode mode, bool hostIsEnemy, string label)
        {
            var c = BindingDefinitionBuilder.ComboOf(in m, trigger, kind, hostIsEnemy, bindsAfterPlacement: true, hostCannotHoldAggro: false);
            c.Magnitude = mode;
            return BindingDefinitionBuilder.CheckCombo(in c, label);
        }

        /// <summary>새 형식에만 있는 값 가드 — CC · 스택 선택자(옛 저작은 고를 수 없던 값이 생겼다) · 비율형의 비율.</summary>
        private static bool ValuesValid(in EffectValues v, EffectKind kind, string label)
        {
            if ((kind == EffectKind.ApplyCcToTarget || kind == EffectKind.AreaCc)
                && v.ccKind != CcKind.Stun && v.ccKind != CcKind.Impulse && v.ccKind != CcKind.Sleep)
            { Warn($"{label}: CC 종류 {v.ccKind} 는 {kind} 에 배선되지 않았다(기절 · 넉백 · 수면만) — 건너뛴다."); return false; }
            if ((kind == EffectKind.ApplyStackToTarget || kind == EffectKind.AreaApplyStack) && v.stackKind == StackKind.None)
            { Warn($"{label}: {kind} 의 스택 종류가 None — 건너뛴다."); return false; }
            if (v.magnitudeMode == MagnitudeMode.OwnerStatRatio && v.ratio <= 0f)
            { Warn($"{label}: 비율형인데 비율이 0 이하 — 건너뛴다."); return false; }
            return true;
        }

        private static bool HasDot(HazardSO so)
        {
            if (so?.effects == null) return false;
            foreach (var he in so.effects) if (he.kind == CcKind.DoT) return true;
            return false;
        }

        // ── 부착 즉시(카드 · 트리거 없음) — 옛 카드 빌더의 그 갈래와 같은 값(효과 id 만 저작 id) ──────────

        private static BindingDef CardRow(string label, TriggerKind trigger, EffectKind payload, out EffectDef fx)
        {
            var b = BindingDef.Default();
            b.Label = label;
            b.Trigger = trigger;
            b.Origin = BindingOrigin.Card;
            b.Skill = SkillRouting.Resolve(trigger, payload);
            fx = EffectDef.Default();
            fx.Kind = payload;
            return b;
        }

        private static void AttachInstant(in DcMechanic m, in EffectValues v, EffectKind kind, string label, string effectId,
                                          CardTargetAxis axis, ref bool aura, List<BindingDef> rows, List<EffectDef> effects,
                                          List<int> mine, MatchViewAssets view)
        {
            var trigger = TriggerKind.None;
            switch (kind)
            {
                case EffectKind.SelfBuffLethal:
                {
                    if (v.percent <= 0f || v.durationSec <= 0f) { Warn($"{label}: SelfBuffLethal percent/duration <= 0 — 건너뛴다."); return; }
                    var b = CardRow(label, trigger, kind, out var fx);
                    fx.Magnitude = 1f + v.percent / 100f;
                    fx.Duration = v.durationSec;
                    b.FireCap = 1;
                    BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                    BindingDefinitionBuilder.AddRow(rows, effects, mine, b, fx, effectId);
                    return;
                }
                case EffectKind.DreamCocoon:
                {
                    if (v.percent <= 0f || v.durationSec <= ProgressiveStates.CocoonEpsilon) { Warn($"{label}: DreamCocoon percent <= 0 또는 duration <= ε — 건너뛴다."); return; }
                    if (!CardDefinitionBuilder.MapBuff(v.buffStat, v.percent, out var stat, out float mul)) { Warn($"{label}: DreamCocoon 스탯 {v.buffStat} 을 옮길 수 없다 — 건너뛴다."); return; }
                    var b = CardRow(label, trigger, kind, out var fx);
                    fx.StatKind = (int)stat;
                    fx.Magnitude = mul;
                    fx.Duration = v.durationSec;
                    b.FireCap = 1;
                    BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                    BindingDefinitionBuilder.AddRow(rows, effects, mine, b, fx, effectId);
                    return;
                }
                case EffectKind.BountyMark:
                {
                    if (v.mul <= 1f) { Warn($"{label}: BountyMark mul <= 1(현상금 없음) — 건너뛴다."); return; }
                    if (v.percent < 0f || v.percent >= 100f) { Warn($"{label}: BountyMark percent(받는 피해 감소) [0,100) 밖 — 건너뛴다."); return; }
                    int cut = m.payload.tileRange;   // 옛 굽기와 같은 정수 %(ToLegacy)
                    var b = CardRow(label, trigger, kind, out var fx);
                    fx.Magnitude = v.mul;
                    fx.HitThreshold = cut > 0 ? 1f - cut / 100f : 0f;
                    b.FireCap = 1;
                    BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                    BindingDefinitionBuilder.AddRow(rows, effects, mine, b, fx, effectId);
                    return;
                }
                case EffectKind.PlacementAura:
                {
                    if (v.percent <= 0f) { Warn($"{label}: PlacementAura percent <= 0 — 건너뛴다."); return; }
                    if (aura) { Warn($"{label}: 카드당 PlacementAura 는 하나만 — 추가 오라는 건너뛴다."); return; }
                    if (!CardDefinitionBuilder.ToAxis(axis, out int mask, out int cost)) { Warn($"{label}: 축 {axis} 을 옮길 수 없다 — 건너뛴다."); return; }
                    aura = true;
                    var speed = CardRow(label + " 공속", TriggerKind.OnPlace, kind, out var speedFx);
                    speed.Skill = new SelfStatBuffSkill();
                    speed.Subject = BindingSubject.Any;
                    speed.SubjectClassMask = mask;
                    speed.SubjectCost = cost;
                    speedFx.StatKind = (int)SkillStatKind.AttackSpeedMul;
                    speedFx.Magnitude = 1f + v.percent / 100f;
                    speed.RevokeOnExpire = true;
                    BindingDefinitionBuilder.BakeAuthoredVisual(ref speedFx, in m, rows.Count, view);
                    BindingDefinitionBuilder.AddRow(rows, effects, mine, speed, speedFx, effectId);
                    if (v.durationSec > 0f)
                    {
                        // 한 효과가 규칙 둘로 펴진다(시제) — 둘째 줄 id 는 빌더 파생(`.sleep`).
                        var sleep = CardRow(label + " 수면", TriggerKind.OnPlace, kind, out var sleepFx);
                        sleep.Skill = new PlacementSleepSkill();
                        sleep.Subject = BindingSubject.Any;
                        sleep.SubjectClassMask = mask;
                        sleep.SubjectCost = cost;
                        sleepFx.Duration = v.durationSec;
                        sleep.RevokeOnExpire = false;
                        BindingDefinitionBuilder.AddRow(rows, effects, mine, sleep, sleepFx, effectId + ".sleep");
                    }
                    return;
                }
                default:
                    Warn($"{label}: 트리거 없음 × {kind} 는 배선되지 않았다 — 건너뛴다.");
                    return;
            }
        }

        private static void Warn(string msg) => Debug.LogWarning("[BindingSpecBuilder] " + msg);
        private static void Error(string msg) => Debug.LogError("[BindingSpecBuilder] " + msg);
    }
}
