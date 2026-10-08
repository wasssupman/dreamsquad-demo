using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Somnia.Battle.Editor.UnitStatImport;
using Somnia.Battle.Data.StatImport;

namespace Somnia.Battle.Tests.EditMode.UnitStatImport
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
            var unit = UnityEngine.ScriptableObject.CreateInstance<Somnia.Battle.Data.DreamcatcherCard>();
            unit.id = "u"; unit.type = Somnia.Battle.Data.CardType.Unit;
            var squad = UnityEngine.ScriptableObject.CreateInstance<Somnia.Battle.Data.DreamcatcherCard>();
            squad.id = "s"; squad.type = Somnia.Battle.Data.CardType.Squad;
            var active = UnityEngine.ScriptableObject.CreateInstance<Somnia.Battle.Data.DreamcatcherCard>();
            active.id = "a"; active.type = Somnia.Battle.Data.CardType.Active; active.cooldownSec = 0f; active.needsTwoTiles = false;
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

        // battle-content-finish unit 3 (D5) — 시트 push(`SheetPushClient` · `SheetPushPayload`)는 뗐다. 제약 「에이전트는 시트에
        // 쓰지 않는다」의 반대편 도구라서다. push 바디 계약 테스트도 함께 갔다 — 로컬 JSON export 와 헤더 계약은 그대로 지킨다.
    }
}
