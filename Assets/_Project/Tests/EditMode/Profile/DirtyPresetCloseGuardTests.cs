using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode.Profile
{
    // page-local-presets unit 8 — 저장하지 않은 작업본을 Close로 조용히 버리지 않는다.
    public class DirtyPresetCloseGuardTests
    {
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private PlayerProfile _profile;
        private PlayerProfileSO _profileSO;

        [SetUp]
        public void SetUp()
        {
            _profile = new PlayerProfile
            {
                squads = new List<SquadPreset>
                {
                    new SquadPreset
                    {
                        id = "squad_1",
                        name = "스쿼드 1",
                        unitIds = new List<string> { "unit_saved" },
                    },
                    new SquadPreset
                    {
                        id = "squad_2",
                        name = "스쿼드 2",
                        unitIds = new List<string> { "unit_other" },
                    },
                },
                dreamcatcherDecks = new List<DreamcatcherPreset>
                {
                    new DreamcatcherPreset
                    {
                        id = "deck_1",
                        name = "덱 1",
                        cardIds = new List<string> { "card_saved" },
                    },
                    new DreamcatcherPreset
                    {
                        id = "deck_2",
                        name = "덱 2",
                        cardIds = new List<string> { "card_other" },
                    },
                },
                selectedSquadId = "squad_1",
                selectedDeckId = "deck_1",
            };
            _profile.NormalizePresets();

            _profileSO = Own(ScriptableObject.CreateInstance<PlayerProfileSO>());
            _profileSO.SetLoadedProfile(_profile);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) UnityEngine.Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test]
        public void SquadClose_WhenClean_ClosesImmediately()
        {
            var controller = MakeSquad(out _);
            bool closed = false;

            controller.RequestClose(() => closed = true);

            Assert.IsTrue(closed);
        }

        [Test]
        public void SquadClose_WhenDirty_WaitsForConfirmation()
        {
            var controller = MakeSquad(out var popup);
            SquadWorking(controller)[0] = "unit_edited";
            bool closed = false;

            controller.RequestClose(() => closed = true);

            Assert.IsFalse(closed, "팝업 확인 전에는 닫지 않는다");
            Assert.IsTrue(PopupRoot(popup).activeSelf);

            Invoke(popup, "OnConfirm");

            Assert.IsTrue(closed);
            Assert.IsFalse(PopupRoot(popup).activeSelf);
        }

        [Test]
        public void DeckClose_WhenDirty_WaitsForConfirmation()
        {
            var controller = MakeDeck(out var popup);
            DeckWorking(controller).Add("card_edited");
            bool closed = false;

            controller.RequestClose(() => closed = true);

            Assert.IsFalse(closed);
            Assert.IsTrue(PopupRoot(popup).activeSelf);

            Invoke(popup, "OnConfirm");

            Assert.IsTrue(closed);
        }

        [Test]
        public void DirtyClose_WithoutPopup_IsFailClosed()
        {
            var controller = MakeSquad(out _, injectPopup: false);
            SquadWorking(controller)[0] = "unit_edited";
            bool closed = false;
            LogAssert.Expect(LogType.Error, new Regex("confirmPopup 미주입.*닫기"));

            controller.RequestClose(() => closed = true);

            Assert.IsFalse(closed);
        }

        [Test]
        public void SquadPresetPick_WhenDirty_WaitsThenLoadsTargetStoredPreset()
        {
            var controller = MakeSquad(out var popup);
            SquadWorking(controller)[0] = "unit_edited";

            Invoke(controller, "OnPresetPicked", "squad_2");

            Assert.AreEqual("squad_1", Field(controller, "_viewingPresetId").GetValue(controller));
            Assert.IsTrue(PopupRoot(popup).activeSelf);

            Invoke(popup, "OnConfirm");

            Assert.AreEqual("squad_2", Field(controller, "_viewingPresetId").GetValue(controller));
            Assert.AreEqual("unit_other", SquadWorking(controller)[0]);
        }

        [Test]
        public void DeckPresetPick_WhenDirty_CancelKeepsCurrentWorkingCopy()
        {
            var controller = MakeDeck(out var popup);
            DeckWorking(controller).Add("card_edited");

            Invoke(controller, "OnPresetPicked", "deck_2");

            Assert.IsTrue(PopupRoot(popup).activeSelf);
            Invoke(popup, "Hide");

            Assert.AreEqual("deck_1", Field(controller, "_viewingPresetId").GetValue(controller));
            CollectionAssert.AreEqual(
                new[] { "card_saved", "card_edited" }, DeckWorking(controller));
        }

        [Test]
        public void MenuClose_DirtySquad_CancelKeepsPanelAndWorkingCopy()
        {
            var panel = Own(new GameObject("SquadPanel"));
            var controller = MakeSquad(out var popup, parent: panel.transform);
            SquadWorking(controller)[0] = "unit_edited";
            var menu = MakeMenu(squadPanel: panel);

            menu.OnClosePanels();

            Assert.IsTrue(panel.activeSelf);
            Assert.IsTrue(PopupRoot(popup).activeSelf);

            Invoke(popup, "Hide");

            Assert.IsTrue(panel.activeSelf, "취소하면 페이지를 유지한다");
            Assert.AreEqual("unit_edited", SquadWorking(controller)[0], "작업본도 유지한다");
        }

        [Test]
        public void MenuClose_DirtyDeck_ConfirmClosesPanel()
        {
            var panel = Own(new GameObject("DeckPanel"));
            var controller = MakeDeck(out var popup, parent: panel.transform);
            DeckWorking(controller).Add("card_edited");
            var menu = MakeMenu(deckPanel: panel);

            menu.OnClosePanels();
            Assert.IsTrue(panel.activeSelf);

            Invoke(popup, "OnConfirm");

            Assert.IsFalse(panel.activeSelf);
        }

        private SquadCharacterPageController MakeSquad(
            out ConfirmPopup popup, bool injectPopup = true, Transform parent = null)
        {
            var host = Own(new GameObject("SquadController"));
            if (parent != null) host.transform.SetParent(parent, false);
            host.SetActive(false);
            var controller = host.AddComponent<SquadCharacterPageController>();
            SetField(controller, "profileSO", _profileSO);
            SetField(controller, "_viewingPresetId", "squad_1");
            Invoke(controller, "LoadWorking", "squad_1");
            popup = injectPopup ? MakePopup(parent) : null;
            if (injectPopup) SetField(controller, "confirmPopup", popup);
            return controller;
        }

        private DreamcatcherDeckPageController MakeDeck(
            out ConfirmPopup popup, Transform parent = null)
        {
            var host = Own(new GameObject("DeckController"));
            if (parent != null) host.transform.SetParent(parent, false);
            host.SetActive(false);
            var controller = host.AddComponent<DreamcatcherDeckPageController>();
            SetField(controller, "profileSO", _profileSO);
            SetField(controller, "_viewingPresetId", "deck_1");
            Invoke(controller, "LoadWorking");
            popup = MakePopup(parent);
            SetField(controller, "confirmPopup", popup);
            return controller;
        }

        private ConfirmPopup MakePopup(Transform parent)
        {
            var host = Own(new GameObject("ConfirmPopup", typeof(RectTransform)));
            if (parent != null) host.transform.SetParent(parent, false);
            return host.AddComponent<ConfirmPopup>();
        }

        private OutgameMenuController MakeMenu(
            GameObject squadPanel = null, GameObject deckPanel = null)
        {
            var host = Own(new GameObject("Menu"));
            host.SetActive(false);
            var menu = host.AddComponent<OutgameMenuController>();
            SetField(menu, "squadPanel", squadPanel);
            SetField(menu, "dreamcatcherPanel", deckPanel);
            return menu;
        }

        private static List<string> SquadWorking(SquadCharacterPageController controller) =>
            (List<string>)Field(controller, "_workingUnits").GetValue(controller);

        private static List<string> DeckWorking(DreamcatcherDeckPageController controller) =>
            (List<string>)Field(controller, "_working").GetValue(controller);

        private static GameObject PopupRoot(ConfirmPopup popup) =>
            (GameObject)Field(popup, "_root").GetValue(popup);

        private static void SetField(object target, string name, object value) =>
            Field(target, name).SetValue(target, value);

        private static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, args);

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            _owned.Add(value);
            return value;
        }
    }
}
