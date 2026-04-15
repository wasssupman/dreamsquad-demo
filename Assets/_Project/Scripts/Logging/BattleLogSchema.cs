using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Logging
{
    [Serializable]
    public class BattleLogEntry
    {
        public string session_id;
        public string phase = "phase0";
        public string timestamp_start;
        public string timestamp_end;
        public string attack_deck_id;
        public List<PlacementLog> placements = new();
        public BattleResult result = new();
    }

    [Serializable]
    public class PlacementLog
    {
        public string unit_type;
        public Vector2Int tile;
        public float time;
    }

    [Serializable]
    public class BattleResult
    {
        public string outcome = "unknown";
        public float duration_sec;
        public int enemies_reached_goal;
    }
}
