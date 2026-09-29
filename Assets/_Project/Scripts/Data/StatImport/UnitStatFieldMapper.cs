using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Wassup.Data.StatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — copies non-null DTO fields onto a
    // ScriptableObject by matching field names. DTO field names are chosen to equal
    // their SO counterpart 1:1 (see 0_json_schema_contract.md): adding a new stat
    // column later means adding one same-named field to the DTO, nothing here changes.
    public static class UnitStatFieldMapper
    {
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        // unit-stat-projection Unit 3 — fields the generic name-match copy must NOT
        // touch. `id` = match key; `atk`/`heal` = projected onto outputs elsewhere;
        // `attackDamage` = deprecated shim (warned, never written). NOTE: this is an
        // exact-name set, not a `*AttackDamage` pattern — `aggroAttackDamage` is a live
        // field and stays reflection-mapped.
        private static readonly HashSet<string> NonReflectedFields = new()
        {
            "id", "atk", "heal", "attackDamage",
        };

        // unit 5 — export skip set. `id` IS read (it is the row key in the export);
        // atk/heal are reverse-projected from outputs elsewhere; attackDamage is a
        // deprecated shim that must never reappear in exported files.
        private static readonly HashSet<string> ExportSkippedFields = new()
        {
            "atk", "heal", "attackDamage",
        };

        // unit 5 — reverse of ApplyNonNullFields: reads same-named SO fields into the
        // DTO's nullable fields, producing a full snapshot row for export.
        public static int ReadFieldsToDto(ScriptableObject so, object dto)
        {
            int readCount = 0;

            foreach (var dtoField in dto.GetType().GetFields(PublicInstance))
            {
                // dreamcatcher-sheet-sync unit 3 — `_`-prefixed fields are the
                // informational sheet columns (out of contract): export fills them
                // by hand, reflection must never look for an SO counterpart.
                if (dtoField.Name.StartsWith("_")) continue;
                if (ExportSkippedFields.Contains(dtoField.Name)) continue;

                var soField = so.GetType().GetField(dtoField.Name, PublicInstance);
                if (soField == null)
                {
                    Debug.LogWarning($"[UnitStatExport] {so.GetType().Name} '{so.name}' has no field '{dtoField.Name}' — skipped.");
                    continue;
                }

                var targetType = System.Nullable.GetUnderlyingType(dtoField.FieldType) ?? dtoField.FieldType;
                if (!targetType.IsAssignableFrom(soField.FieldType))
                {
                    Debug.LogWarning($"[UnitStatExport] '{dtoField.Name}' on '{so.name}': DTO expects {targetType.Name}, SO has {soField.FieldType.Name} — skipped.");
                    continue;
                }

                dtoField.SetValue(dto, soField.GetValue(so));
                readCount++;
            }

            return readCount;
        }

        /// <summary>
        /// `changes`(선택 · skill-data-table unit 9) = 값이 **실제로 바뀐** 칸마다 「열: 전 → 후」 한 줄(열 = 시트 JSON 이름 — `SheetColumns`).
        /// 짝은 여전히 C# 필드 이름이다(DTO ↔ SO 1:1).
        /// </summary>
        private static string Show(object v)
            => v == null ? "(없음)" : System.Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);

        public static int ApplyNonNullFields(object dto, ScriptableObject so, List<string> changes = null)
        {
            int appliedCount = 0;

            foreach (var dtoField in dto.GetType().GetFields(PublicInstance))
            {
                if (dtoField.Name.StartsWith("_")) continue; // `_` columns: out of contract
                if (NonReflectedFields.Contains(dtoField.Name)) continue;

                object dtoValue = dtoField.GetValue(dto);
                if (dtoValue == null) continue; // absent in JSON -> keep existing SO value

                var soField = so.GetType().GetField(dtoField.Name, PublicInstance);
                if (soField == null)
                {
                    Debug.LogWarning($"[UnitStatImport] {so.GetType().Name} '{so.name}' has no field '{dtoField.Name}' — skipped.");
                    continue;
                }

                if (!soField.FieldType.IsInstanceOfType(dtoValue))
                {
                    Debug.LogWarning($"[UnitStatImport] '{dtoField.Name}' on '{so.name}': expected {soField.FieldType.Name}, got {dtoValue.GetType().Name} — skipped.");
                    continue;
                }

                if (changes != null)
                {
                    object before = soField.GetValue(so);
                    if (!Equals(before, dtoValue))
                        changes.Add($"{SheetColumns.NameOf(dto.GetType(), dtoField.Name)}: {Show(before)} → {Show(dtoValue)}");
                }
                soField.SetValue(so, dtoValue);
                appliedCount++;
            }

            return appliedCount;
        }
    }
}
