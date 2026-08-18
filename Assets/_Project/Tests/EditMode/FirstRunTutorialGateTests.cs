using NUnit.Framework;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // first-run-tutorial unit 0 — 「이 계정이 온보딩을 봤는가」.
    //
    // 이 신호를 matchesPlayed 와 겸직시키지 않는 것이 이 spec 의 계약 2 다. 그래서 겸직
    // 금지를 여기서 못 박는다 — matchesPlayed 를 어떻게 흔들어도 판정이 변하지 않아야 한다.
    //
    // ⚠ 이건 게이트의 **절반**이다. 호출부는 여기에 profileSO.IsLoadedThisSession 을 곱한다
    // (미로드 프로필의 빈 인스턴스가 false 로 읽혀 이미 본 유저에게 다시 뜨는 것을 막는다).
    // 세션 가드는 SO 상태라 여기서 겨눌 수 없다 — FirstMatchTournamentBypassTests 와 같은 형태.
    public class FirstRunTutorialGateTests
    {
        [Test]
        public void FreshProfile_Runs()
        {
            Assert.IsTrue(FirstRunTutorialConfig.ShouldRun(new PlayerProfile()),
                "새 계정은 온보딩을 봐야 한다.");
        }

        [Test]
        public void CompletedProfile_DoesNotRun()
        {
            var profile = new PlayerProfile { firstRunTutorialDone = true };
            Assert.IsFalse(FirstRunTutorialConfig.ShouldRun(profile));
        }

        [Test]
        public void NullProfile_DoesNotRun()
        {
            // 프로필을 모르는 상태에서 딤을 띄우면 로비가 잠긴다. 「확실히 처음일 때만」이
            // 이 가드의 방향이다.
            Assert.IsFalse(FirstRunTutorialConfig.ShouldRun(null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(37)]
        public void MatchHistory_DoesNotAffectGate(int matchesPlayed)
        {
            var fresh = new PlayerProfile { matchesPlayed = matchesPlayed };
            var done = new PlayerProfile { matchesPlayed = matchesPlayed, firstRunTutorialDone = true };

            Assert.IsTrue(FirstRunTutorialConfig.ShouldRun(fresh),
                "matchesPlayed 는 온보딩 판정에 끼어들면 안 된다 — 토너먼트 우회 신호와 겸직 금지.");
            Assert.IsFalse(FirstRunTutorialConfig.ShouldRun(done));
        }

        [Test]
        public void LegacyJsonWithoutField_Runs()
        {
            // 기존 세이브에는 이 키가 없다. JsonUtility 가 없는 키를 이니셜라이저 값(false)으로
            // 남기므로 마이그레이션 없이 「아직 안 봤다」로 읽혀야 한다.
            var profile = UnityEngine.JsonUtility.FromJson<PlayerProfile>(
                "{\"schemaVersion\":1,\"matchesPlayed\":5}");
            Assert.IsFalse(profile.firstRunTutorialDone);
            Assert.IsTrue(FirstRunTutorialConfig.ShouldRun(profile));
        }
    }
}
