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
                    if (c?.bindings == null) continue;
                    foreach (var b in c.bindings)
                        if (b.effect != null && b.effect.values.kind == EffectKind.SpawnHazard && b.effect.hazard != null && !list.Contains(b.effect.hazard))
                            list.Add(b.effect.hazard);
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
                // skill-data-table unit 4 — 시전 한 줄(Cast × 액티브 효과) · 대기 · 두 칸 조준은 카드 칸. `skill`(SkillData)은 문안 · 덱 구성만 읽는다.
                if (!HasBindings(card)) { Error($"'{card.id}': 액티브인데 소유 줄(bindings)이 없다 — 시전이 거절된다."); return c; }
                c.CooldownSeconds = card.cooldownSec;
                c.NeedsTwoCells = card.needsTwoTiles;
                c.ActiveBinding = BakeActiveBinding(card, projectiles, rows, effects);
                return c;
            }

            c.Kind = CardKind.Attach;
            c.Requirement = ToRequirement(card);
            // skill-data-table unit 8 — 상시 효과(진영 버프 · 공격 수식자)도 **소유 줄**이다. 진영 버프 줄은 카드 종류가 아니라 **효과 종류로**
            // `SquadBindings` 에 간다(동치 조건 1 — `Bindings` 로 가면 부착 순간 판 위 아군에게 안 걸린다) · 수식자는 규칙 줄을 안 만든다(조건 2).
            // ⚠ 과도기(단계 B 전): 상시 효과 줄이 **없는** 카드는 옛 칸(`effects` · `attackMods`)에서 오늘 결과를 낸다 · 있으면 옛 칸은 안 읽는다.
            bool legacy = !card.HasAlwaysOnRows();
            var mine = new List<int>();
            var mods = new List<AttackModDef>();
            var squad = new List<int>();
            var hosts = card.hostKinds;
            if (card.type == CardType.Squad)
            {
                // Squad = 진영 버프 줄만(카드 분류 검증 — 다른 종류 줄은 빌더가 거절한다 · 덱 상한이 이 분류를 본다).
                if (HasBindings(card))
                    BindingSpecBuilder.Bake(card.bindings, new RuleOwner
                        {
                            Origin = BindingOrigin.Card, Label = $"카드 '{card.id}'", IsCard = true, SquadCard = true,
                            CardHosts = hosts, Axis = card.axis,
                        },
                        projectiles, patterns, hazards, rows, effects, mine, mods, squad, ref c.DeclaresRetireRecall, view);
                if (legacy)
                {
                    var old = BakeSquad(card, rows, effects);
                    if (old != null) squad.AddRange(old);
                }
                if (squad.Count > 0) c.SquadBindings = squad.ToArray();
                return c;
            }

            // Unit — 소유 줄(bindings) + (과도기) attackMods. 적 겨냥 = 숙주 종류가 적만(U5 — 옛 `HasBountyMark()` 파생을 값으로).
            c.TargetsEnemies = hosts == HostKinds.Enemy;
            if (hosts == (HostKinds.Defender | HostKinds.Enemy))
                Warn($"'{card.id}': 숙주 종류가 방어유닛 · 적 둘 다 — 코어 카드 줄은 한쪽만 싣는다. 방어유닛 카드로 굽는다(조합 검증은 둘 다).");
            if (card.bindings != null)
                BindingSpecBuilder.Bake(card.bindings, new RuleOwner
                    {
                        Origin = BindingOrigin.Card, Label = $"카드 '{card.id}'", IsCard = true,
                        CardHosts = hosts, CardTargetsEnemies = c.TargetsEnemies, Axis = card.axis,
                    },
                    projectiles, patterns, hazards, rows, effects, mine, mods, squad, ref c.DeclaresRetireRecall, view);
            if (legacy && !c.TargetsEnemies && card.attackMods != null)
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
            if (squad.Count > 0) c.SquadBindings = squad.ToArray();
            if (mods.Count > 0) c.AttackMods = mods.ToArray();
            if (mine.Count == 0 && mods.Count == 0 && squad.Count == 0 && !c.DeclaresRetireRecall)
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

        // ── Squad(과도기 — 옛 칸 `effects`) ────────────────────────────────────
        // skill-data-table unit 8 — 새 형식 = 진영 버프 소유 줄(`BindingSpecBuilder` 가 같은 값을 편다). 이 갈래는 이전 전 에셋만 지나고
        // 단계 B(이전 적용 · 옛 칸 제거)에서 은퇴한다.

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

        /// <summary>소유 줄을 든 카드인가.</summary>
        internal static bool HasBindings(DreamcatcherCard card) => card.bindings != null && card.bindings.Length > 0;

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
