using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;
using Wassup.UI.Tutorial;

namespace Wassup.Tests.EditMode
{
    public class TutorialDragGuidanceTests
    {
        [Test]
        public void SimulatedTapFlight_ClearsArmWithoutDisarmGuidanceSignal()
        {
            var controllerGo = new GameObject("DragControllerTest");
            var slotGo = new GameObject("DragSlotTest");
            var controller = controllerGo.AddComponent<DefenderDragPlacementController>();
            var slot = slotGo.AddComponent<DefenderDragSlot>();
            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            int disarmedCount = 0;
            controller.Disarmed += () => disarmedCount++;

            controller.ToggleArm(slot, unit, Vector2.zero);
            Assert.IsTrue(controller.IsArmed(slot));

            controller.Disarm(notify: false);
            Assert.IsFalse(controller.IsArmed(slot));
            Assert.AreEqual(0, disarmedCount,
                "Tap-to-place's internal flight must not rewind guidance to Pick.");

            controller.ToggleArm(slot, unit, Vector2.zero);
            controller.Disarm();
            Assert.AreEqual(1, disarmedCount,
                "An actual player disarm must still rewind guidance to Pick.");

            Object.DestroyImmediate(unit);
            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(controllerGo);
        }
    }

    public class TutorialInteractionAndSafeAreaTests
    {
        // first-run-tutorial unit 3 — 술어가 두 인자로 돌아왔다(인트로 홀드).
        // 규칙 둘: 드래그/조준이 물려 있으면 끝내지 않고, 온보딩이 붙잡고 있어도 끝내지 않는다.
        //
        // ⚠ 이 함수가 **하나**여야 한다는 것이 계약이다. TickAutoStart 와 FinishPlacement 가
        // 서로 다른 술어를 보면 종료가 거절된 프레임에 카운트다운만 0으로 눌려 판이 벽돌이
        // 된다(a1392b4d). 그래서 두 소비자가 다 여기를 지난다.
        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public void PlacementPolicy_BlockOrHoldStopsFinish(bool interactionBlocked, bool introHeld, bool expected)
        {
            Assert.AreEqual(expected, PlacementPhasePolicy.CanFinish(interactionBlocked, introHeld));
        }

        // match-intro-phase-toggles unit 0 + tutorial-content-teardown unit 0 —
        // 첫 판 예외가 사라져 플래그가 곧 진실이다.
        [TestCase(true, false)]   // 현행 배치(플래그 on)
        [TestCase(false, true)]   // 자동 시작
        public void PlacementPolicy_FlagIsTheOnlyTruth(bool placementPhaseEnabled, bool expected)
        {
            Assert.AreEqual(expected, PlacementPhasePolicy.UseAutoStart(placementPhaseEnabled));
        }

        [Test]
        public void SafeAreaLayout_FitsAndClampsPulsingFocus()
        {
            var safe = new Rect(-200f, -100f, 400f, 200f);
            Vector2 size = TutorialSafeAreaLayout.FitSize(
                safe, new Vector2(880f, 116f), 20f, 1.1f);
            Vector2 center = TutorialSafeAreaLayout.ClampCenter(
                safe, new Vector2(500f, 500f), size, 20f, 1.1f);

            Assert.LessOrEqual(size.x * 1.1f, safe.width - 40f + 0.001f);
            Assert.LessOrEqual(size.y * 1.1f, safe.height - 40f + 0.001f);
            Assert.LessOrEqual(center.x + size.x * 0.55f, safe.xMax - 20f + 0.001f);
            Assert.LessOrEqual(center.y + size.y * 0.55f, safe.yMax - 20f + 0.001f);
        }

        [Test]
        public void SafeAreaLayout_PointerFlipsBelowNearTopEdge()
        {
            var safe = new Rect(-200f, -100f, 400f, 200f);
            Vector2 pointer = TutorialSafeAreaLayout.PlacePointer(
                safe, new Vector2(0f, 65f), new Vector2(100f, 40f),
                new Vector2(32f, 48f), 30f, 10f, 20f);

            Assert.Less(pointer.y, 65f, "Pointer should flip below a top-edge focus ring.");
            Assert.GreaterOrEqual(pointer.y - 24f, safe.yMin + 20f - 0.001f);
            Assert.LessOrEqual(pointer.y + 24f, safe.yMax - 20f + 0.001f);
        }
    }

    public class TutorialSurvivalHintTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void B5_FocusesTimerAfterRemovingInputBlock()
        {
            var controllerGo = new GameObject("TutorialControllerTest");
            var guidanceGo = new GameObject("TutorialGuidanceTest");
            var overlayGo = new GameObject("TutorialOverlayTest");
            var hudGo = new GameObject("ScoreHudTest");
            var config = ScriptableObject.CreateInstance<FirstRunTutorialConfig>();

            try
            {
                config.survivalHintSeconds = 0f;
                var controller = controllerGo.AddComponent<FirstRunTutorialController>();
                var guidance = guidanceGo.AddComponent<TutorialGuidanceView>();
                var overlay = overlayGo.AddComponent<OutgameTutorialOverlay>();
                var hud = hudGo.AddComponent<ScoreHudView>();
                var timerGo = new GameObject("MatchTimerBadge", typeof(RectTransform));
                timerGo.transform.SetParent(hudGo.transform, false);

                // ScoreHud 전체 런타임 UI를 짓지 않고, 그 소유 seam만 고정한다. 전체 BuildCanvas는
                // Editor 기본 UI skin에 의존해 이 단위 테스트의 관심사를 벗어난다.
                Set(hud, "_timerRoot", timerGo);
                Assert.IsNotNull(hud.TimerFocusRect, "시간 배지가 튜토리얼 포커스 seam을 제공해야 한다.");

                Set(controller, "config", config);
                Set(controller, "guidance", guidance);
                Set(controller, "overlay", overlay);
                Set(controller, "scoreHud", hud);
                Set(controller, "_b4Completed", true);

                overlay.Show(0f);
                Set(controller, "_dimShown", true);

                var routine = (IEnumerator)typeof(FirstRunTutorialController)
                    .GetMethod("RunSurvivalHint", PrivateInstance).Invoke(controller, null);

                Assert.IsTrue(routine.MoveNext(), "B5 안내 노출 구간에 진입해야 한다.");
                Assert.IsFalse(Get<bool>(controller, "_dimShown"),
                    "B5는 안내를 띄우기 전에 입력 차단막을 내려야 한다.");
                Assert.AreSame(hud.TimerFocusRect, Get<RectTransform>(guidance, "_uiTarget"),
                    "B5 포커스 대상은 실제 시간 배지여야 한다.");

                var wait = routine.Current as IEnumerator;
                Assert.IsNotNull(wait);
                while (wait.MoveNext()) { }
                Assert.IsFalse(routine.MoveNext());
                Assert.IsTrue(Get<bool>(controller, "_b5Completed"));
                Assert.IsNull(Get<RectTransform>(guidance, "_uiTarget"),
                    "안내가 끝나면 시간 UI 포커스를 정리해야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(controllerGo);
                Object.DestroyImmediate(guidanceGo);
                Object.DestroyImmediate(overlayGo);
                Object.DestroyImmediate(hudGo);
            }
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private static T Get<T>(object target, string field)
            => (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);
    }
}
