using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-deck-builder Unit 3 — the in-game deck resolves from the
    // profile's selected saved deck (via catalog), and falls back to the
    // serialized deck when none is selected.
    // dreamcatcher-bridge-partial-cleanup unit 1 — 원 대상이던 구 3중1
    // DreamcatcherController 삭제로, 동일 검증을 살아있는 각성 손패 경로
    // (DreamcatcherHandController.ResolveAttachDeck)로 이관.
    public class DreamcatcherDeckCarryInTest
    {
        private PlayerProfileSO _profSO;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            // clear test deck so it doesn't leak into other tests
            if (_profSO != null && _profSO.profile != null)
            {
                _profSO.profile.dreamcatcherDecks?.Clear();
                _profSO.profile.selectedDeckId = null;
            }
        }

        [UnityTest]
        public IEnumerator SelectedSavedDeck_DrivesDraws()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var ctrl = Object.FindObjectOfType<DreamcatcherHandController>();
            Assert.IsNotNull(ctrl, "scene DreamcatcherHandController present");
            _profSO = (PlayerProfileSO)GetField(ctrl, "profileSO");
            var cardCatalog = (DreamcatcherCardCatalog)GetField(ctrl, "cardCatalog");
            Assert.IsNotNull(_profSO, "profileSO wired");
            Assert.IsNotNull(cardCatalog, "cardCatalog wired");

            // build a valid saved deck of 10x ranger_atk_10 and select it.
            var profile = _profSO.profile;
            profile.dreamcatcherDecks.Clear();
            var deck = new DeckSave { id = "deck_test", name = "T", cardIds = Enumerable.Repeat("ranger_atk_10", 10).ToList() };
            profile.dreamcatcherDecks.Add(deck);
            profile.selectedDeckId = "deck_test";

            // resolve via the controller's private method
            var resolved = (List<DreamcatcherCard>)Invoke(ctrl, "ResolveAttachDeck");
            Assert.AreEqual(10, resolved.Count, "resolved 10 from saved deck");
            Assert.IsTrue(resolved.All(c => c.id == "ranger_atk_10"), "all from saved deck");

            // no selection → fallback to serialized deck (the default 10)
            profile.selectedDeckId = null;
            var fallback = (List<DreamcatcherCard>)Invoke(ctrl, "ResolveAttachDeck");
            Assert.AreEqual(10, fallback.Count, "fallback default deck has 10");
            Assert.IsTrue(fallback.Any(c => c.id != "ranger_atk_10"), "fallback is the default mix, not the saved deck");
        }

        private static object GetField(object o, string name) =>
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static object Invoke(object o, string name) =>
            o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);
    }
}
