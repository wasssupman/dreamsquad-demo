using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // first-session-tutorial unit 15 — 첫 판 각성 봉인의 **릴레이 배선**만 고정한다.
    //
    // 사실의 소유자는 AwakeningGaugeView(_suppressed)이고, 쓰기 창구는 튜토리얼이 부르는
    // SetSuppressed 하나다. DcInspectController 는 그 사실을 DreamcatcherHandView 릴레이로
    // 풀해서 선택을 막는다(선택이 손패를 열기 때문). 소비자가 둘이 되면서 생긴 위험은
    // **부호 역전과 이름 오타**다 — 릴레이가 조용히 뒤집히면 첫 판 봉인이 통째로 무력화되고
    // (숨긴 손패가 유닛 탭으로 열린다) 반대로 뒤집히면 둘째 판부터 선택이 영영 막힌다.
    //
    // 게이트 본체(DcInspectController.Update)는 BattleBridge·Entity·EventSystem 의존이라
    // 여기서 못 덮는다. 그쪽 회귀 방지는 spec 의 Play 체크리스트가 진다 — spec unit 15 참조.
    public class AwakeningSealRelayTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("AwakeningSealRelayTests");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private static void Wire(DreamcatcherHandView view, AwakeningGaugeView gauge)
        {
            // gaugeView 는 [SerializeField] private — 씬이 하는 일을 테스트가 대신한다.
            var so = new UnityEditor.SerializedObject(view);
            so.FindProperty("gaugeView").objectReferenceValue = gauge;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Relay_DefaultsToUnsealed()
        {
            var gauge = _go.AddComponent<AwakeningGaugeView>();
            var view = _go.AddComponent<DreamcatcherHandView>();
            Wire(view, gauge);

            Assert.IsFalse(gauge.IsSuppressed, "봉인은 튜토리얼이 켜기 전엔 꺼져 있어야 한다");
            Assert.IsFalse(view.AwakeningSealedThisMatch);
        }

        [Test]
        public void Relay_FollowsOwnerBothWays()
        {
            var gauge = _go.AddComponent<AwakeningGaugeView>();
            var view = _go.AddComponent<DreamcatcherHandView>();
            Wire(view, gauge);

            gauge.SetSuppressed(true);
            Assert.IsTrue(view.AwakeningSealedThisMatch, "봉인이 켜지면 릴레이도 참 — 뒤집히면 첫 판 봉인이 무력화된다");

            // 해제도 따라와야 한다. 안 따라오면 둘째 판부터 선택이 영영 막힌다.
            gauge.SetSuppressed(false);
            Assert.IsFalse(view.AwakeningSealedThisMatch);
        }

        [Test]
        public void Relay_FailsOpenWhenGaugeViewMissing()
        {
            // 미배선이면 봉인을 주장하지 않는다(fail-open) — 참조 누락이 선택을 잠그면 안 된다.
            var view = _go.AddComponent<DreamcatcherHandView>();

            Assert.IsFalse(view.AwakeningSealedThisMatch);
        }
    }
}
