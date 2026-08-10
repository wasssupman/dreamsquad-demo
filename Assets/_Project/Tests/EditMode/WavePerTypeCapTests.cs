using NUnit.Framework;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // structure-hunter-enemy unit 1 — 종류별 동시 등장 상한(maxPerWave)의 순수 클램프.
    //
    // 이 축이 왜 있나: 생성기에는 종류별 수량 상한이 없었고, 기존 적은 전부 몸으로 막을 수
    // 있어 문제가 없었다. 유인·차단이 통하지 않는 적은 수량이 곧 «막을 수 없는 시간» 이다.
    //
    // 가장 중요한 단언은 마지막 둘이다 — «상한 미저작은 완전 무변경» 과 «총량 보존».
    // 앞쪽이 깨지면 상한을 안 쓴 6개 맵의 난이도 곡선이 조용히 바뀐다.
    public class WavePerTypeCapTests
    {
        [Test]
        public void NoCapAuthored_LeavesCountsUntouched()
        {
            int a = 17, b = 6;
            WavePatternGenerator.ClampGroupCounts(0, 0, ref a, ref b);
            Assert.AreEqual(17, a);
            Assert.AreEqual(6, b);
        }

        [Test]
        public void CapOnA_TrimsAndGivesRemainderToUncappedB()
        {
            int a = 20, b = 4;   // total 24
            WavePatternGenerator.ClampGroupCounts(2, 0, ref a, ref b);
            Assert.AreEqual(2, a, "상한까지 잘려야 한다");
            Assert.AreEqual(22, b, "남은 몫은 상한 없는 쪽으로 — 웨이브 총량 보존");
            Assert.AreEqual(24, a + b);
        }

        [Test]
        public void CapOnB_TrimsAndGivesRemainderToUncappedA()
        {
            int a = 4, b = 20;
            WavePatternGenerator.ClampGroupCounts(0, 3, ref a, ref b);
            Assert.AreEqual(21, a);
            Assert.AreEqual(3, b);
            Assert.AreEqual(24, a + b);
        }

        [Test]
        public void BothCapped_TotalShrinks_RatherThanOverflowing()
        {
            int a = 12, b = 12;
            WavePatternGenerator.ClampGroupCounts(2, 3, ref a, ref b);
            Assert.AreEqual(2, a);
            Assert.AreEqual(3, b);
            Assert.AreEqual(5, a + b, "둘 다 상한이면 총량이 준다 — 그게 상한의 목적이다");
        }

        [Test]
        public void RemainderFillsPartialRoomOnTheOtherCappedSide()
        {
            int a = 10, b = 1;   // total 11
            WavePatternGenerator.ClampGroupCounts(2, 6, ref a, ref b);
            Assert.AreEqual(2, a);
            Assert.AreEqual(6, b, "B 의 남은 여유(6-1=5)까지만 채운다");
            Assert.AreEqual(8, a + b, "여유를 넘는 몫은 버린다");
        }

        [Test]
        public void CountsBelowCap_AreNotInflatedToTheCap()
        {
            int a = 1, b = 2;
            WavePatternGenerator.ClampGroupCounts(5, 5, ref a, ref b);
            Assert.AreEqual(1, a, "상한은 천장이지 목표가 아니다");
            Assert.AreEqual(2, b);
        }
    }
}
