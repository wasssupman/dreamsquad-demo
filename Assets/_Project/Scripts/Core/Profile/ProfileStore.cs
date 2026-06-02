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
            return p;
        }

        // JsonUtility may leave collection fields null when absent/partial in the
        // source JSON. Keep callers from null-checking every list.
        static void EnsureNonNull(PlayerProfile p)
        {
            if (p.ownedUnitIds == null) p.ownedUnitIds = new System.Collections.Generic.List<string>();
            if (p.squads == null) p.squads = new System.Collections.Generic.List<SquadSave>();
            if (p.dreamcatcherDecks == null) p.dreamcatcherDecks = new System.Collections.Generic.List<DeckSave>();
        }

        static void TryBackup(string path)
        {
            try { File.Copy(path, path + ".bak", true); }
            catch (System.Exception e) { Debug.LogWarning($"[ProfileStore] Backup failed: {e.Message}"); }
        }
    }
}
