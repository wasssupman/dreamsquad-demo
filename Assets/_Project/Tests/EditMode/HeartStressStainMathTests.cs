using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // heart-stress-axis unit 1 rev — 마음 스트레스가 보드에 번지는 규칙.
    //
    // 고정하는 계약 셋:
    //   (1) **단조**다 — 스트레스가 오르면 어느 링도 줄지 않는다. 줄면 «회복했나?» 로 오독된다.
    //   (2) **바깥 링이 안쪽보다 먼저 켜지지 않는다** — 번짐은 중심에서 나간다.
    //   (3) 맥박은 밝기 **배율**(0<v<=1)이지 알파 자체가 아니다. 1 을 넘으면 색이 날아간다.
    public class HeartStressStainMathTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void NoStress_NothingShows()
        {
            for (int r = 0; r < HeartStressStainMath.RingCount; r++)
                Assert.AreEqual(0f, HeartStressStainMath.RingFill(0f, r), Tol, $"ring {r}");
        }

        [Test]
        public void FullStress_EveryRingFull()
        {
            for (int r = 0; r < HeartStressStainMath.RingCount; r++)
                Assert.AreEqual(1f, HeartStressStainMath.RingFill(1f, r), Tol, $"ring {r}");
        }

        [Test]
        public void RingFill_IsMonotonicInStress()
        {
            for (int r = 0; r < HeartStressStainMath.RingCount; r++)
            {
                float prev = -1f;
                for (int i = 0; i <= 100; i++)
                {
                    float v = HeartStressStainMath.RingFill(i / 100f, r);
                    Assert.GreaterOrEqual(v, prev,
                        $"ring {r} 이 스트레스 {i}% 에서 줄었다 — 잠식이 물러나면 «회복했나» 로 읽힌다");
                    prev = v;
                }
            }
        }

        [Test]
        public void SpreadsOutward_InnerNeverTrailsOuter()
        {
            // 중심이 대각보다 항상 같거나 앞선다 = 번짐이 안에서 밖으로 간다.
            for (int i = 0; i <= 100; i++)
            {
                float s = i / 100f;
                Assert.GreaterOrEqual(HeartStressStainMath.RingFill(s, 0),
                                      HeartStressStainMath.RingFill(s, 1), $"{i}% — 중심 < 직교");
                Assert.GreaterOrEqual(HeartStressStainMath.RingFill(s, 1),
                                      HeartStressStainMath.RingFill(s, 2), $"{i}% — 직교 < 대각");
            }
        }

        [Test]
        public void CenterLightsUpOnFirstHit()
        {
            // 첫 피격(스트레스 몇 %)에 이미 보여야 «맞았다» 가 읽힌다.
            Assert.Greater(HeartStressStainMath.RingFill(0.05f, 0), 0f);
        }

        [Test]
        public void RingOf_ClassifiesOffsets()
        {
            Assert.AreEqual(0, HeartStressStainMath.RingOf(0, 0));
            Assert.AreEqual(1, HeartStressStainMath.RingOf(1, 0));
            Assert.AreEqual(1, HeartStressStainMath.RingOf(0, -1));
            Assert.AreEqual(2, HeartStressStainMath.RingOf(-1, 1));
            Assert.AreEqual(-1, HeartStressStainMath.RingOf(2, 0), "3×3 밖은 링이 아니다");
        }

        [Test]
        public void Pulse_StaysABrightnessMultiplier()
        {
            // 알파에 곱해지는 값이라 1 을 넘으면 색이 날아가고, 0 이하면 사라진다.
            for (int i = 0; i <= 40; i++)
            {
                float t = i * 0.1f;
                foreach (float s in new[] { 0f, 0.5f, 1f })
                {
                    float v = HeartStressStainMath.Pulse(s, t, 1.1f, 7.5f, 0.3f);
                    Assert.LessOrEqual(v, 1f + Tol, $"stress {s}, t {t}");
                    Assert.GreaterOrEqual(v, 0.7f - Tol, $"stress {s}, t {t} — depth 0.3 이면 하한 0.7");
                }
            }
        }

        [Test]
        public void Pulse_ZeroDepth_IsFlat()
            => Assert.AreEqual(1f, HeartStressStainMath.Pulse(1f, 3.7f, 1f, 8f, 0f), Tol);

        [Test]
        public void Pulse_FasterAtHighStress()
        {
            // 같은 시간 창에서 고스트레스가 더 여러 번 뛴다 = 주기가 짧다.
            int lowCrossings = 0, highCrossings = 0;
            float prevLow = HeartStressStainMath.Pulse(0f, 0f, 1.1f, 7.5f, 0.3f);
            float prevHigh = HeartStressStainMath.Pulse(1f, 0f, 1.1f, 7.5f, 0.3f);
            for (int i = 1; i <= 400; i++)
            {
                float t = i * 0.01f;
                float lo = HeartStressStainMath.Pulse(0f, t, 1.1f, 7.5f, 0.3f);
                float hi = HeartStressStainMath.Pulse(1f, t, 1.1f, 7.5f, 0.3f);
                if (Mathf.Sign(lo - prevLow) != Mathf.Sign(prevLow - HeartStressStainMath.Pulse(0f, t - 0.02f, 1.1f, 7.5f, 0.3f))) lowCrossings++;
                if (Mathf.Sign(hi - prevHigh) != Mathf.Sign(prevHigh - HeartStressStainMath.Pulse(1f, t - 0.02f, 1.1f, 7.5f, 0.3f))) highCrossings++;
                prevLow = lo; prevHigh = hi;
            }
            Assert.Greater(highCrossings, lowCrossings,
                "고스트레스에서 맥박이 더 빨라야 한다 — 그게 «위험» 의 신호다");
        }
    }
}
