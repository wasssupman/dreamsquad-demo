using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Wassup.BattleCore.Trigger;

namespace Wassup.Data.StatImport
{
    /// <summary>
    /// skill-data-table unit 5 — 임포터가 id 로 찾는 표들(`tables.md` §10 — 표마다 네임스페이스). 에디터는 에셋 스캔으로,
    /// 런타임 refresher 는 들고 있는 소유자에서 닿는 것으로 채운다(`FromOwners`). **없는 id 는 만들지 않는다** — 보고만 한다.
    /// </summary>
    public sealed class SkillSheetIndex
    {
        public Dictionary<string, EffectData> Effects = new Dictionary<string, EffectData>();
        public Dictionary<string, ProjectileData> Projectiles = new Dictionary<string, ProjectileData>();
        public Dictionary<string, ProjectilePatternData> Patterns = new Dictionary<string, ProjectilePatternData>();
        public Dictionary<string, HazardSO> Hazards = new Dictionary<string, HazardSO>();   // 키 = 에셋 이름
        public Dictionary<string, DreamcatcherCard> Cards = new Dictionary<string, DreamcatcherCard>();
        public Dictionary<string, DefenderUnitData> Defenders = new Dictionary<string, DefenderUnitData>();
        public Dictionary<string, AttackUnitData> Enemies = new Dictionary<string, AttackUnitData>();

        public static SkillSheetIndex Build(IEnumerable<EffectData> effects, IEnumerable<ProjectileData> projectiles,
            IEnumerable<ProjectilePatternData> patterns, IEnumerable<HazardSO> hazards, IEnumerable<DreamcatcherCard> cards,
            IEnumerable<DefenderUnitData> defenders, IEnumerable<AttackUnitData> enemies, StringBuilder log)
        {
            return new SkillSheetIndex
            {
                Effects = UnitStatApplier.BuildIndex(effects, so => so.id, log, nameof(EffectData)),
                Projectiles = UnitStatApplier.BuildIndex(projectiles, so => so.id, log, nameof(ProjectileData)),
                Patterns = UnitStatApplier.BuildIndex(patterns, so => so.id, log, nameof(ProjectilePatternData)),
                Hazards = UnitStatApplier.BuildIndex(hazards, so => so.name, log, nameof(HazardSO)),
                Cards = UnitStatApplier.BuildIndex(cards, so => so.id, log, nameof(DreamcatcherCard)),
                Defenders = UnitStatApplier.BuildIndex(defenders, so => so.id, log, nameof(DefenderUnitData)),
                Enemies = UnitStatApplier.BuildIndex(enemies, so => so.id, log, nameof(AttackUnitData)),
            };
        }

        /// <summary>
        /// 런타임(에셋 스캔 없음) — 소유자의 소유 줄이 가리키는 효과와, 그 효과들이 가리키는 탄 · 패턴 · 장판만 안다.
        /// ⚠ 그래서 런타임 refresh 는 **지금 어떤 효과도 안 쓰는** 탄 · 패턴 · 장판으로 바꾸지 못한다(보고됨) — 에디터 import 는 전부 안다.
        /// </summary>
        public static SkillSheetIndex FromOwners(IEnumerable<DreamcatcherCard> cards, IEnumerable<DefenderUnitData> defenders,
            IEnumerable<AttackUnitData> enemies, StringBuilder log)
        {
            var cardList = (cards ?? Enumerable.Empty<DreamcatcherCard>()).Where(c => c != null).Distinct().ToList();
            var defList = (defenders ?? Enumerable.Empty<DefenderUnitData>()).Where(d => d != null).Distinct().ToList();
            var enemyList = (enemies ?? Enumerable.Empty<AttackUnitData>()).Where(e => e != null).Distinct().ToList();
            var effects = cardList.SelectMany(c => c.bindings ?? Array.Empty<BindingSpec>())
                .Concat(defList.SelectMany(d => d.bindings ?? Array.Empty<BindingSpec>()))
                .Concat(enemyList.SelectMany(e => e.bindings ?? Array.Empty<BindingSpec>()))
                .Select(b => b.effect).Where(e => e != null).Distinct().ToList();
            return Build(effects,
                effects.Select(e => e.projectile).Where(p => p != null).Distinct(),
                effects.Select(e => e.pattern).Where(p => p != null).Distinct(),
                effects.Select(e => e.hazard).Where(h => h != null).Distinct(),
                cardList, defList, enemyList, log);
        }
    }

    /// <summary>
    /// skill-data-table unit 5 — 새 시트 두 탭(`Skills` · `SkillOwners` — U19)의 export · import **하나**. 카드 · 방어유닛 · 적이 같은 형식의
    /// 소유 줄을 들므로(계약 2) 소유자 종류는 `owner_kind` 열 하나로만 갈린다 — 임포터에 소유자별 경로가 없다.
    ///
    /// import 는 두 단계다: ① **계획**(에셋을 건드리지 않고 무엇이 바뀌는지 계산) → diff 표를 로그에 쓴다 ② `apply` 일 때만 쓴다.
    /// 시트 import 에는 dry-run 이 없어 돌리면 바로 에셋에 쓴다 — 쓰기 **전에** 무엇이 바뀌는지가 로그에 먼저 남는다.
    ///
    /// 탭 의미: `Skills` = 효과 id 별 부분 갱신(빈 칸 = 그대로 · 없는 id = 보고만). `SkillOwners` = 탭에 나온 소유자의 `bindings` 를
    /// 그 줄들로 **다시 짓는다**(slot 순 · 옛 `DcCardEffects` 와 같은 시트-정본 규칙 — 줄을 지우면 소유 줄이 지워진다). 탭에 안 나온
    /// 소유자는 그대로다.
    /// </summary>
    public static class SkillSheet
    {
        public const string OwnerCard = "card";
        public const string OwnerDefender = "defender";
        public const string OwnerEnemy = "enemy";

        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        // 이름 짝(DTO 필드 이름 = 저작 칸 이름). 짝 없는 DTO 필드는 손으로 다룬다(id · 참조 · 보기 전용).
        private static readonly (FieldInfo dto, FieldInfo target)[] EffectPairs = Pairs(typeof(SkillRowDto), typeof(EffectValues));
        private static readonly (FieldInfo dto, FieldInfo target)[] TriggerPairs = Pairs(typeof(SkillOwnerRowDto), typeof(TriggerSpec));

        private static (FieldInfo, FieldInfo)[] Pairs(Type dto, Type target)
        {
            var list = new List<(FieldInfo, FieldInfo)>();
            foreach (var f in dto.GetFields(PublicInstance))
            {
                var t = target.GetField(f.Name, PublicInstance);
                if (t != null) list.Add((f, t));
            }
            return list.ToArray();
        }

        /// <summary>저작 struct 의 칸 중 시트 열 짝이 없는 것(테스트 — 칸을 늘리고 열을 잊으면 export 가 조용히 값을 잃는다).</summary>
        public static IEnumerable<string> UnpairedAuthoringFields()
        {
            foreach (var f in typeof(EffectValues).GetFields(PublicInstance))
                if (EffectPairs.All(p => p.target != f)) yield return nameof(EffectValues) + "." + f.Name;
            foreach (var f in typeof(TriggerSpec).GetFields(PublicInstance))
                if (TriggerPairs.All(p => p.target != f)) yield return nameof(TriggerSpec) + "." + f.Name;
        }

        // ───────────────────────── export ─────────────────────────

        /// <summary>
        /// 에셋 → 두 탭의 줄(메모리). 효과는 id 순 · 소유 줄은 (card · defender · enemy) → owner_id → slot 순.
        /// skill-data-table unit 9 — `Skills` 는 **그 종류가 쓰는 칸만, 기본값이어도** 쓴다(`EffectSlots.UsedColumns` — 오라 줄의 `cc_kind` 같은
        /// 잡음 0 · 「공격력 버프」의 `buff_stat` 처럼 첫 enum 값이 빈 칸으로 숨지 않는다 · 참조 셋도 같다). 비율 칸은 고정 수치 줄에서 비운다.
        /// 안 쓰는 칸에 남은 값(옛 이전이 채운 기본값 등)은 에셋에만 있고 시트에 보이지 않는다. 소유 줄은 기본값이 아닐 때만 채운다
        /// (`trigger` · `effect_id` 는 늘).
        /// </summary>
        public static SkillSheetPayload Export(IEnumerable<EffectData> effects, IEnumerable<DreamcatcherCard> cards,
            IEnumerable<DefenderUnitData> defenders, IEnumerable<AttackUnitData> enemies)
        {
            var skillRows = new List<SkillRowDto>();
            foreach (var e in (effects ?? Enumerable.Empty<EffectData>()).Where(x => x != null)
                         .OrderBy(x => x.id, StringComparer.Ordinal))
            {
                EffectSlots.UsedColumns(e.values.kind, out var used);
                var row = new SkillRowDto
                {
                    id = e.id,
                    kindKo = KindKo(e.values.kind),
                    deprecated = e.deprecated ? true : (bool?)null,
                    projectileId = (used & EffectColumns.ProjectileId) != 0 && e.projectile != null ? e.projectile.id : null,
                    patternId = (used & EffectColumns.PatternId) != 0 && e.pattern != null ? e.pattern.id : null,
                    hazardId = (used & EffectColumns.HazardId) != 0 && e.hazard != null ? e.hazard.name : null,
                };
                // 종류가 쓰는 칸은 **기본값이어도** 적는다 — 기획자가 빈 칸을 보고 「공격력 버프」(`buff_stat` 첫 enum)인지 알 수 없다.
                // 비율 칸(기준 스탯 · 비율)만 고정 수치 줄에서 비운다(고정 줄에서 뜻이 없다).
                if (e.values.magnitudeMode == MagnitudeMode.Flat) used &= ~(EffectColumns.BasisStat | EffectColumns.Ratio);
                ReadUsed(e.values, row, EffectPairs, t => (used & EffectSlots.ColumnOfField(t.Name)) != 0);
                row.kind = e.values.kind;
                skillRows.Add(row);
            }

            var ownerRows = new List<SkillOwnerRowDto>();
            AddOwners(ownerRows, OwnerCard, cards, c => c.id, c => c.bindings);
            AddOwners(ownerRows, OwnerDefender, defenders, d => d.id, d => d.bindings);
            AddOwners(ownerRows, OwnerEnemy, enemies, e => e.id, e => e.bindings);
            return new SkillSheetPayload { skills = skillRows.ToArray(), owners = ownerRows.ToArray() };
        }

        private static void AddOwners<T>(List<SkillOwnerRowDto> rows, string ownerKind, IEnumerable<T> owners,
            Func<T, string> idOf, Func<T, BindingSpec[]> bindingsOf) where T : ScriptableObject
        {
            foreach (var o in (owners ?? Enumerable.Empty<T>()).Where(x => x != null).OrderBy(idOf, StringComparer.Ordinal))
            {
                var b = bindingsOf(o);
                for (int i = 0; i < (b?.Length ?? 0); i++)
                {
                    var row = new SkillOwnerRowDto
                    {
                        ownerKind = ownerKind, ownerId = idOf(o), slot = i,
                        fireCap = b[i].fireCap != 0 ? b[i].fireCap : (int?)null,
                        effectId = b[i].effect != null ? b[i].effect.id : null,
                    };
                    ReadNonDefault(b[i].trigger, row, TriggerPairs);
                    row.kind = b[i].trigger.kind;
                    rows.Add(row);
                }
            }
        }

        private static void ReadUsed(object source, object dto, (FieldInfo dto, FieldInfo target)[] pairs, Func<FieldInfo, bool> include)
        {
            foreach (var (d, t) in pairs)
                if (include(t)) d.SetValue(dto, t.GetValue(source));
        }

        private static void ReadNonDefault(object source, object dto, (FieldInfo dto, FieldInfo target)[] pairs,
            Func<FieldInfo, bool> include = null)
        {
            foreach (var (d, t) in pairs)
            {
                if (include != null && !include(t)) continue;
                object v = t.GetValue(source);
                if (!Equals(v, Activator.CreateInstance(t.FieldType))) d.SetValue(dto, v);
            }
        }

        /// <summary>시트 페이로드 규약(옛 탭과 같다 — null 칸 생략 · enum = 멤버 이름).</summary>
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        public static string ToJson<T>(IEnumerable<T> rows) => JsonConvert.SerializeObject(rows, Formatting.Indented, JsonSettings);

        /// <summary>U19 — 효과 종류 한국어 표시(시트 보기 전용 열 `kind_ko`). 임포터는 읽지 않는다.</summary>
        public static string KindKo(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.None: return "없음";
                case EffectKind.ProjectileToTarget: return "대상에게 투사체";
                case EffectKind.SelfTileAoe: return "제자리 광역 피해";
                case EffectKind.NextAttackDoubleFire: return "다음 공격 2연발";
                case EffectKind.SelfBuffLethal: return "공속 버프 후 사망";
                case EffectKind.AreaBarrage: return "(이관됨) 광역 포격";
                case EffectKind.SelfBlink: return "순간이동(착지 슬램)";
                case EffectKind.SelfWarmupBuff: return "(죽은 값) 예열 버프";
                case EffectKind.PlacementAura: return "배치 오라(공속)";
                case EffectKind.AllyMoveSpeedAura: return "아군 이동 속도 오라";
                case EffectKind.ApplyCcToTarget: return "대상 상태이상";
                case EffectKind.ApplyStackToTarget: return "대상 스택 부여";
                case EffectKind.SelfStatBuff: return "자기 스탯 버프";
                case EffectKind.HeavyStrike: return "강타(피해 배율)";
                case EffectKind.DreamCocoon: return "꿈 고치(수면 후 버프)";
                case EffectKind.BountyMark: return "현상금 표식";
                case EffectKind.AreaSleep: return "광역 수면";
                case EffectKind.EmitProjectilePattern: return "발사 명세(패턴)";
                case EffectKind.UltimateLeap: return "궁극기 도약(착지 슬램)";
                case EffectKind.GrantShield: return "실드 부여";
                case EffectKind.SplitOnDeath: return "(적 고유) 사망 시 분열";
                case EffectKind.AreaBreath: return "부채꼴 브레스";
                case EffectKind.SelfOrbitProjectile: return "주위를 도는 투사체";
                case EffectKind.AreaTaunt: return "광역 도발";
                case EffectKind.SpawnHazard: return "장판 설치";
                case EffectKind.RecallAttachedToFront: return "부착 카드 손패 회수";
                case EffectKind.AllyStatAura: return "아군 스탯 오라";
                case EffectKind.OpponentStatAura: return "상대 스탯 오라";
                case EffectKind.GainCost: return "코스트 획득";
                case EffectKind.ReduceSkillCooldown: return "스킬 재사용 단축";
                case EffectKind.AreaApplyStack: return "광역 스택 부여";
                case EffectKind.AreaCc: return "광역 상태이상";
                case EffectKind.AreaDot: return "광역 지속 피해";
                case EffectKind.ActiveMeteor: return "액티브 · 운석";
                case EffectKind.ActiveSlowField: return "액티브 · 감속 지대";
                case EffectKind.ActivePowerSurge: return "액티브 · 공격력 증폭";
                case EffectKind.ActiveRapidFire: return "액티브 · 속사";
                case EffectKind.ActiveTornado: return "액티브 · 회오리";
                case EffectKind.ActivePortal: return "액티브 · 포탈";
                case EffectKind.FactionStatBuff: return "아군 전체 스탯";
                case EffectKind.ProjectileBounce: return "투사체 튕김";
                case EffectKind.FrontmostTarget: return "최전방 우선";
                case EffectKind.DamageVsSleeping: return "수면 적 특효";
                default: return kind.ToString();
            }
        }

        // ───────────────────────── import ─────────────────────────

        private sealed class EffectPlan
        {
            public EffectData So;
            public EffectValues Values;
            public bool Deprecated;
            public ProjectileData Projectile;
            public ProjectilePatternData Pattern;
            public HazardSO Hazard;
        }

        private sealed class OwnerPlan
        {
            public ScriptableObject So;
            public Action<BindingSpec[]> Set;
            public BindingSpec[] Next;
        }

        private sealed class Counters
        {
            public int matched, unmatched, skipped, changedFields, ignoredCells;
        }

        /// <summary>
        /// 두 탭을 계획 → diff 로그 → (`apply` 일 때만) 적용. `onApplied` 는 **바뀐** 에셋마다 한 번(에디터 = 디스크 저장 · 런타임 = null).
        /// 반환 = 로그 전체(첫 줄 = 요약).
        /// </summary>
        public static string Import(SkillSheetPayload payload, SkillSheetIndex index, bool apply,
            Action<ScriptableObject> onApplied, StringBuilder log)
        {
            log ??= new StringBuilder();
            var c = new Counters();
            var diff = new List<string>();
            var effectPlans = PlanEffects(payload?.skills, index, log, diff, c);
            var ownerPlans = PlanOwners(payload?.owners, index, effectPlans, log, diff, c);

            // ── diff 표(쓰기 전) ──
            log.AppendLine($"[skills-diff] {(apply ? "적용 전" : "미리보기 — 쓰지 않는다")} · 바뀌는 칸 {diff.Count}");
            foreach (var line in diff) log.Append("[skills-diff] ").AppendLine(line);

            int written = 0;
            if (apply)
            {
                foreach (var p in effectPlans)
                {
                    p.So.values = p.Values;
                    p.So.deprecated = p.Deprecated;
                    p.So.projectile = p.Projectile;
                    p.So.pattern = p.Pattern;
                    p.So.hazard = p.Hazard;
                    onApplied?.Invoke(p.So);
                    written++;
                }
                foreach (var p in ownerPlans)
                {
                    p.Set(p.Next);
                    onApplied?.Invoke(p.So);
                    written++;
                }
            }

            log.Insert(0, $"Skills/SkillOwners: matched {c.matched}, unmatched {c.unmatched}, skipped {c.skipped}, "
                          + $"changed fields {c.changedFields}, assets {(apply ? "written" : "would write")} {(apply ? written : effectPlans.Count + ownerPlans.Count)}, "
                          + $"ignored cells {c.ignoredCells}.\n");
            return log.ToString();
        }

        private static List<EffectPlan> PlanEffects(SkillRowDto[] rows, SkillSheetIndex index, StringBuilder log,
            List<string> diff, Counters c)
        {
            var plans = new List<EffectPlan>();
            if (rows == null) return plans;
            var seen = new HashSet<string>();
            foreach (var row in rows)
            {
                string id = row.id;
                if (!seen.Add(id ?? ""))
                { c.skipped++; log.AppendLine($"[Skills] duplicate row for effect_id='{id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(id) || !index.Effects.TryGetValue(id, out var so))
                { c.unmatched++; log.AppendLine($"[Skills] no effect for effect_id='{id}' — not created (make the asset first)."); continue; }
                c.matched++;

                // skill-data-table unit 9 — 칸 규칙 = 그 줄의 **결과 종류**(이 줄이 kind 를 바꾸면 새 종류)가 쓰는 칸만. 안 쓰는 칸에 값이 오면
                // **경고하고 무시**한다(쓰지 않는다 — 에셋의 그 칸은 그대로 · export 가 안 쓰는 칸을 안 내보내는 것과 대칭).
                var kindAfter = row.kind ?? so.values.kind;
                EffectSlots.UsedColumns(kindAfter, out var used);
                object boxed = so.values;
                foreach (var (d, t) in EffectPairs)
                {
                    object v = d.GetValue(row);
                    if (v == null) continue;
                    var col = EffectSlots.ColumnOfField(t.Name);
                    if (col != EffectColumns.None && (used & col) == 0)
                    {
                        c.ignoredCells++;
                        log.AppendLine($"[Skills] '{id}' {Column(typeof(SkillRowDto), d.Name)}={Show(v)} — kind {kindAfter} does not use this column; ignored.");
                        continue;
                    }
                    t.SetValue(boxed, v);
                }
                string projectileRef = UsedRef(row.projectileId, used, EffectColumns.ProjectileId, "projectile_id", id, kindAfter, log, c);
                string patternRef = UsedRef(row.patternId, used, EffectColumns.PatternId, "pattern_id", id, kindAfter, log, c);
                string hazardRef = UsedRef(row.hazardId, used, EffectColumns.HazardId, "hazard_id", id, kindAfter, log, c);
                var plan = new EffectPlan
                {
                    So = so,
                    Values = (EffectValues)boxed,
                    Deprecated = row.deprecated ?? so.deprecated,
                    Projectile = so.projectile,
                    Pattern = so.pattern,
                    Hazard = so.hazard,
                };
                if (!Resolve(projectileRef, index.Projectiles, ref plan.Projectile, "projectile_id", id, log)
                    | !Resolve(patternRef, index.Patterns, ref plan.Pattern, "pattern_id", id, log)
                    | !Resolve(hazardRef, index.Hazards, ref plan.Hazard, "hazard_id", id, log))
                { c.skipped++; continue; }

                int before = diff.Count;
                foreach (var (_, t) in EffectPairs)
                    AddDiff(diff, $"Skills {id}", Column(typeof(SkillRowDto), t.Name), t.GetValue(so.values), t.GetValue(plan.Values));
                AddDiff(diff, $"Skills {id}", "deprecated", so.deprecated, plan.Deprecated);
                AddDiff(diff, $"Skills {id}", "projectile_id", so.projectile != null ? so.projectile.id : null, plan.Projectile != null ? plan.Projectile.id : null);
                AddDiff(diff, $"Skills {id}", "pattern_id", so.pattern != null ? so.pattern.id : null, plan.Pattern != null ? plan.Pattern.id : null);
                AddDiff(diff, $"Skills {id}", "hazard_id", so.hazard != null ? so.hazard.name : null, plan.Hazard != null ? plan.Hazard.name : null);
                if (diff.Count == before) continue;   // 바뀐 칸 없음 = 쓰지 않는다
                c.changedFields += diff.Count - before;
                plans.Add(plan);
            }
            return plans;
        }

        // unit 9 — 종류가 안 쓰는 참조 칸 = 경고하고 무시(null = 그대로).
        private static string UsedRef(string refId, EffectColumns used, EffectColumns col, string column, string id,
            EffectKind kind, StringBuilder log, Counters c)
        {
            if (string.IsNullOrEmpty(refId) || (used & col) != 0) return refId;
            c.ignoredCells++;
            log.AppendLine($"[Skills] '{id}' {column}='{refId}' — kind {kind} does not use this column; ignored.");
            return null;
        }

        // 빈 칸 = 그대로. 모르는 id = 그 줄을 통째로 건너뛴다(반쯤 쓰인 효과를 남기지 않는다).
        private static bool Resolve<T>(string id, Dictionary<string, T> table, ref T slot, string column, string owner,
            StringBuilder log) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (table.TryGetValue(id, out var found)) { slot = found; return true; }
            log.AppendLine($"[Skills] '{owner}' {column}='{id}' not found — row skipped.");
            return false;
        }

        private static List<OwnerPlan> PlanOwners(SkillOwnerRowDto[] rows, SkillSheetIndex index, List<EffectPlan> effectPlans,
            StringBuilder log, List<string> diff, Counters c)
        {
            var plans = new List<OwnerPlan>();
            if (rows == null) return plans;

            var groups = new Dictionary<(string kind, string id), List<SkillOwnerRowDto>>();
            var order = new List<(string kind, string id)>();
            foreach (var row in rows)
            {
                var key = ((row.ownerKind ?? "").Trim().ToLowerInvariant(), row.ownerId ?? "");
                if (!groups.TryGetValue(key, out var list)) { groups[key] = list = new List<SkillOwnerRowDto>(); order.Add(key); }
                list.Add(row);
            }

            foreach (var key in order)
            {
                var list = groups[key];
                string label = $"{key.kind}/{key.id}";
                if (!TryOwner(key.kind, key.id, index, out var so, out var get, out var set))
                {
                    c.unmatched += list.Count;
                    log.AppendLine($"[SkillOwners] no owner for owner_kind='{key.kind}' owner_id='{key.id}' — {list.Count} row(s) not applied (owner_kind = {OwnerCard} · {OwnerDefender} · {OwnerEnemy}).");
                    continue;
                }
                if (list.Any(r => r.slot == null || r.slot < 0) || list.GroupBy(r => r.slot).Any(g => g.Count() > 1))
                {
                    c.skipped += list.Count;
                    log.AppendLine($"[SkillOwners] '{label}' has a blank/negative/duplicate slot — owner skipped.");
                    continue;
                }
                list.Sort((a, b) => a.slot.Value.CompareTo(b.slot.Value));

                var old = get() ?? Array.Empty<BindingSpec>();
                var next = new BindingSpec[list.Count];
                string error = null;
                for (int i = 0; i < list.Count && error == null; i++)
                {
                    var row = list[i];
                    bool isNew = row.slot.Value >= old.Length;
                    if (isNew && (row.kind == null || string.IsNullOrEmpty(row.effectId)))
                    { error = $"new slot {row.slot} needs trigger and effect_id"; break; }
                    var b = isNew ? default : old[row.slot.Value];
                    object trigger = b.trigger;
                    foreach (var (d, t) in TriggerPairs)
                    {
                        object v = d.GetValue(row);
                        if (v != null) t.SetValue(trigger, v);
                    }
                    b.trigger = (TriggerSpec)trigger;
                    if (row.fireCap != null) b.fireCap = row.fireCap.Value;
                    if (!string.IsNullOrEmpty(row.effectId))
                    {
                        if (!index.Effects.TryGetValue(row.effectId, out var effect))
                        { error = $"slot {row.slot} effect_id='{row.effectId}' not in Skills"; break; }
                        b.effect = effect;
                    }
                    var planned = effectPlans.FirstOrDefault(p => p.So == b.effect);
                    if (b.effect != null && (planned != null ? planned.Deprecated : b.effect.deprecated))
                    { error = $"slot {row.slot} effect '{b.effect.id}' is deprecated"; break; }
                    next[i] = b;
                }
                if (error != null)
                {
                    c.skipped += list.Count;
                    log.AppendLine($"[SkillOwners] '{label}' {error} — owner skipped.");
                    continue;
                }
                // skill-data-table unit 9 — U20(시트 층만 · 계약 13): 공격 변형 효과는 방어유닛 쪽 소유자만 받는다. 어긋나면 그 소유자의 시트 줄
                // **전체**를 건너뛴다(에셋 소유 줄 그대로 — 줄만 빼고 재구성하면 인스펙터 저작이 로그인마다 지워진다).
                string u20 = AttackModifierOwnerError(key.kind, so, next, effectPlans);
                if (u20 != null)
                {
                    c.skipped += list.Count;
                    log.AppendLine($"[SkillOwners] '{label}' U20 — {u20} — owner's sheet rows skipped (asset bindings untouched).");
                    continue;
                }
                if (key.kind == OwnerEnemy)
                    foreach (var b in next)
                    {
                        var v = ValuesOf(b.effect, effectPlans);
                        if (v.kind == EffectKind.FactionStatBuff && v.allyFilter != CardTargetAxis.All)
                            log.AppendLine($"[SkillOwners] '{label}' warning — enemy-owned FactionStatBuff '{b.effect.id}' ally_filter={v.allyFilter} "
                                           + "(class · cost filters are defender values — no enemy receives it; use All).");
                    }
                c.matched++;

                int before = diff.Count;
                string where = $"SkillOwners {label}";
                if (old.Length != next.Length) diff.Add($"{where} · bindings {old.Length} → {next.Length}");
                for (int i = 0; i < Math.Max(old.Length, next.Length); i++)
                {
                    string at = $"{where} · slot {i}";
                    if (i >= next.Length) { diff.Add($"{at} · removed ({Describe(old[i])})"); continue; }
                    if (i >= old.Length) { diff.Add($"{at} · added ({Describe(next[i])})"); continue; }
                    foreach (var (d, t) in TriggerPairs)
                        AddDiff(diff, at, Column(typeof(SkillOwnerRowDto), d.Name), t.GetValue(old[i].trigger), t.GetValue(next[i].trigger));
                    AddDiff(diff, at, "fire_cap", old[i].fireCap, next[i].fireCap);
                    AddDiff(diff, at, "effect_id", old[i].effect != null ? old[i].effect.id : null, next[i].effect != null ? next[i].effect.id : null);
                }
                if (diff.Count == before) continue;
                c.changedFields += diff.Count - before;
                plans.Add(new OwnerPlan { So = so, Set = set, Next = next });
            }
            return plans;
        }

        // U20 — 공격 변형(그 공격의 성질 — 강타 · 튕김 · 최전방 · 수면 특효)은 방어유닛 쪽 소유자만(방어유닛 · 숙주가 방어유닛뿐인 카드).
        private static bool IsAttackModifier(EffectKind k)
            => k == EffectKind.HeavyStrike || k == EffectKind.ProjectileBounce
            || k == EffectKind.FrontmostTarget || k == EffectKind.DamageVsSleeping;

        // 이 import 가 효과 값도 바꾸면 **바뀐 뒤** 종류로 본다(같은 import 의 Skills 계획).
        private static EffectValues ValuesOf(EffectData effect, List<EffectPlan> effectPlans)
        {
            if (effect == null) return default;
            var planned = effectPlans.FirstOrDefault(p => p.So == effect);
            return planned != null ? planned.Values : effect.values;
        }

        private static string AttackModifierOwnerError(string ownerKind, ScriptableObject so, BindingSpec[] next, List<EffectPlan> effectPlans)
        {
            foreach (var b in next)
            {
                var kind = ValuesOf(b.effect, effectPlans).kind;
                if (!IsAttackModifier(kind)) continue;
                if (ownerKind == OwnerDefender) return null;
                if (ownerKind == OwnerCard && so is DreamcatcherCard card)
                {
                    if (card.hostKinds == HostKinds.Defender) return null;
                    return $"attack modifier '{b.effect.id}' ({kind}) on a card whose host_kinds = {card.hostKinds} (Defender only)";
                }
                return $"attack modifier '{b.effect.id}' ({kind}) on owner_kind = {ownerKind} (defender · defender-hosted card only)";
            }
            return null;
        }

        private static bool TryOwner(string kind, string id, SkillSheetIndex index, out ScriptableObject so,
            out Func<BindingSpec[]> get, out Action<BindingSpec[]> set)
        {
            so = null; get = null; set = null;
            switch (kind)
            {
                case OwnerCard when index.Cards.TryGetValue(id, out var card):
                    so = card; get = () => card.bindings; set = v => card.bindings = v; return true;
                case OwnerDefender when index.Defenders.TryGetValue(id, out var unit):
                    so = unit; get = () => unit.bindings; set = v => unit.bindings = v; return true;
                case OwnerEnemy when index.Enemies.TryGetValue(id, out var enemy):
                    so = enemy; get = () => enemy.bindings; set = v => enemy.bindings = v; return true;
                default: return false;
            }
        }

        private static string Describe(in BindingSpec b)
            => $"{b.trigger.kind} → {(b.effect != null ? b.effect.id : "null")}";

        private static string Column(Type dto, string field) => SheetColumns.NameOf(dto, field);

        private static void AddDiff(List<string> diff, string where, string column, object before, object after)
        {
            if (Equals(before, after)) return;
            diff.Add($"{where} · {column}: {Show(before)} → {Show(after)}");
        }

        private static string Show(object v)
        {
            switch (v)
            {
                case null: return "(없음)";
                case float f: return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                default: return v.ToString();
            }
        }
    }
}
