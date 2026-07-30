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
            // units are not profile-owned — availability comes from the catalog directly.
            // squad-loadout Unit 0 — default profile now ships a free starter squad.
            Assert.AreEqual(1, profile.squads.Count);
            Assert.AreEqual("squad_1", profile.selectedSquadId);
            Assert.IsEmpty(profile.dreamcatcherDecks);
        }

        [Test]
        public void SaveThenLoad_RoundTripsAllFields()
        {
            var cat = MakeCatalog("scout", "ranger");
            var original = ProfileStore.LoadOrCreateAt(_path, cat);
            original.selectedSquadId = "alpha";
            original.selectedDeckId = "deck1";
            original.squads.Add(new SquadPreset { id = "alpha" });
            original.dreamcatcherDecks.Add(new DreamcatcherPreset { id = "deck1" });
            ProfileStore.SaveAt(_path, original);

            var loaded = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.AreEqual(original.schemaVersion, loaded.schemaVersion);
            Assert.AreEqual("alpha", loaded.selectedSquadId);
            Assert.AreEqual("deck1", loaded.selectedDeckId);
            // default "squad_1" + added "alpha"
            Assert.AreEqual(2, loaded.squads.Count);
            Assert.IsNotNull(loaded.CommittedSquad());
            Assert.AreEqual("alpha", loaded.CommittedSquad().id);
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
        }

        [Test]
        public void NewProfile_HasSeededStarterSquad()
        {
            // rev 2026-06-05 — a fresh profile now ships a PLAYABLE starter squad
            // (selected squad seeded from owned units) so builds enter squad mode
            // instead of the legacy draft fallback.
            var cat = MakeCatalog("scout", "ranger");

            var profile = ProfileStore.LoadOrCreateAt(_path, cat);

            Assert.AreEqual(1, profile.squads.Count, "free starter squad");
            var squad = profile.CommittedSquad();
            Assert.IsNotNull(squad, "selected squad resolves");
            Assert.AreEqual("squad_1", profile.selectedSquadId);
            Assert.AreEqual(SquadPreset.SlotCount, squad.unitIds.Count, "7 slots");
            Assert.IsFalse(squad.IsEmpty(), "starter squad is seeded, not empty");
            // owned has 2 → 2 slots filled, in owned order.
            Assert.AreEqual(2, squad.FilledCount());
            Assert.AreEqual("scout", squad.unitIds[0]);
            Assert.AreEqual("ranger", squad.unitIds[1]);
        }

        [Test]
        public void StarterSquad_FillsUpToSlotCount()
        {
            // owned > SlotCount → exactly SlotCount filled.
            var cat = MakeCatalog("a", "b", "c", "d", "e", "f", "g", "h", "i");
            var profile = ProfileStore.LoadOrCreateAt(_path, cat);
            var squad = profile.CommittedSquad();
            Assert.AreEqual(SquadPreset.SlotCount, squad.FilledCount());
        }

        [Test]
        public void Squad_SlotAssignment_RoundTrips()
        {
            var cat = MakeCatalog("scout", "ranger");
            var profile = ProfileStore.LoadOrCreateAt(_path, cat);
            // Clear the seeded starter so this test exercises explicit assignment
            // + empty-slot round-trip deterministically.
            var sel = profile.CommittedSquad();
            for (int i = 0; i < sel.unitIds.Count; i++) sel.unitIds[i] = "";
            sel.unitIds[0] = "scout";
            sel.unitIds[3] = "ranger";
            ProfileStore.SaveAt(_path, profile);

            var loaded = ProfileStore.LoadOrCreateAt(_path, cat);
            var squad = loaded.CommittedSquad();

            Assert.AreEqual(SquadPreset.SlotCount, squad.unitIds.Count);
            Assert.AreEqual("scout", squad.unitIds[0]);
            Assert.AreEqual("ranger", squad.unitIds[3]);
            Assert.AreEqual("", squad.unitIds[1]);
            Assert.AreEqual(2, squad.FilledCount());
            Assert.IsFalse(squad.IsEmpty());
        }

        // dreamstone-loadout Unit 1 — stoneIds round-trip, normalization, legacy JSON
        // compat, and the SetStoneSlot helper.

        [Test]
        public void Squad_StoneSlots_RoundTrip_IncludingDuplicates()
        {
            var cat = MakeCatalog("scout", "ranger");
            var profile = ProfileStore.LoadOrCreateAt(_path, cat);
            var sel = profile.CommittedSquad();
            sel.stoneIds[0] = "atk_unique";
            sel.stoneIds[1] = "atk_unique";
            sel.stoneIds[3] = "hp_rare";
            ProfileStore.SaveAt(_path, profile);

            var loaded = ProfileStore.LoadOrCreateAt(_path, cat);
            var squad = loaded.CommittedSquad();

            Assert.AreEqual(SquadPreset.StoneSlotCount, squad.stoneIds.Count);
            Assert.AreEqual("atk_unique", squad.stoneIds[0]);
            Assert.AreEqual("atk_unique", squad.stoneIds[1], "duplicate stone ids across slots are allowed");
            Assert.AreEqual("", squad.stoneIds[2]);
            Assert.AreEqual("hp_rare", squad.stoneIds[3]);
        }

        [Test]
        public void NormalizeSlots_StoneIds_PadsTrimsAndReplacesNull()
        {
            var squad = new SquadPreset { stoneIds = null };
            squad.NormalizeSlots();
            CollectionAssert.AreEqual(new[] { "", "", "", "" }, squad.stoneIds, "null list pads to 4 empty slots");

            squad.stoneIds = new List<string> { "a", null };
            squad.NormalizeSlots();
            CollectionAssert.AreEqual(new[] { "a", "", "", "" }, squad.stoneIds, "short list pads, null entries become \"\"");

            squad.stoneIds = new List<string> { "a", "b", "c", "d", "e" };
            squad.NormalizeSlots();
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, squad.stoneIds, "long list trims to 4");
        }

        [Test]
        public void LoadOrCreate_OnLegacyJsonWithoutStoneIds_NormalizesToEmptySlots()
        {
            // Pre-dreamstone-loadout profile JSON: SquadPreset had no stoneIds field.
            var legacyJson = "{\"schemaVersion\":1,\"ownedUnitIds\":[\"scout\"],\"squads\":[{\"id\":\"squad_1\",\"name\":\"Squad 1\",\"unitIds\":[\"scout\",\"\",\"\",\"\",\"\",\"\",\"\"]}],\"dreamcatcherDecks\":[],\"selectedSquadId\":\"squad_1\",\"selectedDeckId\":\"\"}";
            File.WriteAllText(_path, legacyJson);
            var cat = MakeCatalog("scout");

            var profile = ProfileStore.LoadOrCreateAt(_path, cat);
            var squad = profile.CommittedSquad();

            Assert.IsNotNull(squad);
            Assert.AreEqual(SquadPreset.StoneSlotCount, squad.stoneIds.Count);
            CollectionAssert.AreEqual(new[] { "", "", "", "" }, squad.stoneIds);
        }

        [Test]
        public void SetStoneSlot_AssignsClearsDuplicatesAndRejectsOutOfRange()
        {
            var squad = new SquadPreset();

            Assert.IsTrue(squad.SetStoneSlot(0, "atk_unique"));
            Assert.AreEqual("atk_unique", squad.stoneIds[0]);

            Assert.IsTrue(squad.SetStoneSlot(1, "atk_unique"), "duplicate id in another slot is allowed");
            Assert.AreEqual("atk_unique", squad.stoneIds[1]);

            Assert.IsTrue(squad.SetStoneSlot(0, ""), "empty id clears the slot");
            Assert.AreEqual("", squad.stoneIds[0]);

            Assert.IsFalse(squad.SetStoneSlot(-1, "x"), "negative index rejected");
            Assert.IsFalse(squad.SetStoneSlot(SquadPreset.StoneSlotCount, "x"), "index at/beyond StoneSlotCount rejected");
        }
    }
}
