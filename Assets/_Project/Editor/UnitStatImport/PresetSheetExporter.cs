using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.PresetImport;

namespace Wassup.Editor.UnitStatImport
{
    // preset-sheet-import unit 2 — SquadPresetCollection → Presets 탭 시드 JSON.
    // import 의 역방향: 각 SquadPreset → PresetDto{ presetName, squad=id csv, dreamcatcher=id csv }.
    // 현 프리셋(추천 A/B)을 시트 초기값으로 뽑아 id 손전사를 없앤다. import 와 같은 DTO 를 통과.
    public static class PresetSheetExporter
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string ExportToFile(string path)
        {
            var log = new StringBuilder();
            var collection = PresetCollectionAsset.Load(log);
            if (collection == null) return log.ToString();

            var presets = collection.presets ?? new List<SquadPreset>();
            var rows = new List<PresetDto>(presets.Count);
            foreach (var p in presets)
            {
                if (p == null) continue;
                rows.Add(new PresetDto
                {
                    presetName = p.presetName,
                    squad = JoinIds(p.units, u => u != null ? u.id : null),
                    dreamcatcher = JoinIds(p.cards, c => c != null ? c.id : null),
                });
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(rows, Formatting.Indented, Settings),
                new UTF8Encoding(false));
            log.AppendLine($"Exported {rows.Count} presets → {path}");
            return log.ToString();
        }

        // null 슬롯은 csv 에서 표현 불가(빈 항목은 import 가 drop) → 스킵. 시드는 완성 프리셋 전제.
        private static string JoinIds<T>(T[] arr, Func<T, string> idOf)
        {
            if (arr == null) return "";
            var ids = new List<string>();
            foreach (var x in arr)
            {
                var id = idOf(x);
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            return string.Join(",", ids);
        }
    }
}
