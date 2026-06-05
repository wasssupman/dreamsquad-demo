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

        public static PlayerProfile LoadOrCreate(DefenderCatalog catalog) => LoadOrCreateAt(Path, catalog);

        public static void Save(PlayerProfile profile) => SaveAt(Path, profile);

        public static PlayerProfile LoadOrCreateAt(string path, DefenderCatalog catalog)
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
                        EnsureNonNull(profile);
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

            var created = CreateDefault(catalog);
            SaveAt(path, created);
            return created;
        }

        public static void SaveAt(string path, PlayerProfile profile)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
        }

        static PlayerProfile CreateDefault(DefenderCatalog catalog)
        {
            var p = new PlayerProfile { schemaVersion = CurrentSchemaVersion };
            if (catalog != null)
            {
                foreach (var id in catalog.AllIds()) p.ownedUnitIds.Add(id);
            }
            EnsureDefaultSquad(p);
            return p;
        }

        // JsonUtility may leave collection fields null when absent/partial in the
        // source JSON. Keep callers from null-checking every list.
        static void EnsureNonNull(PlayerProfile p)
        {
            if (p.ownedUnitIds == null) p.ownedUnitIds = new System.Collections.Generic.List<string>();
            if (p.squads == null) p.squads = new System.Collections.Generic.List<SquadSave>();
            if (p.dreamcatcherDecks == null) p.dreamcatcherDecks = new System.Collections.Generic.List<DeckSave>();
            EnsureDefaultSquad(p);
        }

        // squad-loadout Unit 0 — guarantee at least one squad (free starter, per
        // design) with normalized 7-slot arrays, and a valid selection.
        // rev 2026-06-05: seed a PLAYABLE starter squad. A fresh install (or a device
        // that ran an earlier build and saved an empty squad) would otherwise resolve
        // to an empty squad, and GameManager.Start falls back to the legacy draft.
        // Filling the selected squad from owned units when it is empty makes the build
        // enter squad mode out of the box.
        static void EnsureDefaultSquad(PlayerProfile p)
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
            // player has filled. Fills slots in owned order, up to SlotCount.
            var selected = p.SelectedSquad();
            if (selected != null && selected.IsEmpty() && p.ownedUnitIds != null && p.ownedUnitIds.Count > 0)
            {
                int n = Mathf.Min(SquadSave.SlotCount, p.ownedUnitIds.Count);
                for (int i = 0; i < n; i++) selected.unitIds[i] = p.ownedUnitIds[i];
            }
        }

        static void TryBackup(string path)
        {
            try { File.Copy(path, path + ".bak", true); }
            catch (System.Exception e) { Debug.LogWarning($"[ProfileStore] Backup failed: {e.Message}"); }
        }
    }
}
