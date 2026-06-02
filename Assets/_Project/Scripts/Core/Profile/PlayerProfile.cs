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
    }

    // Stub. Field expansion belongs to B (squad-loadout). Do not over-design here.
    [Serializable]
    public class SquadSave
    {
        public string id;
    }

    // Stub. Field expansion belongs to C/D (dreamcatcher).
    [Serializable]
    public class DeckSave
    {
        public string id;
    }
}
