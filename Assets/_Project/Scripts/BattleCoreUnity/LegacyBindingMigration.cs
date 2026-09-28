using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Wassup.BattleCore.Trigger;
using Wassup.Data;
using Wassup.Skills;

namespace Wassup.BattleCoreUnity
{
    // skill-data-table unit 4 — **옛 저작 → 새 저작 형식 이전 계획**(에셋을 쓰지 않는다 · 에디터 API 없음).
    //
    // 옛 저장처 넷(카드 `mechanics` + 액티브 `skill` · 방어유닛 `UnitSkillAbility` + `ShieldCastAbility` · 적 `nightmareMechanics`)을 읽어
    // 소유자마다 「소유 줄(`BindingSpec`) + 효과 한 줄(`EffectValues` + 참조)」 계획을 낸다. 에디터 메뉴가 이 계획을 dry-run 표로 쓰거나
    // 에셋으로 옮기고, 테스트는 같은 계획을 **메모리 사본**에 입혀 옛 굽기와 새 굽기를 대조한다(이전 전 증명).
    //
    // 규칙(README · `tables.md`): U13 — **병합 없음**(옛 규칙 한 줄 = 효과 한 줄) · U14 — 실드 캐스트 반경 = 오늘 사거리 파생값을 고정값으로 ·
    // U10 — 발사 명세 · 길막 · 장판 피해를 효과 줄로 · CC · 스택 정수 변환 · 분열 = 적 고유 값 · 효과 id = `tables.md` §10.
    // 공격 수식자 · 스쿼드 스탯 효과는 이미 카드 자식 값이라 옮기지 않는다(표에 그대로 적는다).
    //
    // 이 파일은 이전 과도기 전용이다 — 옛 칸이 걷히면(4-정리) 같이 지운다.
    public static class LegacyBindingMigration
    {
        public const string EffectFolder = "Assets/_Project/Data/Effects";

        public sealed class RowPlan
        {
            public int Slot;
            /// <summary>옛 자리(`mechanics[2]` · `ShieldCastAbility` · `skill` …).</summary>
            public string Source;
            public string OldLabel;
            public string NewLabel;
            public string EffectId;
            public TriggerSpec Trigger;
            public int FireCap;
            public EffectValues Values;
            public ProjectileData Projectile;
            public ProjectilePatternData Pattern;
            public HazardSO Hazard;
            public GameObject AuraPrefab;
            public float AuraScale;
            public StackModifierSO StackModifier;
            public readonly List<string> Notes = new List<string>();
            public string AssetPath => $"{EffectFolder}/Effect_{EffectId}.asset";
        }

        public sealed class OwnerPlan
        {
            /// <summary>`card` · `unit` · `enemy`(시트 `owner_kind` 초안 — 이름은 unit 5 에서 묻는다).</summary>
            public string OwnerKind;
            public string OwnerId;
            public ScriptableObject Owner;
            public readonly List<RowPlan> Rows = new List<RowPlan>();
            public readonly List<string> Notes = new List<string>();
            // 소유자 고유 값(쓸 때만 뜻이 있다).
            public bool WritesHostKinds;
            public HostKinds HostKinds;
            public bool WritesActive;
            public float CooldownSec;
            public bool NeedsTwoTiles;
            public bool WritesSplit;
            public AttackUnitData SplitUnit;
            public int SplitCount;
        }

        public sealed class Plan
        {
            public readonly List<OwnerPlan> Owners = new List<OwnerPlan>();
            public readonly List<string> Notes = new List<string>();
        }

        private static readonly Regex IdRule = new Regex("^[a-z][a-z0-9_]*$");

        /// <summary>이전 계획. 입력 순서가 표 순서다(부르는 쪽이 경로 순으로 넘긴다).</summary>
        public static Plan Build(IEnumerable<DreamcatcherCard> cards, IEnumerable<DefenderUnitData> units, IEnumerable<AttackUnitData> enemies)
        {
            var plan = new Plan();
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            if (cards != null) foreach (var c in cards) if (c != null) PlanCard(plan, c, usedIds);
            if (units != null) foreach (var u in units) if (u != null) PlanUnit(plan, u, usedIds);
            if (enemies != null) foreach (var e in enemies) if (e != null) PlanEnemy(plan, e, usedIds);
            return plan;
        }

        // ── 카드 ──────────────────────────────────────────────────────────

        private static void PlanCard(Plan plan, DreamcatcherCard card, HashSet<string> used)
        {
            if (CardDefinitionBuilder.HasBindings(card)) { plan.Notes.Add($"카드 '{card.id}' 는 이미 새 형식 — 건너뛴다."); return; }
            var o = new OwnerPlan { OwnerKind = "card", OwnerId = card.id, Owner = card };
            if (card.type == CardType.Active)
            {
                var s = card.skill;
                if (s == null) { o.Notes.Add("액티브인데 SkillData 가 없다 — 옮길 것이 없다(옛 굽기도 시전 거절)."); plan.Owners.Add(o); return; }
                var r = new RowPlan
                {
                    Slot = 0, Source = "skill", OldLabel = $"액티브 '{card.id}'", NewLabel = $"액티브 '{card.id}'",
                    Trigger = new TriggerSpec { kind = TriggerKind.Cast }, FireCap = 1, Projectile = s.projectile,
                };
                var kind = CardDefinitionBuilder.ActiveKindOf(s.effect);
                r.Values = EffectSlots.FromActive(kind, SkillMath.RangeToTiles(s.range), s.magnitude, s.durationSec, s.warningSec, r.Notes);
                if (kind == EffectKind.ActiveMeteor && s.durationSec != 0f)
                    r.Notes.Add($"메테오 durationSec {Num(s.durationSec)} 는 옛 굽기도 버렸다(지속 = 낙하 예고 warningSec) — 옮기지 않음");
                if (kind != EffectKind.ActiveMeteor && s.projectile != null) r.Notes.Add("탄 참조는 메테오만 읽는다 — 옮기되 굽기가 무시");
                r.EffectId = UniqueId(Sanitize(s.id, r.Notes), used, r.Notes);
                o.Rows.Add(r);
                o.WritesActive = true;
                o.CooldownSec = s.cooldownSec;
                o.NeedsTwoTiles = s.needsTwoTiles;
                o.Notes.Add($"SkillData 의 표시 칸(displayName · description · uiTint · cost={s.cost})은 옮기지 않는다(`tables.md` §11 밖 — cost 는 문안만 읽는다)");
                plan.Owners.Add(o);
                return;
            }
            if (card.type == CardType.Squad)
            {
                if (card.mechanics != null && card.mechanics.Length > 0)
                    o.Notes.Add($"Squad 카드의 mechanics {card.mechanics.Length} 줄은 옛 굽기도 안 읽었다 — 옮기지 않음");
                if (card.effects != null && card.effects.Length > 0)
                    o.Notes.Add($"스쿼드 스탯 효과 {card.effects.Length} 줄 = 카드 자식 값(`CardStatEffects`) — 그대로");
                if (o.Notes.Count > 0) plan.Owners.Add(o);
                return;
            }
            // Unit
            o.WritesHostKinds = true;
            o.HostKinds = card.HasBountyMark() ? HostKinds.Enemy : HostKinds.Defender;
            if (o.HostKinds == HostKinds.Enemy) o.Notes.Add("적 표식 카드 → hostKinds = Enemy(옛 `HasBountyMark()` 파생을 값으로)");
            if (card.attackMods != null && card.attackMods.Length > 0)
                o.Notes.Add($"공격 수식자 {card.attackMods.Length} 줄 = 카드 자식 값(`CardAttackMods`) — 그대로");
            var mech = card.mechanics ?? Array.Empty<DcMechanic>();
            string baseId = Sanitize(card.id, o.Notes);
            for (int i = 0; i < mech.Length; i++)
            {
                var r = FromMechanic(in mech[i], i, i, cardOwner: true, $"카드 '{card.id}'", $"카드 '{card.id}'");
                r.EffectId = UniqueId(mech.Length > 1 ? $"{baseId}_{i}" : baseId, used, r.Notes);
                o.Rows.Add(r);
            }
            plan.Owners.Add(o);
        }

        // ── 방어유닛 ──────────────────────────────────────────────────────

        private static void PlanUnit(Plan plan, DefenderUnitData d, HashSet<string> used)
        {
            if (d.bindings != null && d.bindings.Length > 0) { plan.Notes.Add($"방어유닛 '{d.id}' 는 이미 새 형식 — 건너뛴다."); return; }
            var skill = d.GetAbility<UnitSkillAbility>();
            var shield = d.GetAbility<ShieldCastAbility>();
            var mech = skill?.mechanics ?? Array.Empty<DcMechanic>();
            if (mech.Length == 0 && shield == null) return;
            var o = new OwnerPlan { OwnerKind = "unit", OwnerId = d.id, Owner = d };
            string abilityId = Sanitize(!string.IsNullOrEmpty(skill?.id) ? skill.id : d.id, o.Notes);
            for (int i = 0; i < mech.Length; i++)
            {
                var r = FromMechanic(in mech[i], i, i, cardOwner: false, d.name, d.name);
                r.Source = $"UnitSkillAbility '{skill.name}'.mechanics[{i}]";
                r.EffectId = UniqueId(mech.Length > 1 ? $"{abilityId}_{i}" : abilityId, used, r.Notes);
                o.Rows.Add(r);
            }
            if (shield != null)
            {
                if (shield.cooldown <= 0f || shield.amount <= 0f)
                    o.Notes.Add($"ShieldCastAbility '{shield.name}' cooldown/amount <= 0 — 옛 굽기도 건너뛰었다(옮기지 않음)");
                else
                {
                    int slot = o.Rows.Count;
                    int radius = SkillMath.RangeToTiles(d.attackRange);
                    var r = new RowPlan
                    {
                        Slot = slot, Source = $"ShieldCastAbility '{shield.name}'",
                        OldLabel = d.name + " 실드 캐스트", NewLabel = $"{d.name} mechanic {slot}",
                        Trigger = new TriggerSpec { kind = TriggerKind.PeriodicTimer, periodSeconds = shield.cooldown },
                        Values = EffectSlots.FromShieldCast(shield.amount, shield.targetCount, shield.filter, radius),
                    };
                    r.Notes.Add($"U14 — 실드 반경 = 사거리 {Num(d.attackRange)} 파생 {radius}칸을 **고정값**으로(사거리를 바꿔도 안 따라간다)");
                    r.Notes.Add("라벨 변화(해시 밖 · 굽기 스냅샷 텍스트): 「실드 캐스트」 → 「mechanic N」");
                    r.Notes.Add("해시 변화 1칸: coneSinCos 0,0 → 0,1(옛 전용 굽기가 반각을 안 구웠다 · 실드는 반각을 안 읽는다 — 동작 무변)");
                    string sid = Sanitize(!string.IsNullOrEmpty(shield.id) ? shield.id : abilityId + "_shield", r.Notes);
                    r.EffectId = UniqueId(sid, used, r.Notes);
                    o.Rows.Add(r);
                }
            }
            plan.Owners.Add(o);
        }

        // ── 적 ────────────────────────────────────────────────────────────

        private static void PlanEnemy(Plan plan, AttackUnitData e, HashSet<string> used)
        {
            if (e.bindings != null && e.bindings.Length > 0) { plan.Notes.Add($"적 '{e.id}' 는 이미 새 형식 — 건너뛴다."); return; }
            var mech = e.nightmareMechanics;
            if (mech == null || mech.Length == 0) return;
            var o = new OwnerPlan { OwnerKind = "enemy", OwnerId = e.id, Owner = e };
            string baseId = Sanitize(e.id, o.Notes);
            int slot = 0;
            for (int i = 0; i < mech.Length; i++)
            {
                if (mech[i].payload.kind == EffectKind.SplitOnDeath)
                {
                    if (mech[i].trigger.kind != TriggerKind.OnDeath) o.Notes.Add($"mechanics[{i}] 분열인데 트리거가 {mech[i].trigger.kind} — 옛 굽기는 OnDeath 만 분열로 읽었다(옮기되 확인 필요)");
                    if (o.WritesSplit) { o.Notes.Add($"mechanics[{i}] 분열이 둘 이상 — 첫째만 옮긴다(옛 `SplitChain` 도 첫째만 읽었다)"); continue; }
                    o.WritesSplit = true;
                    o.SplitUnit = mech[i].payload.splitUnit;
                    o.SplitCount = (int)mech[i].payload.magnitude;
                    if (o.SplitCount != mech[i].payload.magnitude) o.Notes.Add($"분열 자식 수 {Num(mech[i].payload.magnitude)} 비정수 — {o.SplitCount} 로 자른다(손실)");
                    o.Notes.Add($"mechanics[{i}] SplitOnDeath → 적 고유 값 splitUnit = {(o.SplitUnit != null ? o.SplitUnit.name : "null")} · splitCount = {o.SplitCount}");
                    if (i < mech.Length - 1) o.Notes.Add("라벨 번호 변화(해시 밖): 분열 뒤 줄의 「mechanic N」이 하나씩 당겨진다");
                    continue;
                }
                var r = FromMechanic(in mech[i], slot, i, cardOwner: false, e.name, e.name);
                r.EffectId = UniqueId($"{baseId}_{slot}", used, r.Notes);
                o.Rows.Add(r);
                slot++;
            }
            plan.Owners.Add(o);
        }

        // ── 옛 메커닉 한 줄 → 소유 줄 + 효과 ───────────────────────────────

        private static RowPlan FromMechanic(in DcMechanic m, int slot, int oldIndex, bool cardOwner, string oldLabelHead, string newLabelHead)
        {
            var p = m.payload;
            var r = new RowPlan
            {
                Slot = slot, Source = $"mechanics[{oldIndex}]",
                OldLabel = $"{oldLabelHead} mechanic {oldIndex}", NewLabel = $"{newLabelHead} mechanic {slot}",
                Trigger = m.trigger,
                FireCap = p.kind == EffectKind.UltimateLeap ? 1 : 0,   // 옛 빌더가 종류로 박던 「생존당 1회」를 칸으로(`tables.md` §4)
                Projectile = p.projectile, Pattern = p.pattern, Hazard = p.hazard,
                AuraPrefab = p.auraPrefab, AuraScale = p.auraScale, StackModifier = p.stackModifier,
            };
            float moved = MovedDamage(in p, r.Notes);
            r.Values = EffectSlots.FromLegacy(ToLegacyPayload(in p), cardOwner, moved, r.Notes);
            if (r.OldLabel != r.NewLabel) r.Notes.Add($"라벨 변화(해시 밖): 「{r.OldLabel}」 → 「{r.NewLabel}」");
            if (p.splitUnit != null) r.Notes.Add("splitUnit 참조는 분열 전용 — 이 종류에선 버림");
            if (!cardOwner && (p.kind == EffectKind.SelfBuffLethal || p.kind == EffectKind.DreamCocoon
                               || p.kind == EffectKind.BountyMark || p.kind == EffectKind.PlacementAura))
                r.Notes.Add($"{p.kind} 는 부착 즉시(카드) 효과 — 유닛·적 소유면 옛 굽기도 거절했다");
            return r;
        }

        /// <summary>옛 굽기가 효과 **밖**(명세 · 길막 · 장판 SO)에서 읽던 피해 — 굽기와 같은 산식(`BindPattern` · `TryDotDamage`).</summary>
        private static float MovedDamage(in DcPayloadSpec p, List<string> notes)
        {
            if (p.kind == EffectKind.EmitProjectilePattern)
            {
                if (p.pattern == null) { notes.Add("발사 명세가 비었다 — 옮길 피해 0(옛 굽기도 거절)"); return 0f; }
                var blocker = p.pattern.barrel != null ? p.pattern.barrel.spawnBlocker : null;
                if (blocker != null)
                {
                    notes.Add($"U10 — 길막 '{blocker.name}' 폭발 피해 {Num(blocker.explodeDamage)} → 효과 damage(명세 damage {Num(p.pattern.damage)} 는 안 쓰였다)");
                    return Mathf.Max(0f, blocker.explodeDamage);
                }
                notes.Add($"U10 — 명세 '{p.pattern.name}' damage {Num(p.pattern.damage)} → 효과 damage");
                return p.pattern.damage;
            }
            if (p.kind == EffectKind.SpawnHazard)
            {
                if (p.hazard == null) { notes.Add("장판이 비었다 — 옮길 피해 0(옛 굽기도 거절)"); return 0f; }
                if (!BoardEffectDefinitionBuilder.TryDotDamage(p.hazard, out float dot))
                { notes.Add($"장판 '{p.hazard.name}' DoT 가 둘 이상 — 옛 굽기도 거절(옮길 피해 0)"); return 0f; }
                notes.Add($"U10 — 장판 '{p.hazard.name}' DoT {Num(dot)} → 효과 damage");
                return dot;
            }
            return 0f;
        }

        public static LegacyPayload ToLegacyPayload(in DcPayloadSpec p) => new LegacyPayload
        {
            Kind = p.kind,
            Magnitude = p.magnitude,
            TileRange = p.tileRange,
            Duration = p.duration,
            SlamDamage = p.slamDamage,
            SlamTileRange = p.slamTileRange,
            TickIntervalSec = p.tickIntervalSec,
            OrbitCount = p.orbitCount,
            ConeHalfAngleDeg = p.coneHalfAngleDeg,
            DcCcKind = (int)p.ccKind,
            DcStackKind = (int)p.stackKind,
            BuffStat = p.buffStat,
            Telegraph = p.telegraph,
        };

        // ── id ────────────────────────────────────────────────────────────

        private static string Sanitize(string raw, List<string> notes)
        {
            string id = (raw ?? "").Trim().ToLowerInvariant();
            id = Regex.Replace(id, "[^a-z0-9_]", "_");
            if (id.Length == 0 || !char.IsLetter(id[0])) id = "e_" + id;
            if (id != raw) notes.Add($"id '{raw}' → '{id}'(스네이크 규칙 `^[a-z][a-z0-9_]*$`)");
            return id;
        }

        private static string UniqueId(string id, HashSet<string> used, List<string> notes)
        {
            string u = id;
            for (int n = 2; !used.Add(u); n++) u = $"{id}_{n}";
            if (u != id) notes.Add($"id '{id}' 충돌 → '{u}'");
            if (!IdRule.IsMatch(u)) notes.Add($"id '{u}' 가 규칙 밖");
            return u;
        }

        // ── 적용(메모리) ──────────────────────────────────────────────────

        /// <summary>계획의 효과 한 줄 → 효과 SO(메모리 · 에셋 아님). 에디터 메뉴는 이것을 에셋으로 만든다.</summary>
        public static EffectData Materialize(RowPlan r)
        {
            var e = ScriptableObject.CreateInstance<EffectData>();
            e.name = "Effect_" + r.EffectId;
            e.id = r.EffectId;
            e.values = r.Values;
            e.projectile = r.Projectile;
            e.pattern = r.Pattern;
            e.hazard = r.Hazard;
            e.auraPrefab = r.AuraPrefab;
            e.auraScale = r.AuraScale;
            e.stackModifier = r.StackModifier;
            return e;
        }

        /// <summary>
        /// 계획을 소유자 **대상 객체**에 입힌다(새 칸만 쓴다 — 옛 칸은 그대로). `target` 은 계획의 소유자이거나 그 사본(테스트).
        /// `effectFor` = 줄 하나의 효과 SO(메모리 사본 또는 만든 에셋).
        /// </summary>
        public static void Apply(OwnerPlan o, ScriptableObject target, Func<RowPlan, EffectData> effectFor)
        {
            var rows = new BindingSpec[o.Rows.Count];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = new BindingSpec { trigger = o.Rows[i].Trigger, fireCap = o.Rows[i].FireCap, effect = effectFor(o.Rows[i]) };
            switch (target)
            {
                case DreamcatcherCard c:
                    if (rows.Length > 0) c.bindings = rows;
                    if (o.WritesHostKinds) c.hostKinds = o.HostKinds;
                    if (o.WritesActive) { c.cooldownSec = o.CooldownSec; c.needsTwoTiles = o.NeedsTwoTiles; }
                    break;
                case DefenderUnitData d:
                    if (rows.Length > 0) d.bindings = rows;
                    break;
                case AttackUnitData e:
                    if (rows.Length > 0) e.bindings = rows;
                    if (o.WritesSplit) { e.splitUnit = o.SplitUnit; e.splitCount = o.SplitCount; }
                    break;
            }
        }

        // ── dry-run 표 ────────────────────────────────────────────────────

        /// <summary>계획 → 마크다운 표(소유자 → 옛 규칙 → 새 효과 id · 소유 줄 값 · 깃발).</summary>
        public static string Report(Plan plan, Func<ScriptableObject, string> pathOf)
        {
            var sb = new StringBuilder();
            int rows = 0, flagged = 0;
            foreach (var o in plan.Owners) foreach (var r in o.Rows) { rows++; if (r.Notes.Exists(IsFlag)) flagged++; }
            sb.Append("# skill-data-table unit 4 — 이전 dry-run 표\n\n");
            sb.Append($"소유자 {plan.Owners.Count} · 효과 줄 {rows}(병합 0 — U13) · 깃발 달린 줄 {flagged}\n\n");
            sb.Append("깃발 = 손실 · 해시 변화 · 확인 필요. 「라벨 변화」는 해시 밖(굽기 스냅샷 텍스트만).\n\n");
            foreach (var n in plan.Notes) sb.Append("- ").Append(n).Append('\n');
            foreach (var o in plan.Owners)
            {
                sb.Append($"\n## {o.OwnerKind} `{o.OwnerId}` — {pathOf?.Invoke(o.Owner) ?? o.Owner?.name}\n\n");
                if (o.WritesHostKinds) sb.Append($"- hostKinds = {o.HostKinds}\n");
                if (o.WritesActive) sb.Append($"- cooldown_sec = {Num(o.CooldownSec)} · needs_two_tiles = {(o.NeedsTwoTiles ? 1 : 0)}\n");
                if (o.WritesSplit) sb.Append($"- split_unit = {(o.SplitUnit != null ? o.SplitUnit.id : "null")} · split_count = {o.SplitCount}\n");
                foreach (var n in o.Notes) sb.Append("- ").Append(n).Append('\n');
                if (o.Rows.Count == 0) continue;
                sb.Append("\n| slot | 옛 자리 | effect_id | kind | 소유 줄(trigger · 값) | 효과 값 | 깃발 |\n|---|---|---|---|---|---|---|\n");
                foreach (var r in o.Rows)
                {
                    sb.Append("| ").Append(r.Slot).Append(" | ").Append(r.Source).Append(" | `").Append(r.EffectId).Append("` | ")
                      .Append(r.Values.kind).Append(" | ").Append(TriggerText(r)).Append(" | ").Append(ValuesText(r)).Append(" | ")
                      .Append(string.Join("<br>", r.Notes)).Append(" |\n");
                }
            }
            return sb.ToString();
        }

        private static bool IsFlag(string note) => note.Contains("손실") || note.Contains("해시 변화") || note.Contains("확인") || note.Contains("버림") || note.Contains("충돌") || note.Contains("규칙 밖");

        private static string TriggerText(RowPlan r)
        {
            var t = r.Trigger;
            var parts = new List<string> { t.kind.ToString() };
            if (t.period != 0) parts.Add($"period {t.period}");
            if (t.periodSeconds != 0f) parts.Add($"period_sec {Num(t.periodSeconds)}");
            if (t.fraction != 0f) parts.Add($"fraction {Num(t.fraction)}");
            if (t.subject != BindingSubject.Self) parts.Add($"subject {t.subject}");
            if (t.gate != GateKind.None) parts.Add($"gate {t.gate}/{t.gateSubject} {Num(t.gateValue)}");
            if (r.FireCap != 0) parts.Add($"fire_cap {r.FireCap}");
            return string.Join(" · ", parts);
        }

        private static string ValuesText(RowPlan r)
        {
            var v = r.Values;
            var parts = new List<string>();
            void F(string name, float x) { if (x != 0f) parts.Add($"{name} {Num(x)}"); }
            F("damage", v.damage); F("shield", v.shield); F("percent", v.percent); F("mul", v.mul); F("count", v.count);
            F("radius_tiles", v.radiusTiles); F("range_tiles", v.rangeTiles); F("duration_sec", v.durationSec); F("flight_sec", v.flightSec);
            F("tick_sec", v.tickSec); F("stack_cap", v.stackCap); F("speed", v.speed); F("cone_half_deg", v.coneHalfDeg);
            F("density_radius_tiles", v.densityRadiusTiles); F("landing_ring_tiles", v.landingRingTiles);
            if (UsesCc(v.kind)) parts.Add($"cc_kind {v.ccKind}");
            if (UsesStack(v.kind)) parts.Add($"stack_kind {v.stackKind}");
            if (UsesBuff(v.kind)) parts.Add($"buff_stat {v.buffStat}");
            if (v.kind == EffectKind.GrantShield && (v.includesSelf || v.shieldFilter != ShieldTargetFilter.Self)) parts.Add($"shield_filter {v.shieldFilter} · includes_self {(v.includesSelf ? 1 : 0)}");
            if (v.telegraph) parts.Add("telegraph 1");
            if (r.Projectile != null) parts.Add($"projectile_id {r.Projectile.id}");
            if (r.Pattern != null) parts.Add($"pattern_id {r.Pattern.id}");
            if (r.Hazard != null) parts.Add($"hazard_id {r.Hazard.name}");
            if (r.AuraPrefab != null) parts.Add($"(뷰) aura {r.AuraPrefab.name}");
            return parts.Count > 0 ? string.Join(" · ", parts) : "—";
        }

        private static bool UsesCc(EffectKind k) => k == EffectKind.ApplyCcToTarget || k == EffectKind.AreaCc;
        private static bool UsesStack(EffectKind k) => k == EffectKind.ApplyStackToTarget || k == EffectKind.AreaApplyStack;
        private static bool UsesBuff(EffectKind k)
            => k == EffectKind.SelfStatBuff || k == EffectKind.DreamCocoon || k == EffectKind.AllyStatAura || k == EffectKind.OpponentStatAura;

        private static string Num(float x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
