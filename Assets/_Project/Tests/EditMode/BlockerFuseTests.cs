using NUnit.Framework;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // bomb-barrel-on-place unit 6 — 퓨즈 진행도의 **방향**을 고정한다.
    //
    // 이 파일의 존재 이유는 끝점이 아니라 가운데다: 지수를 역수(1/exponent)로 쓰면 곡선이
    // 정반대가 되는데(초반에 확 타고 막판엔 변화 없음) 끝점 0/1 은 **양쪽 다 통과**한다.
    // 그 실수를 잡는 것은 중간값 단언 하나뿐이다.
    public class BlockerFuseTests
    {
        [Test]
        public void Endpoints_AreZeroAndOne()
        {
            Assert.AreEqual(0f, BlockerFuse.Progress(10f, 10f, 2.5f), 1e-4f, "갓 놓였으면 0");
            Assert.AreEqual(1f, BlockerFuse.Progress(0f, 10f, 2.5f), 1e-4f, "수명이 다했으면 1");
        }

        [Test]
        public void HighExponent_BackloadsTheBurn()
        {
            // 절반 지났을 때 아직 절반도 안 탔어야 «막판에 몰아서» 가 성립한다.
            // 역수를 쓰면 이 값이 0.5 를 넘어 실패한다 — 그것이 이 단언의 목적이다.
            float mid = BlockerFuse.Progress(5f, 10f, 2.5f);
            Assert.Less(mid, 0.5f, "지수 > 1 은 초반을 느리게 해야 한다(역수를 쓰면 뒤집힌다)");
            Assert.Greater(mid, 0f);
        }

        [Test]
        public void ExponentOne_IsLinear()
        {
            Assert.AreEqual(0.25f, BlockerFuse.Progress(7.5f, 10f, 1f), 1e-4f);
            Assert.AreEqual(0.50f, BlockerFuse.Progress(5f, 10f, 1f), 1e-4f);
        }

        [Test]
        public void InfiniteLifetime_NeverBurns()
        {
            // 수명 0 이하 = 무한(기존 길막 설치물 전부). 「다해 간다」가 정의되지 않는다.
            Assert.AreEqual(0f, BlockerFuse.Progress(float.PositiveInfinity, 0f, 2.5f), 1e-4f);
        }

        [Test]
        public void Progress_IsClamped()
        {
            Assert.AreEqual(0f, BlockerFuse.Progress(20f, 10f, 2.5f), 1e-4f, "남은 수명이 총량보다 커도 0 아래로 안 간다");
            Assert.AreEqual(1f, BlockerFuse.Progress(-5f, 10f, 2.5f), 1e-4f, "음수가 되어도 1 을 안 넘는다");
        }
    }
}
