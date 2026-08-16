using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — EnemyTierBakeTests 에서 추출한 실에셋 검증.
    // bake 로직 테스트(합성 픽스처)는 코어 lane 에 남고, 여기는 카탈로그·보스 에셋의
    // 저작 상태만 본다.
    public class EnemyCatalogAuthoringTests
    {
        // 카탈로그 전수 pin — 슬라임 사슬만 보는 저작 테스트로는 **미래의 순환 적**을 못 잡는다.
        // 런타임 카운터를 추가하지 않고 순환을 «돌기 전에» 잡는 가장 싼 자리다(리뷰 A-M2).
        [Test]
        public void EveryCatalogEnemy_HasValidSplitChain()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/_Project/Data/EnemyCatalog.asset");
            Assert.IsNotNull(catalog, "EnemyCatalog");
            Assert.IsNotNull(catalog.units);
            foreach (var u in catalog.units)
            {
                if (u == null) continue;
                Assert.IsTrue(SplitChain.Validate(u, out string err),
                    $"{u.displayName}: {err}");
            }
        }

        // 마이그레이션 pin — 라이브 보스 3종이 tier=Boss 를 잃으면 «보스가 잡몹처럼 굴고
        // 경보도 안 뜨는» 형태로 조용히 회귀한다. 에셋 저작 실수를 여기서 잡는다.
        [TestCase("Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset")]
        [TestCase("Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset")]
        [TestCase("Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset")]
        public void LiveBossAssets_AreTaggedBoss(string path)
        {
            var unit = AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
            Assert.IsNotNull(unit, $"에셋을 찾지 못했다: {path}");
            Assert.AreEqual(EnemyTier.Boss, unit.tier,
                $"{unit.displayName}: tier 가 Boss 가 아니다 — BossTag 가 안 붙어 CC·어그로 면역과 " +
                "등장경보가 통째로 사라진다");
            Assert.IsTrue(unit.nightmareMechanics != null && unit.nightmareMechanics.Length > 0,
                $"{unit.displayName}: 보스인데 메커닉이 없다");
        }
    }
}
