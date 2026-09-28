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
            var cards = new List<CardDef>();
            // unit 7c — 카드 줄 번호 → 카드 에셋(아트·문안). 빈 칸을 건너뛰는 **이 순회**가 번호를 매기므로 목록도 여기서 낸다.
            var assets = new List<DreamcatcherCard>();
            if (src.Cards != null)
                for (int i = 0; i < src.Cards.Count; i++)
                {
                    var c = src.Cards[i];
                    if (c == null) { Warn($"덱 {i} 번 카드가 비었다 — 건너뛴다."); continue; }
                    cards.Add(Bake(c, src.Awakening, projectiles, patterns, hazards, rows));
                    assets.Add(c);
                }
            if (cards.Count > 0) def.Cards = cards.ToArray();
            view?.SetCards(assets);
            var match = BakeDreamstones(src.Dreamstones, rows);
            if (match.Length > 0) def.MatchBindings = match;
            def.Bindings = rows.ToArray();
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
                    if (c?.mechanics == null) continue;
                    foreach (var m in c.mechanics)
                        if (m.payload.kind == DcPayloadKind.SpawnHazard && m.payload.hazard != null && !list.Contains(m.payload.hazard))
                            list.Add(m.payload.hazard);
                }
            return list.ToArray();
        }

        // ── 카드 한 장 ────────────────────────────────────────────────────────

        private static CardDef Bake(DreamcatcherCard card, AwakeningConfig awakening, List<ProjectileData> projectiles,
                                    List<ProjectilePatternData> patterns, HazardSO[] hazards, List<BindingDef> rows)
        {
            var c = CardDef.Default();
            c.Id = card.id;
            if (awakening != null) c.Cost = awakening.CostFor(card.type);
            else Error($"'{card.id}': 각성 저작(AwakeningConfig)이 없어 카드 값을 모른다 — 0 으로 둔다.");

            if (card.type == CardType.Active)
            {
                c.Kind = CardKind.Active;
                if (card.skill == null) { Error($"'{card.id}': 액티브인데 SkillData 가 없다 — 시전이 거절된다."); return c; }
                c.CooldownSeconds = card.skill.cooldownSec;
                c.NeedsTwoCells = card.skill.NeedsTwoTiles;
                c.ActiveBinding = BakeActive(card, projectiles, rows);
                return c;
            }

            c.Kind = CardKind.Attach;
            c.Requirement = ToRequirement(card);
            if (card.type == CardType.Squad)
            {
                // Squad 는 effects 만 읽는다(옛 계약 — mechanics 는 Unit 카드만).
                c.SquadBindings = BakeSquad(card, rows);
                return c;
            }

            // Unit — mechanics + attackMods.
            var mine = new List<int>();
            var mods = new List<AttackModDef>();
            c.TargetsEnemies = card.HasBountyMark();
            var mech = card.mechanics ?? System.Array.Empty<DcMechanic>();
            bool aura = false;
            for (int i = 0; i < mech.Length; i++)
            {
                string label = $"카드 '{card.id}' mechanic {i}";
                if (c.TargetsEnemies && mech[i].payload.kind != DcPayloadKind.BountyMark)
                {
                    Warn($"{label}: 적 표식 카드는 표식 메커닉만 쓴다(옛 `ApplyBountyMark`) — {mech[i].payload.kind} 는 건너뛴다.");
                    continue;
                }
                BakeMechanic(in mech[i], label, card, projectiles, patterns, hazards, rows, mine, mods, ref aura, ref c);
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

        private static BindingDef CardRow(string label, TriggerKind trigger, TriggerPayload payload)
        {
            var b = BindingDef.Default();
            b.Label = label;
            b.Trigger = trigger;
            b.Payload = payload;
            b.Origin = BindingOrigin.Card;
            b.Effect = SkillRouting.Resolve(trigger, payload);
            return b;
        }

        private static void Add(List<BindingDef> rows, List<int> mine, in BindingDef b)
        {
            mine.Add(rows.Count);
            rows.Add(b);
        }

        private static void BakeMechanic(in DcMechanic m, string label, DreamcatcherCard card,
                                         List<ProjectileData> projectiles, List<ProjectilePatternData> patterns,
                                         HazardSO[] hazards, List<BindingDef> rows, List<int> mine,
                                         List<AttackModDef> mods, ref bool aura, ref CardDef c)
        {
            var trigger = BindingDefinitionBuilder.ToCoreTrigger(m.trigger.kind);
            var payload = BindingDefinitionBuilder.ToCorePayload(m.payload.kind);
            var p = m.payload;

            // ── 트리거 없음 = 부착되는 순간(3장 + 배치 오라) ──
            if (trigger == TriggerKind.None)
            {
                switch (payload)
                {
                    case TriggerPayload.SelfBuffLethal:
                    {
                        if (p.magnitude <= 0f || p.duration <= 0f) { Warn($"{label}: SelfBuffLethal magnitude/duration <= 0 — 건너뛴다."); return; }
                        var b = CardRow(label, trigger, payload);
                        b.Magnitude = 1f + p.magnitude / 100f;   // % → 배율(도메인은 저작 인코딩을 모른다)
                        b.Duration = p.duration;
                        b.FireCap = 1;
                        Add(rows, mine, in b);
                        return;
                    }
                    case TriggerPayload.DreamCocoon:
                    {
                        if (p.magnitude <= 0f || p.duration <= ProgressiveStates.CocoonEpsilon) { Warn($"{label}: DreamCocoon magnitude <= 0 또는 duration <= ε — 건너뛴다(무수면 즉시 완주)."); return; }
                        if (!MapBuff(p.buffStat, p.magnitude, out var stat, out float mul)) { Warn($"{label}: DreamCocoon 스탯 {p.buffStat} 을 옮길 수 없다 — 건너뛴다."); return; }
                        var b = CardRow(label, trigger, payload);
                        b.StatKind = (int)stat;
                        b.Magnitude = mul;
                        b.Duration = p.duration;
                        b.FireCap = 1;
                        Add(rows, mine, in b);
                        return;
                    }
                    case TriggerPayload.BountyMark:
                    {
                        if (p.magnitude <= 1f) { Warn($"{label}: BountyMark magnitude <= 1(현상금 없음) — 건너뛴다."); return; }
                        if (p.tileRange < 0 || p.tileRange >= 100) { Warn($"{label}: BountyMark tileRange(받는 피해 감소 %) [0,100) 밖 — 건너뛴다."); return; }
                        var b = CardRow(label, trigger, payload);
                        b.Magnitude = p.magnitude;                                     // 각성 배율
                        b.HitThreshold = p.tileRange > 0 ? 1f - p.tileRange / 100f : 0f;   // 받는 피해 배율(0 = 안 건다)
                        b.FireCap = 1;
                        Add(rows, mine, in b);
                        return;
                    }
                    case TriggerPayload.PlacementAura:
                    {
                        if (p.magnitude <= 0f) { Warn($"{label}: PlacementAura magnitude <= 0 — 건너뛴다."); return; }
                        // 카드당 하나(옛 review M1 — 둘째는 핸들이 덮여 누수됐다).
                        if (aura) { Warn($"{label}: 카드당 PlacementAura 는 하나만 — 추가 오라는 건너뛴다."); return; }
                        if (!ToAxis(card.axis, out int mask, out int cost)) { Warn($"{label}: 축 {card.axis} 을 옮길 수 없다 — 건너뛴다."); return; }
                        aura = true;
                        // **규칙 둘**(정정 3 · H6) — 공속은 숙주가 떠나면 소급 회수, 수면은 등록부에서만 빠진다.
                        var speed = CardRow(label + " 공속", TriggerKind.OnPlace, payload);
                        speed.Effect = new SelfStatBuffSkill();
                        speed.Subject = BindingSubject.Any;
                        speed.SubjectClassMask = mask;
                        speed.SubjectCost = cost;
                        speed.StatKind = (int)SkillStatKind.AttackSpeedMul;
                        speed.Magnitude = 1f + p.magnitude / 100f;
                        speed.RevokeOnExpire = true;
                        Add(rows, mine, in speed);
                        if (p.duration > 0f)
                        {
                            var sleep = CardRow(label + " 수면", TriggerKind.OnPlace, payload);
                            sleep.Effect = new PlacementSleepSkill();
                            sleep.Subject = BindingSubject.Any;
                            sleep.SubjectClassMask = mask;
                            sleep.SubjectCost = cost;
                            sleep.Duration = p.duration;
                            sleep.RevokeOnExpire = false;
                            Add(rows, mine, in sleep);
                        }
                        return;
                    }
                    default:
                        Warn($"{label}: 트리거 없음 × {payload} 는 배선되지 않았다 — 건너뛴다.");
                        return;
                }
            }

            if (payload == TriggerPayload.None)
            {
                Warn($"{label}: None 종류 — 건너뛴다.");
                return;
            }
            // 손패 동작(인수인계) — 규칙이 아니라 퇴근 회수 규칙의 선언이다. **퇴근에만**, 게이트 없이.
            if (payload == TriggerPayload.RecallAttachedToFront)
            {
                if (trigger != TriggerKind.OnRetire) { Warn($"{label}: 인수인계는 OnRetire 에만 배선돼 있다(현재 {trigger}) — 건너뛴다."); return; }
                if (m.trigger.gate != DcGateKind.None) { Warn($"{label}: 인수인계에는 게이트가 배선돼 있지 않다 — 건너뛴다."); return; }
                c.DeclaresRetireRecall = true;
                return;
            }
            // unified-effect-layer unit 5 — 조합은 **검증 한 함수**(출처는 입력이 아니다). 카드는 숙주가 **놓인 뒤** 붙는다 —
            // 자기 배치는 이미 지났다. 숙주는 부착 때 정해져 가디언 여부를 여기서 모른다(부착 판정 몫).
            if (!BindingDefinitionBuilder.CheckCombo(BindingDefinitionBuilder.ComboOf(in m, trigger, payload, hostIsEnemy: false,
                                                     bindsAfterPlacement: true, hostCannotHoldAggro: false), label)) return;
            var gate = BindingDefinitionBuilder.ToCoreGate(m.trigger.gate);
            var gateSubject = BindingDefinitionBuilder.ToCoreGateSubject(m.trigger.gateSubject);
            if (!BindingDefinitionBuilder.TriggerValuesValid(in m, trigger, gate, gateSubject, label)) return;

            // 강공 — 어휘 밖(그 공격의 성질). 공격 수식자로 접는다.
            if (payload == TriggerPayload.HeavyStrike)
            {
                if (BindingDefinitionBuilder.TryHeavyStrike(in m, trigger, gate, label, out var heavy)) mods.Add(heavy);
                return;
            }

            var r = CardRow(label, trigger, payload);
            r.Period = Mathf.Clamp(m.trigger.period, 0, ushort.MaxValue);
            r.PeriodSeconds = m.trigger.periodSeconds;
            r.Fraction = m.trigger.fraction;
            r.Gate = gate;
            r.GateSubject = gateSubject;
            r.GateValue = m.trigger.gateValue;
            r.Magnitude = p.magnitude;
            r.TileRange = Mathf.Max(0, p.tileRange);
            r.Duration = Mathf.Max(0f, p.duration);
            r.CcKind = (int)BindingDefinitionBuilder.ToSkillCc(p.ccKind);
            r.StackKind = (int)BindingDefinitionBuilder.ToSkillStack(p.stackKind);
            r.StatKind = (int)(BindingDefinitionBuilder.TryToSkillStat(p.buffStat, out var st) ? st : SkillStatKind.DamageMul);
            float cone = Mathf.Cos(Mathf.Deg2Rad * Mathf.Max(0f, p.coneHalfAngleDeg));
            r.ConeHalfAngleDeg = p.coneHalfAngleDeg;
            r.ConeCosSq = cone * cone;

            // 값 가드 · 표 참조는 두 빌더 공용(`BindingDefinitionBuilder.BindPayload`). 아래 둘은 **카드 저작 인코딩**이다(H4 후속).
            if (!BindingDefinitionBuilder.BindPayload(ref r, in m, label, projectiles, patterns, hazards)) return;
            switch (payload)
            {
                case TriggerPayload.SelfTileAoe:
                    r.VisualScale = p.projectile.visualScale;   // 카드는 착탄 연출 배율을 싣는다(유닛 bake 는 0)
                    break;
                case TriggerPayload.SelfStatBuff:
                {
                    // 카드 버프는 % 저작 → 배율(유닛 저작은 배율 그대로).
                    if (!MapBuff(p.buffStat, p.magnitude, out var stat, out float mul)) { Warn($"{label}: SelfStatBuff 스탯 {p.buffStat} 을 옮길 수 없다 — 건너뛴다."); return; }
                    // 최대 중첩(tileRange > 0)은 배율 > 1 에서만 성립한다(곱셈 버킷 값을 더하면 뜻이 뒤집힌다).
                    if (p.tileRange > 0 && mul <= 1f) { Warn($"{label}: SelfStatBuff 최대 중첩은 배율 > 1 에서만 — 건너뛴다."); return; }
                    r.StatKind = (int)stat;
                    r.Magnitude = mul;
                    break;
                }
            }

            if (r.Effect == null) { Warn($"{label}: '{trigger} × {payload}' 조합에 라우팅이 없다 — 건너뛴다."); return; }
            Add(rows, mine, in r);
        }

        // ── Squad ─────────────────────────────────────────────────────────────

        private static int[] BakeSquad(DreamcatcherCard card, List<BindingDef> rows)
        {
            var mine = new List<int>();
            if (!ToAxis(card.axis, out int mask, out int cost)) { Error($"'{card.id}': 축 {card.axis} 을 옮길 수 없다 — 효과 없음."); return null; }
            var effects = card.effects ?? System.Array.Empty<CardEffect>();
            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                // `CostRate` 는 유닛 스탯이 아니다 — 카드 경로에서는 옛 전투도 무동작이었다(드림스톤 전용 · 판 진입 배율).
                if (!MapBuff(e.kind, e.percent, out var stat, out float mul)) { Warn($"'{card.id}' effect {i}: {e.kind} 는 카드 스탯이 아니다 — 건너뛴다."); continue; }
                var b = CardRow($"카드 '{card.id}' effect {i}", TriggerKind.OnPlace, TriggerPayload.SelfStatBuff);
                b.Subject = BindingSubject.Any;
                b.SubjectClassMask = mask;
                b.SubjectCost = cost;
                b.StatKind = (int)stat;
                b.Magnitude = mul;
                b.RevokeOnExpire = true;   // 숙주가 떠나면(사망 ∪ 퇴근) 판 전체에서 소급 회수(정정 1)
                Add(rows, mine, in b);
            }
            return mine.Count > 0 ? mine.ToArray() : null;
        }

        private static int[] BakeDreamstones(IReadOnlyList<DreamstoneData> stones, List<BindingDef> rows)
        {
            var mine = new List<int>();
            if (stones == null) return mine.ToArray();
            for (int i = 0; i < stones.Count; i++)
            {
                var s = stones[i];
                if (s == null || s.effect.kind == CardBuffKind.CostRate) continue;   // 코스트 돌은 `CostRateOf`
                if (!MapBuff(s.effect.kind, s.effect.percent, out var stat, out float mul)) { Warn($"드림스톤 '{s.id}': {s.effect.kind} 를 옮길 수 없다 — 건너뛴다."); continue; }
                var b = BindingDef.Default();
                b.Label = $"드림스톤 '{s.id}'";
                b.Trigger = TriggerKind.OnPlace;
                b.Subject = BindingSubject.Any;          // 축 All(옛 `MatchesDcAxis(All)`)
                b.Effect = new DreamstoneStatSkill();
                b.StatKind = (int)stat;
                b.Magnitude = mul;
                b.Lifetime = BindingLifetime.Match;
                b.RevokeOnExpire = true;                 // 칸 판별자 = 이 규칙(돌마다 새 칸 — 옛 `_dcStackCounter++`)
                b.Origin = BindingOrigin.Match;
                mine.Add(rows.Count);
                rows.Add(b);
            }
            return mine.ToArray();
        }

        // ── 액티브 ────────────────────────────────────────────────────────────

        private static int BakeActive(DreamcatcherCard card, List<ProjectileData> projectiles, List<BindingDef> rows)
        {
            var s = card.skill;
            var b = BindingDef.Default();
            b.Label = $"액티브 '{card.id}'";
            b.Trigger = TriggerKind.None;
            b.Origin = BindingOrigin.Card;
            b.FireCap = 1;
            b.Lifetime = BindingLifetime.UntilFireCap;
            b.Magnitude = s.magnitude;
            b.Duration = s.durationSec;
            b.TileRange = SkillMath.RangeToTiles(s.range);
            int id;
            switch (s.effect)
            {
                case SkillEffectType.SlowField: id = TileStatBurstSkill.Id; b.StatKind = (int)SkillStatKind.MoveSpeedMul; break;
                case SkillEffectType.Tornado: id = PullFieldSkill.Id; break;
                case SkillEffectType.Meteor:
                    id = TileMeteorSkill.Id;
                    // ⚠ 저작 오류라도 시전은 성공이다(옛 동작) — 떨어질 것이 없을 뿐 값·대기는 소모된다.
                    if (s.projectile == null) Error($"'{card.id}': 메테오에 탄(ProjectileData)이 없다 — 시전해도 아무것도 안 떨어진다.");
                    else { b.DataIndex = CombatDefinitionBuilder.IndexOf(projectiles, s.projectile); b.VisualScale = s.projectile.visualScale; }
                    b.Duration = s.warningSec > 0f ? s.warningSec : 0f;   // 메테오만 지속이 **낙하 예고**다
                    break;
                // 아군 버프는 시간제 장판이다(두 갈래가 같은 concrete — 스탯만 다르다).
                case SkillEffectType.PowerSurge: id = AllyBuffFieldSkill.Id; b.StatKind = (int)SkillStatKind.DamageMul; break;
                case SkillEffectType.RapidFire: id = AllyBuffFieldSkill.Id; b.StatKind = (int)SkillStatKind.AttackSpeedMul; break;
                case SkillEffectType.Portal:
                    id = PortalSkill.Id;
                    if (!s.NeedsTwoTiles) Warn($"'{card.id}': 포탈인데 두 칸 조준(needsTwoTiles)이 꺼져 있다 — 출구가 없어 발동해도 아무 일이 없다.");
                    break;
                default:
                    Error($"'{card.id}': 모르는 액티브 효과 {s.effect} — 시전이 거절된다.");
                    return -1;
            }
            if (!SkillRouting.Registry.TryGet(id, out var skill)) { Error($"'{card.id}': 액티브 실행자({id})가 레지스트리에 없다."); return -1; }
            b.Effect = skill;
            rows.Add(b);
            return rows.Count - 1;
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
