using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — targetClassMask is contracted as enum
    // member names OR'd into one [Flags] value. Newtonsoft has no built-in way to do
    // that, so this converter bridges the gap for DefenderClassFlags specifically.
    // Unit 4 — the real sheet pipeline delivers cells as scalar strings, so the
    // comma-separated cell convention ("Ranger,Guardian" / "Everything" / "None")
    // is accepted alongside the unit 1 JSON string-array form.
    public class DefenderClassFlagsJsonConverter : JsonConverter<DefenderClassFlags?>
    {
        public override DefenderClassFlags? ReadJson(JsonReader reader, Type objectType, DefenderClassFlags? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            string[] names;
            if (reader.TokenType == JsonToken.String)
            {
                // comma-separated sheet cell. A blank cell means "keep existing"
                // (same as an omitted key), so all-whitespace parses to null.
                var parts = ((string)reader.Value).Split(',');
                var trimmed = new List<string>(parts.Length);
                foreach (var part in parts)
                {
                    var name = part.Trim();
                    if (name.Length > 0) trimmed.Add(name);
                }
                if (trimmed.Count == 0) return null;
                names = trimmed.ToArray();
            }
            else
            {
                names = serializer.Deserialize<string[]>(reader);
                if (names == null || names.Length == 0) return DefenderClassFlags.None;
            }

            // sentinels are exclusive: "Everything"/"None" may not mix with class names.
            if (names.Length > 1 && (ContainsIgnoreCase(names, nameof(DefenderClassFlags.Everything)) || ContainsIgnoreCase(names, nameof(DefenderClassFlags.None))))
            {
                throw new JsonSerializationException(
                    $"targetClassMask mixes a sentinel (\"{nameof(DefenderClassFlags.Everything)}\"/\"{nameof(DefenderClassFlags.None)}\") with other class names ({string.Join(", ", names)}) — use a sentinel alone or a list of individual classes.");
            }

            DefenderClassFlags result = DefenderClassFlags.None;
            foreach (var name in names)
            {
                // hotfix ⑤ — case-insensitive, matching Json.NET's default enum
                // parsing so planners get one consistent rule across all columns.
                if (!Enum.TryParse(name, ignoreCase: true, out DefenderClassFlags flag))
                {
                    throw new JsonSerializationException(
                        $"targetClassMask contains unknown class name \"{name}\" — valid: {string.Join(", ", Enum.GetNames(typeof(DefenderClassFlags)))}.");
                }
                result |= flag;
            }
            return result;
        }

        public override void WriteJson(JsonWriter writer, DefenderClassFlags? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            if (value.Value == DefenderClassFlags.Everything)
            {
                writer.WriteValue(nameof(DefenderClassFlags.Everything));
                writer.WriteEndArray();
                return;
            }

            foreach (DefenderClassFlags flag in Enum.GetValues(typeof(DefenderClassFlags)))
            {
                if (flag == DefenderClassFlags.None || flag == DefenderClassFlags.Everything) continue;
                if ((value.Value & flag) == flag) writer.WriteValue(flag.ToString());
            }
            writer.WriteEndArray();
        }

        private static bool ContainsIgnoreCase(string[] names, string target)
            => Array.Exists(names, n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase));
    }
}
