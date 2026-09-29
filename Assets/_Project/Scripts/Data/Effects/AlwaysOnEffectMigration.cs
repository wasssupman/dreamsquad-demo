using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Wassup.BattleCore.Trigger;

namespace Wassup.Data
{
    /// <summary>
    /// skill-data-table unit 8 — **상시 효과 이전 계획**(카드 전용 저장처 둘 → 효과 줄 + 소유 줄). 순수 함수(제약 10) — 엔진 · 에셋을 모른다:
    /// 입력은 카드의 plain 값(`CardInput`), 출력은 계획 표(`Plan`)와 dry-run 마크다운(`Report`). 에디터 메뉴가 에셋에서 입력을 모아 표를 쓰고
    /// (적용은 사용자 승인 뒤), 테스트가 같은 계획을 메모리 사본에 입혀 굽기 · 문안이 오늘과 같은지 대조하고, 헤드리스 하네스가 Unity 없이 표를 낸다.
    ///
    /// 규칙(`8_always_on_effects_as_skills.md` 「굽기 스냅샷 동치의 조건」 · `tables.md` §3 · §10):
    ///  - 옛 항목 하나 = 효과 줄 하나(U13 — 병합 없음) · 순서 보존 · 소유 줄 = 트리거 `None` · 카드의 다음 빈 slot.
    ///  - Squad `effects[i]` → `FactionStatBuff`(buff_stat · percent · ally_filter = 카드 axis) · Unit `attackMods[i]` → 수식자 3종.
    ///  - `effect_id` = 카드 id · 옮길 항목이 둘 이상이면 `{card_id}_{k}`(k = 이 카드에서 옮기는 순번) · 기존 id 와 겹치면 `_2` …(깃발).
    ///  - 배치 오라 효과의 `ally_filter` = 그 효과를 드는 카드의 axis(수혜 대상은 효과의 뜻 — 계약 12). 두 카드가 다른 축으로 들면 깃발.
    ///  - 옛 굽기가 안 읽던 항목(Squad 의 attackMods · Unit 의 effects · 적 표식 카드의 attackMods · 수식자 None)은 옮기지 않는다(메모).
    /// 이 파일은 이전 과도기 전용이다 — 옛 칸이 걷히면(단계 B) 같이 지운다.
    /// </summary>
    public static class AlwaysOnEffectMigration
    {
        public const string EffectFolder = "Assets/_Project/Data/Effects";

        /// <summary>카드 한 장의 입력(plain 값 — 에셋 없이 만들 수 있다).</summary>
        public struct CardInput
        {
            public string Id;
            /// <summary>표에 적는 출처(에셋 경로 · 이름). 계획에는 안 쓴다.</summary>
            public string Source;
            public CardType Type;
            public CardTargetAxis Axis;
            public HostKinds Hosts;
            public CardEffect[] Effects;
            public DcAttackModSpec[] AttackMods;
            /// <summary>이미 든 소유 줄 수(새 줄은 그 뒤에 붙는다).</summary>
            public int BindingCount;
            /// <summary>이미 상시 효과 줄을 든다(이전됨) — 건너뛴다.</summary>
            public bool HasAlwaysOnRows;
            /// <summary>이 카드의 소유 줄이 가리키는 배치 오라 효과 id 와 그 효과의 지금 `allyFilter`(짝 — 같은 길이).</summary>
            public string[] PlacementAuraEffectIds;
            public CardTargetAxis[] PlacementAuraFilters;
        }

        /// <summary>새 효과 줄 + 소유 줄 하나.</summary>
        public sealed class Row
        {
            public string CardId;
            /// <summary>옛 자리(`effects[1]` · `attackMods[0]`).</summary>
            public string OldSource;
            /// <summary>옛 항목 값(사람이 읽는 형).</summary>
            public string OldText;
            public string EffectId;
            /// <summary>카드 `bindings` 에서의 자리(= 이미 든 줄 수 + 순번).</summary>
            public int Slot;
            public EffectValues Values;
            public readonly List<string> Notes = new List<string>();
            public string AssetPath => $"{EffectFolder}/Effect_{EffectId}.asset";
        }

        /// <summary>배치 오라 효과의 수혜 대상 칸 쓰기 하나.</summary>
        public sealed class AuraFilterRow
        {
            public string CardId;
            public string EffectId;
            public CardTargetAxis From;
            public CardTargetAxis To;
            public readonly List<string> Notes = new List<string>();
        }

        public sealed class Plan
        {
            public readonly List<Row> Rows = new List<Row>();
            public readonly List<AuraFilterRow> AuraFilters = new List<AuraFilterRow>();
            public readonly List<string> Notes = new List<string>();
            public int Cards;
        }

        private static readonly Regex IdRule = new Regex("^[a-z][a-z0-9_]*$");

        /// <summary>이전 계획. 입력 순서가 표 순서다(부르는 쪽이 경로 순으로 넘긴다). `existingEffectIds` = 이미 있는 효과 id(충돌 검사).</summary>
        public static Plan Build(IEnumerable<CardInput> cards, IEnumerable<string> existingEffectIds)
        {
            var plan = new Plan();
            var used = new HashSet<string>(existingEffectIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var auraSeen = new Dictionary<string, AuraFilterRow>(StringComparer.Ordinal);
            if (cards == null) return plan;
            foreach (var c in cards)
            {
                plan.Cards++;
                PlanAura(plan, in c, auraSeen);
                if (c.HasAlwaysOnRows) { plan.Notes.Add($"카드 '{c.Id}' 는 이미 상시 효과 줄을 든다 — 건너뛴다."); continue; }
                PlanCard(plan, in c, used);
            }
            return plan;
        }

        private static void PlanCard(Plan plan, in CardInput c, HashSet<string> used)
        {
            var effects = c.Effects ?? Array.Empty<CardEffect>();
            var mods = c.AttackMods ?? Array.Empty<DcAttackModSpec>();
            // 옛 굽기가 읽던 것만 옮긴다(`CardDefinitionBuilder` — Squad = effects · Unit = attackMods(적 표식 카드 제외)).
            bool readsEffects = c.Type == CardType.Squad;
            bool readsMods = c.Type == CardType.Unit && c.Hosts != HostKinds.Enemy;
            if (!readsEffects && effects.Length > 0)
                plan.Notes.Add($"카드 '{c.Id}'({c.Type}) 의 effects {effects.Length} 줄은 옛 굽기도 안 읽었다 — 옮기지 않음(확인)");
            if (!readsMods && mods.Length > 0)
                plan.Notes.Add($"카드 '{c.Id}'({c.Type} · 숙주 {c.Hosts}) 의 attackMods {mods.Length} 줄은 옛 굽기도 안 읽었다 — 옮기지 않음(확인)");

            var pending = new List<Row>();
            if (readsEffects)
                for (int i = 0; i < effects.Length; i++)
                {
                    var e = effects[i];
                    var r = new Row
                    {
                        CardId = c.Id, OldSource = $"effects[{i}]",
                        OldText = $"{e.kind} {Signed(e.percent)}% · 카드 axis {c.Axis}",
                        Values = new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = e.kind, percent = e.percent, allyFilter = c.Axis },
                    };
                    if (e.kind == CardBuffKind.CostRate) r.Notes.Add("CostRate 는 카드 스탯이 아니다 — 옛 굽기도 건너뛰었다(새 굽기도 건너뛴다 · 확인)");
                    pending.Add(r);
                }
            if (readsMods)
                for (int i = 0; i < mods.Length; i++)
                {
                    var m = mods[i];
                    string old = $"{m.kind} count {m.count} · tileRange {m.tileRange} · damageMul {Num(m.damageMul)}";
                    var kind = ToEffectKind(m.kind);
                    if (kind == EffectKind.None) { plan.Notes.Add($"카드 '{c.Id}' attackMods[{i}] {old} — None 종류는 옛 굽기도 건너뛰었다(옮기지 않음 · 확인)"); continue; }
                    var r = new Row { CardId = c.Id, OldSource = $"attackMods[{i}]", OldText = old };
                    r.Values = new EffectValues { kind = kind, mul = m.damageMul };
                    if (kind == EffectKind.ProjectileBounce) { r.Values.count = m.count; r.Values.rangeTiles = m.tileRange; }
                    else if (m.count != 0 || m.tileRange != 0)
                        r.Notes.Add($"{kind} 는 count · tileRange 를 안 읽는다 — 값 {m.count} · {m.tileRange} 버림(옛 굽기는 해시에 실었다 · 해시 변화)");
                    pending.Add(r);
                }

            string baseId = Sanitize(c.Id, pending.Count > 0 ? pending[0].Notes : plan.Notes);
            for (int k = 0; k < pending.Count; k++)
            {
                var r = pending[k];
                r.EffectId = UniqueId(pending.Count > 1 ? $"{baseId}_{k}" : baseId, used, r.Notes);
                r.Slot = c.BindingCount + k;
                plan.Rows.Add(r);
            }
        }

        private static void PlanAura(Plan plan, in CardInput c, Dictionary<string, AuraFilterRow> seen)
        {
            var ids = c.PlacementAuraEffectIds;
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;
                var from = c.PlacementAuraFilters != null && i < c.PlacementAuraFilters.Length ? c.PlacementAuraFilters[i] : default;
                if (seen.TryGetValue(id, out var prior))
                {
                    if (prior.To != c.Axis)
                        prior.Notes.Add($"카드 '{c.Id}' 도 이 효과를 축 {c.Axis} 로 든다 — 효과 칸 하나에 두 수혜 대상(확인 필요 · 효과 줄을 나눠야 한다)");
                    continue;
                }
                var r = new AuraFilterRow { CardId = c.Id, EffectId = id, From = from, To = c.Axis };
                if (from != default(CardTargetAxis) && from != c.Axis)
                    r.Notes.Add($"효과 칸이 이미 {from} — 카드 축 {c.Axis} 과 다르다(오늘 굽기는 효과 칸을 읽는다 · 확인 필요)");
                seen[id] = r;
                plan.AuraFilters.Add(r);
            }
        }

        /// <summary>옛 저작 수식자 종류 → 효과 종류(상시 효과 3종). None = 옮길 수 없다.</summary>
        public static EffectKind ToEffectKind(DcAttackModKind kind)
        {
            switch (kind)
            {
                case DcAttackModKind.ProjectileBounce: return EffectKind.ProjectileBounce;
                case DcAttackModKind.FrontmostTarget: return EffectKind.FrontmostTarget;
                case DcAttackModKind.DamageVsSleeping: return EffectKind.DamageVsSleeping;
                default: return EffectKind.None;
            }
        }

        // ── id ────────────────────────────────────────────────────────────

        private static string Sanitize(string raw, List<string> notes)
        {
            string id = (raw ?? "").Trim().ToLowerInvariant();
            id = Regex.Replace(id, "[^a-z0-9_]", "_");
            if (id.Length == 0 || !char.IsLetter(id[0])) id = "e_" + id;
            if (id != raw) notes.Add($"id '{raw}' → '{id}'(스네이크 규칙 `^[a-z][a-z0-9_]*$` · 확인)");
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

        // ── dry-run 표 ────────────────────────────────────────────────────

        /// <summary>깃발 = 손실 · 해시 변화 · 확인 필요 · 충돌.</summary>
        public static bool IsFlag(string note)
            => note.Contains("버림") || note.Contains("해시 변화") || note.Contains("확인") || note.Contains("충돌") || note.Contains("규칙 밖");

        /// <summary>계획 → 마크다운 표(카드 · 옛 항목 · 새 effect_id · 종류 · 값 · slot · 깃발).</summary>
        public static string Report(Plan plan, string generatedBy)
        {
            var sb = new StringBuilder();
            int flagged = 0;
            foreach (var r in plan.Rows) if (r.Notes.Exists(IsFlag)) flagged++;
            foreach (var a in plan.AuraFilters) if (a.Notes.Exists(IsFlag)) flagged++;
            foreach (var n in plan.Notes) if (IsFlag(n)) flagged++;
            var cards = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in plan.Rows) cards.Add(r.CardId);

            sb.Append("# skill-data-table unit 8 — 상시 효과 이전 dry-run 표(2부)\n\n");
            if (!string.IsNullOrEmpty(generatedBy)) sb.Append("> ").Append(generatedBy).Append("\n\n");
            sb.Append($"카드 {plan.Cards} 장 검사 · 옮기는 카드 {cards.Count} · 새 효과 줄 {plan.Rows.Count}(병합 0 — U13) · 배치 오라 수혜 대상 쓰기 {plan.AuraFilters.Count} · 깃발 {flagged}\n\n");
            sb.Append("소유 줄 = 트리거 `None`(보유 시작 순간부터) · 게이트 없음 · fire_cap 0 · 카드의 다음 빈 slot. 효과 에셋 = `")
              .Append(EffectFolder).Append("/Effect_{effect_id}.asset`. 깃발 = 손실 · 해시 변화 · 확인 필요 · 충돌.\n\n");

            sb.Append("## 새 효과 줄 + 카드 소유 줄\n\n");
            sb.Append("| 카드 | 옛 항목 | 새 effect_id | kind | 값 | slot | 깃발 |\n|---|---|---|---|---|---|---|\n");
            foreach (var r in plan.Rows)
                sb.Append("| `").Append(r.CardId).Append("` | ").Append(r.OldSource).Append(" — ").Append(r.OldText)
                  .Append(" | `").Append(r.EffectId).Append("` | ").Append(r.Values.kind).Append(" | ").Append(ValuesText(in r.Values))
                  .Append(" | ").Append(r.Slot).Append(" | ").Append(string.Join("<br>", r.Notes)).Append(" |\n");

            sb.Append("\n## 배치 오라 효과의 수혜 대상(`ally_filter`)\n\n");
            if (plan.AuraFilters.Count == 0) sb.Append("없음\n");
            else
            {
                sb.Append("| 카드 | 효과 id | 지금 | 쓸 값 | 깃발 |\n|---|---|---|---|---|\n");
                foreach (var a in plan.AuraFilters)
                    sb.Append("| `").Append(a.CardId).Append("` | `").Append(a.EffectId).Append("` | ").Append(a.From).Append("(기본값 = 카드 축으로 폴백)")
                      .Append(" | ").Append(a.To).Append(" | ").Append(string.Join("<br>", a.Notes)).Append(" |\n");
            }

            sb.Append("\n## 메모\n\n");
            if (plan.Notes.Count == 0) sb.Append("없음\n");
            foreach (var n in plan.Notes) sb.Append("- ").Append(n).Append('\n');
            return sb.ToString();
        }

        /// <summary>값 = 그 종류가 쓰는 칸만(`EffectSlots.UsedColumns` — unit 9 export 와 같은 표).</summary>
        public static string ValuesText(in EffectValues v)
        {
            var parts = new List<string>();
            EffectSlots.UsedColumns(v.kind, out var cols);
            if ((cols & EffectColumns.BuffStat) != 0) parts.Add($"buff_stat {v.buffStat}");
            if ((cols & EffectColumns.Percent) != 0) parts.Add($"percent {Num(v.percent)}");
            if ((cols & EffectColumns.AllyFilter) != 0) parts.Add($"ally_filter {v.allyFilter}");
            if ((cols & EffectColumns.Count) != 0) parts.Add($"count {v.count}");
            if ((cols & EffectColumns.RangeTiles) != 0) parts.Add($"range_tiles {v.rangeTiles}");
            if ((cols & EffectColumns.Mul) != 0) parts.Add($"mul {Num(v.mul)}");
            return parts.Count > 0 ? string.Join(" · ", parts) : "—";
        }

        private static string Signed(float x) => x >= 0f ? "+" + Num(x) : Num(x);

        private static string Num(float x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
