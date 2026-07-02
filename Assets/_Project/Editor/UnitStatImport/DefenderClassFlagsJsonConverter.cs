using System;
using Newtonsoft.Json;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — targetClassMask is contracted as a JSON
    // string array (e.g. ["Ranger","Guardian"], ["Everything"], []). Newtonsoft has no
    // built-in way to OR multiple enum names from an array into one [Flags] value, so
    // this converter bridges that gap for DefenderClassFlags specifically.
    public class DefenderClassFlagsJsonConverter : JsonConverter<DefenderClassFlags?>
    {
        public override DefenderClassFlags? ReadJson(JsonReader reader, Type objectType, DefenderClassFlags? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var names = serializer.Deserialize<string[]>(reader);
            if (names == null || names.Length == 0) return DefenderClassFlags.None;

            if (names.Length > 1 && Array.IndexOf(names, nameof(DefenderClassFlags.Everything)) >= 0)
            {
                throw new JsonSerializationException(
                    $"targetClassMask mixes \"{nameof(DefenderClassFlags.Everything)}\" with other class names ({string.Join(", ", names)}) — use either [\"{nameof(DefenderClassFlags.Everything)}\"] alone or a list of individual classes.");
            }

            DefenderClassFlags result = DefenderClassFlags.None;
            foreach (var name in names)
            {
                result |= (DefenderClassFlags)Enum.Parse(typeof(DefenderClassFlags), name, ignoreCase: false);
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
    }
}
