using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // first-session-tutorial unit 17 rev — 손패가 **이미 열린 채** 선택 대상이 잡히는 사건에
    // 신호가 나가는지 고정한다.
    //
    // 이 테스트가 지키는 실제 버그: 항아리로 손패를 먼저 연 뒤 유닛을 탭하면 탭 즉발 안내가
    // 안 떴다(사용자 보고 2026-07-30). 원인은 튜토리얼이 `HandOpened` 만 듣고 있었던 것 —
    // 그건 닫힘→열림 전이에서만 발화하는데, 이미 열려 있으면 `OpenForSelection` 이
    // `State == Hand` 라 no-op 이라(계약: 선택 전환은 재딜 없음) 아무 신호도 안 나간다.
    //
    // 그래서 신호를 **대상이 잡히는 시점**으로 옮겼다. 이 발화가 사라지면 그 버그가 그대로
    // 되돌아온다 — 안내가 조용히 안 뜨는 형태라 육안으로도 늦게 발견된다.
    public class HandViewSelectionSignalTests
    {
        private GameObject _go;
        private DreamcatcherHandView _view;
        private int _fired;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HandViewSelectionSignalTests");
            _view = _go.AddComponent<DreamcatcherHandView>();
            _fired = 0;
            _view.SelectionTargetSet += () => _fired++;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void SetSelectionTarget_Fires_AndStoresTarget()
        {
            var target = new Entity { Index = 7, Version = 1 };

            _view.SetSelectionTarget(target);

            Assert.AreEqual(1, _fired, "선택 대상이 잡히면 신호가 나가야 한다");
            Assert.AreEqual(target, _view.SelectionTarget);
            Assert.IsTrue(_view.InSelectionMode);
        }

        [Test]
        public void SelectionSwitch_FiresAgain()
        {
            // 유닛 A → B 전환도 사건이다. 튜토리얼은 자기 래치로 중복을 걸러내므로
            // 뷰는 사건을 빠짐없이 알리는 쪽이 맞다.
            _view.SetSelectionTarget(new Entity { Index = 1, Version = 1 });
            _view.SetSelectionTarget(new Entity { Index = 2, Version = 1 });

            Assert.AreEqual(2, _fired);
        }

        [Test]
        public void ClearSelectionTarget_DoesNotFire()
        {
            _view.SetSelectionTarget(new Entity { Index = 3, Version = 1 });
            _fired = 0;

            _view.ClearSelectionTarget();

            Assert.AreEqual(0, _fired, "해제는 안내를 띄울 사건이 아니다");
            Assert.IsFalse(_view.InSelectionMode);
        }

        [Test]
        public void SetNullTarget_DoesNotFire()
        {
            _view.SetSelectionTarget(Entity.Null);

            Assert.AreEqual(0, _fired);
            Assert.IsFalse(_view.InSelectionMode);
        }
    }
}
