using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class TutorialProgressTests
    {
        private PlayerProfileSO _holder;

        [SetUp]
        public void SetUp() => _holder = ScriptableObject.CreateInstance<PlayerProfileSO>();

        [TearDown]
        public void TearDown()
        {
            if (_holder != null) Object.DestroyImmediate(_holder);
        }

        [Test]
        public void NewProfile_AllTutorialsPending()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyIntroPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(profile));
        }

        [Test]
        public void LobbyCompletion_IsIndependentAndIdempotent()
        {
            var profile = new PlayerProfile();

            Assert.IsTrue(TutorialProgress.CompleteLobbyIntro(profile));
            Assert.IsFalse(TutorialProgress.IsLobbyIntroPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteLobbyIntro(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));

            Assert.IsTrue(TutorialProgress.CompleteLobbyLoadoutHint(profile));
            Assert.IsFalse(TutorialProgress.IsLobbyLoadoutHintPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteLobbyLoadoutHint(profile));

            Assert.IsFalse(TutorialProgress.CompleteLobbyIntro(null));
            Assert.IsFalse(TutorialProgress.CompleteLobbyLoadoutHint(null));
        }

        // Chapter B must never run alongside chapter A: it requires the in-game core
        // tutorial to be complete, which chapter A always precedes.
        [Test]
        public void LobbyLoadoutHint_RunsOnlyAfterCoreComplete()
        {
            var profile = new PlayerProfile();
            _holder.SetLoadedProfile(profile);

            Assert.IsTrue(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            // Chapter A done, but the first battle has not run yet.
            TutorialProgress.CompleteLobbyIntro(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            // Core complete → chapter B fires exactly here.
            TutorialProgress.CompleteCore(profile);
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            TutorialProgress.CompleteLobbyLoadoutHint(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));
        }

        [Test]
        public void LobbyChapters_RequireLoadedSessionAndNonNullProfile()
        {
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyIntro(_holder),
                "asset default profile is not a loaded session");
            TutorialProgress.CompleteCore(_holder.profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            _holder.SetLoadedProfile(null);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            _holder.SetLoadedProfile(new PlayerProfile());
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyIntro(_holder));
        }

        [Test]
        public void Completion_IsIndependentAndIdempotent()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.CompleteCore(profile));
            Assert.IsFalse(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteCore(profile));

            Assert.IsTrue(TutorialProgress.CompleteAwakeningHint(profile));
            Assert.IsFalse(TutorialProgress.IsAwakeningHintPending(profile));

            Assert.IsTrue(TutorialProgress.CompleteGiftTutorial(profile));
            Assert.IsFalse(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteGiftTutorial(profile));
        }

        [Test]
        public void GiftCompletion_DoesNotTouchOtherVersions()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.CompleteGiftTutorial(profile));
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(profile));
        }

        [Test]
        public void CurrentOrNewerVersion_DoesNotRun()
        {
            var profile = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion + 1,
                awakeningHintVersion = TutorialProgress.AwakeningHintVersion + 1,
                giftTutorialVersion = TutorialProgress.GiftTutorialVersion + 1,
            };
            Assert.IsFalse(TutorialProgress.IsCorePending(profile));
            Assert.IsFalse(TutorialProgress.IsAwakeningHintPending(profile));
            Assert.IsFalse(TutorialProgress.IsGiftTutorialPending(profile));
        }

        [Test]
        public void GiftTutorial_RunsOnlyAfterCoreComplete_NeverAlongsideCore()
        {
            var profile = new PlayerProfile();
            _holder.SetLoadedProfile(profile);

            // First run: core pending → the gift walkthrough must not fire.
            Assert.IsTrue(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));

            // Second run: core complete, gift pending → walkthrough fires exactly here.
            TutorialProgress.CompleteCore(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsTrue(TutorialProgress.ShouldRunGiftTutorial(_holder));

            // Third run: gift complete → normal presentation.
            TutorialProgress.CompleteGiftTutorial(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));
        }

        [Test]
        public void Holder_MustHaveBeenLoadedThisSession()
        {
            Assert.IsNotNull(_holder.profile, "asset default profile is intentionally non-null");
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunAwakeningHint(_holder));
            // Even with core complete on the asset default, not-loaded blocks gift too.
            TutorialProgress.CompleteCore(_holder.profile);
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));

            _holder.SetLoadedProfile(new PlayerProfile());
            Assert.IsTrue(_holder.IsLoadedThisSession);
            Assert.IsTrue(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsTrue(TutorialProgress.ShouldRunAwakeningHint(_holder));
        }

        [Test]
        public void NullProfile_NeverRunsOrCompletes()
        {
            _holder.SetLoadedProfile(null);
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunAwakeningHint(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));
            Assert.IsFalse(TutorialProgress.CompleteCore(null));
            Assert.IsFalse(TutorialProgress.CompleteAwakeningHint(null));
            Assert.IsFalse(TutorialProgress.CompleteGiftTutorial(null));
        }

        [Test]
        public void JsonRoundTrip_PreservesVersions()
        {
            var source = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
                awakeningHintVersion = TutorialProgress.AwakeningHintVersion,
                giftTutorialVersion = TutorialProgress.GiftTutorialVersion,
            };
            var loaded = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(source));
            Assert.AreEqual(TutorialProgress.CoreVersion, loaded.firstBattleTutorialVersion);
            Assert.AreEqual(TutorialProgress.AwakeningHintVersion, loaded.awakeningHintVersion);
            Assert.AreEqual(TutorialProgress.GiftTutorialVersion, loaded.giftTutorialVersion);
        }

        [Test]
        public void LegacyJson_MissingFields_DefaultsToPending()
        {
            var loaded = JsonUtility.FromJson<PlayerProfile>("{\"schemaVersion\":1}");
            Assert.AreEqual(0, loaded.firstBattleTutorialVersion);
            Assert.AreEqual(0, loaded.awakeningHintVersion);
            Assert.AreEqual(0, loaded.giftTutorialVersion);
            Assert.AreEqual(0, loaded.lobbyIntroVersion);
            Assert.AreEqual(0, loaded.lobbyLoadoutHintVersion);
            Assert.IsTrue(TutorialProgress.IsCorePending(loaded));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(loaded));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(loaded));
            Assert.IsTrue(TutorialProgress.IsLobbyIntroPending(loaded));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(loaded));
        }

        [Test]
        public void ResetAll_ClearsOnlyTutorialVersionsAndIsIdempotent()
        {
            var profile = new PlayerProfile
            {
                selectedSquadId = "keep_squad",
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
                awakeningHintVersion = TutorialProgress.AwakeningHintVersion,
                giftTutorialVersion = TutorialProgress.GiftTutorialVersion,
                lobbyIntroVersion = TutorialProgress.LobbyIntroVersion,
                lobbyLoadoutHintVersion = TutorialProgress.LobbyLoadoutHintVersion,
            };

            Assert.IsTrue(TutorialProgress.ResetAll(profile));
            Assert.AreEqual(0, profile.firstBattleTutorialVersion);
            Assert.AreEqual(0, profile.awakeningHintVersion);
            Assert.AreEqual(0, profile.giftTutorialVersion);
            Assert.AreEqual(0, profile.lobbyIntroVersion);
            Assert.AreEqual(0, profile.lobbyLoadoutHintVersion);
            Assert.AreEqual("keep_squad", profile.selectedSquadId);
            Assert.IsFalse(TutorialProgress.ResetAll(profile));
            Assert.IsFalse(TutorialProgress.ResetAll(null));

            // A gift-only stamp must still count as a change.
            profile.giftTutorialVersion = TutorialProgress.GiftTutorialVersion;
            Assert.IsTrue(TutorialProgress.ResetAll(profile));

            // ...and so must a lobby-only stamp.
            profile.lobbyLoadoutHintVersion = TutorialProgress.LobbyLoadoutHintVersion;
            Assert.IsTrue(TutorialProgress.ResetAll(profile));
        }

        [Test]
        public void ResetAllInJson_PreservesKnownAndUnknownAccountData()
        {
            const string source = @"{
                'schemaVersion': 99,
                'firstBattleTutorialVersion': 7,
                'awakeningHintVersion': 3,
                'giftTutorialVersion': 5,
                'selectedSquadId': 'keep_squad',
                'futureAccountData': {
                    'currency': 12345,
                    'entitlements': ['founder', 'season_9']
                },
                'squads': [{ 'id': 'keep_squad', 'futureRank': 42 }]
            }";
            var expected = JObject.Parse(source);
            expected[nameof(PlayerProfile.firstBattleTutorialVersion)] = 0;
            expected[nameof(PlayerProfile.awakeningHintVersion)] = 0;
            expected[nameof(PlayerProfile.giftTutorialVersion)] = 0;
            expected[nameof(PlayerProfile.lobbyIntroVersion)] = 0;
            expected[nameof(PlayerProfile.lobbyLoadoutHintVersion)] = 0;

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed);
            Assert.IsTrue(JToken.DeepEquals(expected, JObject.Parse(result)),
                "Tutorial reset must change only the tutorial version tokens.");
        }

        // outgame-tutorial unit 0 — ProfileStore.ResetTutorialProgressAt gates the
        // backup and the file replacement on `changed`. A lobby token left out of that
        // expression would be written to the JObject and then silently dropped whenever
        // it is the only non-zero token, so RESET TUTORIAL would log "already reset"
        // while the disk still blocks the lobby chapters.
        [Test]
        public void ResetAllInJson_ReportsChanged_WhenOnlyLobbyTokensAreSet()
        {
            const string source = @"{
                'firstBattleTutorialVersion': 0,
                'awakeningHintVersion': 0,
                'giftTutorialVersion': 0,
                'lobbyIntroVersion': 1,
                'lobbyLoadoutHintVersion': 1
            }";

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed, "lobby-only progress must still count as a change");
            var stored = JObject.Parse(result);
            Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyIntroVersion)));
            Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyLoadoutHintVersion)));
        }

        [Test]
        public void StoredReset_PatchesOnlyTutorialDataAndSynchronizesLoadedProfile()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"wassup-tutorial-reset-{System.Guid.NewGuid():N}.json");
            string backupPath = null;
            const string source = @"{
                'firstBattleTutorialVersion': 1,
                'awakeningHintVersion': 1,
                'giftTutorialVersion': 1,
                'lobbyIntroVersion': 1,
                'lobbyLoadoutHintVersion': 1,
                'selectedSquadId': 'keep_squad',
                'futureAccountData': { 'currency': 12345 }
            }";
            var loaded = new PlayerProfile
            {
                firstBattleTutorialVersion = 1,
                awakeningHintVersion = 1,
                giftTutorialVersion = 1,
                lobbyIntroVersion = 1,
                lobbyLoadoutHintVersion = 1,
                selectedSquadId = "keep_squad",
            };

            try
            {
                File.WriteAllText(path, source);

                bool changed = ProfileStore.ResetTutorialProgressAt(path, loaded, out backupPath);

                Assert.IsTrue(changed);
                var stored = JObject.Parse(File.ReadAllText(path));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.firstBattleTutorialVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.awakeningHintVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.giftTutorialVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyIntroVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyLoadoutHintVersion)));
                Assert.AreEqual("keep_squad", stored.Value<string>(nameof(PlayerProfile.selectedSquadId)));
                Assert.AreEqual(12345, stored["futureAccountData"]?["currency"]?.Value<int>());
                Assert.AreEqual(0, loaded.firstBattleTutorialVersion);
                Assert.AreEqual(0, loaded.awakeningHintVersion);
                Assert.AreEqual(0, loaded.giftTutorialVersion);
                Assert.AreEqual(0, loaded.lobbyIntroVersion);
                Assert.AreEqual(0, loaded.lobbyLoadoutHintVersion);
                Assert.AreEqual("keep_squad", loaded.selectedSquadId);
                Assert.IsTrue(File.Exists(backupPath));
                Assert.AreEqual(source, File.ReadAllText(backupPath));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath)) File.Delete(backupPath);
            }
        }
    }
}
