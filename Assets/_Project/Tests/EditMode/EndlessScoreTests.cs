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
        [Test]
        public void EndlessUsesTheSameKillOnlyFormula()
        {
            // 같은 처치 점수면 모드와 무관하게 같은 총점이다 — 산식에 모드 인자가 없다.
            Assert.AreEqual(1234, ScoreMath.Evaluate(1234).Total);
        }

        [Test]
        public void LeaksDoNotReduceScore()
        {
            // 유출은 안정도를 깎을 뿐 점수를 깎지 않는다(브리지에서 처리, 산식 밖).
            // 유출이 점수를 깎던 구 스트레스 축이 되살아나면 여기서 잡힌다.
            var s = ScoreMath.Evaluate(500);
            Assert.AreEqual(500, s.Total, "산식 입력은 처치 점수 하나뿐이다");
        }

        [Test]
        public void StabilityRidesInSubmissionOnly()
        {
            // 안정도는 총점을 바꾸지 않고 제출값의 tie-break 자리에만 들어간다.
            var s = ScoreMath.Evaluate(500);
            int full = ScoreMath.EncodeSubmission(s.Total, 20, 20);
            int empty = ScoreMath.EncodeSubmission(s.Total, 0, 20);
            Assert.AreNotEqual(full, empty, "제출값은 갈린다");
            Assert.AreEqual(ScoreMath.DecodeKillScore(full), ScoreMath.DecodeKillScore(empty),
                "표시 점수는 같다");
        }
    }
}
