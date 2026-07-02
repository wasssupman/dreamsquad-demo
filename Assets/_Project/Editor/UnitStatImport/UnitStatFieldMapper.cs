using System.Reflection;
using UnityEngine;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — copies non-null DTO fields onto a
    // ScriptableObject by matching field names. DTO field names are chosen to equal
    // their SO counterpart 1:1 (see 0_json_schema_contract.md): adding a new stat
    // column later means adding one same-named field to the DTO, nothing here changes.
    public static class UnitStatFieldMapper
    {
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        public static int ApplyNonNullFields(object dto, ScriptableObject so)
        {
            int appliedCount = 0;

            foreach (var dtoField in dto.GetType().GetFields(PublicInstance))
            {
                if (dtoField.Name == "id") continue;

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

                soField.SetValue(so, dtoValue);
                appliedCount++;
            }

            return appliedCount;
        }
    }
}
