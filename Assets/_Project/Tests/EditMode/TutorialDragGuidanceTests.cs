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
        // tutorial-content-teardown unit 0 — 튜토리얼 홀드 축이 걷히면서 술어가 한 인자로
        // 줄었다. 남은 규칙 하나: **드래그/조준이 물려 있으면 배치를 끝내지 않는다.**
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void PlacementPolicy_InteractionBlockAlwaysWins(bool interactionBlocked, bool expected)
        {
            Assert.AreEqual(expected, PlacementPhasePolicy.CanFinish(interactionBlocked));
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
}
