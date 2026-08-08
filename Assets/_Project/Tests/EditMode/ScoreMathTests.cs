using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // three-minute-survival unit 3 — 점수 산식과 제출값 인코딩을 고정한다.
    //
    // 산식은 이제 분기가 없다: 총점 = 처치한 적의 killScore 합. 시간·스트레스 축과
    // "패배 시 0" 예외는 폐기됐다(져도 잡은 만큼은 남는다).
    //
    // 인코딩이 이 파일의 무게 중심이다 — 서버가 int 하나만 받아서 동점 판정(남은 안정도)을
    // 값에 실어 보내고, 표시 지점 3곳(결과·리더보드·히스토리)이 그걸 되꺼내 쓴다.
    // 왕복이 깨지면 리더보드에 10억대 숫자가 뜨거나 구 기록이 가짜 점수로 읽힌다.
    public class ScoreMathTests
    {
        // ── 산식 ────────────────────────────────────────────────────────────────

        [Test]
        public void Total_IsKillScoreSum()
        {
            var s = ScoreMath.Evaluate(47);
            Assert.AreEqual(47, s.Kill);
            Assert.AreEqual(47, s.Total, "처치 축이 유일하므로 총점 == 처치 점수");
        }

        [Test]
        public void NegativeKillScore_ClampsToZero()
        {
            Assert.AreEqual(0, ScoreMath.Evaluate(-5).Total);
        }

        [Test]
        public void ZeroKills_IsZero()
        {
            Assert.AreEqual(0, ScoreMath.Evaluate(0).Total);
        }

        // ── 안정도 permille ─────────────────────────────────────────────────────

        [Test]
        public void StabilityPermille_FullIsNineNineNine_EmptyIsZero()
        {
            Assert.AreEqual(999, ScoreMath.StabilityPermille(20, 20));
            Assert.AreEqual(0, ScoreMath.StabilityPermille(0, 20));
        }

        [Test]
        public void StabilityPermille_MidpointRounds()
        {
            // 12/20 = 60% → 599.4 → 599
            Assert.AreEqual(599, ScoreMath.StabilityPermille(12, 20));
            // 10/20 = 50% → 499.5 → 500 (반올림)
            Assert.AreEqual(500, ScoreMath.StabilityPermille(10, 20));
        }

        [Test]
        public void StabilityPermille_DegenerateInputs_AreZero()
        {
            Assert.AreEqual(0, ScoreMath.StabilityPermille(5, 0), "max 0 = 정보 없음");
            Assert.AreEqual(0, ScoreMath.StabilityPermille(-3, 20));
        }

        [Test]
        public void StabilityPermille_NeverExceedsBucket()
        {
            // 999 를 넘으면 killScore 자리로 넘쳐 점수가 1 올라간다 — 절대 금지.
            for (int max = 1; max <= 64; max++)
                for (int v = 0; v <= max; v++)
                    Assert.LessOrEqual(ScoreMath.StabilityPermille(v, max), 999, $"{v}/{max}");
        }

        // ── 인코딩 왕복 ─────────────────────────────────────────────────────────

        [Test]
        public void Encode_RoundTripsKillScore()
        {
            int submitted = ScoreMath.EncodeSubmission(47, 12, 20);
            Assert.AreEqual(ScoreMath.SubmissionBase + 47 * 1000 + 599, submitted);
            Assert.AreEqual(47, ScoreMath.DecodeKillScore(submitted));
            Assert.AreEqual(599, ScoreMath.DecodeStabilityPermille(submitted));
            Assert.AreEqual(47, ScoreMath.DisplayScore(submitted));
        }

        [Test]
        public void Encode_TieBreak_HigherStabilityWinsAtEqualKills()
        {
            int a = ScoreMath.EncodeSubmission(47, 18, 20);
            int b = ScoreMath.EncodeSubmission(47, 4, 20);
            Assert.Greater(a, b, "같은 처치 점수면 남은 안정도가 높은 쪽이 위로 정렬돼야 한다");
            Assert.AreEqual(ScoreMath.DecodeKillScore(a), ScoreMath.DecodeKillScore(b),
                "tie-break 는 표시 점수를 바꾸지 않는다");
        }

        [Test]
        public void Encode_MoreKillsAlwaysBeatsMoreStability()
        {
            // 안정도는 1000 미만 버킷이라 처치 1점 차이를 절대 뒤집지 못한다.
            int fewerKillsFullStability = ScoreMath.EncodeSubmission(47, 20, 20);
            int moreKillsZeroStability = ScoreMath.EncodeSubmission(48, 0, 20);
            Assert.Greater(moreKillsZeroStability, fewerKillsFullStability);
        }

        [Test]
        public void Encode_ZeroEverything_IsStillEncoded()
        {
            int submitted = ScoreMath.EncodeSubmission(0, 0, 20);
            Assert.AreEqual(ScoreMath.SubmissionBase, submitted);
            Assert.IsTrue(ScoreMath.IsEncodedSubmission(submitted));
            Assert.AreEqual(0, ScoreMath.DecodeKillScore(submitted));
        }

        [Test]
        public void Encode_NegativeKillScore_ClampsToZero()
        {
            Assert.AreEqual(0, ScoreMath.DecodeKillScore(ScoreMath.EncodeSubmission(-9, 20, 20)));
        }

        [Test]
        public void Encode_AtMaxEncodable_DoesNotOverflow()
        {
            int submitted = ScoreMath.EncodeSubmission(ScoreMath.MaxEncodableKillScore, 20, 20);
            Assert.Greater(submitted, 0, "int 오버플로로 음수가 되면 서버 정렬이 뒤집힌다");
            Assert.AreEqual(ScoreMath.MaxEncodableKillScore, ScoreMath.DecodeKillScore(submitted));
        }

        [Test]
        public void Encode_AboveMaxEncodable_SaturatesInsteadOfOverflowing()
        {
            int submitted = ScoreMath.EncodeSubmission(int.MaxValue, 20, 20);
            Assert.Greater(submitted, 0);
            Assert.AreEqual(ScoreMath.MaxEncodableKillScore, ScoreMath.DecodeKillScore(submitted));
        }

        // ── 구 포맷 판별 ────────────────────────────────────────────────────────

        [Test]
        public void LegacyScore_IsNotDecoded()
        {
            // 구 산식 총점(시간+스트레스+킬)은 현실적으로 1~3만이다. 이걸 /1000 하면
            // 10~30 이라는 그럴듯한 가짜 점수가 나와 신규 기록과 구분이 불가능하다.
            const int legacyTotal = 26_000;
            Assert.IsFalse(ScoreMath.IsEncodedSubmission(legacyTotal));
            Assert.AreEqual(-1, ScoreMath.DecodeKillScore(legacyTotal));
            Assert.AreEqual(-1, ScoreMath.DecodeStabilityPermille(legacyTotal));
            Assert.AreEqual(legacyTotal, ScoreMath.DisplayScore(legacyTotal),
                "구 기록은 원값 그대로 보여준다");
        }

        [Test]
        public void LegacyMaxTotal_IsBelowSubmissionBase()
        {
            // 구 산식의 산술적 최악값(18,000,000 = 180초 × 100 × 1000ms 환산 상한)조차
            // 오프셋 미만이어야 판별이 성립한다.
            Assert.Less(18_000_000, ScoreMath.SubmissionBase);
        }
    }
}
