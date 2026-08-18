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
        // dreamstone-default-loadout — stones 도 같은 옵트인 규칙: 넘기지 않으면 "스톤을
        // 시드하지 않는다"는 뜻이다(빈 세트를 시드하는 게 아니라).
        public static PlayerProfile LoadOrCreate(DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null,
            DreamstoneData[] defaultStones = null)
            => LoadOrCreateAt(Path, catalog, defaultDeck, cards, defaultStones);

        public static void Save(PlayerProfile profile) => SaveAt(Path, profile);

        public static PlayerProfile LoadOrCreateAt(string path, DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null,
            DreamstoneData[] defaultStones = null)
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
                        EnsureNonNull(profile, catalog, defaultDeck, cards, defaultStones);
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

            var created = CreateDefault(catalog, defaultDeck, cards, defaultStones);
            SaveAt(path, created);
            return created;
        }

        public static void SaveAt(string path, PlayerProfile profile)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
        }


        // outgame-login-gate unit 6 — public so the dev "DEFAULT LOADOUT" button can
        // rebuild the same starter profile a fresh install gets, instead of defining
        // a second notion of "default" next to this one.
        public static PlayerProfile CreateDefault(DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck = null, DreamcatcherCardCatalog cards = null,
            DreamstoneData[] defaultStones = null)
        {
            var p = new PlayerProfile { schemaVersion = CurrentSchemaVersion };
            if (p.dreamcatcherDecks == null) p.dreamcatcherDecks = new System.Collections.Generic.List<DreamcatcherPreset>();
            EnsureDefaultSquad(p, catalog);
            EnsureDefaultDeck(p, defaultDeck, cards);
            EnsureDefaultStones(p, defaultStones);
            // page-local-presets unit 5 — 프리셋 리스트 불변식(상한·칸수·확정 포인터)의
            // "로드" 호출 지점. 세 EnsureDefault* 안에 각각 넣지 않는다 — 3중 호출이 되고
            // 불변식의 소유자가 흐려진다.
            p.NormalizePresets();
            return p;
        }

        // JsonUtility may leave collection fields null when absent/partial in the
        // source JSON. Keep callers from null-checking every list.
        static void EnsureNonNull(PlayerProfile p, DefenderCatalog catalog,
            DreamcatcherDeck defaultDeck, DreamcatcherCardCatalog cards,
            DreamstoneData[] defaultStones = null)
        {
            if (p.squads == null) p.squads = new System.Collections.Generic.List<SquadPreset>();
            if (p.dreamcatcherDecks == null) p.dreamcatcherDecks = new System.Collections.Generic.List<DreamcatcherPreset>();
            EnsureDefaultSquad(p, catalog);
            EnsureDefaultDeck(p, defaultDeck, cards);
            EnsureDefaultStones(p, defaultStones);
            p.NormalizePresets();   // page-local-presets unit 5 — 위 CreateDefault 와 같은 이유
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
            if (p.squads == null) p.squads = new System.Collections.Generic.List<SquadPreset>();
            if (p.squads.Count == 0)
            {
                p.squads.Add(new SquadPreset { id = "squad_1", name = "스쿼드 1" });
            }
            foreach (var s in p.squads) if (s != null) s.NormalizeSlots();
            // page-local-presets unit 5 — 이 포인터 교정은 아래 시딩의 **선행 조건**이다
            // (시딩 대상이 CommittedSquad()). NormalizePresets 가 같은 일을 하지만 그건
            // EnsureNonNull 말미에 돌므로 여기서 빼면 시딩이 조용히 건너뛰어진다.
            if (string.IsNullOrEmpty(p.selectedSquadId) || p.CommittedSquad() == null)
                p.selectedSquadId = p.squads[0].id;

            // Seed only when the selected squad is empty — never overwrite a squad the
            // player has filled. Units are not profile-owned (all units are always
            // available), so the starter squad is seeded straight from the catalog.
            var selected = p.CommittedSquad();
            if (selected != null && selected.IsEmpty() && catalog != null)
            {
                int i = 0;
                // 어느 유닛을 줄지는 카탈로그가 **저작으로** 정한다(defaultSquadUnits).
                // 비어 있으면 예전 동작 — 배열 앞에서부터. 그 폴백은 카탈로그 순서에
                // 시작 편성이 딸려가는 암묵 규칙이라 저작이 있으면 그쪽이 이긴다.
                var authored = catalog.defaultSquadUnits;
                if (authored != null && authored.Length > 0)
                {
                    for (int a = 0; a < authored.Length && i < SquadPreset.SlotCount; a++)
                    {
                        var u = authored[a];
                        if (u == null || string.IsNullOrEmpty(u.id)) continue; // 미배정 칸은 건너뛴다
                        selected.unitIds[i++] = u.id;
                    }
                }
                if (i == 0)
                {
                    foreach (var id in catalog.AllIds())
                    {
                        if (i >= SquadPreset.SlotCount) break;
                        selected.unitIds[i++] = id;
                    }
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
            if (p.CommittedDeck() != null) return;               // player's choice stands

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
        public static DreamcatcherPreset BuildDefaultDeck(DreamcatcherDeck source, int deckSize)
        {
            var save = new DreamcatcherPreset { id = "deck_1", name = "덱 1", cardIds = new System.Collections.Generic.List<string>() };
            if (source == null || source.cards == null) return save;
            for (int i = 0; i < source.cards.Length && save.cardIds.Count < deckSize; i++)
            {
                var card = source.cards[i];
                if (card != null && card.visible != 0 && !string.IsNullOrEmpty(card.id))
                    save.cardIds.Add(card.id);
            }
            return save;
        }

        // dreamstone-default-loadout — EnsureDefaultSquad 의 스톤 짝. 어느 스톤을 줄지는
        // 코드가 정하지 않는다: authored 배열(인스펙터 배정)을 그대로 슬롯에 넣는다.
        // 유닛·덱과 같은 정책 — 비어 있을 때만 시드하고 플레이어가 채운 슬롯은 덮어쓰지 않는다.
        // "4칸 전부 빈칸"일 때만 도는 이유: 한 칸이라도 끼웠다면 그건 플레이어의 편성이고,
        // 의도적으로 뺀 상태를 로드할 때마다 되살리면 해제가 불가능해진다.
        static void EnsureDefaultStones(PlayerProfile p, DreamstoneData[] defaultStones)
        {
            if (defaultStones == null || defaultStones.Length == 0) return;  // 호출자가 시딩을 옵트아웃
            var squad = p.CommittedSquad();
            if (squad == null) return;
            squad.NormalizeSlots();

            for (int i = 0; i < squad.stoneIds.Count; i++)
                if (!string.IsNullOrEmpty(squad.stoneIds[i])) return;   // 하나라도 장착돼 있으면 손대지 않는다

            int slot = 0;
            for (int i = 0; i < defaultStones.Length && slot < SquadPreset.StoneSlotCount; i++)
            {
                var s = defaultStones[i];
                if (s == null || string.IsNullOrEmpty(s.id)) continue;   // 미배정 칸은 건너뛴다
                squad.stoneIds[slot++] = s.id;
            }
        }

        static void TryBackup(string path)
        {
            try { File.Copy(path, path + ".bak", true); }
            catch (System.Exception e) { Debug.LogWarning($"[ProfileStore] Backup failed: {e.Message}"); }
        }
    }
}
