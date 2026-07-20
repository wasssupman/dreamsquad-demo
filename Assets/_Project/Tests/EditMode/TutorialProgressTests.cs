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
        public void NewProfile_BothTutorialsPending()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(profile));
        }

        [Test]
        public void Completion_IsIndependentAndIdempotent()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.CompleteCore(profile));
            Assert.IsFalse(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteCore(profile));

            Assert.IsTrue(TutorialProgress.CompleteAwakeningHint(profile));
            Assert.IsFalse(TutorialProgress.IsAwakeningHintPending(profile));
        }

        [Test]
        public void CurrentOrNewerVersion_DoesNotRun()
        {
            var profile = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion + 1,
                awakeningHintVersion = TutorialProgress.AwakeningHintVersion + 1,
            };
            Assert.IsFalse(TutorialProgress.IsCorePending(profile));
            Assert.IsFalse(TutorialProgress.IsAwakeningHintPending(profile));
        }

        [Test]
        public void Holder_MustHaveBeenLoadedThisSession()
        {
            Assert.IsNotNull(_holder.profile, "asset default profile is intentionally non-null");
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunAwakeningHint(_holder));

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
            Assert.IsFalse(TutorialProgress.CompleteCore(null));
            Assert.IsFalse(TutorialProgress.CompleteAwakeningHint(null));
        }

        [Test]
        public void JsonRoundTrip_PreservesVersions()
        {
            var source = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
                awakeningHintVersion = TutorialProgress.AwakeningHintVersion,
            };
            var loaded = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(source));
            Assert.AreEqual(TutorialProgress.CoreVersion, loaded.firstBattleTutorialVersion);
            Assert.AreEqual(TutorialProgress.AwakeningHintVersion, loaded.awakeningHintVersion);
        }

        [Test]
        public void LegacyJson_MissingFields_DefaultsToPending()
        {
            var loaded = JsonUtility.FromJson<PlayerProfile>("{\"schemaVersion\":1}");
            Assert.AreEqual(0, loaded.firstBattleTutorialVersion);
            Assert.AreEqual(0, loaded.awakeningHintVersion);
            Assert.IsTrue(TutorialProgress.IsCorePending(loaded));
            Assert.IsTrue(TutorialProgress.IsAwakeningHintPending(loaded));
        }

        [Test]
        public void ResetAll_ClearsOnlyTutorialVersionsAndIsIdempotent()
        {
            var profile = new PlayerProfile
            {
                selectedSquadId = "keep_squad",
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
                awakeningHintVersion = TutorialProgress.AwakeningHintVersion,
            };

            Assert.IsTrue(TutorialProgress.ResetAll(profile));
            Assert.AreEqual(0, profile.firstBattleTutorialVersion);
            Assert.AreEqual(0, profile.awakeningHintVersion);
            Assert.AreEqual("keep_squad", profile.selectedSquadId);
            Assert.IsFalse(TutorialProgress.ResetAll(profile));
            Assert.IsFalse(TutorialProgress.ResetAll(null));
        }

        [Test]
        public void ResetAllInJson_PreservesKnownAndUnknownAccountData()
        {
            const string source = @"{
                'schemaVersion': 99,
                'firstBattleTutorialVersion': 7,
                'awakeningHintVersion': 3,
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

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed);
            Assert.IsTrue(JToken.DeepEquals(expected, JObject.Parse(result)),
                "Tutorial reset must change only the two tutorial version tokens.");
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
                'selectedSquadId': 'keep_squad',
                'futureAccountData': { 'currency': 12345 }
            }";
            var loaded = new PlayerProfile
            {
                firstBattleTutorialVersion = 1,
                awakeningHintVersion = 1,
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
                Assert.AreEqual("keep_squad", stored.Value<string>(nameof(PlayerProfile.selectedSquadId)));
                Assert.AreEqual(12345, stored["futureAccountData"]?["currency"]?.Value<int>());
                Assert.AreEqual(0, loaded.firstBattleTutorialVersion);
                Assert.AreEqual(0, loaded.awakeningHintVersion);
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
