using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // dot-tick-cadence unit 1 — 순수 tick 누적 산식 회귀.
    public class DotTickTests
    {
        [Test]
        public void Advance_Below_Interval_No_Tick_Accumulates_Timer()
        {
            float timer = 0f;
            int ticks = DotTick.Advance(ref timer, 0.5f, 0.016f);
            Assert.AreEqual(0, ticks);
            Assert.AreEqual(0.016f, timer, 1e-6f);
        }

        [Test]
        public void Advance_Reaches_Interval_Fires_One()
        {
            float timer = 0.49f;
            int ticks = DotTick.Advance(ref timer, 0.5f, 0.02f);
            Assert.AreEqual(1, ticks);
            Assert.AreEqual(0.01f, timer, 1e-6f, "청크 후 잔여 누적");
        }

        [Test]
        public void Advance_Immediate_When_Timer_Preloaded_To_Interval()
        {
            // CcApply add-path 컨벤션(tickTimer=tickInterval) → 첫 호출 즉발.
            float timer = 0.5f;
            int ticks = DotTick.Advance(ref timer, 0.5f, 0.016f);
            Assert.AreEqual(1, ticks);
            Assert.AreEqual(0.016f, timer, 1e-6f);
        }

        [Test]
        public void Advance_Large_Dt_Fires_Multiple()
        {
            float timer = 0f;
            int ticks = DotTick.Advance(ref timer, 0.5f, 1.2f);
            Assert.AreEqual(2, ticks);
            Assert.AreEqual(0.2f, timer, 1e-6f);
        }

        [Test]
        public void Advance_Continuous_Interval_Zero_Returns_Zero_And_Keeps_Timer()
        {
            float timer = 3f;
            int ticks = DotTick.Advance(ref timer, 0f, 0.1f);
            Assert.AreEqual(0, ticks, "tickInterval<=0 은 연속 경로 전제 → 0");
            Assert.AreEqual(3f, timer, 1e-6f, "timer 불변");
        }

        [Test]
        public void Advance_Caps_At_MaxTicksPerFrame()
        {
            // 미세 interval + 큰 dt 무한루프 방지 상한.
            float timer = 0f;
            int ticks = DotTick.Advance(ref timer, 0.0001f, 1000f);
            Assert.AreEqual(DotTick.MaxTicksPerFrame, ticks);
        }
    }
}
