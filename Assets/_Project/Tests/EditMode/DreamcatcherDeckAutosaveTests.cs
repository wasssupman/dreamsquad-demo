using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    public class DreamcatcherDeckAutosaveTests
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
            _host = new GameObject("DreamcatcherDeckAutosaveTestHost");
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

        [Test]
        public void AddCard_Persists_And_Creates_Selected_Deck()
        {
            var profile = LoadedProfile();
            SetWorking(Ids(9));
            int saves = 0;
            _controller.ProfileSaver = value => { saves++; Assert.AreSame(profile, value); };

            Invoke("AddCard", "card_9");

            Assert.AreEqual(1, saves);
            Assert.AreEqual("deck_1", profile.selectedDeckId);
            var deck = profile.SelectedDeck();
            Assert.IsNotNull(deck);
            CollectionAssert.AreEqual(Ids(10), deck.cardIds);
        }

        [Test]
        public void RemoveOccurrence_Persists_Invalid_Nine_Card_Deck()
        {
            var profile = LoadedProfile(Ids(10));
            Invoke("LoadWorking");
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("RemoveOccurrence", "card_9");

            Assert.AreEqual(1, saves);
            CollectionAssert.AreEqual(Ids(9), profile.SelectedDeck().cardIds);
            Assert.IsFalse(DeckRules.Validate(profile.SelectedDeck().cardIds, _catalog, out _));
        }

        [Test]
        public void Edit_Does_Not_Save_Before_Profile_Is_Loaded()
        {
            _profileSO.profile = new PlayerProfile();
            SetWorking(Ids(9));
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("AddCard", "card_9");

            Assert.AreEqual(10, Working().Count);
            Assert.AreEqual(0, saves);
            Assert.IsEmpty(_profileSO.profile.dreamcatcherDecks);
        }

        [Test]
        public void LoadWorking_Does_Not_Save()
        {
            LoadedProfile(Ids(10));
            int saves = 0;
            _controller.ProfileSaver = _ => saves++;

            Invoke("LoadWorking");

            Assert.AreEqual(0, saves);
            CollectionAssert.AreEqual(Ids(10), Working());
        }

        private PlayerProfile LoadedProfile(List<string> ids = null)
        {
            var profile = new PlayerProfile();
            if (ids != null)
            {
                profile.dreamcatcherDecks.Add(new DeckSave { id = "deck_1", cardIds = new List<string>(ids) });
                profile.selectedDeckId = "deck_1";
            }
            _profileSO.SetLoadedProfile(profile);
            return profile;
        }

        private static List<string> Ids(int count)
        {
            var result = new List<string>();
            for (int i = 0; i < count; i++) result.Add($"card_{i}");
            return result;
        }

        private void SetWorking(List<string> ids) => Working().AddRange(ids);

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
