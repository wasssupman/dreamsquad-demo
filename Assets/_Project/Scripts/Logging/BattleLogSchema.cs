using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Logging
{
    [Serializable]
    public class BattleLogEntry
    {
        public string session_id;
        public string phase = "phase8";
        public string timestamp_start;
        public string timestamp_end;
        public string attack_deck_id;
        public MatchSeedRecord match = new();
        public EntryRecord entry = new();
        public MapRecord map = new();
        public WavePatternRecord wavePattern = new();
        public DraftRecord draft = new();
        public SquadRecord squad = new();
        public List<DreamstoneRecord> dreamstones = new();
        public DreamcatcherRecord dreamcatcher = new();
        public SkillRecord skill = new();
        public List<HazardLog> hazards = new();
        public List<BlockingHazardLog> blocking_hazards = new();
        public SynergyRecord synergy = new();
        public List<OnPlaceUsageLog> on_place_usages = new();
        public List<PlacementLog> placements = new();
        public List<AttackOutputUsageLog> attack_outputs = new();
        public List<KillLog> kills = new();
        public List<ScoreEventLog> score_events = new();
        public BattleResult result = new();
    }

    [Serializable]
    public class MatchSeedRecord
    {
        public int matchSeed;
        public int mapSeed;
        public int waveSeed;
        public int visualSeed;
        public bool fixedSeed;
    }

    [Serializable]
    public class EntryRecord
    {
        public string mode;
        public bool testMode;
        public int restartIndex;
    }

    [Serializable]
    public class MapRecord
    {
        public int seed;
        public int generatorVersion;
        public int gridWidth;
        public int gridHeight;
        public int spawnCount;
        public string pathShape;
    }

    [Serializable]
    public class WavePatternRecord
    {
        public int seed;
        public int generatorVersion;
        public float waveIntervalSec;
        public int waveCount;
        public List<WaveRecord> waves = new();
        public List<WaveEventRecord> events = new();
    }

    [Serializable]
    public class WaveRecord
    {
        public int waveIndex;
        public float triggerTimeSec;
        // wave-authoring-test-mode unit 1 — 2타입 고정(unitA/B/countA/B) → N-entry.
        public List<WaveEntryRecord> entries = new();
        public int totalCount;
    }

    [Serializable]
    public class WaveEntryRecord
    {
        public string unit;
        public int count;
    }

    [Serializable]
    public class WaveEventRecord
    {
        public string eventType;
        public int waveIndex;
        public float elapsedSec;
        public bool forced;
    }

    [Serializable]
    public class PlacementLog
    {
        public string unit_type;
        public Vector2Int tile;
        public float time;
        public int cost_spent;
    }

    [Serializable]
    public class BattleResult
    {
        public string outcome = "unknown";
        public float duration_sec;
        public int enemies_reached_goal;
        public int score;
    }

    // Phase 1 draft audit trail: the full 10-unit pool the player saw, the 7 units
    // the player kept (pool order, non-discarded — model inverted in spec
    // draft-ux-upgrade), and the seed used to sample the pool so identical play
    // sessions can be reconstructed.
    [Serializable]
    public class DraftRecord
    {
        public List<string> pool = new();
        public List<string> picked = new();
        public List<string> discarded = new();
        public int seed;
    }

    [Serializable]
    public class SquadRecord
    {
        public string id;
        public string name;
        public List<string> unitIds = new();
        public List<string> unitNames = new();
    }

    [Serializable]
    public class DreamstoneRecord
    {
        public string id;
        public string name;
        public string grade;
        public string kind;
        public float percent;
        public int slotIndex;
    }

    [Serializable]
    public class DreamcatcherRecord
    {
        public string deckId;
        public string deckName;
        public List<string> deckCardIds = new();
        public List<DreamcatcherOfferLog> offers = new();
        public List<DreamcatcherPickLog> picks = new();
    }

    [Serializable]
    public class DreamcatcherOfferLog
    {
        public string trigger;
        public int waveNumber;
        public float time;
        public List<string> cardIds = new();
    }

    [Serializable]
    public class DreamcatcherPickLog
    {
        public string cardId;
        public string cardName;
        public string axis;
        public float time;
        public List<DreamcatcherEffectRecord> effects = new();
    }

    [Serializable]
    public class DreamcatcherEffectRecord
    {
        public string kind;
        public float percent;
    }

    [Serializable]
    public class SkillRecord
    {
        public List<string> loadout = new();
        public List<SkillUsageLog> usages = new();

        // Phase 7: full 6-skill pool the loadout was rolled from, and the seed
        // the roll used. `loadout` above holds the 2 picked ids, so `picked` is
        // intentionally not duplicated — loadout IS the picked set.
        public List<string> pool = new();
        public int seed;
    }

    [Serializable]
    public class SkillUsageLog
    {
        public string skill_id;
        public float time;
        public Vector2Int target_tile;
        public int affected_count;
        public int cost_spent;

        // Phase 7: Portal is the only 2-tile skill (entry + exit). All other
        // skills leave this at (-1, -1). Analyzers can treat non-(-1,-1) as
        // "this was a dual-tile cast."
        public Vector2Int target_tile_b = new(-1, -1);
    }

    [Serializable]
    public class HazardLog
    {
        public string event_type;
        public string hazard_id;
        public string kind;
        public Vector2Int tile;
        public float time;
        public float scalar;
        public float amount;
        public int target_index;
    }

    [Serializable]
    public class BlockingHazardLog
    {
        public string event_type;
        public string hazard_id;
        public Vector2Int tile;
        public float time;
        public int width;
        public int height;
        public int hp;
        public string reason;
    }

    // Phase 4: tracks adjacency synergy activity over the session. `activations`
    // counts how many distinct defender entities have transitioned from "no
    // synergy" to "has synergy" at least once; `peakCount` is the largest number
    // of defenders concurrently holding SynergyBuff.
    [Serializable]
    public class SynergyRecord
    {
        public int activations;
        public int peakCount;
    }

    [Serializable]
    public class OnPlaceUsageLog
    {
        public string unit_type;
        public string effect;
        public Vector2Int tile;
        public float time;
        public int affected_count;
    }

    [Serializable]
    public class AttackOutputUsageLog
    {
        public string source_unit;    // attacker unit_type (or "<unknown>")
        public string kind;           // "Damage" / "Heal" / "ApplyStat" / "ApplyStack"
        public float magnitude;
        public string detail;         // ApplyStat: stat name, ApplyStack: kind name — else ""
        public float duration;        // ApplyStat / ApplyStack only, else 0
        public Vector2Int source_tile;
        public Vector2Int target_tile;
        public float time;
    }

    [Serializable]
    public class KillLog
    {
        public string unit_type;
        public Vector2Int tile;
        public float time;
    }

    [Serializable]
    public class ScoreEventLog
    {
        public string event_type;
        public int delta;
        public int score;
        public float time;
    }
}
