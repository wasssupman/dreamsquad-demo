using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — UnitKitSummaryTests 에서 추출한 실카탈로그 검증.
    // 문장 조립 로직 테스트(합성 유닛)는 코어 lane 에 남는다.
    public class UnitKitCatalogTests
    {
        [Test]
        public void CatalogDescriptions_UseThreeFixedSections()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(catalog.units);
            Assert.IsNotEmpty(catalog.units);

            string[] prefixes = { "기본 기능: ", "배치 스킬: ", "특수 효과: " };
            const int maxCharactersPerLine = 28; // detail card: font 34, 148px = 3 single-line rows
            foreach (var unit in catalog.units)
            {
                Assert.IsNotNull(unit, "DefenderCatalog contains a null unit");
                string description = UnitKitSummary.Describe(unit).Replace("\r\n", "\n");
                string[] lines = description.Split('\n');
                Assert.AreEqual(3, lines.Length, $"{unit.id}: description must have exactly 3 lines");

                for (int i = 0; i < prefixes.Length; i++)
                {
                    Assert.That(lines[i], Does.StartWith(prefixes[i]),
                        $"{unit.id}: line {i + 1} must start with '{prefixes[i]}'");
                    Assert.IsNotEmpty(lines[i].Substring(prefixes[i].Length).Trim(),
                        $"{unit.id}: line {i + 1} body must not be empty");
                    Assert.LessOrEqual(lines[i].Length, maxCharactersPerLine,
                        $"{unit.id}: line {i + 1} must stay on one visual row");
                }
            }
        }
    }
}
