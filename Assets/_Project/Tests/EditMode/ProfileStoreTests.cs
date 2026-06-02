using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // outgame-scene-and-flow Unit 1 — ProfileStore JSON round-trip + recovery.
    public class ProfileStoreTests
    {
        private string _path;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(),
                "wassup_profiletest_" + System.Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_path + ".bak")) File.Delete(_path + ".bak");
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DefenderCatalog MakeCatalog(params string[] ids)
        {
            var cat = ScriptableObject.CreateInstance<DefenderCatalog>();
            _created.Add(cat);
            cat.units = new DefenderUnitData[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var u = ScriptableObject.CreateInstance<DefenderUnitData>();
                u.id = ids[i];
                _created.Add(u);
                cat.units[i] = u;
            }
            return cat;
        }

        [Test]
        public void LoadOrCreate_OnMissingFile_CreatesDefaultAndWritesFile()
        {
            var cat = MakeCatalog("scout", "ranger", "guardian");

            var profile = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.IsTrue(File.Exists(_path), "default profile should be persisted");
            Assert.AreEqual(ProfileStore.CurrentSchemaVersion, profile.schemaVersion);
            CollectionAssert.AreEquivalent(new[] { "scout", "ranger", "guardian" }, profile.ownedUnitIds);
            Assert.IsEmpty(profile.squads);
            Assert.IsEmpty(profile.dreamcatcherDecks);
            Assert.IsNull(profile.selectedSquadId);
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            var cat = MakeCatalog("scout", "ranger");
            var original = ProfileStore.LoadOrCreateAt(_path, cat);
            original.selectedSquadId = "alpha";
            original.selectedDeckId = "deck1";
            original.squads.Add(new SquadSave { id = "alpha" });
            original.dreamcatcherDecks.Add(new DeckSave { id = "deck1" });
            ProfileStore.SaveAt(_path, original);

            var loaded = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.AreEqual(original.schemaVersion, loaded.schemaVersion);
            CollectionAssert.AreEqual(original.ownedUnitIds, loaded.ownedUnitIds);
            Assert.AreEqual("alpha", loaded.selectedSquadId);
            Assert.AreEqual("deck1", loaded.selectedDeckId);
            Assert.AreEqual(1, loaded.squads.Count);
            Assert.AreEqual("alpha", loaded.squads[0].id);
            Assert.AreEqual(1, loaded.dreamcatcherDecks.Count);
            Assert.AreEqual("deck1", loaded.dreamcatcherDecks[0].id);
        }

        [Test]
        public void LoadOrCreate_OnCorruptFile_RecreatesDefaultAndBacksUp()
        {
            File.WriteAllText(_path, "{ this is not valid json ]");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[ProfileStore\]"));
            var cat = MakeCatalog("scout");

            var profile = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.IsNotNull(profile);
            Assert.IsTrue(File.Exists(_path + ".bak"), "corrupt file should be backed up");
            CollectionAssert.AreEquivalent(new[] { "scout" }, profile.ownedUnitIds);
        }
    }
}
