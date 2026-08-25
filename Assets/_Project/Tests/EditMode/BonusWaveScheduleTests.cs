using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // bonus-wave-pull unit 4 — 배분·타임라인의 **결정론**을 값 수준에서 고정한다.
    // 「구조적 결정론」(seeded RNG 없음)이 계약 3 이라, 여기가 빨개지면 그 계약이 깨진 것이다.
    public class BonusWaveScheduleTests
    {
        [Test]
        public void 포탈은_순번대로_번갈아_배분된다()
        {
            var s = BonusWaveSchedule.Build(2, 10, 3f, 0.5f);
            Assert.AreEqual(10, s.Length);
            for (int i = 0; i < s.Length; i++)
                Assert.AreEqual(i % 2, s[i].portalIndex, $"{i}번째 적의 포탈");
        }

        [Test]
        public void 스폰_시각은_첫스폰_기준_등차다()
        {
            var s = BonusWaveSchedule.Build(2, 10, 3f, 0.5f);
            for (int i = 0; i < s.Length; i++)
                Assert.AreEqual(3f + i * 0.5f, s[i].spawnAtSec, 1e-4f, $"{i}번째 적의 시각");
        }

        [Test]
        public void 같은_입력은_두_번_불러도_같은_결과다()
        {
            var a = BonusWaveSchedule.Build(2, 10, 3f, 0.5f);
            var b = BonusWaveSchedule.Build(2, 10, 3f, 0.5f);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].portalIndex, b[i].portalIndex);
                Assert.AreEqual(a[i].spawnAtSec, b[i].spawnAtSec, 1e-6f);
                Assert.AreEqual(a[i].ringIndex, b[i].ringIndex);
                Assert.AreEqual(a[i].ringCount, b[i].ringCount);
            }
        }

        // 링 배치는 «같은 포탈에서 나온 것끼리» 겹치지 않게 하는 축이다. 포탈별 총수가
        // 틀리면 각도가 뭉쳐 여러 기가 한 점에 태어난다(좁은 복도 교착 조건).
        [Test]
        public void 포탈별_총수는_전체를_나눠_가진다()
        {
            Assert.AreEqual(5, BonusWaveSchedule.CountForPortal(2, 10, 0));
            Assert.AreEqual(5, BonusWaveSchedule.CountForPortal(2, 10, 1));
            // 안 나눠떨어지면 앞쪽이 하나 더
            Assert.AreEqual(4, BonusWaveSchedule.CountForPortal(3, 10, 0));
            Assert.AreEqual(3, BonusWaveSchedule.CountForPortal(3, 10, 1));
            Assert.AreEqual(3, BonusWaveSchedule.CountForPortal(3, 10, 2));
        }

        [Test]
        public void 링_인덱스는_포탈_안에서_0부터_증가한다()
        {
            var s = BonusWaveSchedule.Build(2, 10, 0f, 1f);
            for (int i = 0; i < s.Length; i++)
            {
                Assert.AreEqual(i / 2, s[i].ringIndex);
                Assert.AreEqual(5, s[i].ringCount);
            }
        }

        [Test]
        public void 잘못된_입력은_빈_배열이다()
        {
            Assert.AreEqual(0, BonusWaveSchedule.Build(0, 10, 1f, 1f).Length);
            Assert.AreEqual(0, BonusWaveSchedule.Build(2, 0, 1f, 1f).Length);
        }
    }
}
