using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // page-local-presets — 코드리뷰 CRITICAL 의 회귀 테스트.
    //
    // 결함: 프리셋 목록 팝업이 PresetBar 의 자식인데 UGUI 렌더/레이캐스트 순서는 계층
    // 순서다. 두 페이지 빌더는 PresetBar 를 BrowserPanel **앞에** 만들므로, 팝업을 바
    // 안에서만 SetAsLastSibling 하면 나중 생성된 불투명 BrowserPanel 이 위에 덮는다.
    // `[+] 새 프리셋`이 그 팝업 안에만 있어 프리셋 생성·전환이 전부 도달 불가였다.
    //
    // 이 결함은 **컴파일도 EditMode 도 잡지 못한다**(런타임 계층 순서). 그래서 스크린샷
    // 육안 확인이 아니라 형제 인덱스를 직접 단정해 회귀를 고정한다.
    public class PresetBarPopupLayerTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator SquadPage_OpenPopup_RaisesBarAboveBrowser()
        {
            yield return LoadLobbyAndOpen("OnOpenSquad");

            var bar = Object.FindObjectOfType<PresetBarView>();
            Assert.IsNotNull(bar, "스쿼드 페이지에 PresetBarView 가 있다");

            var page = bar.transform.parent;   // CharacterPage (페이지 루트)
            var browser = FindChild(page, "BrowserPanel");
            Assert.IsNotNull(browser, "BrowserPanel 이 같은 페이지 루트 아래 있다");

            // 열기 전: 빌드 순서상 바가 브라우저보다 앞이다(= 아래에 그려진다).
            Assert.Less(bar.transform.GetSiblingIndex(), browser.GetSiblingIndex(),
                "precondition — 빌더가 PresetBar 를 BrowserPanel 앞에 만든다");

            OpenPopup(bar);
            yield return null;

            Assert.Greater(bar.transform.GetSiblingIndex(), browser.GetSiblingIndex(),
                "팝업을 열면 바가 브라우저보다 뒤 형제로 올라가야 한다 — 아니면 그리드가 목록을 덮는다");

            var popup = FindChild(bar.transform, "PresetPopup");
            Assert.IsNotNull(popup, "팝업이 생성돼 있다");
            Assert.IsTrue(popup.gameObject.activeInHierarchy, "팝업이 열려 있다");
            Assert.AreEqual(bar.transform.childCount - 1, popup.GetSiblingIndex(),
                "팝업은 바 안에서도 최상단이어야 한다(이름 필드·버튼에 가리지 않게)");
        }

        [UnityTest]
        public IEnumerator DreamcatcherPage_OpenPopup_RaisesBarAboveBrowser()
        {
            yield return LoadLobbyAndOpen("OnOpenDreamcatcher");

            var bar = Object.FindObjectOfType<PresetBarView>();
            Assert.IsNotNull(bar, "드림캐쳐 페이지에 PresetBarView 가 있다");

            var page = bar.transform.parent;   // DreamPage
            var browser = FindChild(page, "BrowserPanel");
            Assert.IsNotNull(browser, "BrowserPanel 이 같은 페이지 루트 아래 있다");

            Assert.Less(bar.transform.GetSiblingIndex(), browser.GetSiblingIndex(),
                "precondition");

            OpenPopup(bar);
            yield return null;

            Assert.Greater(bar.transform.GetSiblingIndex(), browser.GetSiblingIndex(),
                "드림캐쳐 페이지도 동일 — 두 빌더가 같은 골격이라 같은 함정을 공유한다");
        }

        // ---- helpers --------------------------------------------------------

        private IEnumerator LoadLobbyAndOpen(string openMethod)
        {
            // 로비 진입 시 프로필 로드 + 튜토리얼/로그인 게이트 로그가 섞인다.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindObjectOfType<OutgameMenuController>();
            Assert.IsNotNull(menu, "OutgameMenuController present");

            // 패널을 여는 것이 페이지 빌드(OnEnable)를 돌린다.
            menu.GetType().GetMethod(openMethod, BindingFlags.Public | BindingFlags.Instance)
                .Invoke(menu, null);
            yield return null;
            yield return null;
        }

        // TogglePopup 은 private — 피커 버튼 클릭과 같은 경로를 리플렉션으로 구동한다.
        private static void OpenPopup(PresetBarView bar)
        {
            var m = typeof(PresetBarView).GetMethod("TogglePopup",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "TogglePopup 존재");
            m.Invoke(bar, null);
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }
    }
}
