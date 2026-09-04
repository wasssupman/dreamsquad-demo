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

        // distance-based-range unit 20 리뷰 T-2 — **몸 반경 0 저작 금지.**
        // 0 이면 「화면엔 있는데 판정엔 없는」 유닛이 된다: 그림자 지름 = 2r 이라 그림자가
        // 사라지고, 대상일 때 targetR 도 0 이라 상대가 0.5칸 가까이 와야 때린다.
        // ⚠ `bodyRadius` 는 **Boss 티어에서만** 읽힌다(`AttackUnitData.BodyRadiusTiles`) —
        //    Small/Medium/Large 는 티어표라 0 이 나올 수 없고 Boss 만 저작 사고가 가능하다.
        // 이 단언이 뷰의 사일런트 보정을 대신한다: 종전 `QuadUnitView` 는 `Mathf.Max(0.05f, r)`
        // 로 0 을 조용히 0.05 로 올려 «없는 몸» 을 화면이 주장하게 만들었고(그마저 Spine
        // 경로엔 없어 두 뷰가 다른 거짓말을 했다), 리뷰 L-2 에서 그 클램프를 제거했다.
        [Test]
        public void EveryEnemy_HasNonZeroBody()
        {
            int seen = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AttackUnitData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var e = AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
                if (e == null) continue;
                seen++;
                Assert.Greater(e.BodyRadiusTiles, 0f,
                    $"{e.name} — 몸 반경 0. 그림자도 표적도 사라진다 ({path})");
                if (e.bodySize == AttackUnitData.BodySize.Boss)
                    Assert.Greater(e.bodyRadius, 0f,
                        $"{e.name} — Boss 티어인데 bodyRadius 미저작(0). 티어표를 안 타므로 " +
                        $"조용히 몸이 없어진다 ({path})");
            }
            Assert.Greater(seen, 0, "AttackUnitData 를 하나도 못 찾았다 — 경로/타입 규약이 바뀌었나?");
        }
    }
}
