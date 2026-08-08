using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // first-session-tutorial unit 19 — 전투 HUD 안내가 읽는 seam 을 고정한다.
    // 이 두 값이 뒤집히면 실패가 조용하다: ShowsStressLimit 이 참으로 새면
    // "스트레스가 N이 되면 패배합니다" 라는 거짓 문구가 나간다. 어느 쪽도 콘솔에 흔적을
    // 남기지 않는다.
    //
    // three-minute-survival unit 0 — 이제 **전 모드**가 showLimit:false 다(패배는 골 안정도가
    // 소유하므로 스트레스 분모는 어디서도 참이 아니다). 뷰 seam 은 양방향을 그대로 지원해야
    // 하므로 아래 테스트는 불변이다 — 튜토리얼의 패배 문구 생략이 이 가드에 걸려 있다.
    public class ScoreHudStressSeamTests
    {
        private GameObject _go;
        private ScoreHudView _hud;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ScoreHudStressSeamTest");
            _hud = _go.AddComponent<ScoreHudView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void SetLeakStatus_ExposesLimitAndShowFlag()
        {
            _hud.SetLeakStatus(2, 7);

            Assert.AreEqual(7, _hud.StressLimit,
                "안내 문구의 한계 수치는 배지 분모와 같은 스냅샷에서 와야 한다.");
            Assert.IsTrue(_hud.ShowsStressLimit);
        }

        [Test]
        public void SetLeakStatus_WithoutLimit_ReportsHiddenLimit()
        {
            _hud.SetLeakStatus(2, 7, showLimit: false);

            Assert.IsFalse(_hud.ShowsStressLimit,
                "엔드리스는 분모를 표기하지 않는다 — 패배 조건 문구를 생략해야 한다.");
        }

        [Test]
        public void BeforeAnySnapshot_LimitIsZero()
        {
            // 스냅샷 전에는 한계가 0 이고 표기 플래그는 기본 true 다. 튜토리얼이 이 조합을
            // 걸러내지 않으면 `스트레스가 0이 되면 패배합니다.` 가 경고 없이 나간다.
            Assert.AreEqual(0, _hud.StressLimit);
            Assert.IsTrue(_hud.ShowsStressLimit);
        }

        [Test]
        public void StressBadgeRect_PointsAtThePlateNotTheValueText()
        {
            // EditMode 는 AddComponent 로 Awake 를 부르지 않으므로 캔버스를 직접 세운다.
            // 위 세 테스트와 달리 이 검증만 BuildCanvas 산물을 요구한다.
            //
            // BuildCanvas 는 EditMode 에서 Unity 빌트인 UI 리소스(`UI/Skin/Knob.psd`)를 찾지
            // 못해 [Assert] 로그를 남긴다. 이 spec 과 무관한 에디터 환경 한계이고 실행은
            // 정상 완료되므로 이 테스트에서만 로그 검사를 끈다(LogAssert.Expect 로 문자열을
            // 고정하면 Unity 버전이 바뀔 때 같이 깨진다).
            LogAssert.ignoreFailingMessages = true;
            typeof(ScoreHudView)
                .GetMethod("BuildCanvas", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_hud, null);

            Assert.IsNotNull(_hud.StressBadgeRect,
                "포커스 링 대상이 null 이면 FocusUi 가 링을 조용히 끈다.");
            Assert.AreEqual("LeakPlate", _hud.StressBadgeRect.gameObject.name,
                "숫자 텍스트(LeakValue)가 아니라 배지 플레이트를 가리켜야 링이 `스트레스` 캡션까지 감싼다.");
        }
    }
}
