using NUnit.Framework;
using Wassup.Battle.Units;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // skill-layer-foundation unit 2a — 도메인 핸들과 ECS 컴포넌트의 **값 일치**를 고정한다.
    //
    // 투트랙 리뷰 M3 — 둘이 같은 sentinel 을 써야 한다는 계약이 **주석에만** 있었다.
    // 어긋나면 어댑터 변환에서 미발급이 「0번 유닛」과 섞이고, 그러면 타겟팅 동률 순위를
    // 조용히 훔친다(`SimEntityId` 자신이 경고한 바로 그 사고다).
    // 두 어셈블리를 다 참조하는 EditMode 가 이 핀을 공짜로 들 수 있다.
    public class SkillEntityIdPinTests
    {
        [Test]
        public void UnassignedSentinel_MatchesSimEntityId()
        {
            Assert.AreEqual(SimEntityId.Unassigned, SkillEntityId.UnassignedValue,
                "도메인 핸들과 ECS 컴포넌트의 미발급 sentinel 이 갈렸다 — " +
                "어댑터 변환에서 미발급이 0번 유닛과 섞인다.");
        }

        [Test]
        public void None_IsNotValid_AndRoundTrips()
        {
            Assert.IsFalse(SkillEntityId.None.IsValid);
            Assert.IsTrue(new SkillEntityId(0).IsValid, "0 은 유효한 발급 번호다");
            Assert.AreEqual(new SkillEntityId(7), new SkillEntityId(7));
            Assert.AreNotEqual(new SkillEntityId(7), SkillEntityId.None);
        }

        // 시전자가 판 위에 없는 경우(액티브)를 타입이 표현하는지.
        [Test]
        public void PlayerCaster_HasNoUnit_ButKeepsFaction()
        {
            var c = CasterRef.Player(Faction.DefenderUnit);
            Assert.IsFalse(c.HasUnit);
            Assert.AreEqual(Faction.DefenderUnit, c.Faction);
            Assert.AreEqual(Faction.EnemyUnit, FactionRelation.OpponentUnitsOf(c.Faction),
                "시전자가 없어도 진영은 알아야 후보를 고를 수 있다");
        }
    }
}
