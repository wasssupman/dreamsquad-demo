using System.Collections.Generic;
using NUnit.Framework;
using Wassup.BattleCore;
using Wassup.BattleCore.Wave;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 모드 × 저작 유효성, 그리고 모드가 담당자에게 «값으로만» 말한다는 것.
    [TestFixture]
    public class MatchModeTests
    {
        private static readonly List<string> Problems = new List<string>();

        [Test]
        public void 목표_웨이브가_덱_최대치를_넘으면_빨갛다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.Goal = GoalKind.WaveClear;
            def.Mode.Clock = ClockKind.CountUp;
            def.Mode.TargetWaves = 12;
            var deck = CoreMatchFixtures.Deck();
            deck.MaxWaveCount = 10;
            def.WaveDeck = deck;

            Assert.IsFalse(ModeValidation.Validate(def, Problems),
                "12웨이브를 막으라는데 덱이 10웨이브뿐이면 그 판은 영영 안 끝난다");
            StringAssert.Contains("목표 웨이브", Problems[0]);

            def.Mode.TargetWaves = 10;
            Assert.IsTrue(ModeValidation.Validate(def, Problems));
        }

        [Test]
        public void 저작_플랜_모드인데_플랜이_비면_빨갛다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.WaveSource = WaveSourceKind.AuthoredPlan;

            Assert.IsFalse(ModeValidation.Validate(def, Problems));

            def.WavePlan = new WavePlanDef
            {
                DisplayName = "t",
                Waves = new[] { new AuthoredWaveDef { DurationSec = 5f } },
            };
            Assert.IsTrue(ModeValidation.Validate(def, Problems));
        }

        [Test]
        public void 타임어택에_제한_시간을_붙이면_빨갛다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.Goal = GoalKind.TimeAttack;
            def.Mode.Clock = ClockKind.FixedLimit;
            def.Mode.TargetWaves = 3;

            Assert.IsFalse(ModeValidation.Validate(def, Problems),
                "제한 시간이 먼저 끝내면 「빨리」가 의미를 잃는다");

            def.Mode.Clock = ClockKind.CountUp;
            Assert.IsTrue(ModeValidation.Validate(def, Problems));
        }

        [Test]
        public void 저작_플랜과_제한_시간의_조합은_문제가_아니다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.WaveSource = WaveSourceKind.AuthoredPlan;
            def.Mode.Clock = ClockKind.FixedLimit;
            def.WavePlan = new WavePlanDef
            {
                DisplayName = "t",
                Waves = new[] { new AuthoredWaveDef { DurationSec = 5f } },
            };

            Assert.IsTrue(ModeValidation.Validate(def, Problems),
                "플랜이 자기 길이를 갖고, 라이브 튜토리얼 판이 그 조합이다");
        }

        [Test]
        public void 손패가_덱보다_크면_빨갛다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.DeckSize = 2;
            def.Mode.PublicActiveCount = 0;
            def.Mode.HandSize = 5;

            Assert.IsFalse(ModeValidation.Validate(def, Problems));
        }

        [Test]
        public void 기본_모드는_유효하다()
        {
            var def = CoreMatchFixtures.Definition();
            Assert.IsTrue(ModeValidation.Validate(def, Problems),
                Problems.Count > 0 ? Problems[0] : "");
        }

        [Test]
        public void 모드_id_는_해시에_들어간다()
        {
            var a = CoreMatchFixtures.Definition();
            var b = CoreMatchFixtures.Definition();
            b.Mode.ModeId = "some_other_mode";

            Assert.AreNotEqual(a.ComputeConfigHash(), b.ComputeConfigHash(),
                "재현 축은 modeId + seed 이고 해시는 앞의 것을 답한다");
        }

        [Test]
        public void 리더보드_키가_비면_modeId_가_그_자리다()
        {
            var mode = ModeDef.Default();
            Assert.AreEqual(mode.ModeId, mode.EffectiveLeaderboardId);

            mode.LeaderboardId = "season_3";
            Assert.AreEqual("season_3", mode.EffectiveLeaderboardId);
        }

        [Test]
        public void 배치_페이즈_술어는_길이가_아니라_입력과_대기다()
        {
            var mode = ModeDef.Default();
            Assert.IsFalse(mode.HasPlacementPhase, "고정구의 모드에는 배치 페이즈가 없다");

            mode.PlacementInputEnabled = true;
            Assert.IsTrue(mode.HasPlacementPhase, "길이가 0 이어도 신호는 난다");

            mode.PlacementInputEnabled = false;
            mode.PlacementSeconds = 3f;
            Assert.IsTrue(mode.HasPlacementPhase, "카운트다운 변형");
        }
    }
}
