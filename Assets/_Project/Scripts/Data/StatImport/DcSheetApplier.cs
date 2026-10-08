using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Somnia.Battle.Data.StatImport
{
    // dreamcatcher-sheet-sync unit 2 — the dreamcatcher-tab apply core. Same
    // id-match partial-update philosophy as UnitStatApplier(flat tabs — cards · skills · configs).
    //  - 카드 규칙(소유 줄)은 이 코어 밖이다 — skill-data-table unit 5 의 `SkillSheet`(탭 `Skills` · `SkillOwners`).
    //    옛 DcMechanics 탭은 은퇴했다.
    //  - skill-data-table unit 8 단계 B — 시트-정본 자식 탭 둘(DcCardEffects · DcAttackMods — 카드 배열 재구성)도 은퇴했다.
    //    스쿼드 스탯 효과 · 공격 수식자는 효과 줄 + 카드 소유 줄(같은 `SkillSheet`)이다.
    public static class DcSheetApplier
    {
        public static string Apply(DcSheetPayload payload,
            Dictionary<string, DreamcatcherCard> cardsById,
            Dictionary<string, SkillData> skillsById,
            Dictionary<string, ScriptableObject> configsById,
            Action<ScriptableObject> onApplied,
            StringBuilder log)
        {
            var c = new Counters();

            ApplyFlat(payload?.cards, "dc-card", dto => dto.id, cardsById, onApplied, log, c, null);
            ApplyFlat(payload?.skills, "dc-skill", dto => dto.id, skillsById, onApplied, log, c, null);
            ApplyFlat(payload?.configs, "dc-config", dto => dto.id, configsById, onApplied, log, c, null);

            log.Insert(0, $"Matched {c.matched}, unmatched {c.unmatched}, fields applied {c.fieldsApplied}, skipped {c.skipped}.\n");
            return log.ToString();
        }

        private class Counters
        {
            public int matched, unmatched, fieldsApplied, skipped;
        }

        // -------- flat tabs (cards / skills / configs) --------

        private static void ApplyFlat<TDto, TSo>(TDto[] rows, string label,
            Func<TDto, string> idOf, Dictionary<string, TSo> byId,
            Action<ScriptableObject> onApplied, StringBuilder log, Counters c,
            Action<TSo> postApply) where TSo : ScriptableObject
        {
            if (rows == null) return;
            var seen = new HashSet<string>();
            foreach (var dto in rows)
            {
                string id = idOf(dto);
                if (!seen.Add(id ?? ""))
                { c.skipped++; log.AppendLine($"[{label}] duplicate row for id='{id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(id) || !byId.TryGetValue(id, out var so))
                { c.unmatched++; log.AppendLine($"[{label}] no match for id='{id}'"); continue; }
                // skill-data-table unit 9 — 바뀐 칸을 한 줄씩 남긴다(열 = 시트 JSON 이름 · 새 카드 칸 host_kinds · cooldown_sec · needs_two_tiles 포함).
                var changes = new List<string>();
                c.fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so, changes);
                foreach (var change in changes) log.AppendLine($"[{label}-diff] '{id}' · {change}");
                postApply?.Invoke(so);
                onApplied?.Invoke(so);
                c.matched++;
            }
        }
    }
}
