using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Wassup.Data.StatImport
{
    // runtime-stat-refresh Unit 1 — the id-match partial-update core, shared by the
    // editor importer (AssetDatabase scan + disk save) and the runtime refresher
    // (catalog arrays, in-memory only). One code path so the two never diverge.
    public static class UnitStatApplier
    {
        // An id shared by 2+ assets is an ambiguous write target: drop it from the
        // index entirely and report, never guess (editor hotfix ⑥ rule, now the
        // single policy for both asset scans and catalog arrays).
        public static Dictionary<string, T> BuildIndex<T>(IEnumerable<T> assets,
            Func<T, string> idSelector, StringBuilder log, string label) where T : ScriptableObject
        {
            var byId = new Dictionary<string, T>();
            var ambiguous = new HashSet<string>();
            if (assets == null) return byId;
            foreach (var asset in assets)
            {
                if (asset == null) continue;
                string id = idSelector(asset);
                if (string.IsNullOrEmpty(id) || ambiguous.Contains(id)) continue;
                if (!byId.TryAdd(id, asset))
                {
                    ambiguous.Add(id);
                    byId.Remove(id);
                    log.AppendLine($"[{label}] duplicate asset id '{id}' — all assets with this id skipped.");
                }
            }
            return byId;
        }

        // simplify pass (2026-07-06) — the partial-update payload contract in one
        // place: a failed sheet contributes no rows (the healthy one still
        // applies); both failed -> null (caller reports errors only).
        public static UnitStatImportPayload BuildPayload(DefenderStatDto[] defenders, EnemyStatDto[] enemies)
        {
            if (defenders == null && enemies == null) return null;
            return new UnitStatImportPayload
            {
                defenders = defenders ?? Array.Empty<DefenderStatDto>(),
                enemies = enemies ?? Array.Empty<EnemyStatDto>(),
            };
        }

        // onApplied: the editor passes SetDirty+SaveAssetIfDirty; the runtime
        // refresher passes null (in-memory instances only — asset writes are
        // editor-only API).
        public static string Apply(UnitStatImportPayload payload,
            Dictionary<string, DefenderUnitData> defendersById,
            Dictionary<string, AttackUnitData> enemiesById,
            Action<ScriptableObject> onApplied,
            StringBuilder log)
        {
            int matched = 0, unmatched = 0, fieldsApplied = 0, projected = 0, skipped = 0;

            var seenIds = new HashSet<string>();
            foreach (var dto in payload?.defenders ?? Array.Empty<DefenderStatDto>())
            {
                if (!seenIds.Add($"defender:{dto.id}")) { log.AppendLine($"[defender] duplicate row for id='{dto.id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(dto.id) || !defendersById.TryGetValue(dto.id, out var so))
                { unmatched++; log.AppendLine($"[defender] no match for id='{dto.id}'"); continue; }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                ProjectMagnitude(so.outputs, AttackOutputKind.Damage, dto.atk, "atk", $"defender '{dto.id}'", log, ref projected, ref skipped);
                ProjectMagnitude(so.outputs, AttackOutputKind.Heal, dto.heal, "heal", $"defender '{dto.id}'", log, ref projected, ref skipped);
                WarnDeprecatedAttackDamage(dto.attackDamage, $"defender '{dto.id}'", log);
                onApplied?.Invoke(so);
                matched++;
            }

            foreach (var dto in payload?.enemies ?? Array.Empty<EnemyStatDto>())
            {
                if (!seenIds.Add($"enemy:{dto.id}")) { log.AppendLine($"[enemy] duplicate row for id='{dto.id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(dto.id) || !enemiesById.TryGetValue(dto.id, out var so))
                { unmatched++; log.AppendLine($"[enemy] no match for id='{dto.id}'"); continue; }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                ProjectMagnitude(so.outputs, AttackOutputKind.Damage, dto.atk, "atk", $"enemy '{dto.id}'", log, ref projected, ref skipped);
                WarnDeprecatedAttackDamage(dto.attackDamage, $"enemy '{dto.id}'", log);
                onApplied?.Invoke(so);
                matched++;
            }

            log.Insert(0, $"Matched {matched}, unmatched {unmatched}, fields applied {fieldsApplied}, projected {projected}, skipped {skipped}.\n");
            return log.ToString();
        }

        // unit-stat-projection Unit 3 — a planner-facing scalar (atk/heal) writes the
        // unique output of its kind. 0 or 2+ matches is ambiguous: skip + report the
        // reason so the miss is visible, never guess a target.
        public static void ProjectMagnitude(AttackOutput[] outputs, AttackOutputKind kind, float? value,
            string field, string label, StringBuilder log, ref int projected, ref int skipped)
        {
            if (value == null) return; // omitted -> keep existing magnitude
            if (AttackOutputStats.TrySetUniqueMagnitude(outputs, kind, value.Value))
            {
                projected++;
                return;
            }
            skipped++;
            int count = CountOfKind(outputs, kind);
            string reason = count == 0
                ? $"no {kind} output"
                : $"{count} {kind} outputs (need exactly 1)";
            log.AppendLine($"[{label}] {field}={value.Value} skipped — {reason}.");
        }

        public static void WarnDeprecatedAttackDamage(float? attackDamage, string label, StringBuilder log)
        {
            if (attackDamage == null) return;
            log.AppendLine($"[{label}] 'attackDamage' is deprecated (renamed to 'atk') and was NOT applied — update the sheet column.");
        }

        private static int CountOfKind(AttackOutput[] outputs, AttackOutputKind kind)
        {
            if (outputs == null) return 0;
            int n = 0;
            foreach (var o in outputs) if (o.kind == kind) n++;
            return n;
        }
    }
}
