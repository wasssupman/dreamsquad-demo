using NUnit.Framework;
using Wassup.Core.Session;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction unit 14 — 종료 사유 → 로그/트레이스 문자열 표.
    //
    // 이 표가 존재하는 이유는 "한 글자도 달라선 안 된다" 인데, 그 주장의 유일한 증인이던 골든은
    // `victory`/`victory_timeout`/`defeat` 중 시나리오가 실제로 도달하는 것만 덮고 `aborted`·`none`
    // 은 **한 번도 거치지 않는다**. 리뷰 지적대로 표만 있고 증인이 없었으므로 여기서 5종을 못박는다.
    //
    // 문자열이 바뀌면 `BattleLogger.SetResult` 기록과 `CaptureLegacyTraceResult` 의 outcome 이
    // 함께 바뀌어 골든 byte diff 가 난다 — 즉 이 테스트는 골든보다 먼저 깨지는 조기 경보다.
    public class MatchOutcomeNamesTests
    {
        [Test]
        public void Of_MapsEveryOutcome_ToItsLegacyString()
        {
            Assert.AreEqual("victory", MatchOutcomeNames.Of(MatchOutcome.Victory));
            Assert.AreEqual("victory_timeout", MatchOutcomeNames.Of(MatchOutcome.VictoryTimeout));
            Assert.AreEqual("defeat", MatchOutcomeNames.Of(MatchOutcome.Defeat));
            Assert.AreEqual("aborted", MatchOutcomeNames.Of(MatchOutcome.Aborted));
            Assert.AreEqual("none", MatchOutcomeNames.Of(MatchOutcome.None));
        }

        // enum 에 값이 추가되면 표가 조용히 "none" 을 돌려주는 것을 막는다.
        [Test]
        public void Of_CoversEveryEnumMember()
        {
            foreach (MatchOutcome outcome in System.Enum.GetValues(typeof(MatchOutcome)))
            {
                string name = MatchOutcomeNames.Of(outcome);
                if (outcome == MatchOutcome.None)
                {
                    Assert.AreEqual("none", name);
                    continue;
                }
                Assert.AreNotEqual("none", name,
                    $"MatchOutcome.{outcome} 에 대응 문자열이 없어 'none' 으로 접혔다 — 표에 arm 을 추가할 것");
            }
        }
    }
}
