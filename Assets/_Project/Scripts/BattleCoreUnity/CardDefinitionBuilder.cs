using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using Wassup.Data;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.BattleCoreUnity
{
    /// <summary>카드 bake 의 입력 — 이 판의 덱(저장 부착 + 공용 액티브, 구성 순서 그대로) · 값 저작 · 판 진입 드림스톤.</summary>
    public struct CardAuthoring
    {
        public IReadOnlyList<DreamcatcherCard> Cards;
        public AwakeningConfig Awakening;
        public IReadOnlyList<DreamstoneData> Dreamstones;
    }

    // battle-core-rebuild unit 7b — 카드 SO → **정의표**(카드 줄 + 규칙 줄)(← 옛 `BattleBridge.ApplyDreamcatcherCardToUnit` 의
    // 저작 검증 몫 · `ApplyBountyMark` 의 검증 · `CastSkillAtTile` 의 효과 분기 · `ApplyPendingDreamstones`).
    //
    // 옛 전투는 이 검증을 **부착할 때마다** 했다(카드 데이터가 틀렸다는 경고가 부착 시점에 떴다). 새 코어에서는 어느 숙주에서나
    // 답이 같은 검증(magnitude·탄·주기·트리거 축 가드)이 **판 밖에서 한 번**이고, 숙주 종속 판정만 부착 때(`Applicability`)다.
    // 옛 가드는 **전부 loud skip 으로 옮겼다** — 슬롯만 구워지고 발화하고 아무 일도 안 일어나는 것이 옛 전투가 반복해서 당한 형태다.
    //
    // ⚠ 카드 **값**(각성 비용)은 카드 종류별 저작(`AwakeningConfig.CostFor`)이다 — C# 기본값(15/30/20)이 아니라 에셋이 정본이다.
    public static class CardDefinitionBuilder
    {
        /// <summary>
        /// 덱의 카드 → `def.Cards`(순서 = 덱 구성 순서) + 카드 규칙 줄. 드림스톤 → `def.MatchBindings`.
        /// `CombatDefinitionBuilder.Fill` 이 **탄·패턴 표를 굳히기 전**에 부른다(카드 탄·패턴이 같은 표에 든다).
        /// </summary>
        public static void Fill(MatchDefinition def, in CardAuthoring src, List<ProjectileData> projectiles,
                                List<ProjectilePatternData> patterns, HazardSO[] hazards,
                                MatchViewAssets view = null)
        {
            var rows = new List<BindingDef>(def.Bindings ?? System.Array.Empty<BindingDef>());
            var effects = new List<EffectDef>(def.Effects ?? System.Array.Empty<EffectDef>());
            var cards = new List<CardDef>();
            // unit 7c — 카드 줄 번호 → 카드 에셋(아트·문안). 빈 칸을 건너뛰는 **이 순회**가 번호를 매기므로 목록도 여기서 낸다.
            var assets = new List<DreamcatcherCard>();
            if (src.Cards != null)
                for (int i = 0; i < src.Cards.Count; i++)
                {
                    var c = src.Cards[i];
                    if (c == null) { Warn($"덱 {i} 번 카드가 비었다 — 건너뛴다."); continue; }
                    cards.Add(Bake(c, src.Awakening, projectiles, patterns, hazards, rows, effects, view));
                    assets.Add(c);
                }
            if (cards.Count > 0) def.Cards = cards.ToArray();
            view?.SetCards(assets);
            var match = BakeDreamstones(src.Dreamstones, rows, effects);
            if (match.Length > 0) def.MatchBindings = match;
            def.Bindings = rows.ToArray();
            def.Effects = effects.ToArray();
        }

        /// <summary>판 진입 드림스톤의 코스트 재생 배율(옛 `ResolveCostRateMultiplier` — 1 + Σ%/100). 스탯 돌은 규칙 줄이다.</summary>
        public static float CostRateOf(IReadOnlyList<DreamstoneData> stones)
        {
            float sum = 0f;
            if (stones != null)
                foreach (var s in stones)
                    if (s != null && s.effect.kind == CardBuffKind.CostRate) sum += s.effect.percent;
            return 1f + sum / 100f;
        }

        /// <summary>카드가 까는 장판 SO — 장판 표(`BoardEffectAuthoring.Hazards`)에 들어야 규칙이 줄 번호로 가리킨다.</summary>
        public static HazardSO[] WithCardHazards(HazardSO[] board, IReadOnlyList<DreamcatcherCard> cards)
        {
            var list = new List<HazardSO>(board ?? System.Array.Empty<HazardSO>());
            if (cards != null)
                foreach (var c in cards)
                {
                    if (c == null) continue;
                    if (HasBindings(c))
                    {
                        // skill-data-table unit 4 — 새 저작 형식이면 소유 줄의 효과 에셋에서(옛 칸과 같은 규칙).
                        foreach (var b in c.bindings)
                            if (b.effect != null && b.effect.values.kind == EffectKind.SpawnHazard && b.effect.hazard != null && !list.Contains(b.effect.hazard))
                                list.Add(b.effect.hazard);
                        continue;
                    }
                    if (c.mechanics == null) continue;
                    foreach (var m in c.mechanics)
                        if (m.payload.kind == EffectKind.SpawnHazard && m.payload.hazard != null && !list.Contains(m.payload.hazard))
                            list.Add(m.payload.hazard);
                }
            return list.ToArray();
        }

        // ── 카드 한 장 ────────────────────────────────────────────────────────

        private static CardDef Bake(DreamcatcherCard card, AwakeningConfig awakening, List<ProjectileData> projectiles,
                                    List<ProjectilePatternData> patterns, HazardSO[] hazards, List<BindingDef> rows,
                                    List<EffectDef> effects, MatchViewAssets view)
        {
            var c = CardDef.Default();
            c.Id = card.id;
            if (awakening != null) c.Cost = awakening.CostFor(card.type);
            else Error($"'{card.id}': 각성 저작(AwakeningConfig)이 없어 카드 값을 모른다 — 0 으로 둔다.");

            if (card.type == CardType.Active)
            {
                c.Kind = CardKind.Active;
                if (HasBindings(card))
                {
                    // skill-data-table unit 4 — 새 저작 형식: 시전 한 줄 × 액티브 효과 · 대기 · 두 칸 조준은 카드 칸.
                    c.CooldownSeconds = card.cooldownSec;
                    c.NeedsTwoCells = card.needsTwoTiles;
                    c.ActiveBinding = BakeActiveBinding(card, projectiles, rows, effects);
                    return c;
                }
                if (card.skill == null) { Error($"'{card.id}': 액티브인데 SkillData 가 없다 — 시전이 거절된다."); return c; }
                c.CooldownSeconds = card.skill.cooldownSec;
                c.NeedsTwoCells = card.skill.NeedsTwoTiles;
                c.ActiveBinding = BakeActive(card, projectiles, rows, effects);
                return c;
            }

            c.Kind = CardKind.Attach;
            c.Requirement = ToRequirement(card);
            if (card.type == CardType.Squad)
            {
                if (HasBindings(card)) Warn($"'{card.id}': Squad 카드의 소유 줄(bindings)은 읽지 않는다 — 스쿼드 스탯 효과는 effects 다.");
                // Squad 는 effects 만 읽는다(옛 계약 — mechanics 는 Unit 카드만).
                c.SquadBindings = BakeSquad(card, rows, effects);
                return c;
            }

            // Unit — mechanics(또는 새 형식 bindings) + attackMods.
            var mine = new List<int>();
            var mods = new List<AttackModDef>();
            var mech = card.mechanics ?? System.Array.Empty<DcMechanic>();
            bool aura = false;
            if (HasBindings(card))
            {
                // skill-data-table unit 4 — 새 저작 형식. 적 겨냥 = 숙주 종류가 적만(U5 — 옛 `HasBountyMark()` 파생을 값으로).
                var hosts = card.hostKinds;
                c.TargetsEnemies = hosts == HostKinds.Enemy;
                if (hosts == (HostKinds.Defender | HostKinds.Enemy))
                    Warn($"'{card.id}': 숙주 종류가 방어유닛 · 적 둘 다 — 코어 카드 줄은 한쪽만 싣는다. 방어유닛 카드로 굽는다(조합 검증은 둘 다).");
                BindingSpecBuilder.Bake(card.bindings, new RuleOwner
                    {
                        Origin = BindingOrigin.Card, Label = $"카드 '{card.id}'", IsCard = true,
                        CardHosts = hosts, CardTargetsEnemies = c.TargetsEnemies, Axis = card.axis,
                    },
                    projectiles, patterns, hazards, rows, effects, mine, mods, ref c.DeclaresRetireRecall, view);
                mech = System.Array.Empty<DcMechanic>();
            }
            else c.TargetsEnemies = card.HasBountyMark();
            for (int i = 0; i < mech.Length; i++)
            {
                string label = $"카드 '{card.id}' mechanic {i}";
                if (c.TargetsEnemies && mech[i].payload.kind != EffectKind.BountyMark)
                {
                    Warn($"{label}: 적 표식 카드는 표식 메커닉만 쓴다(옛 `ApplyBountyMark`) — {mech[i].payload.kind} 는 건너뛴다.");
                    continue;
                }
                BakeMechanic(in mech[i], label, card, projectiles, patterns, hazards, rows, effects, mine, mods, ref aura, ref c, view);
            }
            if (!c.TargetsEnemies && card.attackMods != null)
                for (int i = 0; i < card.attackMods.Length; i++)
                {
                    var am = card.attackMods[i];
                    string label = $"카드 '{card.id}' attackMod {i}";
                    if (am.kind == DcAttackModKind.None || am.damageMul <= 0f) { Warn($"{label}: None 종류 / damageMul <= 0 — 건너뛴다."); continue; }
                    if (am.kind == DcAttackModKind.ProjectileBounce && am.count <= 0) { Warn($"{label}: ProjectileBounce count <= 0 — 건너뛴다."); continue; }
                    if (am.kind == DcAttackModKind.DamageVsSleeping && am.damageMul <= 1f) { Warn($"{label}: DamageVsSleeping damageMul <= 1(특효가 아님) — 건너뛴다."); continue; }
                    var kind = BindingDefinitionBuilder.ToCoreAttackMod(am.kind);
                    if (kind == AttackModKind.None) { Warn($"{label}: 옮길 수 없는 종류 {am.kind} — 건너뛴다."); continue; }
                    mods.Add(new AttackModDef { Kind = kind, Count = am.count, TileRange = am.tileRange, DamageMul = am.damageMul });
                }
            if (mine.Count > 0) c.Bindings = mine.ToArray();
            if (mods.Count > 0) c.AttackMods = mods.ToArray();
            if (mine.Count == 0 && mods.Count == 0 && !c.DeclaresRetireRecall)
                Error($"'{card.id}': 구워진 규칙이 하나도 없다 — 어떤 유닛에도 안 붙는다.");
            return c;
        }

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

        // 카드 규칙 줄 — id = `{카드}.{자리}`(자리 = 그 카드 목록 안 순번).
        private static void Add(List<BindingDef> rows, List<EffectDef> effects, List<int> mine, string cardId,
                                in BindingDef b, in EffectDef fx)
            => BindingDefinitionBuilder.AddRow(rows, effects, mine, b, fx, cardId + "." + mine.Count);

        private static void BakeMechanic(in DcMechanic m, string label, DreamcatcherCard card,
                                         List<ProjectileData> projectiles, List<ProjectilePatternData> patterns,
                                         HazardSO[] hazards, List<BindingDef> rows, List<EffectDef> effects, List<int> mine,
                                         List<AttackModDef> mods, ref bool aura, ref CardDef c, MatchViewAssets view)
        {
            if (!BindingDefinitionBuilder.KnownKinds(in m, label)) return;
            var trigger = m.trigger.kind;
            var payload = m.payload.kind;
            var p = m.payload;

            // ── 트리거 없음 = 부착되는 순간(3장 + 배치 오라) ──
            if (trigger == TriggerKind.None)
            {
                switch (payload)
                {
                    case EffectKind.SelfBuffLethal:
                    {
                        if (p.magnitude <= 0f || p.duration <= 0f) { Warn($"{label}: SelfBuffLethal magnitude/duration <= 0 — 건너뛴다."); return; }
                        var b = CardRow(label, trigger, payload, out var fx);
                        fx.Magnitude = 1f + p.magnitude / 100f;   // % → 배율(도메인은 저작 인코딩을 모른다)
                        fx.Duration = p.duration;
                        b.FireCap = 1;
                        BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                        Add(rows, effects, mine, card.id, in b, in fx);
                        return;
                    }
                    case EffectKind.DreamCocoon:
                    {
                        if (p.magnitude <= 0f || p.duration <= ProgressiveStates.CocoonEpsilon) { Warn($"{label}: DreamCocoon magnitude <= 0 또는 duration <= ε — 건너뛴다(무수면 즉시 완주)."); return; }
                        if (!MapBuff(p.buffStat, p.magnitude, out var stat, out float mul)) { Warn($"{label}: DreamCocoon 스탯 {p.buffStat} 을 옮길 수 없다 — 건너뛴다."); return; }
                        var b = CardRow(label, trigger, payload, out var fx);
                        fx.StatKind = (int)stat;
                        fx.Magnitude = mul;
                        fx.Duration = p.duration;
                        b.FireCap = 1;
                        BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                        Add(rows, effects, mine, card.id, in b, in fx);
                        return;
                    }
                    case EffectKind.BountyMark:
                    {
                        if (p.magnitude <= 1f) { Warn($"{label}: BountyMark magnitude <= 1(현상금 없음) — 건너뛴다."); return; }
                        if (p.tileRange < 0 || p.tileRange >= 100) { Warn($"{label}: BountyMark tileRange(받는 피해 감소 %) [0,100) 밖 — 건너뛴다."); return; }
                        var b = CardRow(label, trigger, payload, out var fx);
                        fx.Magnitude = p.magnitude;                                     // 각성 배율
                        fx.HitThreshold = p.tileRange > 0 ? 1f - p.tileRange / 100f : 0f;   // 받는 피해 배율(0 = 안 건다)
                        b.FireCap = 1;
                        BindingDefinitionBuilder.BakeAuthoredVisual(ref fx, in m, rows.Count, view);
                        Add(rows, effects, mine, card.id, in b, in fx);
                        return;
                    }
                    case EffectKind.PlacementAura:
                    {
                        if (p.magnitude <= 0f) { Warn($"{label}: PlacementAura magnitude <= 0 — 건너뛴다."); return; }
                        // 카드당 하나(옛 review M1 — 둘째는 핸들이 덮여 누수됐다).
                        if (aura) { Warn($"{label}: 카드당 PlacementAura 는 하나만 — 추가 오라는 건너뛴다."); return; }
                        if (!ToAxis(card.axis, out int mask, out int cost)) { Warn($"{label}: 축 {card.axis} 을 옮길 수 없다 — 건너뛴다."); return; }
                        aura = true;
                        // **규칙 둘**(정정 3 · H6) — 공속은 숙주가 떠나면 소급 회수, 수면은 등록부에서만 빠진다.
                        var speed = CardRow(label + " 공속", TriggerKind.OnPlace, payload, out var speedFx);
                        speed.Skill = new SelfStatBuffSkill();
                        speed.Subject = BindingSubject.Any;
                        speed.SubjectClassMask = mask;
                        speed.SubjectCost = cost;
                        speedFx.StatKind = (int)SkillStatKind.AttackSpeedMul;
                        speedFx.Magnitude = 1f + p.magnitude / 100f;
                        speed.RevokeOnExpire = true;
                        // 오라는 숙주당 하나(뷰 규약) — 저작 연출은 첫 줄(공속)에 싣는다.
                        BindingDefinitionBuilder.BakeAuthoredVisual(ref speedFx, in m, rows.Count, view);
                        Add(rows, effects, mine, card.id, in speed, in speedFx);
                        if (p.duration > 0f)
                        {
                            var sleep = CardRow(label + " 수면", TriggerKind.OnPlace, payload, out var sleepFx);
                            sleep.Skill = new PlacementSleepSkill();
                            sleep.Subject = BindingSubject.Any;
                            sleep.SubjectClassMask = mask;
                            sleep.SubjectCost = cost;
                            sleepFx.Duration = p.duration;
                            sleep.RevokeOnExpire = false;
                            Add(rows, effects, mine, card.id, in sleep, in sleepFx);
                        }
                        return;
                    }
                    default:
                        Warn($"{label}: 트리거 없음 × {payload} 는 배선되지 않았다 — 건너뛴다.");
                        return;
                }
            }

            if (payload == EffectKind.None)
            {
                Warn($"{label}: None 종류 — 건너뛴다.");
                return;
            }
            // 손패 동작(인수인계) — 규칙이 아니라 퇴근 회수 규칙의 선언이다. **퇴근에만**, 게이트 없이.
            if (payload == EffectKind.RecallAttachedToFront)
            {
                if (trigger != TriggerKind.OnRetire) { Warn($"{label}: 인수인계는 OnRetire 에만 배선돼 있다(현재 {trigger}) — 건너뛴다."); return; }
                if (m.trigger.gate != GateKind.None) { Warn($"{label}: 인수인계에는 게이트가 배선돼 있지 않다 — 건너뛴다."); return; }
                c.DeclaresRetireRecall = true;
                return;
            }
            // unified-effect-layer unit 5 — 조합은 **검증 한 함수**(출처는 입력이 아니다). 카드는 숙주가 **놓인 뒤** 붙는다 —
            // 자기 배치는 이미 지났다. 숙주는 부착 때 정해져 가디언 여부를 여기서 모른다(부착 판정 몫).
            if (!BindingDefinitionBuilder.CheckCombo(BindingDefinitionBuilder.ComboOf(in m, trigger, payload, hostIsEnemy: false,
                                                     bindsAfterPlacement: true, hostCannotHoldAggro: false), label)) return;
            var gate = m.trigger.gate;
            var gateSubject = m.trigger.gateSubject;
            if (!BindingDefinitionBuilder.TriggerValuesValid(in m, trigger, gate, gateSubject, label)) return;

            // 강공 — 어휘 밖(그 공격의 성질). 공격 수식자로 접는다.
            if (payload == EffectKind.HeavyStrike)
            {
                if (BindingDefinitionBuilder.TryHeavyStrike(in m, trigger, gate, label, out var heavy)) mods.Add(heavy);
                return;
            }

            var r = CardRow(label, trigger, payload, out var rFx);
            r.Period = Mathf.Clamp(m.trigger.period, 0, ushort.MaxValue);
            r.PeriodSeconds = m.trigger.periodSeconds;
            r.Fraction = m.trigger.fraction;
            r.Gate = gate;
            r.GateSubject = gateSubject;
            r.GateValue = m.trigger.gateValue;
            rFx.Magnitude = p.magnitude;
            rFx.TileRange = Mathf.Max(0, p.tileRange);
            rFx.Duration = Mathf.Max(0f, p.duration);
            rFx.CcKind = (int)BindingDefinitionBuilder.ToSkillCc(p.ccKind);
            rFx.StackKind = (int)BindingDefinitionBuilder.ToSkillStack(p.stackKind);
            rFx.StatKind = (int)(BindingDefinitionBuilder.TryToSkillStat(p.buffStat, out var st) ? st : SkillStatKind.DamageMul);
            BindingDefinitionBuilder.BakeCone(ref rFx, p.coneHalfAngleDeg);
            BindingDefinitionBuilder.ApplyAuthoredAxes(ref r, ref rFx, in m);

            // 값 가드 · 표 참조는 두 빌더 공용(`BindingDefinitionBuilder.BindPayload`). 아래 둘은 **카드 저작 인코딩**이다(H4 후속).
            if (!BindingDefinitionBuilder.BindPayload(ref r, ref rFx, in m, label, projectiles, patterns, hazards)) return;
            switch (payload)
            {
                case EffectKind.SelfTileAoe:
                    rFx.VisualScale = p.projectile.visualScale;   // 카드는 착탄 연출 배율을 싣는다(유닛 bake 는 0)
                    break;
                case EffectKind.SelfStatBuff:
                {
                    // 카드 버프는 % 저작 → 배율(유닛 저작은 배율 그대로).
                    if (!MapBuff(p.buffStat, p.magnitude, out var stat, out float mul)) { Warn($"{label}: SelfStatBuff 스탯 {p.buffStat} 을 옮길 수 없다 — 건너뛴다."); return; }
                    // 최대 중첩(tileRange > 0)은 배율 > 1 에서만 성립한다(곱셈 버킷 값을 더하면 뜻이 뒤집힌다).
                    if (p.tileRange > 0 && mul <= 1f) { Warn($"{label}: SelfStatBuff 최대 중첩은 배율 > 1 에서만 — 건너뛴다."); return; }
                    rFx.StatKind = (int)stat;
                    rFx.Magnitude = mul;
                    break;
                }
            }

            if (r.Skill == null) { Warn($"{label}: '{trigger} × {payload}' 조합에 라우팅이 없다 — 건너뛴다."); return; }
            // 효과가 선언한 연출(빔 · 부착 오라)은 소유자와 무관하다 — 유닛 빌더와 같은 함수(U15).
            BindingDefinitionBuilder.BakeAuthoredVisual(ref rFx, in m, rows.Count, view);
            Add(rows, effects, mine, card.id, in r, in rFx);
        }

        // ── Squad ─────────────────────────────────────────────────────────────

        private static int[] BakeSquad(DreamcatcherCard card, List<BindingDef> rows, List<EffectDef> table)
        {
            var mine = new List<int>();
            if (!ToAxis(card.axis, out int mask, out int cost)) { Error($"'{card.id}': 축 {card.axis} 을 옮길 수 없다 — 효과 없음."); return null; }
            var effects = card.effects ?? System.Array.Empty<CardEffect>();
            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                // `CostRate` 는 유닛 스탯이 아니다 — 카드 경로에서는 옛 전투도 무동작이었다(드림스톤 전용 · 판 진입 배율).
                if (!MapBuff(e.kind, e.percent, out var stat, out float mul)) { Warn($"'{card.id}' effect {i}: {e.kind} 는 카드 스탯이 아니다 — 건너뛴다."); continue; }
                var b = CardRow($"카드 '{card.id}' effect {i}", TriggerKind.OnPlace, EffectKind.SelfStatBuff, out var fx);
                b.Subject = BindingSubject.Any;
                b.SubjectClassMask = mask;
                b.SubjectCost = cost;
                fx.StatKind = (int)stat;
                fx.Magnitude = mul;
                b.RevokeOnExpire = true;   // 숙주가 떠나면(사망 ∪ 퇴근) 판 전체에서 소급 회수(정정 1)
                BindingDefinitionBuilder.AddRow(rows, table, mine, b, fx, card.id + ".squad" + mine.Count);
            }
            return mine.Count > 0 ? mine.ToArray() : null;
        }

        private static int[] BakeDreamstones(IReadOnlyList<DreamstoneData> stones, List<BindingDef> rows, List<EffectDef> effects)
        {
            var mine = new List<int>();
            if (stones == null) return mine.ToArray();
            for (int i = 0; i < stones.Count; i++)
            {
                var s = stones[i];
                if (s == null || s.effect.kind == CardBuffKind.CostRate) continue;   // 코스트 돌은 `CostRateOf`
                if (!MapBuff(s.effect.kind, s.effect.percent, out var stat, out float mul)) { Warn($"드림스톤 '{s.id}': {s.effect.kind} 를 옮길 수 없다 — 건너뛴다."); continue; }
                // skill-data-table unit 4 — 드림스톤도 조합 검증을 지난다: 판 시전(주인 없음) × 남의 배치 × 스탯 버프(고정 수치).
                var stoneCombo = new EffectCombo { Trigger = TriggerKind.OnPlace, Subject = BindingSubject.Any, Payload = EffectKind.SelfStatBuff,
                                                   Magnitude = MagnitudeMode.Flat, CastHasNoOwner = true };
                if (!BindingDefinitionBuilder.CheckCombo(in stoneCombo, $"드림스톤 '{s.id}'")) continue;
                var b = BindingDef.Default();
                var fx = EffectDef.Default();
                b.Label = $"드림스톤 '{s.id}'";
                b.Trigger = TriggerKind.OnPlace;
                b.Subject = BindingSubject.Any;          // 축 All(옛 `MatchesDcAxis(All)`)
                b.Skill = new DreamstoneStatSkill();
                fx.StatKind = (int)stat;
                fx.Magnitude = mul;
                b.Lifetime = BindingLifetime.Match;
                b.RevokeOnExpire = true;                 // 칸 판별자 = 이 규칙(돌마다 새 칸 — 옛 `_dcStackCounter++`)
                b.Origin = BindingOrigin.Match;
                BindingDefinitionBuilder.AddRow(rows, effects, mine, b, fx, "match." + mine.Count);
            }
            return mine.ToArray();
        }

        // ── 액티브 ────────────────────────────────────────────────────────────

        private static int BakeActive(DreamcatcherCard card, List<ProjectileData> projectiles, List<BindingDef> rows, List<EffectDef> effects)
        {
            var s = card.skill;
            // skill-data-table unit 4 — 액티브도 조합 검증 한 함수를 지난다(시전 · 주인 없음). 옛 칸은 전량 고정 수치라 허용.
            if (!CheckCast(ActiveKindOf(s.effect), MagnitudeMode.Flat, $"액티브 '{card.id}'")) return -1;
            var b = BindingDef.Default();
            var fx = EffectDef.Default();
            b.Label = $"액티브 '{card.id}'";
            b.Trigger = TriggerKind.None;
            b.Origin = BindingOrigin.Card;
            b.FireCap = 1;
            b.Lifetime = BindingLifetime.UntilFireCap;
            fx.Magnitude = s.magnitude;
            fx.Duration = s.durationSec;
            fx.TileRange = SkillMath.RangeToTiles(s.range);
            int id;
            switch (s.effect)
            {
                case SkillEffectType.SlowField: id = TileStatBurstSkill.Id; fx.StatKind = (int)SkillStatKind.MoveSpeedMul; break;
                case SkillEffectType.Tornado: id = PullFieldSkill.Id; break;
                case SkillEffectType.Meteor:
                    id = TileMeteorSkill.Id;
                    // ⚠ 저작 오류라도 시전은 성공이다(옛 동작) — 떨어질 것이 없을 뿐 값·대기는 소모된다.
                    if (s.projectile == null) Error($"'{card.id}': 메테오에 탄(ProjectileData)이 없다 — 시전해도 아무것도 안 떨어진다.");
                    else { fx.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, s.projectile); fx.VisualScale = s.projectile.visualScale; }
                    fx.Duration = s.warningSec > 0f ? s.warningSec : 0f;   // 메테오만 지속이 **낙하 예고**다
                    break;
                // 아군 버프는 시간제 장판이다(두 갈래가 같은 concrete — 스탯만 다르다).
                case SkillEffectType.PowerSurge: id = AllyBuffFieldSkill.Id; fx.StatKind = (int)SkillStatKind.DamageMul; break;
                case SkillEffectType.RapidFire: id = AllyBuffFieldSkill.Id; fx.StatKind = (int)SkillStatKind.AttackSpeedMul; break;
                case SkillEffectType.Portal:
                    id = PortalSkill.Id;
                    if (!s.NeedsTwoTiles) Warn($"'{card.id}': 포탈인데 두 칸 조준(needsTwoTiles)이 꺼져 있다 — 출구가 없어 발동해도 아무 일이 없다.");
                    break;
                default:
                    Error($"'{card.id}': 모르는 액티브 효과 {s.effect} — 시전이 거절된다.");
                    return -1;
            }
            if (!SkillRouting.Registry.TryGet(id, out var skill)) { Error($"'{card.id}': 액티브 실행자({id})가 레지스트리에 없다."); return -1; }
            b.Skill = skill;
            BindingDefinitionBuilder.AddRow(rows, effects, null, b, fx, card.id + ".active0");
            return rows.Count - 1;
        }

        /// <summary>
        /// skill-data-table unit 4 — 새 저작 형식의 액티브 시전 한 줄(옛 `BakeActive` 와 같은 값 · 효과 id 만 저작 id). 효과 줄의 종류는 옛 굽기처럼
        /// `None` 으로 남긴다(해시 무변 — 실행자는 레지스트리 id 가 고른다).
        /// </summary>
        private static int BakeActiveBinding(DreamcatcherCard card, List<ProjectileData> projectiles, List<BindingDef> rows, List<EffectDef> effects)
        {
            string label = $"액티브 '{card.id}'";
            if (card.bindings.Length != 1) { Error($"{label}: 액티브 카드의 소유 줄은 정확히 하나다(지금 {card.bindings.Length}) — 시전이 거절된다."); return -1; }
            var spec = card.bindings[0];
            var e = spec.effect;
            if (e == null) { Error($"{label}: 효과 에셋이 비었다 — 시전이 거절된다."); return -1; }
            if (e.deprecated) { Error($"{label}: 폐기된 효과 '{e.id}' — 시전이 거절된다."); return -1; }
            if (spec.trigger.kind != TriggerKind.Cast) { Error($"{label}: 액티브 줄의 트리거는 시전(Cast)이다(지금 {spec.trigger.kind}) — 시전이 거절된다."); return -1; }
            var v = e.values;
            if (!CheckCast(v.kind, v.magnitudeMode, label)) return -1;
            if (spec.fireCap != 1) Warn($"{label}: 시전 줄의 fireCap({spec.fireCap}) — 시전은 1회 발동이다(1 로 굽는다).");
            var p = EffectSlots.ToLegacy(in v);
            var b = BindingDef.Default();
            var fx = EffectDef.Default();
            b.Label = label;
            b.Trigger = TriggerKind.None;
            b.Origin = BindingOrigin.Card;
            b.FireCap = 1;
            b.Lifetime = BindingLifetime.UntilFireCap;
            fx.Magnitude = p.Magnitude;
            fx.Duration = p.Duration;
            fx.TileRange = p.TileRange;
            int id;
            switch (v.kind)
            {
                case EffectKind.ActiveSlowField: id = TileStatBurstSkill.Id; fx.StatKind = (int)SkillStatKind.MoveSpeedMul; break;
                case EffectKind.ActiveTornado: id = PullFieldSkill.Id; break;
                case EffectKind.ActiveMeteor:
                    id = TileMeteorSkill.Id;
                    if (e.projectile == null) Error($"{label}: 메테오에 탄(ProjectileData)이 없다 — 시전해도 아무것도 안 떨어진다.");
                    else { fx.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, e.projectile); fx.VisualScale = e.projectile.visualScale; }
                    break;
                case EffectKind.ActivePowerSurge: id = AllyBuffFieldSkill.Id; fx.StatKind = (int)SkillStatKind.DamageMul; break;
                case EffectKind.ActiveRapidFire: id = AllyBuffFieldSkill.Id; fx.StatKind = (int)SkillStatKind.AttackSpeedMul; break;
                case EffectKind.ActivePortal:
                    id = PortalSkill.Id;
                    if (!card.needsTwoTiles) Warn($"{label}: 포탈인데 두 칸 조준(needsTwoTiles)이 꺼져 있다 — 출구가 없어 발동해도 아무 일이 없다.");
                    break;
                default:
                    Error($"{label}: 모르는 액티브 효과 {v.kind} — 시전이 거절된다.");
                    return -1;
            }
            if (!SkillRouting.Registry.TryGet(id, out var skill)) { Error($"{label}: 액티브 실행자({id})가 레지스트리에 없다."); return -1; }
            b.Skill = skill;
            BindingDefinitionBuilder.AddRow(rows, effects, null, b, fx, e.id);
            return rows.Count - 1;
        }

        /// <summary>시전 조합 검증(주인 없는 시전 — 비율형 기준 없음 · 액티브 효과만).</summary>
        private static bool CheckCast(EffectKind kind, MagnitudeMode mode, string label)
        {
            var c = new EffectCombo { Trigger = TriggerKind.Cast, Payload = kind, Magnitude = mode, CastHasNoOwner = true };
            return BindingDefinitionBuilder.CheckCombo(in c, label);
        }

        /// <summary>새 저작 형식을 쓰는 카드인가(비어 있으면 옛 칸 — 이전 과도기).</summary>
        internal static bool HasBindings(DreamcatcherCard card) => card.bindings != null && card.bindings.Length > 0;

        /// <summary>옛 액티브 효과(`SkillEffectType`) → 효과 종류(1:1 · `tables.md` §3). 모르는 값 = None.</summary>
        public static EffectKind ActiveKindOf(SkillEffectType t)
        {
            switch (t)
            {
                case SkillEffectType.Meteor: return EffectKind.ActiveMeteor;
                case SkillEffectType.SlowField: return EffectKind.ActiveSlowField;
                case SkillEffectType.PowerSurge: return EffectKind.ActivePowerSurge;
                case SkillEffectType.RapidFire: return EffectKind.ActiveRapidFire;
                case SkillEffectType.Tornado: return EffectKind.ActiveTornado;
                case SkillEffectType.Portal: return EffectKind.ActivePortal;
                default: return EffectKind.None;
            }
        }

        // ── 저작 어휘 → 코어 어휘 ────────────────────────────────────────────

        /// <summary>
        /// 저작 버프(kind + %) → 스탯 + 배율(옛 `MapDcBuff`). 받는 피해 대리는 **역수**다(최대 체력을 안 바꾸고 덜 맞는다).
        /// `CostRate` 는 스탯이 아니다(false).
        /// </summary>
        public static bool MapBuff(CardBuffKind kind, float percent, out SkillStatKind stat, out float mul)
        {
            switch (kind)
            {
                case CardBuffKind.AttackDamage: stat = SkillStatKind.DamageMul; mul = 1f + percent / 100f; return true;
                case CardBuffKind.AttackSpeed: stat = SkillStatKind.AttackSpeedMul; mul = 1f + percent / 100f; return true;
                case CardBuffKind.EffectiveHealth: stat = SkillStatKind.DmgTakenMul; mul = 1f / (1f + percent / 100f); return true;
                case CardBuffKind.MoveSpeed: stat = SkillStatKind.MoveSpeedMul; mul = 1f + percent / 100f; return true;
                case CardBuffKind.DamageVsCc: stat = SkillStatKind.DamageVsCcMul; mul = 1f + percent / 100f; return true;
                default: stat = SkillStatKind.DamageMul; mul = 1f; return false;
            }
        }

        /// <summary>카드 축 → 주어 필터(직업 비트 · 코스트). 옛 `MatchesDcAxis`.</summary>
        public static bool ToAxis(CardTargetAxis axis, out int classMask, out int cost)
        {
            classMask = 0; cost = 0;
            switch (axis)
            {
                case CardTargetAxis.ClassRanger: classMask = 1 << (int)DefenderClass.Ranger; return true;
                case CardTargetAxis.ClassGuardian: classMask = 1 << (int)DefenderClass.Guardian; return true;
                case CardTargetAxis.Cost1: cost = 1; return true;
                case CardTargetAxis.All: return true;   // 명시해야 한다(기본 false 로 떨어지면 조용히 무동작)
                default: return false;
            }
        }

        /// <summary>부착 제한(옛 `attachType + attachValue`) → 해석을 끝낸 값. 무효 저작은 `Invalid`(어디에도 안 붙는다).</summary>
        public static AttachRequirementDef ToRequirement(DreamcatcherCard card)
        {
            var r = new AttachRequirementDef();
            switch (card.attachType)
            {
                case DcAttachType.None: return r;
                case DcAttachType.Class:
                    r.Kind = AttachRequirementKind.Class;
                    if (Wassup.Core.DreamcatcherAttachEval.TryParseAttachClass(card.attachValue, out var cls)) r.Role = (int)cls;
                    else { r.Invalid = true; Warn($"'{card.id}': 부착 제한 직업 '{card.attachValue}' 이 무효 — 어떤 유닛에도 안 붙는다."); }
                    return r;
                case DcAttachType.UnitId:
                    r.Kind = AttachRequirementKind.UnitId;
                    r.UnitId = card.attachValue;
                    if (string.IsNullOrEmpty(card.attachValue)) { r.Invalid = true; Warn($"'{card.id}': 부착 제한 유닛 id 가 비었다 — 어떤 유닛에도 안 붙는다."); }
                    return r;
                default:
                    r.Kind = AttachRequirementKind.Class;
                    r.Invalid = true;
                    return r;
            }
        }

        private static void Warn(string msg) => Debug.LogWarning("[CardDefinitionBuilder] " + msg);
        private static void Error(string msg) => Debug.LogError("[CardDefinitionBuilder] " + msg);
    }
}
