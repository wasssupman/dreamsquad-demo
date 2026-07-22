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
    // import 와 같은 DTO 를 통과. unit 6 — sheet push 용 ExportToFolder 추가(행 빌드 공유).
    public static class PresetSheetExporter
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        // 수동 seed 용: 파일 다이얼로그 경로에 JSON 배열 기록.
        public static string ExportToFile(string path)
        {
            var log = new StringBuilder();
            var rows = BuildRows(log);
            if (rows == null) return log.ToString();
            File.WriteAllText(path, ToJson(rows), new UTF8Encoding(false));
            log.AppendLine($"Exported {rows.Count} presets → {path}");
            return log.ToString();
        }

        // unit 6 — sheet push payload 용: {folder}/{tabName}.json (UnitStat/DcSheetExporter 패턴).
        // 컬렉션이 없으면 throw — 빈 파일을 써서 push 가 시트를 통째 비우는 사고를 막는다.
        public static void ExportToFolder(string folder, string tabName)
        {
            var log = new StringBuilder();
            var rows = BuildRows(log);
            if (rows == null) throw new Exception("[preset] SquadPresetCollection 없음 — push 중단(시트 보호). " + log);
            File.WriteAllText(Path.Combine(folder, tabName.Trim() + ".json"), ToJson(rows), new UTF8Encoding(false));
        }

        // 컬렉션 → PresetDto 행. 컬렉션 없으면 null(호출처가 처리).
        private static List<PresetDto> BuildRows(StringBuilder log)
        {
            var collection = PresetCollectionAsset.Load(log);
            if (collection == null) return null;

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
            return rows;
        }

        private static string ToJson(List<PresetDto> rows) =>
            JsonConvert.SerializeObject(rows, Formatting.Indented, Settings);

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
