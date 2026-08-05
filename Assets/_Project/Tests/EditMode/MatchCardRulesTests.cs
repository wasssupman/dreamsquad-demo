using NUnit.Framework;
using Wassup.Core.Session;
using Wassup.Sim.Match;

// battle-sim-extraction unit 16-C — 카드 커밋 적법성 판정.
//
// 적출 전에는 이 판정이 네 곳(`TryGetUsable`·`TryGetUsableAttach`·`TryGetUsableActive` +
// `CommitAttach` 본문)에 흩어져 있었고 전부 `bool` 만 돌려줘서, 어떤 조건에 걸렸는지 확인하려면
// 컨트롤러 + 덱 + Bridge + World 를 세워야 했다. 이제 값만으로 단정한다.
//
// 골든은 이 규칙을 증인하지 못한다 — `dreamcatcher_heavy` 는 `ApplyHarnessDreamcatcherCard` 로
// **컨트롤러·손패·게이지·부착제한을 통째로 우회**한다(하네스 전용 seam).
namespace Wassup.Tests.EditMode
{
    public class MatchCardRulesTests
    {
        /// 모든 게이트를 통과하는 기준 입력. 각 테스트는 여기서 한 축만 무너뜨린다.
        static MatchCardRules.CommitInputs Ok() => new MatchCardRules.CommitInputs
        {
            CardExists = true,
            InHand = true,
            TypeMatches = true,
            SkillWired = true,
            Gauge = 10,
            Cost = 10,          // 경계: 같으면 통과(>= 계약)
            LeakRemaining = 5,
            LeakCost = 0,
            AttachedToHost = 0,
            AttachCap = 3,
        };

        [Test]
        public void 기준_입력은_통과한다()
        {
            Assert.AreEqual(CommandReject.None, MatchCardRules.Check(Ok()));
        }

        [Test]
        public void 덱에_없으면_NotInHand()
        {
            var c = Ok(); c.CardExists = false;
            Assert.AreEqual(CommandReject.Card_NotInHand, MatchCardRules.Check(c));
        }

        [Test]
        public void 덱에_있어도_손패_밖이면_NotInHand()
        {
            // 이 구분이 부분 커밋 구멍의 핵심이었다: `TryGetCard`(큐 또는 부착)는 통과하는데
            // `IndexInHand`(큐 앞 N칸)는 실패해서, 검증 후 커밋 단계에서 터졌다.
            var c = Ok(); c.InHand = false;
            Assert.AreEqual(CommandReject.Card_NotInHand, MatchCardRules.Check(c));
        }

        [Test]
        public void 종류가_다르면_WrongType()
        {
            var c = Ok(); c.TypeMatches = false;
            Assert.AreEqual(CommandReject.Card_WrongType, MatchCardRules.Check(c));
        }

        [Test]
        public void 게이지가_모자라면_InsufficientGauge()
        {
            var c = Ok(); c.Gauge = 9;
            Assert.AreEqual(CommandReject.Card_InsufficientGauge, MatchCardRules.Check(c));
        }

        [Test]
        public void 게이지가_정확히_같으면_통과한다()
        {
            var c = Ok(); c.Gauge = 10; c.Cost = 10;
            Assert.AreEqual(CommandReject.None, MatchCardRules.Check(c));
        }

        [Test]
        public void Active_스킬_미배선은_InternalError()
        {
            // 시트/배선 버그다 — 플레이어가 고칠 수 있는 거절이 아니므로 Card_* 로 뭉개지 않는다.
            var c = Ok(); c.SkillWired = false;
            Assert.AreEqual(CommandReject.Session_InternalError, MatchCardRules.Check(c));
        }

        [Test]
        public void 게이지가_스킬_배선보다_먼저_판정된다()
        {
            // 순서가 계약이다 — 적출 전에도 둘 다 실패하면 게이지 사유가 나왔다.
            var c = Ok(); c.Gauge = 0; c.SkillWired = false;
            Assert.AreEqual(CommandReject.Card_InsufficientGauge, MatchCardRules.Check(c));
        }

        [Test]
        public void 지불하면_잔여가_1_미만이_되는_유출은_거절()
        {
            // "지불로 즉시 패배" 를 구조적으로 금지한다.
            var c = Ok(); c.LeakRemaining = 3; c.LeakCost = 3;   // 3-3=0 < 1
            Assert.AreEqual(CommandReject.Card_LeakAllowanceTooLow, MatchCardRules.Check(c));
        }

        [Test]
        public void 지불_후_잔여가_정확히_1이면_통과()
        {
            var c = Ok(); c.LeakRemaining = 3; c.LeakCost = 2;   // 3-2=1
            Assert.AreEqual(CommandReject.None, MatchCardRules.Check(c));
        }

        [Test]
        public void 유출_코스트가_0이면_잔여를_보지_않는다()
        {
            var c = Ok(); c.LeakRemaining = 0; c.LeakCost = 0;
            Assert.AreEqual(CommandReject.None, MatchCardRules.Check(c));
        }

        [Test]
        public void 부착_캡에_도달하면_AttachCapReached()
        {
            var c = Ok(); c.AttachedToHost = 3; c.AttachCap = 3;
            Assert.AreEqual(CommandReject.Card_AttachCapReached, MatchCardRules.Check(c));
        }

        [Test]
        public void 캡_0은_미적용이다_적_표식_경로()
        {
            // `CommitMarkEnemy` 는 부착 캡을 의도적으로 보지 않는다(캡은 defender 슬롯 개념).
            var c = Ok(); c.AttachedToHost = 99; c.AttachCap = 0;
            Assert.AreEqual(CommandReject.None, MatchCardRules.Check(c));
        }

        [Test]
        public void 손패_판정이_캡보다_먼저다()
        {
            var c = Ok(); c.InHand = false; c.AttachedToHost = 99;
            Assert.AreEqual(CommandReject.Card_NotInHand, MatchCardRules.Check(c));
        }
    }
}
