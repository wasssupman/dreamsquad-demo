using NUnit.Framework;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // battle-score-formula unit 7 — 결과 화면 원시 상태 2줄의 표기 포매터. MonoBehaviour 를
    // 띄우지 않는 static 함수만 본다(레이아웃은 Play 육안 확인 담당).
    public class ResultScreenStatTextTests
    {
        [Test]
        public void ClockText_PadsSecondsToTwoDigits()
        {
            Assert.AreEqual("0:00", ResultScreen.ClockText(0f));
            Assert.AreEqual("0:07", ResultScreen.ClockText(7f));
            Assert.AreEqual("1:00", ResultScreen.ClockText(60f));
            Assert.AreEqual("2:13", ResultScreen.ClockText(133f));
        }

        [Test]
        public void ClockText_CeilsPartialSecond_AndClampsNegative()
        {
            // 0.4초 남기고 끝냈으면 "0:00" 보다 "0:01" 이 정직하다 — 시간점수는 남아 있다.
            Assert.AreEqual("0:01", ResultScreen.ClockText(0.4f));
            Assert.AreEqual("0:00", ResultScreen.ClockText(-3f));
        }

        [Test]
        public void StressText_ShowsLimitWhenPositive()
        {
            Assert.AreEqual("3 / 10", ResultScreen.StressText(3, 10));
            Assert.AreEqual("0 / 10", ResultScreen.StressText(0, 10));
        }

        [Test]
        public void StressText_HidesLimitWhenNotPositive()
        {
            // 엔드리스(누수로 죽지 않음) · 덱 미배선 — 분모가 무의미하므로 누적만 보여준다.
            Assert.AreEqual("3", ResultScreen.StressText(3, 0));
            Assert.AreEqual("3", ResultScreen.StressText(3, -1));
        }
    }
}
