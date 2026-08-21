using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // camera-direction unit 17 — 배치 스킬 셰이크의 **저작 계약**.
    //
    // 세기 값 자체는 체감 튜닝이라 못박지 않는다(밸런스 리터럴 금지 — test-procedure.md).
    // 지키는 것은 «둘 다 저작돼 있어야 울린다» 는 구조다: 브리지가 strength 로만 게이팅하고
    // duration 은 그대로 넘기므로, duration 을 0 으로 두면 Director 가 조용히 삼켜
    // «세기는 있는데 안 울린다» 가 된다. 그 반쪽 저작을 여기서 잡는다.
    public class OnPlaceShakeAuthoringTests
    {
        [TestCase("malphite")]
        [TestCase("cannon")]
        [TestCase("shotgunner")]
        public void OnPlaceSkillUnits_AuthorBothShakeStrengthAndDuration(string unitId)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog");
            var unit = catalog.ById(unitId);
            Assert.IsNotNull(unit, unitId);

            Assert.Greater(unit.onPlaceShakeStrength, 0f,
                $"{unitId}: 배치 스킬 셰이크 세기가 0 이면 브리지가 호출 자체를 건너뛴다");
            Assert.Greater(unit.onPlaceShakeDuration, 0f,
                $"{unitId}: 세기만 있고 길이가 0 이면 Director 가 조용히 삼킨다(duration<=0 = 끔)");
            Assert.LessOrEqual(unit.onPlaceShakeStrength, 1f,
                $"{unitId}: 세기는 0~1 정규값이다(진폭은 CameraDirectionConfig 소유)");
        }

        // 기준은 «배치 스킬이 있는가» 가 **아니다** — 레거시 onPlaceEffect 를 가진 디펜더만
        // 12기고(궁수도 BindNearby 를 갖는다), 그중 흔드는 건 3기뿐이다. 기준은 «평소 못 하는
        // 일을 하는 스킬» 이라는 선별 목록이고, 전부에 달면 «배치 = 항상 흔들림» 이 되어
        // 구분이 사라진다. 그래서 목록 자체를 못박는다 — 늘리려면 이 테스트를 먼저 고쳐야 한다.
        [Test]
        public void ExactlyTheCuratedUnitsShake()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog");

            var expected = new System.Collections.Generic.HashSet<string>
                { "malphite", "cannon", "shotgunner" };
            var actual = new System.Collections.Generic.List<string>();
            foreach (var u in catalog.units)
                if (u != null && u.onPlaceShakeStrength > 0f) actual.Add(u.id);
            actual.Sort();

            CollectionAssert.AreEquivalent(expected, actual,
                "셰이크를 저작한 유닛 목록이 바뀌었다 — 의도한 확장이면 이 목록을 갱신하고, "
                + "아니면 «평소 못 하는 일을 하는 스킬만» 기준을 다시 확인하라. 실측: "
                + string.Join(", ", actual));
        }
    }
}
