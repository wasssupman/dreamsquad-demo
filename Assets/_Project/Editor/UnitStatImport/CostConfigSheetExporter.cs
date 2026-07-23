using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Editor.UnitStatImport
{
    // sheet-export-push unit 7 — 코스트 경제 SO(CostConfig) → 시트 탭 행. DcConfig
    // 탭과 같은 flat-config 방식(행 키 = id, 동명 필드 reflection 읽기)이고, 행 타입은
    // import 와 공유하는 CostConfigDto — DcSheetExporter ↔ DcSheetImportDto 와 동형.
    public static class CostConfigSheetExporter
    {
        // null 필드는 파일에서 통째로 빠진다 — import 의 "빈 셀 = 기존값 유지" 와
        // 대칭인 export 측 규약. UnitStatExporter/DcSheetExporter 와 동일 설정.
        private static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        // {folder}/{tabName}.json 한 파일. 반환 = 사람이 읽는 로그.
        public static string ExportToFolder(string folder, string tabName, string configAssetFolder)
        {
            var rows = new List<CostConfigDto>();
            foreach (var so in UnitAssetScan.Enumerate<CostConfig>(configAssetFolder))
            {
                var row = new CostConfigDto();
                UnitStatFieldMapper.ReadFieldsToDto(so, row);
                rows.Add(row);
            }
            // 반복 export 가 깨끗하게 diff 되도록 id 오름차순(유닛/DC exporter 와 동일).
            rows.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            string path = Path.Combine(folder, $"{tabName.Trim()}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(rows, Formatting.Indented, Settings),
                new UTF8Encoding(false));
            return $"Exported {rows.Count} cost configs → {path}\n";
        }
    }
}
