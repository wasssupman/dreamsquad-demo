using NUnit.Framework;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // tutorial-content-teardown unit 1 — 계정의 첫 판은 토너먼트 참가 신청을 보내지 않는다
    // (tutorial-offline-match). 서버 `complete` 500 을 피하는 우회이고 **서버는 아직 안 고쳐졌다**.
    //
    // 원래 이 판정은 튜토리얼 술어(`TutorialProgress.ShouldRunCore`)를 공유했는데, 튜토리얼
    // 콘텐츠가 걷히면서 그 술어가 사라졌다. 신호를 `matchesPlayed` 로 옮겼고, 그 옮김이
    // 조용히 뒤집히지 않도록 여기서 못 박는다 — 뒤집히면 첫 판 유저가 서버 버그를 그대로 맞는다.
    //
    // ⚠ 이 순수 함수는 게이트의 **절반**이다. 호출부는 여기에 `profileSO.IsLoadedThisSession`
    // 을 곱한다(옛 술어가 갖고 있던 가드 — 미로드 프로필의 빈 인스턴스가 0 으로 읽혀
    // 정상 유저의 판을 토너먼트에서 빼는 것을 막는다). 세션 가드는 SO 상태라 여기서 못 겨눈다.
    public class FirstMatchTournamentBypassTests
    {
        [Test]
        public void FreshAccount_IsFirstMatch()
        {
            Assert.IsTrue(OutgameMenuController.IsFirstMatch(new PlayerProfile()),
                "matchesPlayed 0 = 계정 첫 판 — 토너먼트에 올리지 않는다.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(37)]
        public void AfterAnyCompletedMatch_IsNotFirstMatch(int matchesPlayed)
        {
            var profile = new PlayerProfile { matchesPlayed = matchesPlayed };
            Assert.IsFalse(OutgameMenuController.IsFirstMatch(profile),
                "한 판이라도 끝냈으면 정상 참가 경로 — 우회가 남으면 그 유저의 판이 토너먼트에서 빠진다.");
        }

        [Test]
        public void NullProfile_IsNotFirstMatch()
        {
            // 프로필을 모르는 상태에서 우회를 켜면 정상 유저의 판이 통째로 빠진다.
            // 「확실히 첫 판일 때만」이 이 가드의 방향이다.
            Assert.IsFalse(OutgameMenuController.IsFirstMatch(null));
        }
    }
}
