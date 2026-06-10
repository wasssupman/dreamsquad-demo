using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class MatchSeedTests
    {
        [Test]
        public void DeriveMapSeed_IsDeterministic()
        {
            Assert.AreEqual(MatchSeed.DeriveMapSeed(12345), MatchSeed.DeriveMapSeed(12345));
        }

        [Test]
        public void DeriveWaveSeed_IsDeterministic()
        {
            Assert.AreEqual(MatchSeed.DeriveWaveSeed(12345), MatchSeed.DeriveWaveSeed(12345));
        }

        [Test]
        public void DeriveVisualSeed_IsDeterministic()
        {
            Assert.AreEqual(MatchSeed.DeriveVisualSeed(12345), MatchSeed.DeriveVisualSeed(12345));
        }

        [Test]
        public void Streams_AreDecorrelated_ForSameMatchSeed()
        {
            int match = 777;
            int map = MatchSeed.DeriveMapSeed(match);
            int wave = MatchSeed.DeriveWaveSeed(match);
            int visual = MatchSeed.DeriveVisualSeed(match);
            Assert.AreNotEqual(map, wave, "map/wave 계열이 같은 matchSeed 에서 동일하면 안 됨");
            Assert.AreNotEqual(map, visual, "map/visual 계열 분리 실패");
            Assert.AreNotEqual(wave, visual, "wave/visual 계열 분리 실패");
        }

        [Test]
        public void DifferentMatchSeeds_ProduceDifferentMapSeeds()
        {
            // 샘플 몇 개로 충돌 회피 확인(완전 무충돌 보장은 아님).
            Assert.AreNotEqual(MatchSeed.DeriveMapSeed(1), MatchSeed.DeriveMapSeed(2));
            Assert.AreNotEqual(MatchSeed.DeriveMapSeed(100), MatchSeed.DeriveMapSeed(101));
            Assert.AreNotEqual(MatchSeed.DeriveMapSeed(-5), MatchSeed.DeriveMapSeed(5));
        }

        [Test]
        public void DerivedSeeds_AreNeverZero_IncludingZeroMatchSeed()
        {
            Assert.AreNotEqual(0, MatchSeed.DeriveMapSeed(0));
            Assert.AreNotEqual(0, MatchSeed.DeriveWaveSeed(0));
            Assert.AreNotEqual(0, MatchSeed.DeriveVisualSeed(0));
        }
    }
}
