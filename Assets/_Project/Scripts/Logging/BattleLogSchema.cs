using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Logging
{
    [Serializable]
    public class BattleLogEntry
    {
        public string session_id;
        public string phase = "phase6";
        public string timestamp_start;
        public string timestamp_end;
        public string attack_deck_id;
        public DraftRecord draft = new();
        public SkillRecord skill = new();
        public SynergyRecord synergy = new();
        public List<OnPlaceUsageLog> on_place_usages = new();
        public List<PlacementLog> placements = new();
        public BattleResult result = new();
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
    }

    [Serializable]
    public class SkillUsageLog
    {
        public string skill_id;
        public float time;
        public Vector2Int target_tile;
        public int affected_count;
        public int cost_spent;
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
