using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pattern unit 7 — 수량 램프 순수 함수(RampedWaveTotal) 회귀 고정.
    // 웨이브 진행에 따라 min→max 선형 증가 + 지터 + [min,max] 클램프.
    public class WaveCountRampTests
    {
        [Test]
        public void NoJitter_FirstWaveIsMin_LastWaveIsMax()
        {
            const int min = 6, max = 10, waveCount = 12;
            Assert.AreEqual(min, WavePatternGenerator.RampedWaveTotal(0, waveCount, min, max, 0, 0.5f));
            Assert.AreEqual(max, WavePatternGenerator.RampedWaveTotal(waveCount - 1, waveCount, min, max, 0, 0.5f));
        }

        [Test]
        public void NoJitter_IsMonotonicNonDecreasing()
        {
            const int min = 6, max = 10, waveCount = 12;
            int prev = int.MinValue;
            for (int i = 0; i < waveCount; i++)
            {
                int total = WavePatternGenerator.RampedWaveTotal(i, waveCount, min, max, 0, 0f);
                Assert.GreaterOrEqual(total, prev, $"wave {i} 은 앞 웨이브보다 작으면 안 된다");
                Assert.GreaterOrEqual(total, min);
                Assert.LessOrEqual(total, max);
                prev = total;
            }
        }

        [Test]
        public void StaysWithinBounds_ForAnyJitter()
        {
            const int min = 6, max = 10, waveCount = 12;
            for (int band = 0; band <= 4; band++)
                for (int i = 0; i < waveCount; i++)
                    for (int j = 0; j <= 10; j++)
                    {
                        float jitter01 = j / 10f; // 0.0 .. 1.0 (극단 포함)
                        int total = WavePatternGenerator.RampedWaveTotal(i, waveCount, min, max, band, jitter01);
                        Assert.GreaterOrEqual(total, min, $"band {band} wave {i} j {jitter01}");
                        Assert.LessOrEqual(total, max, $"band {band} wave {i} j {jitter01}");
                    }
        }

        [Test]
        public void JitterShiftsAroundLinearCenter()
        {
            // 중간 웨이브(center 가 min/max 클램프에 닿지 않는 구간)에서 지터 방향이 반영된다.
            const int min = 4, max = 16, waveCount = 13;
            int mid = 6; // center = lerp(4,16, 0.5) = 10
            int low = WavePatternGenerator.RampedWaveTotal(mid, waveCount, min, max, 3, 0f);   // -3 → 7
            int high = WavePatternGenerator.RampedWaveTotal(mid, waveCount, min, max, 3, 1f);  // +3 → 13
            int none = WavePatternGenerator.RampedWaveTotal(mid, waveCount, min, max, 0, 0.5f); // 10
            Assert.Less(low, none);
            Assert.Greater(high, none);
        }

        [Test]
        public void SingleWave_ReturnsMax()
        {
            Assert.AreEqual(10, WavePatternGenerator.RampedWaveTotal(0, 1, 6, 10, 0, 0.5f));
        }

        [Test]
        public void MinGreaterThanMax_DefensiveSwap()
        {
            // min/max 뒤집혀 들어와도 스왑 후 정상 램프. wave 0 = 작은값, 마지막 = 큰값.
            Assert.AreEqual(6, WavePatternGenerator.RampedWaveTotal(0, 5, 10, 6, 0, 0.5f));
            Assert.AreEqual(10, WavePatternGenerator.RampedWaveTotal(4, 5, 10, 6, 0, 0.5f));
        }
    }
}
