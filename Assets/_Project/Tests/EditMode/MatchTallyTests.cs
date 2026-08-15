using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // three-minute-survival unit 7 — 판 성적 값의 계약을 고정한다(구 ScoreMathTests).
    //
    // 총점 = 처치한 적의 killScore 합. 산식에 분기가 없다 — 시간·스트레스 축과
    // "패배 시 0" 예외는 폐기됐다(져도 잡은 만큼은 남는다).
    //
    // unit 6 — **서버에 보내는 수 = 총점**이다. 인코딩(BASE + kill×1000 + 안정도permille)과
    // 디코딩은 제거됐다. 안정도가 다시 제출값에 새어 들어가면 SubmissionScore 단언이 잡는다.
    public class MatchTallyTests
    {
        private static MatchTally Tally(int killScore, int stability = 12, int stabilityMax = 20)
            => new MatchTally("victory", true, killScore, killCount: 3,
                stability, stabilityMax, waveReached: 7, leaks: 2);

        [Test]
        public void Total_IsKillScoreSum()
        {
            var t = Tally(47);
            Assert.AreEqual(47, t.KillScore);
            Assert.AreEqual(47, t.Total, "처치 축이 유일하므로 총점 == 처치 점수");
        }

        [Test]
        public void SubmissionScore_IsTotal_Unmodified()
        {
            Assert.AreEqual(47, Tally(47).SubmissionScore,
                "서버에 보내는 수는 총점 그대로다 — 가공 지점이 생기면 여기서 잡힌다");
        }

        [Test]
        public void SubmissionScore_IgnoresStability()
        {
            // 안정도는 패배 조건과 결과 화면의 정보 줄일 뿐, 점수 경로에 없다.
            Assert.AreEqual(Tally(47, stability: 20).SubmissionScore,
                            Tally(47, stability: 0).SubmissionScore,
                "같은 처치 점수면 안정도가 어떻든 제출값이 같아야 한다");
        }

        [Test]
        public void NegativeCounts_ClampToZero()
        {
            Assert.AreEqual(0, Tally(-5).Total);
            Assert.AreEqual(0, new MatchTally("defeat", false, 0, -3, 0, 20, 0, 0).KillCount);
        }

        [Test]
        public void ZeroKills_IsZero()
        {
            Assert.AreEqual(0, Tally(0).Total);
        }

        [Test]
        public void LargeKillScore_IsNotRescaled()
        {
            // unit 6 — 상한·saturate 는 인코딩이 만들던 제약이었다. 이제 어떤 값이든
            // 손대지 않고 그대로 나간다(구 상한 1,147,482 를 넘겨 확인).
            Assert.AreEqual(2_000_000, Tally(2_000_000).SubmissionScore);
        }

        [Test]
        public void CarriesResultLabels()
        {
            var t = new MatchTally("defeat_timeout", false, 9, 4, 0, 20, 11, 5);
            Assert.AreEqual("defeat_timeout", t.Outcome, "배틀 로그 라벨");
            Assert.IsFalse(t.Won);
            Assert.AreEqual(11, t.WaveReached);
            Assert.AreEqual(5, t.Leaks);
        }
    }
}
