using NUnit.Framework;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // skill-layer-foundation unit 2b — 진영 상대화의 순수 코어를 고정한다.
    //
    // 이 테스트가 지키는 것은 값이 아니라 **대칭성**이다. arm 56곳이 리터럴을 버리고
    // 이 함수로 옮겨오는데, 여기가 비대칭이면 「누구든 쓸 수 있다」가 한쪽 진영에서만
    // 참이 되고 그 사실이 조용히 숨는다.
    public class FactionRelationTests
    {
        [Test]
        public void Opponents_AreSymmetric_BetweenUnits()
        {
            // 방어유닛의 상대는 적 유닛이고, 그 역도 같다. 이 두 줄이 깨지면
            // 스킬이 host 를 바꿀 때 엉뚱한 쪽을 때린다.
            Assert.AreEqual(Faction.EnemyUnit, FactionRelation.OpponentUnitsOf(Faction.DefenderUnit));
            Assert.AreEqual(Faction.DefenderUnit, FactionRelation.OpponentUnitsOf(Faction.EnemyUnit));
        }

        [Test]
        public void Allies_IncludeSelf()
        {
            // 자기 버프·자기 실드가 이 술어를 탄다. 자신을 아군에서 빼면
            // SelfStatBuff 계열이 통째로 조용히 죽는다.
            Assert.AreEqual(Faction.DefenderUnit, FactionRelation.AllyUnitsOf(Faction.DefenderUnit));
            Assert.AreEqual(Faction.EnemyUnit, FactionRelation.AllyUnitsOf(Faction.EnemyUnit));
            Assert.IsTrue(FactionRelation.AreAllies(Faction.DefenderUnit, Faction.DefenderUnit));
            Assert.IsTrue(FactionRelation.AreAllies(Faction.EnemyUnit, Faction.EnemyUnit));
        }

        [Test]
        public void Structures_ResolveThroughTheirSide()
        {
            // 거점은 대상 축이 아니지만 **시전자**로는 올 수 있다(마음이 무언가를 쏘는 경우).
            // 그때도 진영은 자기 편을 따라간다 — 거점이라고 중립이 되지 않는다.
            Assert.AreEqual(Faction.EnemyUnit, FactionRelation.OpponentUnitsOf(Faction.DefenderCore));
            Assert.AreEqual(Faction.EnemyUnit, FactionRelation.OpponentUnitsOf(Faction.DefenderInstinct));
            Assert.AreEqual(Faction.DefenderUnit, FactionRelation.OpponentUnitsOf(Faction.EnemyCore));
        }

        [Test]
        public void Neutral_And_None_HitNobody()
        {
            // 조용한 오폭 금지. 진영을 모르면 **아무도 안 때리는** 것이 안전한 실패다 —
            // 기본값이 「적」이면 미지정 시전자가 늘 한쪽을 때린다.
            Assert.AreEqual(Faction.None, FactionRelation.OpponentUnitsOf(Faction.None));
            Assert.AreEqual(Faction.None, FactionRelation.OpponentUnitsOf(Faction.NeutralUnit));
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.None, Faction.EnemyUnit));
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.NeutralUnit, Faction.DefenderUnit));
        }

        [Test]
        public void BlockingHazard_IsNotAUnitSide()
        {
            // 방벽은 진영 축 밖이다(Faction.cs 주석: "거점이 아니다 — 종류 축 밖에 남는다").
            // 시전자로 오면 아무도 안 때린다.
            Assert.AreEqual(Faction.None, FactionRelation.OpponentUnitsOf(Faction.BlockingHazard));
            Assert.AreEqual(Faction.None, FactionRelation.AllyUnitsOf(Faction.BlockingHazard));
        }
    }
}
