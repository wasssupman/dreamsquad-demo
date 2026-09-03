using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — PlacementLayerTests 에서 추출한 실카탈로그 검증.
    // 층 비트필드 판정 로직 테스트(합성 맵)는 코어 lane 에 남는다.
    public class CatalogPlacementLayerTests
    {
        // 2026-08-17 사용자 결정 — 지상 전용 **원거리** 예외 목록(지면 착탄 폭발이라 하늘에 안 닿음).
        // rev 2026-09-03 사용자 결정 — **근접(사거리 ≤1, 비지원)도 전원 지상 전용**: 손이 닿을 리
        // 없는 근접이 비행 적을 때리던 버그 교정. 근접은 id 목록이 아니라 사거리로 파생한다
        // (신규 근접 유닛 자동 포함 — DefenderMeleeAirTargetTests 가 폴더 전수판).
        private static readonly string[] GroundOnlyDefenderIds = { "artillery", "bomb_man" };

        [Test]
        public void DefenderCatalog_LayerAuthoring_MatchesRangeRule()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog);

            DefenderUnitData antiAir = null;
            int groundOnlySeen = 0;
            foreach (var unit in catalog.units)
            {
                Assert.IsNotNull(unit, "DefenderCatalog contains a null unit");
                if (unit.id == "anti_air") antiAir = unit;

                if (System.Array.IndexOf(GroundOnlyDefenderIds, unit.id) >= 0)
                {
                    groundOnlySeen++;
                    Assert.AreEqual(PlacementLayer.Path, unit.EffectiveAttackTargetLayers,
                        $"{unit.id} 는 지상 전용 예외라 공중 적을 공격하면 안 된다");
                    continue;
                }

                if (!unit.targetAllies && unit.attackRange <= 1)
                {
                    Assert.AreEqual(PlacementLayer.Path, unit.EffectiveAttackTargetLayers,
                        $"근접 {unit.id} 는 지상 전용이어야 한다(rev 2026-09-03)");
                    continue;
                }

                Assert.AreEqual(PlacementLayer.Path | PlacementLayer.Air,
                    unit.EffectiveAttackTargetLayers,
                    $"원거리 {unit.id} 은 지상과 공중 적을 모두 공격해야 한다");
            }

            Assert.AreEqual(GroundOnlyDefenderIds.Length, groundOnlySeen,
                "지상 전용 예외 목록의 유닛이 카탈로그에서 사라지면 이 테스트가 침묵한다");
            // id 는 `anti_air` 그대로다 — 표시 이름만 「넉백머신」으로 바뀌었다(2026-08-17).
            // id 를 바꾸면 저장된 덱(profile.json)이 Validate 에 걸려 안 열린다.
            Assert.IsNotNull(antiAir, "넉백머신(anti_air) 데이터가 카탈로그에 등록돼야 한다");
            Assert.AreEqual(Wassup.Battle.Units.Faction.EnemyUnit, antiAir.targetFactions);
            // cooldown·magnitude 는 시트 소유 — 값은 자유 튜닝, 여기서는 구조만
            // (test-suite-fast-lane unit 1).
            Assert.Greater(antiAir.attackCooldown, 0f, "쿨다운 0 이면 매 프레임 발사로 폭주한다");
            Assert.AreEqual(1, antiAir.outputs.Length);
            Assert.Greater(antiAir.outputs[0].magnitude, 0f, "피해 0 이면 대공이 장식이 된다");
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
            Assert.Greater(skimmer.attackCooldown, 0f,
                "쿨다운은 시트 소유 — 값은 자유 튜닝, 0 만 아니면 된다");
        }
    }
}
