using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.Api;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // Live-server verification for deck-info-preset-apply unit 5.
    // Explicit because it requires a signed-in account with tournament history.
    public class DeckInfoPresetApplyLiveE2ETest
    {
        [UnityTest, Explicit("Requires a signed-in live account with at least one foreign deck.")]
        public IEnumerator ForeignHistoryDeck_AppliesToNewDirtyPreset_WithoutWritingDisk()
        {
            yield return SceneManager.LoadSceneAsync("OutgameScene");
            yield return WaitFor(() => UserSession.HasAccount, 15f,
                "실계정 로그인 상태가 필요하다.");
            Assert.IsTrue(UserSession.HasAccount, "실계정 로그인 상태가 필요하다.");

            var menu = UnityEngine.Object.FindFirstObjectByType<OutgameMenuController>();
            Assert.IsNotNull(menu);

            var profileSO = GetField<PlayerProfileSO>(menu, "profileSO");
            Assert.IsNotNull(profileSO);
            var originalProfile = profileSO.profile;
            var clone = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(originalProfile));
            profileSO.SetLoadedProfile(clone);

            // The page controllers are runtime-built on first page entry. Build both first so
            // their persistence delegates can be replaced before the live apply route runs.
            menu.OnOpenSquad();
            yield return null;
            var squad = FindIncludingInactive<SquadCharacterPageController>();
            menu.OnOpenDreamcatcher();
            yield return null;
            var deck = FindIncludingInactive<DreamcatcherDeckPageController>();
            Assert.IsNotNull(squad);
            Assert.IsNotNull(deck);
            Assert.AreSame(profileSO, GetField<PlayerProfileSO>(squad, "profileSO"));
            Assert.AreSame(profileSO, GetField<PlayerProfileSO>(deck, "profileSO"));

            var originalSquadSaver = squad.ProfileSaver;
            var originalDeckSaver = deck.ProfileSaver;
            squad.ProfileSaver = _ => { };
            deck.ProfileSaver = _ => { };

            try
            {
                menu.OnOpenHistory();
                yield return WaitFor(() => ActiveButtons("HistoryRow").Length > 0, 15f,
                    "히스토리 목록이 로드되지 않았다.");

                Button[] deckButtons = Array.Empty<Button>();
                foreach (var historyRow in ActiveButtons("HistoryRow"))
                {
                    historyRow.onClick.Invoke();
                    yield return WaitFor(() => ActiveButtons("DeckViewButton").Length > 0, 15f, null);
                    deckButtons = ActiveButtons("DeckViewButton");
                    if (deckButtons.Length > 0) break;
                }
                Assert.IsNotEmpty(deckButtons, "덱보기가 가능한 토너먼트 결과가 없다.");

                bool applied = false;
                PresetApply.Target appliedTarget = PresetApply.Target.Squad;
                int squadsBefore = clone.squads.Count;
                int decksBefore = clone.dreamcatcherDecks.Count;

                foreach (var button in deckButtons)
                {
                    button.onClick.Invoke();
                    yield return null;

                    var popup = FindIncludingInactive<DeckInfoPopup>();
                    if (popup == null || !popup.gameObject.activeInHierarchy) continue;
                    var applyButton = GetField<GameObject>(popup, "_presetButton");
                    if (applyButton == null || !applyButton.activeSelf)
                    {
                        popup.Hide(); // 내 덱
                        continue;
                    }

                    if (!popup.IsPresetButtonInteractable)
                    {
                        popup.SwitchTab(1);
                        appliedTarget = PresetApply.Target.Dreamcatcher;
                    }
                    if (!popup.IsPresetButtonInteractable)
                    {
                        popup.Hide(); // 두 탭 모두 비어 있음
                        continue;
                    }

                    popup.ClickPresetApply();
                    applied = true;
                    break;
                }

                Assert.IsTrue(applied, "적용 가능한 외부 참가자 덱을 찾지 못했다.");
                yield return null;
                Assert.IsFalse(PresetApply.HasPending, "대상 페이지가 예약을 소비해야 한다.");

                if (appliedTarget == PresetApply.Target.Squad)
                {
                    Assert.AreEqual(squadsBefore + 1, clone.squads.Count);
                    Assert.IsTrue(squad.gameObject.activeInHierarchy);
                    Assert.IsTrue(Invoke<bool>(squad, "IsDirty"), "스쿼드 작업본은 미저장 dirty 여야 한다.");
                    Assert.IsTrue(clone.squads.Last().IsEmpty(), "새 스쿼드 저장본은 비어 있어야 한다.");
                }
                else
                {
                    Assert.AreEqual(decksBefore + 1, clone.dreamcatcherDecks.Count);
                    Assert.IsTrue(deck.gameObject.activeInHierarchy);
                    Assert.IsTrue(Invoke<bool>(deck, "IsDirty"), "덱 작업본은 미저장 dirty 여야 한다.");
                    Assert.AreEqual(0, clone.dreamcatcherDecks.Last().Count(),
                        "새 덱 저장본은 비어 있어야 한다.");
                }
            }
            finally
            {
                squad.ProfileSaver = originalSquadSaver;
                deck.ProfileSaver = originalDeckSaver;
                profileSO.SetLoadedProfile(originalProfile);
                PresetApply.Clear();
            }
        }

        private static Button[] ActiveButtons(string name)
            => UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(b => b.name == name && b.gameObject.activeInHierarchy)
                .ToArray();

        private static T FindIncludingInactive<T>() where T : UnityEngine.Object
            => UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

        private static T GetField<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private static T Invoke<T>(object target, string name)
            => (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);

        private static IEnumerator WaitFor(Func<bool> predicate, float timeout, string failure)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
            if (!string.IsNullOrEmpty(failure)) Assert.IsTrue(predicate(), failure);
        }
    }
}
