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

        // ── DeriveSkillSeed (battle-sim-extraction, unit 3 결함 수리) ─────────
        //
        // 스킬 로드아웃은 캡처되는 매치 설정이다. 벽시계로 굴러 실행마다 달라지던 것을 고쳤고,
        // 이 4건이 그 회귀 방지다 — 깨지면 골든의 configHash 가 다시 흔들린다.

        [Test]
        public void DeriveSkillSeed_IsDeterministic_PerMatchSeedAndRollIndex()
        {
            Assert.AreEqual(MatchSeed.DeriveSkillSeed(12345, 0), MatchSeed.DeriveSkillSeed(12345, 0));
            Assert.AreEqual(MatchSeed.DeriveSkillSeed(12345, 3), MatchSeed.DeriveSkillSeed(12345, 3));
        }

        [Test]
        public void DeriveSkillSeed_RollIndex_AdvancesTheStream()
        {
            // REDRAFT 가 같은 매치에서 새 조합을 받는 근거. 같으면 재드래프트가 무의미해진다.
            Assert.AreNotEqual(MatchSeed.DeriveSkillSeed(777, 0), MatchSeed.DeriveSkillSeed(777, 1));
            Assert.AreNotEqual(MatchSeed.DeriveSkillSeed(777, 1), MatchSeed.DeriveSkillSeed(777, 2));
        }

        [Test]
        public void DeriveSkillSeed_IsDecorrelated_FromOtherStreams()
        {
            int match = 777;
            int skill = MatchSeed.DeriveSkillSeed(match, 0);
            Assert.AreNotEqual(MatchSeed.DeriveMapSeed(match), skill, "map/skill 계열 분리 실패");
            Assert.AreNotEqual(MatchSeed.DeriveWaveSeed(match), skill, "wave/skill 계열 분리 실패");
            Assert.AreNotEqual(MatchSeed.DeriveGimmickSeed(match), skill, "gimmick/skill 계열 분리 실패");
        }

        [Test]
        public void DeriveSkillSeed_IsNeverZero()
        {
            // 0 은 SkillLoadoutController 에서 "미설정 = 벽시계 폴백" 을 뜻한다. 파생값이 0 이면
            // 그 폴백이 되살아나 실행마다 로드아웃이 달라진다.
            Assert.AreNotEqual(0, MatchSeed.DeriveSkillSeed(0, 0));
            for (int i = 0; i < 64; i++) Assert.AreNotEqual(0, MatchSeed.DeriveSkillSeed(202608041, i));
        }
    }
}
