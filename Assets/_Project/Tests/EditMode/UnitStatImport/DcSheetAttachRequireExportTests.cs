using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wassup.Editor.UnitStatImport;
using Wassup.Data.StatImport;

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
            var tabs = DcSheetTabs.Default();

            DcSheetExporter.ExportToFolder(_dir, tabs, DcFolder, SkillFolder);

            string path = Path.Combine(_dir, "Cards.json");
            Assert.IsTrue(File.Exists(path), "Cards.json exported");
            var rows = JArray.Parse(File.ReadAllText(path));
            Assert.Greater(rows.Count, 0, "카드가 하나 이상 export 되어야 검증이 의미 있다");

            int checked_ = 0;
            foreach (JObject row in rows)
            {
                // 현재 카탈로그는 전부 제한 없음(attachType == None) → 두 키 모두 부재.
                string kind = (string)row["attach_type"];
                if (kind != null) continue; // 제한이 설정된 카드가 생기면 그 행은 대상 밖
                checked_++;
                Assert.IsNull(row["attach_value"],
                    $"'{(string)row["id"]}': 제한 없는 행에 attachValue 키가 있으면 안 된다");
            }
            Assert.Greater(checked_, 0, "제한 없는 카드 행이 하나 이상 검사되어야 한다");
        }

        // skill-data-table 감사 — 액티브 전용 칸(cooldown_sec · needs_two_tiles)은 액티브 카드 줄에만(기본값이어도) · 나머지 카드 줄은 키 없음.
        [Test]
        public void CardRows_ActiveOnlyColumns_OnlyOnActiveCards()
        {
            var unit = UnityEngine.ScriptableObject.CreateInstance<Wassup.Data.DreamcatcherCard>();
            unit.id = "u"; unit.type = Wassup.Data.CardType.Unit;
            var squad = UnityEngine.ScriptableObject.CreateInstance<Wassup.Data.DreamcatcherCard>();
            squad.id = "s"; squad.type = Wassup.Data.CardType.Squad;
            var active = UnityEngine.ScriptableObject.CreateInstance<Wassup.Data.DreamcatcherCard>();
            active.id = "a"; active.type = Wassup.Data.CardType.Active; active.cooldownSec = 0f; active.needsTwoTiles = false;
            try
            {
                var rows = JArray.Parse(DcSheetExporter.ToJson(DcSheetExporter.CardRows(new[] { unit, squad, active })));
                foreach (JObject row in rows)
                {
                    bool isActive = (string)row["id"] == "a";
                    Assert.AreEqual(isActive, row["cooldown_sec"] != null, $"'{row["id"]}' cooldown_sec");
                    Assert.AreEqual(isActive, row["needs_two_tiles"] != null, $"'{row["id"]}' needs_two_tiles");
                }
                var a = (JObject)rows.Single(r => (string)r["id"] == "a");
                Assert.AreEqual(0f, (float)a["cooldown_sec"], "액티브는 기본값이어도 적는다");
                Assert.AreEqual(false, (bool)a["needs_two_tiles"]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unit);
                UnityEngine.Object.DestroyImmediate(squad);
                UnityEngine.Object.DestroyImmediate(active);
            }
        }

        [Test]
        public void Export_LiveCards_NonActiveRowsHaveNoActiveOnlyColumns()
        {
            DcSheetExporter.ExportToFolder(_dir, DcSheetTabs.Default(), DcFolder, SkillFolder);
            var rows = JArray.Parse(File.ReadAllText(Path.Combine(_dir, DcSheetTabs.Cards + ".json")));
            int active = 0, other = 0;
            foreach (JObject row in rows)
            {
                if ((string)row["type"] == "Active") { active++; Assert.IsNotNull(row["cooldown_sec"], (string)row["id"]); continue; }
                other++;
                Assert.IsNull(row["cooldown_sec"], $"'{row["id"]}': 비-액티브 줄에 cooldown_sec");
                Assert.IsNull(row["needs_two_tiles"], $"'{row["id"]}': 비-액티브 줄에 needs_two_tiles");
            }
            Assert.Greater(active, 0);
            Assert.Greater(other, 0);
        }

        [Test]
        public void PushPayload_UnrestrictedCards_SeedsAttachHeadersWithoutDataRow()
        {
            var tabs = DcSheetTabs.Default();
            string json = SheetPushPayload.BuildCombinedJson(
                "Defenders", "Enemies", DefenderFolder, EnemyFolder,
                tabs, DcFolder, SkillFolder,
                "CostConfig", ConfigFolder);

            // skill-data-table unit 5 — push 바디 = 새 탭 계약(DcMechanics 없음 · Skills · SkillOwners 있음).
            var root = JObject.Parse(json);
            Assert.IsNull(root["DcMechanics"], "은퇴한 DcMechanics 탭을 push 하면 안 된다");
            Assert.IsNull(root["DcCardEffects"], "은퇴한 DcCardEffects 탭을 push 하면 안 된다(unit 8 단계 B)");
            Assert.IsNull(root["DcAttackMods"], "은퇴한 DcAttackMods 탭을 push 하면 안 된다(unit 8 단계 B)");
            Assert.Greater(((JArray)root["Skills"]).Count, 0, "Skills 탭(효과 줄)이 push 바디에 있다");
            Assert.Greater(((JArray)root["SkillOwners"]).Count, 0, "SkillOwners 탭(소유 줄)이 push 바디에 있다");

            Assert.IsNull(root["DcCards"], "unit 9 — 옛 탭 이름 DcCards 로 push 하면 안 된다(→ Cards)");
            var rows = (JArray)root["Cards"];
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
                Assert.AreEqual("", (string)row["attach_type"]);
                Assert.AreEqual("", (string)row["attach_value"]);
                Assert.AreEqual(2, row.Count, "헤더 시드에는 attach 두 키 외 데이터가 없어야 한다");
            }

            Assert.AreEqual(1, seedCount, "키 없는 Push 전용 헤더 시드는 정확히 하나");
            Assert.AreEqual(rows.Count - 1, cardCount, "헤더 시드는 실제 카드 행 수에 포함되지 않는다");
        }
    }
}
