using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Logging
{
    // Session-scoped logger for a single battle. Lifecycle:
    //   StartSession() → gameplay code calls SetAttackDeckId / RecordPlacement / SetResult → EndSession() writes JSON.
    // Outcome defaults to "unknown" so a session that never saw a definitive end still produces a readable file.
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
            currentEntry.result.outcome = "unknown";
            var dir = ResolveLogDirectory();
            Directory.CreateDirectory(dir);
            var fileName = $"session-{startedAt:yyyyMMdd-HHmmss}-{currentEntry.session_id[..8]}.json";
            filePath = Path.Combine(dir, fileName);
            Debug.Log($"[BattleLogger] Session started. Log will be written to: {filePath}");
        }

        public void SetAttackDeckId(string deckId)
        {
            if (currentEntry == null) return;
            currentEntry.attack_deck_id = deckId ?? string.Empty;
        }

        public void LogMap(int seed, int generatorVersion, int2 gridSize, int spawnCount, string pathShape = "")
        {
            if (currentEntry == null) return;
            currentEntry.map.seed = seed;
            currentEntry.map.generatorVersion = generatorVersion;
            currentEntry.map.gridWidth = gridSize.x;
            currentEntry.map.gridHeight = gridSize.y;
            currentEntry.map.spawnCount = spawnCount;
            currentEntry.map.pathShape = pathShape ?? string.Empty;
        }

        public void SetWavePattern(GeneratedWavePlan plan)
        {
            if (currentEntry == null || plan.waves == null) return;

            currentEntry.wavePattern.seed = plan.seed;
            currentEntry.wavePattern.generatorVersion = plan.generatorVersion;
            currentEntry.wavePattern.waveIntervalSec = plan.waveIntervalSec;
            currentEntry.wavePattern.waveCount = plan.waves.Count;
            currentEntry.wavePattern.waves = new List<WaveRecord>(plan.waves.Count);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                var wave = plan.waves[i];
                currentEntry.wavePattern.waves.Add(new WaveRecord
                {
                    waveIndex = wave.waveIndex,
                    triggerTimeSec = wave.triggerTimeSec,
                    unitA = wave.unitA != null ? wave.unitA.displayName : string.Empty,
                    countA = wave.countA,
                    unitB = wave.unitB != null ? wave.unitB.displayName : string.Empty,
                    countB = wave.countB,
                    totalCount = wave.totalCount,
                });
            }
        }

        public void RecordWaveEvent(string eventType, int waveIndex, float elapsedSec, bool forced)
        {
            if (currentEntry == null) return;
            currentEntry.wavePattern.events.Add(new WaveEventRecord
            {
                eventType = eventType ?? string.Empty,
                waveIndex = waveIndex,
                elapsedSec = elapsedSec,
                forced = forced,
            });
        }

        // Caller passes a fully-populated DraftRecord (pool names, picked names in
        // order, and the RNG seed). Copying the lists protects us from later
        // mutation by the caller.
        public void SetDraft(DraftRecord draft)
        {
            if (currentEntry == null || draft == null) return;
            currentEntry.draft.pool = new System.Collections.Generic.List<string>(draft.pool);
            currentEntry.draft.picked = new System.Collections.Generic.List<string>(draft.picked);
            currentEntry.draft.seed = draft.seed;
        }

        public void SetSkillLoadout(System.Collections.Generic.IEnumerable<string> ids)
        {
            if (currentEntry == null || ids == null) return;
            currentEntry.skill.loadout = new System.Collections.Generic.List<string>(ids);
        }

        // Phase 7: record the full skill pool the session rolled from, plus the
        // seed used by SkillLoadoutController, so analyzers can replay the roll.
        public void SetSkillPool(System.Collections.Generic.IEnumerable<string> poolIds, int seed)
        {
            if (currentEntry == null) return;
            if (poolIds != null)
                currentEntry.skill.pool = new System.Collections.Generic.List<string>(poolIds);
            currentEntry.skill.seed = seed;
        }

        public void RecordSkillUsage(SkillUsageLog usage)
        {
            if (currentEntry == null || usage == null) return;
            currentEntry.skill.usages.Add(usage);
        }

        public void RecordHazard(HazardLog hazard)
        {
            if (currentEntry == null || hazard == null) return;
            if (currentEntry.hazards.Count >= 2000) return;
            currentEntry.hazards.Add(hazard);
        }

        public void RecordOnPlace(OnPlaceUsageLog usage)
        {
            if (currentEntry == null || usage == null) return;
            currentEntry.on_place_usages.Add(usage);
        }

        public void RecordAttackOutput(string sourceUnit, string kind, float magnitude, string detail, float duration, Vector2Int sourceTile, Vector2Int targetTile)
        {
            if (currentEntry == null) return;
            currentEntry.attack_outputs.Add(new AttackOutputUsageLog
            {
                source_unit = sourceUnit ?? "<unknown>",
                kind = kind,
                magnitude = magnitude,
                detail = detail ?? "",
                duration = duration,
                source_tile = sourceTile,
                target_tile = targetTile,
                time = (float)(DateTime.UtcNow - startedAt).TotalSeconds,
            });
        }

        public void SetSynergyStats(int activations, int peakCount)
        {
            if (currentEntry == null) return;
            currentEntry.synergy.activations = activations;
            currentEntry.synergy.peakCount = peakCount;
        }

        public void RecordPlacement(string unitType, Vector2Int tile, float time, int costSpent)
        {
            if (currentEntry == null) return;
            currentEntry.placements.Add(new PlacementLog
            {
                unit_type = unitType,
                tile = tile,
                time = time,
                cost_spent = costSpent
            });
        }

        // Set the final battle outcome. Caller should invoke this before EndSession
        // (typically on VICTORY / DEFEAT trigger inside BattleBridge).
        public void SetResult(string outcome, int enemiesReachedGoal)
        {
            if (currentEntry == null) return;
            currentEntry.result.outcome = outcome;
            currentEntry.result.enemies_reached_goal = enemiesReachedGoal;
        }

        // Resolves the log directory. In the Unity Editor we write to the project root (<projectRoot>/GameLogs)
        // so JSON files are easy to find and inspect alongside source. At runtime outside the Editor
        // (standalone / Android builds), we fall back to Application.persistentDataPath which is the
        // only path guaranteed to be writable on every platform.
        private static string ResolveLogDirectory()
        {
#if UNITY_EDITOR
            // Application.dataPath is "<projectRoot>/Assets"; go one level up.
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "GameLogs");
#else
            return Path.Combine(Application.persistentDataPath, "GameLogs");
#endif
        }

        public void EndSession()
        {
            if (currentEntry == null)
            {
                Debug.LogWarning("[BattleLogger] EndSession called without StartSession.");
                return;
            }
            var endedAt = DateTime.UtcNow;
            currentEntry.timestamp_end = endedAt.ToString("o");
            currentEntry.result.duration_sec = (float)(endedAt - startedAt).TotalSeconds;
            var json = JsonUtility.ToJson(currentEntry, prettyPrint: true);
            File.WriteAllText(filePath, json);
            Debug.Log($"[BattleLogger] Session ended. Log written: {filePath}");
            currentEntry = null;
        }
    }
}
