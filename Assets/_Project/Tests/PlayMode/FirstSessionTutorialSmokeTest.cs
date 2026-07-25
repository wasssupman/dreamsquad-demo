using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI;
using Wassup.UI.Tutorial;

namespace Wassup.Tests.PlayMode
{
    public class FirstSessionTutorialSmokeTest
    {
        [UnityTest]
        public IEnumerator PlacementGate_HoldsCountdownUntilUnlockedStartClick()
        {
            var go = new GameObject("PlacementTutorialGateTest");
            var view = go.AddComponent<PlacementPhaseView>();
            yield return null; // Awake/UGUI construction

            bool ready = false;
            view.PlacementReady += () => ready = true;
            view.BeginPlacementPhase();

            Assert.IsTrue(ready, "PlacementReady must fire after placement initialization.");
            Assert.IsTrue(view.IsPlacementActive);
            Assert.IsTrue(view.StartButtonRect.transform.parent.gameObject.activeSelf);

            view.BeginTutorialGate();
            Assert.IsFalse(view.StartButtonRect.transform.parent.gameObject.activeSelf,
                "START must stay hidden until one placement is committed.");

            float before = ReadRemaining(view);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(before, ReadRemaining(view), 0.001f,
                "Tutorial guidance must not consume the placement countdown.");

            view.UnlockTutorialStart();
            Assert.IsTrue(view.StartButtonRect.transform.parent.gameObject.activeSelf);
            Assert.IsTrue(view.StartButtonRect.GetComponent<Button>().interactable);

            view.StartButtonRect.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(view.IsPlacementActive,
                "Only the player's actual START click should release the tutorial gate.");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectionAim_BlocksStartAndCountdown_EvenAfterTutorialGateEnds()
        {
            var selectorGo = new GameObject("PlacementInteractionGuardTest");
            var selector = selectorGo.AddComponent<DefenderSelector>();
            var drag = selector.DragController;
            var aim = selectorGo.AddComponent<DirectionAimController>();
            WriteField(drag, "_aimController", aim);
            WriteField(aim, "_active", true);

            var viewGo = new GameObject("PlacementViewInteractionGuardTest");
            var view = viewGo.AddComponent<PlacementPhaseView>();
            WriteField(view, "defenderSelector", selector);
            view.BeginPlacementPhase();
            view.BeginTutorialGate();
            view.EndTutorialGate(restoreNormalPlacement: true); // Skip path
            yield return null;

            Assert.IsTrue(view.IsPlacementActive);
            Assert.IsFalse(view.StartButtonRect.transform.parent.gameObject.activeSelf,
                "Skip must not expose START while directional aim is still active.");
            float before = ReadRemaining(view);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(before, ReadRemaining(view), 0.001f,
                "Placement countdown must pause while directional aim is active.");

            // Same-frame/programmatic button invocation is guarded independently of
            // visibility, so UI ordering cannot bypass the phase lock.
            view.StartButtonRect.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(view.IsPlacementActive);

            WriteField(aim, "_active", false);
            yield return null;
            Assert.IsTrue(view.StartButtonRect.transform.parent.gameObject.activeSelf);
            view.StartButtonRect.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(view.IsPlacementActive);

            Object.Destroy(viewGo);
            Object.Destroy(selectorGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipEvent_RestoresPlacementAndCompletesProfileThroughController()
        {
            var profileHolder = ScriptableObject.CreateInstance<PlayerProfileSO>();
            var profile = new PlayerProfile();
            profileHolder.SetLoadedProfile(profile);

            var viewGo = new GameObject("TutorialSkipPlacementViewTest");
            var view = viewGo.AddComponent<PlacementPhaseView>();
            view.BeginPlacementPhase();
            view.BeginTutorialGate();

            var guidanceGo = new GameObject("TutorialSkipGuidanceTest");
            var guidance = guidanceGo.AddComponent<TutorialGuidanceView>();

            var controllerGo = new GameObject("TutorialSkipControllerTest");
            controllerGo.SetActive(false);
            var controller = controllerGo.AddComponent<FirstSessionTutorialController>();
            WriteField(controller, "profileSO", profileHolder);
            WriteField(controller, "placementView", view);
            WriteField(controller, "guidance", guidance);
            PlayerProfile saved = null;
            controller.ProfileSaver = value => saved = value;
            controllerGo.SetActive(true);

            // Enter the active core state and use the real GuidanceView button event,
            // exercising event wiring rather than invoking the controller method.
            WriteField(controller, "_coreActive", true);
            guidance.ShowMessage("테스트", showSkip: true);
            var skipObject = (GameObject)ReadField(guidance, "_skipObject");
            skipObject.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreSame(profile, saved, "Skip completion must persist the loaded profile.");
            Assert.AreEqual(TutorialProgress.CoreVersion, profile.firstBattleTutorialVersion);
            Assert.IsTrue(view.IsPlacementActive);
            Assert.IsTrue(view.StartButtonRect.transform.parent.gameObject.activeSelf,
                "Skip must restore normal placement controls.");

            Object.Destroy(controllerGo);
            Object.Destroy(guidanceGo);
            Object.Destroy(viewGo);
            Object.Destroy(profileHolder);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldMarkerBeat_AvoidsFixedUiAndClearsBeforePlacementMethod()
        {
            var cameraGo = new GameObject("TutorialWorldMarkerCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var style = ScriptableObject.CreateInstance<TutorialGuidanceStyle>();
            var guidanceGo = new GameObject("TutorialWorldMarkerGuidance");
            guidanceGo.SetActive(false);
            var guidance = guidanceGo.AddComponent<TutorialGuidanceView>();
            WriteField(guidance, "style", style);
            guidanceGo.SetActive(true);
            guidance.SetWorldMarkerLayout(true);
            guidance.ShowMessage("적이 노란색 베이스에 닿기 전에 막아주세요.", showSkip: true);
            guidance.ShowWorldMarker(camera, Vector3.zero, "방어 목표", Color.yellow,
                preferLabelAbove: true);
            yield return null;

            var messagePanel = (GameObject)ReadField(guidance, "_messagePanel");
            var messageRect = (RectTransform)messagePanel.transform;
            Assert.AreEqual(-style.worldMarkerMessageTopOffset, messageRect.anchoredPosition.y, 0.01f,
                "The map-reading message must move below the top title and spawn markers.");

            var markers = (IList)ReadField(guidance, "_worldPulses");
            Assert.AreEqual(1, markers.Count);
            object marker = markers[0];
            bool persistent = (bool)marker.GetType()
                .GetField("persistent", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(marker);
            Assert.IsTrue(persistent, "Step 1 marker must not expire on a timer.");

            var rect = (RectTransform)marker.GetType()
                .GetField("rect", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(marker);
            var label = (RectTransform)marker.GetType()
                .GetField("label", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(marker);
            Assert.IsTrue(rect.gameObject.activeSelf);
            Assert.Greater(label.anchoredPosition.y, 0f,
                "The bottom goal label must sit above its marker instead of covering the defender tray.");
            Component markerText = null;
            var markerComponents = rect.GetComponentsInChildren<Component>();
            for (int i = 0; i < markerComponents.Length; i++)
                if (markerComponents[i].GetType().Name == "TextMeshProUGUI") markerText = markerComponents[i];
            Assert.IsNotNull(markerText);
            Assert.AreEqual("방어 목표", markerText.GetType().GetProperty("text").GetValue(markerText));

            marker.GetType().GetField("age", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(marker, 100f);
            yield return null;
            Assert.AreEqual(1, markers.Count, "Persistent marker must survive far beyond the intro beat.");

            var controllerGo = new GameObject("TutorialWorldMarkerController");
            controllerGo.SetActive(false);
            var controller = controllerGo.AddComponent<FirstSessionTutorialController>();
            WriteField(controller, "guidance", guidance);
            typeof(FirstSessionTutorialController)
                .GetMethod("BeginPick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);

            Assert.AreEqual(0, markers.Count,
                "The placement-method message must not retain map markers over its multi-line panel.");
            Assert.AreEqual(-style.messageTopOffset, messageRect.anchoredPosition.y, 0.01f,
                "The placement-method message must return to the normal top position.");

            Object.Destroy(controllerGo);
            Object.Destroy(guidanceGo);
            Object.Destroy(cameraGo);
            Object.Destroy(style);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TutorialCanvasOrder_StaysAboveMenusAndBelowSceneTransition()
        {
            const int ExistingMenuLayer = 1000;
            const int SceneTransitionLayer = 10000;

            var style = ScriptableObject.CreateInstance<TutorialGuidanceStyle>();

            var guidanceGo = new GameObject("TutorialCanvasOrderGuidance");
            guidanceGo.SetActive(false);
            var guidance = guidanceGo.AddComponent<TutorialGuidanceView>();
            WriteField(guidance, "style", style);
            guidanceGo.SetActive(true);

            var overlayGo = new GameObject("TutorialCanvasOrderDim");
            var overlay = overlayGo.AddComponent<OutgameTutorialOverlay>();
            overlay.SetSortingOrder(guidance.DimSortingOrder);
            overlay.Show();
            yield return null;

            Canvas guidanceCanvas = guidanceGo.GetComponent<Canvas>();
            Canvas dimCanvas = overlayGo.GetComponent<Canvas>();
            GraphicRaycaster guidanceRaycaster = guidanceGo.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(guidanceCanvas);
            Assert.IsNotNull(dimCanvas);
            Assert.IsNotNull(guidanceRaycaster);
            Assert.AreEqual(style.dimSortingOrder, dimCanvas.sortingOrder);
            Assert.AreEqual(style.guidanceSortingOrder, guidanceCanvas.sortingOrder);
            Assert.Less(dimCanvas.sortingOrder, guidanceCanvas.sortingOrder,
                "The blocking dim must render immediately below guidance and its Skip/focus UI.");
            Assert.Greater(guidanceCanvas.sortingOrder, ExistingMenuLayer,
                "Tutorial guidance and its tap catcher must outrank the battle menu canvases.");
            Assert.Greater(guidanceRaycaster.sortOrderPriority, ExistingMenuLayer,
                "The full-screen tutorial tap catcher must win input priority over the battle menu.");

            guidance.SetElevated(true);
            Assert.AreEqual(style.elevatedSortingOrder, guidanceCanvas.sortingOrder);
            Assert.Greater(guidanceCanvas.sortingOrder, style.guidanceSortingOrder);
            Assert.Less(guidanceCanvas.sortingOrder, SceneTransitionLayer,
                "Scene transitions must remain above every tutorial presentation.");

            guidance.SetElevated(false);
            Assert.AreEqual(style.guidanceSortingOrder, guidanceCanvas.sortingOrder);

            Object.Destroy(overlayGo);
            Object.Destroy(guidanceGo);
            Object.Destroy(style);
            yield return null;
        }

        private static float ReadRemaining(PlacementPhaseView view)
        {
            return (float)typeof(PlacementPhaseView)
                .GetField("_remaining", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(view);
        }

        private static void WriteField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static object ReadField(object target, string name)
        {
            return target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
