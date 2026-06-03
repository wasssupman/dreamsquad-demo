using System;
using System.Collections.Generic;

namespace Wassup.Core
{
    // outgame-scene-and-flow Unit 0 — persisted player profile, serialized to
    // JSON by ProfileStore. Unit/card references use stable string ids
    // (DefenderUnitData.id), never asset GUIDs or list indices.
    [Serializable]
    public class PlayerProfile
    {
        public int schemaVersion = 1;
        public List<string> ownedUnitIds = new List<string>();

        // B (squad-loadout) fills these; C/D (dreamcatcher) fill the decks.
        public List<SquadSave> squads = new List<SquadSave>();
        public List<DeckSave> dreamcatcherDecks = new List<DeckSave>();

        // null/empty selectedSquadId = no squad chosen → BattleScene uses the
        // existing draft fallback. Draft removal is C's scope, not A.
        public string selectedSquadId;
        public string selectedDeckId;

        // squad-loadout Unit 0 — resolve the active squad, or null when unset.
        public SquadSave SelectedSquad()
        {
            if (string.IsNullOrEmpty(selectedSquadId) || squads == null) return null;
            for (int i = 0; i < squads.Count; i++)
                if (squads[i] != null && squads[i].id == selectedSquadId) return squads[i];
            return null;
        }

        // dreamcatcher-deck-builder Unit 0 — resolve the active deck, or null.
        public DeckSave SelectedDeck()
        {
            if (string.IsNullOrEmpty(selectedDeckId) || dreamcatcherDecks == null) return null;
            for (int i = 0; i < dreamcatcherDecks.Count; i++)
                if (dreamcatcherDecks[i] != null && dreamcatcherDecks[i].id == selectedDeckId) return dreamcatcherDecks[i];
            return null;
        }
    }

    // squad-loadout Unit 0 — a 7-slot squad. Slots hold DefenderUnitData.id;
    // empty slot = "" (kept non-null for stable JSON). Trait/class/condition
    // fields are out of scope (follow-up).
    [Serializable]
    public class SquadSave
    {
        public const int SlotCount = 7;

        public string id;
        public string name = "Squad 1";
        public List<string> unitIds = new List<string>();

        public bool IsEmpty()
        {
            if (unitIds == null) return true;
            for (int i = 0; i < unitIds.Count; i++)
                if (!string.IsNullOrEmpty(unitIds[i])) return false;
            return true;
        }

        public int FilledCount()
        {
            if (unitIds == null) return 0;
            int n = 0;
            for (int i = 0; i < unitIds.Count; i++)
                if (!string.IsNullOrEmpty(unitIds[i])) n++;
            return n;
        }

        // Pad/trim to exactly SlotCount with "" for empty slots.
        public void NormalizeSlots()
        {
            if (unitIds == null) unitIds = new List<string>();
            for (int i = 0; i < unitIds.Count; i++)
                if (unitIds[i] == null) unitIds[i] = "";
            while (unitIds.Count < SlotCount) unitIds.Add("");
            if (unitIds.Count > SlotCount) unitIds.RemoveRange(SlotCount, unitIds.Count - SlotCount);
        }
    }

    // dreamcatcher-deck-builder Unit 0 — a saved dreamcatcher deck. cardIds holds
    // DreamcatcherCard.id; deck-rule validity (exactly 10, unique<=2) is checked
    // by DeckRules on save, not enforced here.
    [Serializable]
    public class DeckSave
    {
        public string id;
        public string name = "Deck 1";
        public List<string> cardIds = new List<string>();

        public int Count() => cardIds != null ? cardIds.Count : 0;
    }
}
