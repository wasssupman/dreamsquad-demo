using System.IO;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // outgame-scene-and-flow Unit 1 — disk persistence for PlayerProfile.
    // Static utility (not a Manager singleton, per constraint 5). JSON lives at
    // Application.persistentDataPath/profile.json. The path-injection overloads
    // are public so EditMode tests (separate assembly) can drive a temp file.
    public static class ProfileStore
    {
        public const int CurrentSchemaVersion = 1;
        const string FileName = "profile.json";

        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        // game-start-loadout-gate unit 1 — the deck args are optional so the many
        // squad-only test call sites keep compiling; production (OutgameMenuController)
        // passes them. Omitting them means "do not seed a deck", not "seed an empty one".
        public static PlayerProfile LoadOrCreate(DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null)
            => LoadOrCreateAt(Path, catalog, defaultDeck, cards);

        public static void Save(PlayerProfile profile) => SaveAt(Path, profile);

        public static PlayerProfile LoadOrCreateAt(string path, DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var profile = JsonUtility.FromJson<PlayerProfile>(json);
                    if (profile != null)
                    {
                        // Migration hook: when CurrentSchemaVersion advances, transform
                        // older profiles here before returning. Only v1 exists today.
                        EnsureNonNull(profile, catalog, defaultDeck, cards);
                        return profile;
                    }
                    Debug.LogWarning("[ProfileStore] Parsed profile was null; recreating default.");
                    TryBackup(path);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ProfileStore] Failed to parse '{path}': {e.Message}. Backing up and recreating default.");
                    TryBackup(path);
                }
            }

            var created = CreateDefault(catalog, defaultDeck, cards);
            SaveAt(path, created);
            return created;
        }

        public static void SaveAt(string path, PlayerProfile profile)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
        }

        // first-session-tutorial replay support — patch only the two tutorial
        // tokens in the original JSON. Re-serializing PlayerProfile here could
        // discard account fields written by a newer client or an external system.
        // The loaded instance is synchronized only after the disk replacement
        // succeeds, so a failed write cannot leave memory and disk disagreeing.
        public static bool ResetTutorialProgressAt(string path, PlayerProfile loadedProfile,
            out string backupPath)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Profile file was not found.", path);

            string patchedJson = TutorialProgress.ResetAllInJson(
                File.ReadAllText(path), out bool diskChanged);
            backupPath = null;

            if (diskChanged)
            {
                backupPath = path + $".tutorial-reset.{System.DateTime.Now:yyyyMMdd-HHmmssfff}.bak";
                ReplaceWithBackup(path, patchedJson, backupPath);
            }

            bool memoryChanged = TutorialProgress.ResetAll(loadedProfile);
            return diskChanged || memoryChanged;
        }

        static void ReplaceWithBackup(string path, string contents, string backupPath)
        {
            string tempPath = path + ".tutorial-reset.tmp";
            try
            {
                File.WriteAllText(tempPath, contents);
                try
                {
                    File.Replace(tempPath, path, backupPath);
                }
                catch (System.PlatformNotSupportedException)
                {
                    File.Copy(path, backupPath, false);
                    File.Copy(tempPath, path, true);
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // outgame-login-gate unit 6 — public so the dev "DEFAULT LOADOUT" button can
        // rebuild the same starter profile a fresh install gets, instead of defining
        // a second notion of "default" next to this one.
        public static PlayerProfile CreateDefault(DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null)
        {
            var p = new PlayerProfile { schemaVersion = CurrentSchemaVersion };
            if (p.dreamcatcherDecks == null) p.dreamcatcherDecks = new System.Collections.Generic.List<DeckSave>();
            EnsureDefaultSquad(p, catalog);
            EnsureDefaultDeck(p, defaultDeck, cards);
            return p;
        }

        // JsonUtility may leave collection fields null when absent/partial in the
        // source JSON. Keep callers from null-checking every list.
        static void EnsureNonNull(PlayerProfile p, DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck, DreamcatcherCardCatalog cards)
        {
            if (p.squads == null) p.squads = new System.Collections.Generic.List<SquadSave>();
            if (p.dreamcatcherDecks == null) p.dreamcatcherDecks = new System.Collections.Generic.List<DeckSave>();
            EnsureDefaultSquad(p, catalog);
            EnsureDefaultDeck(p, defaultDeck, cards);
        }

        // squad-loadout Unit 0 — guarantee at least one squad (free starter, per
        // design) with normalized 7-slot arrays, and a valid selection.
        // rev 2026-06-05: seed a PLAYABLE starter squad. A fresh install (or a device
        // that ran an earlier build and saved an empty squad) would otherwise resolve
        // to an empty squad, and GameManager.Start falls back to the legacy draft.
        // Filling the selected squad from owned units when it is empty makes the build
        // enter squad mode out of the box.
        static void EnsureDefaultSquad(PlayerProfile p, DefenderCatalog catalog)
        {
            if (p.squads == null) p.squads = new System.Collections.Generic.List<SquadSave>();
            if (p.squads.Count == 0)
            {
                p.squads.Add(new SquadSave { id = "squad_1", name = "Squad 1" });
            }
            foreach (var s in p.squads) if (s != null) s.NormalizeSlots();
            if (string.IsNullOrEmpty(p.selectedSquadId) || p.SelectedSquad() == null)
                p.selectedSquadId = p.squads[0].id;

            // Seed only when the selected squad is empty — never overwrite a squad the
            // player has filled. Units are not profile-owned (all units are always
            // available), so the starter squad is seeded straight from the catalog.
            var selected = p.SelectedSquad();
            if (selected != null && selected.IsEmpty() && catalog != null)
            {
                int i = 0;
                foreach (var id in catalog.AllIds())
                {
                    if (i >= SquadSave.SlotCount) break;
                    selected.unitIds[i++] = id;
                }
            }
        }

        // game-start-loadout-gate unit 1 — the deck counterpart of EnsureDefaultSquad.
        // Until now only the dev-only DEFAULT LOADOUT button knew what a default deck
        // was, so a fresh install got a starter squad but no deck at all. That was
        // harmless while an invalid deck merely degraded to zero attached cards; once
        // the start gate demands a valid deck it would lock every new player out.
        //
        // Seeding is not a reset: a deck the player chose is never overwritten. If a
        // rule change (e.g. deckSize 8 -> 10) invalidates their saved deck, the gate
        // tells them and they fix it in the builder — we do not silently rebuild it.
        static void EnsureDefaultDeck(PlayerProfile p, DreamcatcherDeck defaultDeck, DreamcatcherCardCatalog cards)
        {
            if (defaultDeck == null || cards == null) return;   // caller opted out of seeding
            if (p.SelectedDeck() != null) return;               // player's choice stands

            if (p.dreamcatcherDecks.Count > 0)
            {
                p.selectedDeckId = p.dreamcatcherDecks[0].id;   // decks exist, selection was broken
                return;
            }

            var seeded = BuildDefaultDeck(defaultDeck, DeckRules.EffectiveDeckSize(cards));
            if (seeded.cardIds.Count == 0) return;              // nothing authored — an empty deck helps no one
            p.dreamcatcherDecks.Add(seeded);
            p.selectedDeckId = seeded.id;
        }

        // outgame-login-gate unit 6 (moved here by game-start-loadout-gate unit 1) —
        // take the authored deck in order, up to the current rule size, so a deckSize
        // change is followed automatically instead of re-authoring the asset. Lives
        // next to CreateDefault so the fresh-install path and the dev DEFAULT LOADOUT
        // button share one definition of "default deck".
        public static DeckSave BuildDefaultDeck(DreamcatcherDeck source, int deckSize)
        {
            var save = new DeckSave { id = "deck_1", name = "Deck 1", cardIds = new System.Collections.Generic.List<string>() };
            if (source == null || source.cards == null) return save;
            for (int i = 0; i < source.cards.Length && save.cardIds.Count < deckSize; i++)
            {
                var card = source.cards[i];
                if (card != null && !string.IsNullOrEmpty(card.id)) save.cardIds.Add(card.id);
            }
            return save;
        }

        static void TryBackup(string path)
        {
            try { File.Copy(path, path + ".bak", true); }
            catch (System.Exception e) { Debug.LogWarning($"[ProfileStore] Backup failed: {e.Message}"); }
        }
    }
}
