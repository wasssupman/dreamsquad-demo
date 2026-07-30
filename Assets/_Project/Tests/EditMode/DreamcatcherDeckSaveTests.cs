using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // page-local-presets unit 6 — 구 `DreamcatcherDeckAutosaveTests` 를 **명시 저장**
    // 의미론으로 전환한 것. 전제가 뒤집혔다: 예전에는 "카드 편집 = 즉시 저장" 이었고
    // 지금은 "편집은 작업본만 바꾸고, [저장]만 디스크에 쓴다".
    //
    // 검증 축은 그대로 저장 호출 **횟수** 다(ProfileSaver 심).
    public class DreamcatcherDeckSaveTests
    {
        private GameObject _host;
        private DreamcatcherDeckPageController _controller;
        private DreamcatcherCardCatalog _catalog;
        private PlayerProfileSO _profileSO;
        private readonly List<DreamcatcherCard> _cards = new List<DreamcatcherCard>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            for (int i = 0; i < 11; i++)
            {
                var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
                card.id = $"card_{i}";
                card.type = CardType.Unit;
                _cards.Add(card);
            }
            _catalog.cards = _cards.ToArray();
            _profileSO = ScriptableObject.CreateInstance<PlayerProfileSO>();
            _host = new GameObject("DreamcatcherDeckSaveTestHost");
            _host.SetActive(false);
            _controller = _host.AddComponent<DreamcatcherDeckPageController>();
            SetField("catalog", _catalog);
            SetField("profileSO", _profileSO);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_profileSO);
            Object.DestroyImmediate(_catalog);
            foreach (var card in _cards) Object.DestroyImmediate(card);
            _cards.Clear();
        }

        // ---- 편집은 저장하지 않는다 -----------------------------------------

        [Test]
        public void AddCard_DoesNotSave()
        {
            LoadedProfile(Ids(9));
            Invoke("LoadWorking");
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("AddCard", "card_9");

            Assert.AreEqual(10, Working().Count, "작업본은 늘어난다");
            Assert.AreEqual(0, saves, "편집은 디스크에 닿지 않는다");
        }

        [Test]
        public void RemoveOccurrence_DoesNotSave()
        {
            var profile = LoadedProfile(Ids(10));
            Invoke("LoadWorking");
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("RemoveOccurrence", "card_9");

            Assert.AreEqual(9, Working().Count);
            Assert.AreEqual(0, saves);
            CollectionAssert.AreEqual(Ids(10), profile.CommittedDeck().cardIds,
                "저장본은 그대로 10장이다");
        }

        [Test]
        public void ManyEdits_StillZeroSaves()
        {
            LoadedProfile(Ids(5));
            Invoke("LoadWorking");
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("AddCard", "card_5");
            Invoke("AddCard", "card_6");
            Invoke("RemoveOccurrence", "card_0");
            Invoke("AddCard", "card_7");

            Assert.AreEqual(0, saves);
        }

        // ---- [저장]만 기록한다 -----------------------------------------------

        [Test]
        public void SavePreset_WritesWorkingIntoStored_Once()
        {
            var profile = LoadedProfile(Ids(9));
            Invoke("LoadWorking");
            Invoke("AddCard", "card_9");

            int saves = 0;
            _controller.ProfileSaver = value => { saves++; Assert.AreSame(profile, value); };

            Invoke("OnSavePreset");

            Assert.AreEqual(1, saves);
            CollectionAssert.AreEqual(Ids(10), profile.CommittedDeck().cardIds);
        }

        [Test]
        public void SavePreset_PersistsInvalidIntermediateDeck()
        {
            // 규칙 미달(9/10)도 저장된다 — START 는 LoadoutGate 가 막는다(기존 계약 유지).
            var profile = LoadedProfile(Ids(10));
            Invoke("LoadWorking");
            Invoke("RemoveOccurrence", "card_9");

            int saves = 0;
            _controller.ProfileSaver = _ => saves++;
            Invoke("OnSavePreset");

            Assert.AreEqual(1, saves);
            CollectionAssert.AreEqual(Ids(9), profile.CommittedDeck().cardIds);
            Assert.IsFalse(DeckRules.Validate(profile.CommittedDeck().cardIds, _catalog, out _));
        }

        [Test]
        public void SavePreset_DoesNotMoveCommittedPointer()
        {
            var profile = LoadedProfile(Ids(9));
            // 확정분이 아닌 두 번째 프리셋을 편집 대상으로 삼는다.
            profile.dreamcatcherDecks.Add(new DreamcatcherPreset
            { id = "deck_2", name = "덱 2", cardIds = new List<string>() });
            SetField("_viewingPresetId", "deck_2");
            Invoke("LoadWorking");
            Invoke("AddCard", "card_0");

            _controller.ProfileSaver = _ => { };
            Invoke("OnSavePreset");

            Assert.AreEqual("deck_1", profile.selectedDeckId,
                "[저장]은 확정을 옮기지 않는다");
        }

        // ---- 프로필 미로드 가드 ----------------------------------------------

        [Test]
        public void Save_DoesNothing_BeforeProfileIsLoaded()
        {
            // SetLoadedProfile 을 거치지 않은 프로필 = 이 세션의 로드본이 아니다.
            _profileSO.profile = new PlayerProfile();
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("OnSavePreset");

            Assert.AreEqual(0, saves);
        }

        [Test]
        public void LoadWorking_DoesNotSave()
        {
            LoadedProfile(Ids(10));
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("LoadWorking");

            Assert.AreEqual(0, saves);
            CollectionAssert.AreEqual(Ids(10), Working());
        }

        // ---- helpers --------------------------------------------------------

        private PlayerProfile LoadedProfile(List<string> ids = null)
        {
            var profile = new PlayerProfile();
            if (ids != null)
            {
                profile.dreamcatcherDecks.Add(new DreamcatcherPreset
                { id = "deck_1", name = "덱 1", cardIds = new List<string>(ids) });
                profile.selectedDeckId = "deck_1";
            }
            _profileSO.SetLoadedProfile(profile);
            SetField("_viewingPresetId", "deck_1");
            return profile;
        }

        private static List<string> Ids(int count)
        {
            var result = new List<string>();
            for (int i = 0; i < count; i++) result.Add($"card_{i}");
            return result;
        }

        private List<string> Working() => (List<string>)typeof(DreamcatcherDeckPageController)
            .GetField("_working", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(_controller);

        private void SetField(string name, object value) => typeof(DreamcatcherDeckPageController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_controller, value);

        private void Invoke(string name, params object[] args) => typeof(DreamcatcherDeckPageController)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_controller, args);
    }
}
