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

            // build a valid saved deck of 10x ranger_atk and select it.
            var profile = _profSO.profile;
            profile.dreamcatcherDecks.Clear();
            var deck = new DreamcatcherPreset { id = "deck_test", name = "T", cardIds = Enumerable.Repeat("ranger_atk", 10).ToList() };
            profile.dreamcatcherDecks.Add(deck);
            profile.selectedDeckId = "deck_test";

            // resolve via the controller's private method
            var resolved = (List<DreamcatcherCard>)Invoke(ctrl, "ResolveAttachDeck");
            Assert.AreEqual(10, resolved.Count, "resolved 10 from saved deck");
            Assert.IsTrue(resolved.All(c => c.id == "ranger_atk"), "all from saved deck");

            // no selection → 폴백 덱은 **의도적으로 제거**됐다 (사용자 결정 2026-07-15,
            // DreamcatcherHandController.ResolveAttachDeck) — 무선택이면 부착덱이 비어야 한다.
            profile.selectedDeckId = null;
            var fallback = (List<DreamcatcherCard>)Invoke(ctrl, "ResolveAttachDeck");
            Assert.AreEqual(0, fallback.Count, "폴백 덱 제거 — 무선택은 0장이어야 한다");
        }

        private static object GetField(object o, string name) =>
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static object Invoke(object o, string name) =>
            o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);
    }
}
