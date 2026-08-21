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

        // 배치 스킬이 없는 유닛까지 흔들면 «배치 = 항상 흔들림» 이 되어 스킬의 신호가 죽는다.
        [Test]
        public void PlainDefenders_DoNotShake()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog");
            var unit = catalog.ById("archer");
            Assert.IsNotNull(unit, "archer");
            Assert.AreEqual(0f, unit.onPlaceShakeStrength,
                "궁수는 배치 스킬 셰이크 대상이 아니다");
        }
    }
}
