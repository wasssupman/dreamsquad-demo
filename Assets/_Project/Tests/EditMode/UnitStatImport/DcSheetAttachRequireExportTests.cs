using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wassup.Editor.UnitStatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // dreamcatcher-attach-requirement unit 2 — export blank 규칙의 실검증.
    // DTO 왕복(DcSheetImportTests)과 달리 여기선 **실제 exporter 를 돌려** 산출 JSON 을
    // 본다: 제한 없는 카드 행에 attach 계열 키가 아예 없어야 한다(enum-zero 노이즈가
    // 설정된 것처럼 보이면 안 된다 — data-hygiene 전례).
    public class DcSheetAttachRequireExportTests
    {
        private const string DcFolder = "Assets/_Project/Data/Dreamcatcher";
        private const string SkillFolder = "Assets/_Project/Data/Skills";
        private const string DefenderFolder = "Assets/_Project/Data/Defenders";
        private const string EnemyFolder = "Assets/_Project/Data/Enemies";
        private const string ConfigFolder = "Assets/_Project/Data/Config";

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dc_attach_export_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Export_UnrestrictedCards_OmitAttachRequireColumns()
        {
            var tabs = new[] { "DcCards", "DcCardEffects", "DcMechanics", "DcAttackMods", "DcSkills", "DcConfig" };

            DcSheetExporter.ExportToFolder(_dir, tabs, DcFolder, SkillFolder);

            string path = Path.Combine(_dir, "DcCards.json");
            Assert.IsTrue(File.Exists(path), "DcCards.json exported");
            var rows = JArray.Parse(File.ReadAllText(path));
            Assert.Greater(rows.Count, 0, "카드가 하나 이상 export 되어야 검증이 의미 있다");

            int checked_ = 0;
            foreach (JObject row in rows)
            {
                // 현재 카탈로그는 전부 제한 없음(attachType == None) → 두 키 모두 부재.
                string kind = (string)row["attachType"];
                if (kind != null) continue; // 제한이 설정된 카드가 생기면 그 행은 대상 밖
                checked_++;
                Assert.IsNull(row["attachValue"],
                    $"'{(string)row["id"]}': 제한 없는 행에 attachValue 키가 있으면 안 된다");
            }
            Assert.Greater(checked_, 0, "제한 없는 카드 행이 하나 이상 검사되어야 한다");
        }

        [Test]
        public void PushPayload_UnrestrictedCards_SeedsAttachHeadersWithoutDataRow()
        {
            var tabs = new[] { "DcCards", "DcCardEffects", "DcMechanics", "DcAttackMods", "DcSkills", "DcConfig" };
            string json = SheetPushPayload.BuildCombinedJson(
                "Defenders", "Enemies", DefenderFolder, EnemyFolder,
                tabs, DcFolder, SkillFolder,
                "Presets",
                "CostConfig", ConfigFolder);

            var rows = (JArray)JObject.Parse(json)["DcCards"];
            int seedCount = 0;
            int cardCount = 0;
            foreach (JObject row in rows)
            {
                if (row["id"] != null)
                {
                    cardCount++;
                    continue;
                }

                seedCount++;
                Assert.AreEqual("", (string)row["attachType"]);
                Assert.AreEqual("", (string)row["attachValue"]);
                Assert.AreEqual(2, row.Count, "헤더 시드에는 attach 두 키 외 데이터가 없어야 한다");
            }

            Assert.AreEqual(1, seedCount, "키 없는 Push 전용 헤더 시드는 정확히 하나");
            Assert.AreEqual(rows.Count - 1, cardCount, "헤더 시드는 실제 카드 행 수에 포함되지 않는다");
        }
    }
}
