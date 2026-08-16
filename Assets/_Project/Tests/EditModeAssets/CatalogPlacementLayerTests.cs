using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — PlacementLayerTests 에서 추출한 실카탈로그 검증.
    // 층 비트필드 판정 로직 테스트(합성 맵)는 코어 lane 에 남는다.
    public class CatalogPlacementLayerTests
    {
        [Test]
        public void DefenderCatalog_ExistingUnitsArePathOnly_AntiAirTargetsPathAndAir()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog);

            DefenderUnitData antiAir = null;
            foreach (var unit in catalog.units)
            {
                Assert.IsNotNull(unit, "DefenderCatalog contains a null unit");
                if (unit.id == "anti_air")
                {
                    antiAir = unit;
                    continue;
                }

                Assert.AreEqual(PlacementLayer.Path, unit.EffectiveAttackTargetLayers,
                    $"기존 방어유닛 {unit.id}은 공중 적을 공격하면 안 된다");
            }

            Assert.IsNotNull(antiAir, "신규 대공사수 데이터가 카탈로그에 등록돼야 한다");
            Assert.AreEqual(PlacementLayer.Path | PlacementLayer.Air,
                antiAir.EffectiveAttackTargetLayers,
                "대공사수는 지상과 공중 적을 모두 공격한다");
            Assert.AreEqual(Wassup.Battle.Units.Faction.EnemyUnit, antiAir.targetFactions);
            Assert.AreEqual(0.2f, antiAir.attackCooldown, 1e-4f,
                "대공사수는 로스터의 초고속 공격 주기를 사용한다");
            Assert.AreEqual(1, antiAir.outputs.Length);
            Assert.AreEqual(7f, antiAir.outputs[0].magnitude, 1e-4f,
                "초고속 연사를 상쇄하는 낮은 발당 피해");
        }

        [Test]
        public void EnemyCatalog_SkimmerIsSingleTargetFastAttacker()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(
                "Assets/_Project/Data/EnemyCatalog.asset");
            Assert.IsNotNull(catalog);

            var skimmer = catalog.ById("skimmer");
            Assert.IsNotNull(skimmer, "Skimmer가 EnemyCatalog에 등록돼야 한다");
            Assert.AreEqual(PlacementLayer.Air, skimmer.EffectiveTraversalLayers);
            Assert.AreEqual(1, skimmer.attackTargetCount,
                "Skimmer는 범위형이 아니라 단일 타겟 공격이어야 한다");
            Assert.AreEqual(0.2f, skimmer.attackCooldown, 1e-4f,
                "Skimmer는 빠른 공격 주기를 사용한다");
        }
    }
}
