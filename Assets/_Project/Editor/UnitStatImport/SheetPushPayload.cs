using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wassup.Data.StatImport;

namespace Wassup.Editor.UnitStatImport
{
    // sheet-export-push unit 2 — 전 8탭(유닛 2 + DC 6)을 하나의 push 바디
    // ({ "<탭명>": [rows], ... })로 병합한다. 검증된 exporter 를 임시 폴더에 그대로
    // 돌린 뒤 산출 JSON 을 다시 읽어 탭명 키로 합친다 — DcSheetExporter.ExportCombinedFile
    // 이 이미 쓰는 패턴(수집 로직 중복 0, 기존 exporter 미변경). null 필드 생략(blank=keep)·
    // enum=멤버명 등 직렬화 규약은 exporter 가 이미 보장하므로 여기서 재선언하지 않는다.
    public static class SheetPushPayload
    {
        public static string BuildCombinedJson(
            string defenderSheet, string enemySheet, string defenderFolder, string enemyFolder,
            string[] dcTabs, string dcFolder, string skillFolder,
            string presetTab,
            string costTab, string costFolder)
        {
            string tempDir = Path.Combine(Path.GetTempPath(),
                "wassup_sheet_push_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                UnitStatExporter.ExportToFolder(tempDir, defenderSheet, enemySheet, defenderFolder, enemyFolder);
                DcSheetExporter.ExportToFolder(tempDir, dcTabs, dcFolder, skillFolder);
                // preset-sheet-import unit 6 — Presets 탭(list-replace 모드, 서버가 라우팅).
                // F3: 컬렉션 결측이면 Presets 만 생략하고 8탭은 진행(false → AddTab 스킵).
                bool hasPresets = PresetSheetExporter.ExportToFolder(tempDir, presetTab);
                // unit 7 — CostConfig 탭(신규). DcConfig 와 같은 keyed-upsert(id).
                CostConfigSheetExporter.ExportToFolder(tempDir, costTab, costFolder);

                var root = new JObject();
                AddTab(root, tempDir, defenderSheet);
                AddTab(root, tempDir, enemySheet);
                foreach (var tab in dcTabs) AddTab(root, tempDir, tab);
                if (hasPresets) AddTab(root, tempDir, presetTab);
                AddTab(root, tempDir, costTab);

                // dreamcatcher-attach-requirement unit 8 — 제한 카드가 0장이면 일반
                // exporter 는 attach 두 키를 전부 생략한다. Push 전용 id-less 행으로
                // 키만 운반하면 기존 Apps Script 가 헤더를 만든 뒤 key 결측으로 행을
                // 건너뛴다. 따라서 서버 재배포·가짜 카드·None 노이즈 없이 첫 Push 에서
                // DcCards 오른쪽에 두 컬럼이 생긴다.
                var dcCardRows = (JArray)root[dcTabs[0].Trim()];
                dcCardRows.Add(new JObject
                {
                    [nameof(DcCardDto.attachType)] = "",
                    [nameof(DcCardDto.attachValue)] = "",
                });

                return root.ToString(Formatting.Indented);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* 임시 폴더 정리 실패는 무해 */ }
            }
        }

        private static void AddTab(JObject root, string dir, string tabName)
        {
            string trimmed = tabName.Trim();
            string path = Path.Combine(dir, $"{trimmed}.json");
            root[trimmed] = JArray.Parse(File.ReadAllText(path));
        }
    }
}
