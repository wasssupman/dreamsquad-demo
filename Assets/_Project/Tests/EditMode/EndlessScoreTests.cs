using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // three-minute-survival unit 3 — 무한 모드도 **같은 산식**을 쓴다.
    //
    // 구 endless-mode unit 4 는 "시간축 0 + 누수가 스트레스 예산까지 선형 감소" 를 모드 가설의
    // 수학적 코어로 고정했다. 그 두 성질은 이제 존재하지 않는다: 시간·스트레스 축이 폐기됐고
    // 무한 모드도 처치로만 점수를 번다. 모드 분기가 산식에 없다는 것이 지금 고정할 성질이다.
    //
    // (무한 모드의 리스크/리워드 재설계는 spec 후속 후보 — 당기기 제거로 기존 가설이 소멸했다.)
    public class EndlessScoreTests
    {
        private static MatchTally Tally(int killScore, int leaks)
            => new MatchTally("victory", true, killScore, killCount: 1,
                stability: 20, stabilityMax: 20, waveReached: 30, leaks: leaks);

        [Test]
        public void EndlessUsesTheSameKillOnlyFormula()
        {
            // 같은 처치 점수면 모드와 무관하게 같은 총점이다 — 산식에 모드 인자가 없다.
            Assert.AreEqual(1234, Tally(1234, leaks: 0).Total);
        }

        [Test]
        public void LeaksDoNotReduceScore()
        {
            // 유출은 안정도를 깎을 뿐 점수를 깎지 않는다(브리지에서 처리, 성적 값 밖).
            // 유출이 점수를 깎던 구 스트레스 축이 되살아나면 여기서 잡힌다.
            Assert.AreEqual(Tally(500, leaks: 0).Total, Tally(500, leaks: 9).Total,
                "유출 수는 점수에 닿지 않는다");
        }

        // unit 6·7 — 「안정도는 제출값의 tie-break 자리에만 들어간다」를 고정하던 테스트는
        // 그 자리 자체가 폐기되어 삭제했다. 안정도가 점수 경로에 없다는 단언은
        // MatchTallyTests.SubmissionScore_IgnoresStability 가 갖는다.
    }
}
