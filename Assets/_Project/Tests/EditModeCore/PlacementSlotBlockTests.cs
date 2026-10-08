using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 5b — **트레이가 읽는 자.**
    //
    // 트레이 칸의 도색과 드롭 거절이 **같은 함수**에서 나오는지를 못박는다. 옛 트레이는
    // 「판 위에 몇이지 / 쿨이 남았나 / 살 수 있나」를 각자 세었고, 그래서 배치 판정과 답이
    // 갈릴 수 있었다 — 그 갈림은 「초록인데 놓으면 거절」로만 보인다.
    //
    // 순서 「소진 &gt; 쿨타임 &gt; 코스트」가 규칙인 이유는 **소진이 더 오래 가는 답**이기
    // 때문이다: 상한 1 짜리 유닛은 쿨이 끝나도 여전히 못 놓는다. 둘 다 걸렸을 때 「재배치
    // 대기 중」이라고 답하면 플레이어는 기다리면 된다고 배운다 — 거짓이다.
    [TestFixture]
    public class PlacementSlotBlockTests
    {
        // 상한 1 + 쿨타임 + 빈 지갑을 **한꺼번에** 걸어 두고 무엇이 먼저 답하는지 본다.
        private static BattleMatch AllThreeBlocked()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].MaxOnBoard = 1;
            def.Units[0].PlacementCooldown = 5f;
            def.Units[0].Cost = 5;
            def.Mode.Cost.Start = 5f;      // 딱 한 번 놓을 만큼만
            def.Mode.Cost.RegenPerSec = 0f;
            def.ConfigHash = def.ComputeConfigHash();

            var match = CoreMatchFixtures.BeginBattle(def);
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted,
                "첫 배치는 통과해야 나머지 셋이 동시에 걸린다");
            return match;
        }

        [Test]
        public void 소진이_쿨타임보다_먼저_답한다()
        {
            var match = AllThreeBlocked();
            Assert.AreEqual(RejectReason.LimitReached, match.Placement.SlotBlock(0),
                "쿨이 끝나도 못 놓는데 「대기 중」이라고 답하면 플레이어가 배우는 것이 틀린다");
        }

        [Test]
        public void 쿨타임이_코스트보다_먼저_답한다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].MaxOnBoard = 4;        // 소진은 비켜 둔다
            def.Units[0].PlacementCooldown = 5f;
            def.Units[0].Cost = 5;
            def.Mode.Cost.Start = 5f;
            def.Mode.Cost.RegenPerSec = 0f;
            def.ConfigHash = def.ComputeConfigHash();

            var match = CoreMatchFixtures.BeginBattle(def);
            Assert.IsTrue(match.Apply(Command.PlaceDefender(0, new int2(3, 1))).Accepted);
            Assert.AreEqual(RejectReason.OnCooldown, match.Placement.SlotBlock(0),
                "「구조 > 자원」 — 자원은 언제나 마지막이다");
        }

        [Test]
        public void 드롭_거절과_칸_도색은_같은_답이다()
        {
            var match = AllThreeBlocked();
            // 놓을 수 있는 자리에 **실제로** 떨궈 본다. 자리가 원인이 아닌데 답이 다르면
            // 화면과 판정이 갈린 것이다.
            var receipt = match.Apply(Command.PlaceDefender(0, new int2(6, 1)));
            Assert.IsFalse(receipt.Accepted);
            Assert.AreEqual(match.Placement.SlotBlock(0), receipt.Reason,
                "트레이가 읽는 자와 드롭이 받는 자가 다르면 「초록인데 거절」이 난다");
        }

        [Test]
        public void 자리가_원인이면_칸_도색은_아무것도_막지_않는다()
        {
            var match = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            // 판 밖 — 공간이 답한다. 그러나 **칸 자체는 멀쩡하다**: 트레이가 빨개지면
            // 「이 유닛은 못 쓴다」는 거짓말이 된다.
            Assert.AreEqual(RejectReason.OutOfBounds,
                match.Apply(Command.PlaceDefender(0, new int2(99, 99))).Reason);
            Assert.AreEqual(RejectReason.None, match.Placement.SlotBlock(0));
        }

        [Test]
        public void 판이_끝나면_모든_칸이_닫힌다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.SubmitUnlockSeconds = 0f;   // 해금 대기는 이 테스트의 축이 아니다
            def.ConfigHash = def.ComputeConfigHash();

            var match = CoreMatchFixtures.BeginBattle(def);
            Assert.IsTrue(match.Apply(Command.Submit()).Accepted);
            Assert.IsTrue(match.Clock.Ended);
            Assert.AreEqual(RejectReason.MatchEnded, match.Placement.SlotBlock(0));
        }
    }
}
