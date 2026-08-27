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

        // 투트랙 리뷰 M4 — 음성 케이스만 4개였고 **양성**이 없었다. 대칭성을 지킨다면서
        // 「서로 적이다」가 참인 것을 한 번도 안 물었다.
        [Test]
        public void AreOpponents_IsTrue_AcrossSides_AndFalse_WithinASide()
        {
            Assert.IsTrue(FactionRelation.AreOpponents(Faction.DefenderUnit, Faction.EnemyUnit));
            Assert.IsTrue(FactionRelation.AreOpponents(Faction.EnemyUnit, Faction.DefenderUnit));
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.DefenderUnit, Faction.DefenderUnit));
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.EnemyUnit, Faction.EnemyUnit));
        }

        [Test]
        public void AreAllies_IsFalse_AcrossSides()
        {
            Assert.IsFalse(FactionRelation.AreAllies(Faction.DefenderUnit, Faction.EnemyUnit));
            Assert.IsFalse(FactionRelation.AreAllies(Faction.EnemyUnit, Faction.DefenderUnit));
        }

        // ⚠ **대상 축은 유닛 태그다.** 거점이 대상으로 오면 거짓이어야 한다 — 거점엔
        // CC·실드 버퍼가 없어서 후보에 넣으면 유령이 cap 자리를 차지하고 실제 대상이 준다.
        // 시전자로서의 거점(위 `Structures_ResolveThroughTheirSide`)과 방향이 다르다.
        [Test]
        public void Structures_AreNotTargets()
        {
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.EnemyUnit, Faction.DefenderCore));
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.EnemyUnit, Faction.DefenderInstinct));
            Assert.IsFalse(FactionRelation.AreOpponents(Faction.DefenderUnit, Faction.EnemyCore));
        }

        // 투트랙 리뷰 M3 — 진영 폴백 4단 체인이 ECS 쪽과 브리지 쪽 두 곳에서 쓰인다.
        // 결정 자체는 이 순수 함수 하나가 소유하므로, 그 우선순위를 여기서 고정한다.
        // 한쪽만 고쳐 조용히 갈리는 것을 막는 핀이다.
        [Test]
        public void Resolve_PrefersFactionTag_ThenTags_ThenNone()
        {
            // FactionTag 이 있으면 태그를 무시하고 그 값이 이긴다.
            Assert.AreEqual(Faction.DefenderUnit,
                FactionRelation.Resolve(true, Faction.DefenderUnit, enemyTagged: true, defenderTagged: false));
            // 없으면 적 태그 → 방어 태그 순.
            Assert.AreEqual(Faction.EnemyUnit,
                FactionRelation.Resolve(false, Faction.None, enemyTagged: true, defenderTagged: true));
            Assert.AreEqual(Faction.DefenderUnit,
                FactionRelation.Resolve(false, Faction.None, enemyTagged: false, defenderTagged: true));
            // 아무것도 없으면 None — 조용한 오폭 금지.
            Assert.AreEqual(Faction.None,
                FactionRelation.Resolve(false, Faction.None, enemyTagged: false, defenderTagged: false));
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
