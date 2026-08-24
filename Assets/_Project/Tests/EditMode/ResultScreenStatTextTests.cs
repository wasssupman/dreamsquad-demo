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

        // heart-stress-axis unit 4 — `StressText` 의 **뜻이 바뀌었다**: 구 「유출 누적 / 한계」
        // (`3 / 10`) → 「마음 스트레스 / 100」. 인게임(마음 위 숫자·프랍 틴트·심박·포스트
        // 비네트)과 같은 축을 쓴다.
        [Test]
        public void StressText_ReadsHeartHealthAsRisingStress()
        {
            Assert.AreEqual("0 / 100", ResultScreen.StressText(1000, 1000), "만피 = 스트레스 0");
            Assert.AreEqual("100 / 100", ResultScreen.StressText(0, 1000), "체력 0 = 스트레스 100");
            Assert.AreEqual("50 / 100", ResultScreen.StressText(500, 1000));
        }

        [Test]
        public void StressText_NoHeart_IsZeroNotFull()
        {
            // ⚠ 마음이 없는 판(미저작·미스폰)을 «스트레스 만점» 으로 읽으면 결과 화면이
            // 「판을 끝낸 축이 만점이었다」고 거짓말한다. StressMath 의 폴백과 같은 판단.
            Assert.AreEqual("0 / 100", ResultScreen.StressText(0, 0));
            Assert.AreEqual("0 / 100", ResultScreen.StressText(0, -1));
        }
    }
}
