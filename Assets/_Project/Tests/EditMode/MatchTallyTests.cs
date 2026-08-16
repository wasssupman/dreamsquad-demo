using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // three-minute-survival unit 7 — 판 성적 값의 계약을 고정한다(구 ScoreMathTests).
    //
    // 총점 = **잡은 마리 수**다(kill-race unit 1 — 1킬 1점, 예외 없음). 산식에 분기가
    // 없다 — 시간·스트레스 축과 "패배 시 0" 예외는 폐기됐고, 애초에 패배가 없다.
    //
    // unit 6 — **서버에 보내는 수 = 총점**이다. 인코딩(BASE + kill×1000 + 안정도permille)과
    // 디코딩은 제거됐다. 안정도가 다시 제출값에 새어 들어가면 SubmissionScore 단언이 잡는다.
    public class MatchTallyTests
    {
        private static MatchTally Tally(int kills, int stability = 12, int stabilityMax = 20)
            => new MatchTally("complete", kills,
                stability, stabilityMax, waveReached: 7, leaks: 2);

        [Test]
        public void Total_IsKillCount()
        {
            var t = Tally(47);
            Assert.AreEqual(47, t.Kills);
            Assert.AreEqual(47, t.Total, "1킬 1점이므로 총점 == 잡은 마리 수");
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
            Assert.AreEqual(0, new MatchTally("complete", -3, 0, 20, 0, 0).Kills);
        }

        [Test]
        public void ZeroKills_IsZero()
        {
            Assert.AreEqual(0, Tally(0).Total);
        }

        [Test]
        public void LargeKillCount_IsNotRescaled()
        {
            // unit 6 — 상한·saturate 는 인코딩이 만들던 제약이었다. 이제 어떤 값이든
            // 손대지 않고 그대로 나간다(구 상한 1,147,482 를 넘겨 확인).
            Assert.AreEqual(2_000_000, Tally(2_000_000).SubmissionScore);
        }

        [Test]
        public void CarriesResultLabels()
        {
            var t = new MatchTally("submitted", 9, 0, 20, 11, 5);
            Assert.AreEqual("submitted", t.Outcome, "배틀 로그 라벨");
            Assert.AreEqual(11, t.WaveReached);
            Assert.AreEqual(5, t.Leaks);
            // three-minute-kill-race unit 0 — 마음이 0 이어도 그것 때문에 달라지는 것은 없다.
            // 승패를 담는 필드가 아예 없으므로 «졌다» 를 표현할 자리가 존재하지 않는다.
            Assert.AreEqual(9, t.SubmissionScore, "안정도 0 이어도 제출값은 처치 점수 그대로");
        }
    }
}
