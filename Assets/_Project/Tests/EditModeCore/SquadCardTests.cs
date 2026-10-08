using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Skills;
using static Somnia.Battle.Tests.EditMode.Core.CoreCardFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7b — Squad 카드의 주인은 **숙주 유닛**이다(정정 1 · C1). 수명 = 소멸 ∪ 퇴근.
    [TestFixture]
    public class SquadCardTests
    {
        private static (BattleMatch m, int card) Board(int classMask = 0, int subjectCost = 0)
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 1;
            int card = AddSquadCard(def, "all_as", 1, SkillStatKind.AttackSpeedMul, 1.5f, classMask, subjectCost);
            return (CardBattle(def), card);
        }

        [Test]
        public void 증상_Squad_카드를_붙인_유닛을_퇴근시키면_판_전체_버프가_사라진다()
        {
            var (m, card) = Board();
            var host = Defender(m, new int2(3, 1));
            var other = Defender(m, new int2(5, 3));
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), host.Id)).Accepted);
            Assert.AreEqual(1.5f, other.Modifiers.Effective.AttackSpeedMul, 1e-4f, "붙는 순간 판 위 전원");
            Assert.AreEqual(1.5f, host.Modifiers.Effective.AttackSpeedMul, 1e-4f, "숙주도 수혜자다");

            Assert.IsTrue(m.Apply(Command.Retire(host.Id)).Accepted);

            Assert.AreEqual(1f, other.Modifiers.Effective.AttackSpeedMul, 1e-4f, "퇴근해도 회수된다");
        }

        [Test]
        public void 회수는_항등_재발행이_아니라_슬롯_삭제다()
        {
            var (m, card) = Board();
            var host = Defender(m, new int2(3, 1));
            var other = Defender(m, new int2(5, 3));
            int before = other.Modifiers.Count;
            m.Apply(Command.AttachCard(EntryOf(m, card), host.Id));
            Assert.AreEqual(before + 1, other.Modifiers.Count);

            host.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            CoreCombatFixtures.Tick(m, 3);

            Assert.AreEqual(before, other.Modifiers.Count, "칸이 사라졌다(옛 「배율 1.0 재발행」은 칸이 남았다)");
        }

        [Test]
        public void 이후_배치되는_유닛은_배치_사건으로_상속한다_한_번만()
        {
            var (m, card) = Board();
            var host = Defender(m, new int2(3, 1));
            m.Apply(Command.AttachCard(EntryOf(m, card), host.Id));

            Assert.IsTrue(m.Apply(Command.PlaceDefender(0, new int2(6, 3))).Accepted);
            var placed = m.World.Units[m.World.Units.Count - 1];
            CoreCombatFixtures.Tick(m, 5);

            Assert.IsFalse(placed.Deploying);
            Assert.AreEqual(1.5f, placed.Modifiers.Effective.AttackSpeedMul, 1e-4f, "상속됐다");
            Assert.AreEqual(1, placed.Modifiers.Count, "한 칸 — 부착 즉시 전개와 상속이 겹치지 않는다");
        }

        [Test]
        public void 배치_중인_유닛은_부착_즉시_전개에서_빠지고_활성화에서_한_번_받는다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 1;
            def.Units[0].DeployMotionSeconds = 0.5f;   // 배치 모션 동안 「배치 중」
            int card = AddSquadCard(def, "all_as", 1, SkillStatKind.AttackSpeedMul, 1.5f);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1));
            m.Apply(Command.PlaceDefender(0, new int2(6, 3)));
            var placed = m.World.Units[m.World.Units.Count - 1];
            m.Apply(Command.LandDefender(placed.Id));
            Assert.IsTrue(placed.Deploying);

            m.Apply(Command.AttachCard(EntryOf(m, card), host.Id));
            Assert.AreEqual(0, placed.Modifiers.Count, "배치 중엔 안 건다");
            CoreCombatFixtures.Tick(m, 60);

            Assert.IsFalse(placed.Deploying);
            Assert.AreEqual(1, placed.Modifiers.Count, "활성화 사건에서 한 번");
        }

        [Test]
        public void 축은_직업과_코스트를_가른다()
        {
            var def = CoreMatchFixtures.Definition();
            var ranger = CoreMatchFixtures.Defender("ranger");
            ranger.Role = 1; ranger.Cost = 3;
            var cheap = CoreMatchFixtures.Defender("cheap");
            cheap.Role = 2; cheap.Cost = 1;
            def.Units = new[] { def.Units[0], ranger, cheap };
            int rangerCard = AddSquadCard(def, "ranger_as", 1, SkillStatKind.AttackSpeedMul, 1.5f, classMask: 1 << 1);
            int costCard = AddSquadCard(def, "cost1_dmg", 1, SkillStatKind.DamageMul, 1.2f, subjectCost: 1);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 1), 0);
            var r = Defender(m, new int2(5, 1), 1);
            var c = Defender(m, new int2(7, 1), 2);

            m.Apply(Command.AttachCard(EntryOf(m, rangerCard), host.Id));
            m.Apply(Command.AttachCard(EntryOf(m, costCard), host.Id));

            Assert.AreEqual(1.5f, r.Modifiers.Effective.AttackSpeedMul, 1e-4f);
            Assert.AreEqual(1f, c.Modifiers.Effective.AttackSpeedMul, 1e-4f, "직업이 다르다");
            Assert.AreEqual(1.2f, c.Modifiers.Effective.DamageMul, 1e-4f);
            Assert.AreEqual(1f, r.Modifiers.Effective.DamageMul, 1e-4f, "코스트가 다르다");
        }
    }
}
