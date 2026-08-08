using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // three-minute-survival unit 2 — 수량 곡선 순수 함수(ExponentialWaveTotal) 회귀 고정.
    // 구 RampedWaveTotal(전체 웨이브 수로 나눈 선형 보간)을 대체했다: 웨이브 상한이 100(명목)이
    // 되면서 "전체 대비 진행률" 분모가 의미를 잃었고, 3분 안에 도달하는 것은 앞의 10~16개뿐이라
    // 성장은 웨이브 번호의 절대 함수여야 한다.
    public class WaveCountRampTests
    {
        [Test]
        public void NoJitter_FirstWaveIsBase()
        {
            const int min = 5, max = 24;
            Assert.AreEqual(min, WavePatternGenerator.ExponentialWaveTotal(0, min, max, 1.12f, 0, 0.5f));
        }

        [Test]
        public void NoJitter_IsMonotonicNonDecreasing_AndSaturatesAtCap()
        {
            const int min = 5, max = 24;
            int prev = int.MinValue;
            for (int i = 0; i < 100; i++)
            {
                int total = WavePatternGenerator.ExponentialWaveTotal(i, min, max, 1.12f, 0, 0f);
                Assert.GreaterOrEqual(total, prev, $"wave {i} 은 앞 웨이브보다 작으면 안 된다");
                Assert.GreaterOrEqual(total, min);
                Assert.LessOrEqual(total, max);
                prev = total;
            }
            // 지수라 후반은 cap 에 붙어 있어야 한다(성장이 무한히 커지지 않는다).
            Assert.AreEqual(max, WavePatternGenerator.ExponentialWaveTotal(99, min, max, 1.12f, 0, 0f));
        }

        [Test]
        public void ReachableBand_ActuallyGrows()
        {
            // 저작값(base 5 · growth 1.12 · cap 24)에서 실제 도달 구간(1~16웨이브)이 상승
            // 구간이어야 한다. 곡선이 도달 전에 포화하면 "완만한 지수 성장" 의도가 죽는다.
            const int min = 5, max = 24;
            int w0 = WavePatternGenerator.ExponentialWaveTotal(0, min, max, 1.12f, 0, 0f);
            int w9 = WavePatternGenerator.ExponentialWaveTotal(9, min, max, 1.12f, 0, 0f);
            int w15 = WavePatternGenerator.ExponentialWaveTotal(15, min, max, 1.12f, 0, 0f);
            Assert.Greater(w9, w0 * 2 - 1, $"10번째 웨이브가 첫 웨이브의 2배 미만이다(w0={w0}, w9={w9})");
            Assert.Greater(w15, w9, $"후반 도달 구간도 계속 올라야 한다(w9={w9}, w15={w15})");
        }

        [Test]
        public void StaysWithinBounds_ForAnyJitter()
        {
            const int min = 5, max = 24;
            for (int band = 0; band <= 4; band++)
                for (int i = 0; i < 40; i++)
                    for (int j = 0; j <= 10; j++)
                    {
                        float jitter01 = j / 10f; // 0.0 .. 1.0 (극단 포함)
                        int total = WavePatternGenerator.ExponentialWaveTotal(i, min, max, 1.12f, band, jitter01);
                        Assert.GreaterOrEqual(total, min, $"band {band} wave {i} j {jitter01}");
                        Assert.LessOrEqual(total, max, $"band {band} wave {i} j {jitter01}");
                    }
        }

        [Test]
        public void JitterShiftsAroundCenter()
        {
            // 중간 웨이브(center 가 min/max 클램프에 닿지 않는 구간)에서 지터 방향이 반영된다.
            const int min = 4, max = 40;
            int mid = 8; // center = 4 × 1.2^8 ≈ 17.2
            int low = WavePatternGenerator.ExponentialWaveTotal(mid, min, max, 1.2f, 3, 0f);   // -3
            int high = WavePatternGenerator.ExponentialWaveTotal(mid, min, max, 1.2f, 3, 1f);  // +3
            int none = WavePatternGenerator.ExponentialWaveTotal(mid, min, max, 1.2f, 0, 0.5f);
            Assert.Less(low, none);
            Assert.Greater(high, none);
        }

        [Test]
        public void JitterSurvivesAtCap()
        {
            // center 를 cap 으로 먼저 클램프하므로 상한 근처에서도 아래 방향 지터가 살아 있다.
            // (클램프를 나중에 하면 center 가 폭발해 지터가 통째로 묻힌다.)
            const int min = 5, max = 24;
            int atCapLow = WavePatternGenerator.ExponentialWaveTotal(60, min, max, 1.12f, 3, 0f);
            Assert.Less(atCapLow, max, "cap 구간에서 음수 지터가 반영되어야 한다");
            Assert.GreaterOrEqual(atCapLow, min);
        }

        [Test]
        public void GrowthOne_IsFlat()
        {
            // growth = 1 = 성장 없음. 기존 "수량 평탄" 저작(min == max 대신 growth 1)을 표현한다.
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(6, WavePatternGenerator.ExponentialWaveTotal(i, 6, 10, 1f, 0, 0.5f));
        }

        [Test]
        public void MinGreaterThanMax_DefensiveSwap()
        {
            // min/max 뒤집혀 들어와도 스왑 후 정상 동작. wave 0 = 작은값, 후반 = 큰값.
            Assert.AreEqual(6, WavePatternGenerator.ExponentialWaveTotal(0, 10, 6, 1.2f, 0, 0.5f));
            Assert.AreEqual(10, WavePatternGenerator.ExponentialWaveTotal(20, 10, 6, 1.2f, 0, 0.5f));
        }

        [Test]
        public void NegativeWaveIndex_TreatedAsFirst()
        {
            Assert.AreEqual(5, WavePatternGenerator.ExponentialWaveTotal(-3, 5, 24, 1.12f, 0, 0.5f));
        }
    }
}
