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

        // on-place-skill-rework unit 6 — 규칙 경로의 «조용히 빈 문안» 안전망.
        // 라이브 유닛이 실제로 쓰는 조합(OnPlace × payload)이 문안을 갖는지 카탈로그에서
        // 확인한다 — 합성 SO 로는 능력 참조를 만들 수 없다. (enum 전수 순회 쪽
        // `EveryOnPlaceEffectKind_HasAClause` 는 합성 픽스처라 코어 lane 에 남아 있다.)
        [Test]
        public void RuleDrivenOnPlaceUnits_HaveAClause()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog);
            foreach (var unit in catalog.units)
            {
                if (unit == null) continue;
                var ability = unit.GetAbility<UnitSkillAbility>();
                if (ability?.mechanics == null) continue;
                bool hasOnPlaceRule = false;
                foreach (var m in ability.mechanics)
                    if (m.trigger.kind == DcTriggerKind.OnPlace) hasOnPlaceRule = true;
                if (!hasOnPlaceRule) continue;

                Assert.That(UnitKitSummary.Build(unit), Does.Contain("배치"),
                    $"{unit.id}: 배치 규칙이 있는데 문안이 비었다 — OnPlaceRuleClause 에 " +
                    "그 payload 를 배선하라(조용히 비는 것이 이 테스트가 막는 것이다)");
            }
        }
    }
}
