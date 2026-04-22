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
        public MapRecord map = new();
        public WavePatternRecord wavePattern = new();
        public DraftRecord draft = new();
        public SkillRecord skill = new();
        public SynergyRecord synergy = new();
        public List<OnPlaceUsageLog> on_place_usages = new();
        public List<PlacementLog> placements = new();
        public BattleResult result = new();
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
        public string unitA;
        public int countA;
        public string unitB;
        public int countB;
        public int totalCount;
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

    // Phase 1 draft audit trail: the full 10-unit pool the player saw, the 7 they
    // locked in (in pick order), and the seed used to sample the pool so identical
    // play sessions can be reconstructed.
    [Serializable]
    public class DraftRecord
    {
        public List<string> pool = new();
        public List<string> picked = new();
        public int seed;
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
}
