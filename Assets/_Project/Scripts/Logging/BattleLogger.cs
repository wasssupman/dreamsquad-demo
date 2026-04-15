using System;
using System.IO;
using UnityEngine;

namespace Wassup.Logging
{
    public class BattleLogger : MonoBehaviour
    {
        private BattleLogEntry currentEntry;
        private DateTime startedAt;
        private string filePath;

        public void StartSession()
        {
            startedAt = DateTime.UtcNow;
            currentEntry = new BattleLogEntry
            {
                session_id = Guid.NewGuid().ToString("N"),
                timestamp_start = startedAt.ToString("o"),
            };
            var dir = Path.Combine(Application.persistentDataPath, "phase0");
            Directory.CreateDirectory(dir);
            var fileName = $"session-{startedAt:yyyyMMdd-HHmmss}-{currentEntry.session_id[..8]}.json";
            filePath = Path.Combine(dir, fileName);
            Debug.Log($"[BattleLogger] Session started. Log will be written to: {filePath}");
        }

        public void EndSession(string outcome = "unknown", int enemiesReachedGoal = 0)
        {
            if (currentEntry == null)
            {
                Debug.LogWarning("[BattleLogger] EndSession called without StartSession.");
                return;
            }
            var endedAt = DateTime.UtcNow;
            currentEntry.timestamp_end = endedAt.ToString("o");
            currentEntry.result.outcome = outcome;
            currentEntry.result.duration_sec = (float)(endedAt - startedAt).TotalSeconds;
            currentEntry.result.enemies_reached_goal = enemiesReachedGoal;
            var json = JsonUtility.ToJson(currentEntry, prettyPrint: true);
            File.WriteAllText(filePath, json);
            Debug.Log($"[BattleLogger] Session ended. Log written: {filePath}");
            currentEntry = null;
        }

        public void RecordPlacement(string unitType, Vector2Int tile, float time)
        {
            if (currentEntry == null) return;
            currentEntry.placements.Add(new PlacementLog { unit_type = unitType, tile = tile, time = time });
        }
    }
}
