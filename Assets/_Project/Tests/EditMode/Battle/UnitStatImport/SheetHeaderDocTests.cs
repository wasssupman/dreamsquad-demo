using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Somnia.Battle.Data.StatImport;
using Somnia.Battle.Editor.UnitStatImport;

namespace Somnia.Battle.Tests.EditMode.UnitStatImport
{
    // skill-data-table unit 9 — **시트 헤더 문서 = DTO 의 JSON 이름**(문서가 코드와 따로 늙지 않게). `5_sheet_io.md` 「실제 시트 설정」의 탭별
    // 헤더 줄(사용자가 시트 1행에 그대로 친다)이 export 가 쓰는 열 순서(`SheetColumns.Of`)와 글자까지 같아야 한다. 디스크 읽기만(문서 한 파일).
    public class SheetHeaderDocTests
    {
        private const string DocPath = "docs/spec/skill-data-table/5_sheet_io.md";

        // 문서에 두지 않는 열 — 폐기 호환 칸(옛 `atk` 개명 경고용 · export 가 안 쓴다 · 시트에 만들지 않는다).
        private static readonly HashSet<string> NotInSheet = new HashSet<string> { "attack_damage" };

        private static readonly (string tab, Type row)[] Tabs =
        {
            (DcSheetTabs.Skills, typeof(SkillRowDto)),
            (DcSheetTabs.SkillOwners, typeof(SkillOwnerRowDto)),
            (DcSheetTabs.Cards, typeof(DcSheetExporter.CardRow)),
            ("Defenders", typeof(DefenderStatDto)),
            ("Enemies", typeof(EnemyStatDto)),
            (DcSheetTabs.ActiveSkills, typeof(DcSheetExporter.SkillRow)),
            (DcSheetTabs.Config, typeof(DcConfigDto)),
            ("CostConfig", typeof(CostConfigDto)),
        };

        private static Dictionary<string, string[]> DocHeaders()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DocPath));
            Assert.IsTrue(File.Exists(path), "문서가 없다: " + path);
            var result = new Dictionary<string, string[]>();
            foreach (var line in File.ReadAllLines(path))
            {
                var m = Regex.Match(line, @"^- `(\w+)` 헤더: `([^`]+)`$");
                if (!m.Success) continue;
                result[m.Groups[1].Value] = m.Groups[2].Value.Split(',').Select(x => x.Trim()).ToArray();
            }
            return result;
        }

        [Test]
        public void SheetHeaderDoc_MatchesDtoJsonNames_ForEveryTab()
        {
            var doc = DocHeaders();
            CollectionAssert.AreEquivalent(Tabs.Select(t => t.tab), doc.Keys, "문서의 탭 = 8탭(Skills · SkillOwners · Cards · Defenders · Enemies · DcSkills · DcConfig · CostConfig)");
            foreach (var (tab, row) in Tabs)
            {
                var expected = SheetColumns.Of(row).Where(c => !NotInSheet.Contains(c)).ToArray();
                CollectionAssert.AreEqual(expected, doc[tab], $"`{tab}` 헤더 문서가 DTO 와 다르다 — 코드: {string.Join(", ", expected)}");
            }
        }

        [Test]
        public void EveryColumn_IsSnakeCase_AndInfoColumnsComeLast()
        {
            foreach (var (tab, row) in Tabs)
            {
                var cols = SheetColumns.Of(row);
                foreach (var c in cols)
                    Assert.IsTrue(Regex.IsMatch(c, "^_?[a-z][a-z0-9_]*$"), $"`{tab}`.{c} 가 스네이크가 아니다");
                int firstInfo = Array.FindIndex(cols, c => c.StartsWith("_"));
                if (firstInfo >= 0)
                    Assert.IsTrue(cols.Skip(firstInfo).All(c => c.StartsWith("_")), $"`{tab}` 정보 열(`_`)은 맨 오른쪽");
            }
        }
    }
}
