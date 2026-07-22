using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            string[] dcTabs, string dcFolder, string skillFolder)
        {
            string tempDir = Path.Combine(Path.GetTempPath(),
                "wassup_sheet_push_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                UnitStatExporter.ExportToFolder(tempDir, defenderSheet, enemySheet, defenderFolder, enemyFolder);
                DcSheetExporter.ExportToFolder(tempDir, dcTabs, dcFolder, skillFolder);

                var root = new JObject();
                AddTab(root, tempDir, defenderSheet);
                AddTab(root, tempDir, enemySheet);
                foreach (var tab in dcTabs) AddTab(root, tempDir, tab);

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
