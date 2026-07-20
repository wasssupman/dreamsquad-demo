using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Core;

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
        private int score;
        private int restartIndex;

        public void StartSession()
        {
            startedAt = DateTime.UtcNow;
            currentEntry = new BattleLogEntry
            {
                session_id = Guid.NewGuid().ToString("N"),
                timestamp_start = startedAt.ToString("o"),
            };
            currentEntry.result.outcome = "unknown";
            score = 0;
            restartIndex = 0;
            var dir = ResolveLogDirectory();
            Directory.CreateDirectory(dir);
            var fileName = $"session-{startedAt:yyyyMMdd-HHmmss}-{currentEntry.session_id[..8]}.json";
            filePath = Path.Combine(dir, fileName);
            Debug.Log($"[BattleLogger] Session started. Log will be written to: {filePath}");
        }

        public void StartReplacementSession(string entryMode, bool incrementRestartIndex)
        {
            var previous = currentEntry;
            int nextRestartIndex = restartIndex;
            if (incrementRestartIndex)
                nextRestartIndex = Math.Max(restartIndex, previous != null ? previous.entry.restartIndex : 0) + 1;

            EndSession();
            StartSession();
            restartIndex = nextRestartIndex;
            CopyCarryInContext(previous);
            SetEntryMode(entryMode, previous != null && previous.entry.testMode);
            currentEntry.entry.restartIndex = restartIndex;
        }

        public void SetAttackDeckId(string deckId)
        {
            if (currentEntry == null) return;
            currentEntry.attack_deck_id = deckId ?? string.Empty;
        }

        public void SetMatchSeeds(int matchSeed, bool fixedSeed)
        {
            if (currentEntry == null) return;
            currentEntry.match.matchSeed = matchSeed;
            int seed = matchSeed != 0 ? matchSeed : 1;
            currentEntry.match.mapSeed = MatchSeed.DeriveMapSeed(seed);
            currentEntry.match.waveSeed = MatchSeed.DeriveWaveSeed(seed);
            currentEntry.match.visualSeed = MatchSeed.DeriveVisualSeed(seed);
            currentEntry.match.fixedSeed = fixedSeed;
        }

        // 실제 맵 빌드에 사용된 시드로 덮어쓴다 (BattleBridge.fixedMapSeed 오버라이드,
        // 수동 MapDocument(-1) 포함). SetMatchSeeds 의 파생값은 빌드 전 추정치다.
        public void SetActualMapSeed(int seed)
        {
            if (currentEntry == null) return;
            currentEntry.match.mapSeed = seed;
        }

        public void SetEntryMode(string mode, bool testMode = false)
        {
            if (currentEntry == null) return;
            currentEntry.entry.mode = mode ?? string.Empty;
            currentEntry.entry.testMode = testMode;
        }

        private void CopyCarryInContext(BattleLogEntry previous)
        {
            if (currentEntry == null || previous == null) return;

            currentEntry.attack_deck_id = previous.attack_deck_id;
            currentEntry.match = CopyMatch(previous.match);
            currentEntry.draft = CopyDraft(previous.draft);
            currentEntry.squad = CopySquad(previous.squad);
            currentEntry.dreamstones = previous.dreamstones != null
                ? new List<DreamstoneRecord>(previous.dreamstones)
                : new List<DreamstoneRecord>();
            currentEntry.dreamcatcher.deckId = previous.dreamcatcher.deckId;
            currentEntry.dreamcatcher.deckName = previous.dreamcatcher.deckName;
            currentEntry.dreamcatcher.deckCardIds = previous.dreamcatcher.deckCardIds != null
                ? new List<string>(previous.dreamcatcher.deckCardIds)
                : new List<string>();
            currentEntry.skill.loadout = previous.skill.loadout != null ? new List<string>(previous.skill.loadout) : new List<string>();
            currentEntry.skill.pool = previous.skill.pool != null ? new List<string>(previous.skill.pool) : new List<string>();
            currentEntry.skill.seed = previous.skill.seed;
        }

        private static MatchSeedRecord CopyMatch(MatchSeedRecord source)
        {
            if (source == null) return new MatchSeedRecord();
            return new MatchSeedRecord
            {
                matchSeed = source.matchSeed,
                mapSeed = source.mapSeed,
                waveSeed = source.waveSeed,
                visualSeed = source.visualSeed,
                fixedSeed = source.fixedSeed,
            };
        }

        private static DraftRecord CopyDraft(DraftRecord source)
        {
            if (source == null) return new DraftRecord();
            return new DraftRecord
            {
                pool = source.pool != null ? new List<string>(source.pool) : new List<string>(),
                picked = source.picked != null ? new List<string>(source.picked) : new List<string>(),
                discarded = source.discarded != null ? new List<string>(source.discarded) : new List<string>(),
                seed = source.seed,
            };
        }

        private static SquadRecord CopySquad(SquadRecord source)
        {
            if (source == null) return new SquadRecord();
            return new SquadRecord
            {
                id = source.id,
                name = source.name,
                unitIds = source.unitIds != null ? new List<string>(source.unitIds) : new List<string>(),
                unitNames = source.unitNames != null ? new List<string>(source.unitNames) : new List<string>(),
            };
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
                var record = new WaveRecord
                {
                    waveIndex = wave.waveIndex,
                    triggerTimeSec = wave.triggerTimeSec,
                    totalCount = wave.totalCount,
                };
                if (wave.groups != null)
                    for (int g = 0; g < wave.groups.Count; g++)
                        record.entries.Add(new WaveEntryRecord
                        {
                            unit = wave.groups[g].unit != null ? wave.groups[g].unit.displayName : string.Empty,
                            count = wave.groups[g].count,
                        });
                currentEntry.wavePattern.waves.Add(record);
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
            currentEntry.draft.discarded = new System.Collections.Generic.List<string>(draft.discarded);
            currentEntry.draft.seed = draft.seed;
        }

        public void SetSquad(string id, string name, IEnumerable<string> unitIds, IEnumerable<string> unitNames)
        {
            if (currentEntry == null) return;
            currentEntry.squad.id = id ?? string.Empty;
            currentEntry.squad.name = name ?? string.Empty;
            currentEntry.squad.unitIds = unitIds != null ? new List<string>(unitIds) : new List<string>();
            currentEntry.squad.unitNames = unitNames != null ? new List<string>(unitNames) : new List<string>();
        }

        public void SetDreamstones(IEnumerable<DreamstoneRecord> stones)
        {
            if (currentEntry == null) return;
            currentEntry.dreamstones = stones != null ? new List<DreamstoneRecord>(stones) : new List<DreamstoneRecord>();
        }

        public void SetDreamcatcherDeck(string deckId, string deckName, IEnumerable<string> cardIds)
        {
            if (currentEntry == null) return;
            currentEntry.dreamcatcher.deckId = deckId ?? string.Empty;
            currentEntry.dreamcatcher.deckName = deckName ?? string.Empty;
            currentEntry.dreamcatcher.deckCardIds = cardIds != null ? new List<string>(cardIds) : new List<string>();
        }

        public void RecordDreamcatcherOffer(string trigger, int waveNumber, float time, IEnumerable<string> cardIds)
        {
            if (currentEntry == null) return;
            currentEntry.dreamcatcher.offers.Add(new DreamcatcherOfferLog
            {
                trigger = trigger ?? string.Empty,
                waveNumber = waveNumber,
                time = time,
                cardIds = cardIds != null ? new List<string>(cardIds) : new List<string>(),
            });
        }

        public void RecordDreamcatcherPick(DreamcatcherPickLog pick)
        {
            if (currentEntry == null || pick == null) return;
            currentEntry.dreamcatcher.picks.Add(pick);
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

        public void RecordBlockingHazard(BlockingHazardLog hazard)
        {
            if (currentEntry == null || hazard == null) return;
            currentEntry.blocking_hazards.Add(hazard);
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

        public void RecordKill(string unitType, Vector2Int tile, float time)
        {
            if (currentEntry == null) return;
            currentEntry.kills.Add(new KillLog
            {
                unit_type = unitType ?? string.Empty,
                tile = tile,
                time = time,
            });
        }

        public void AddScoreEvent(string eventType, int delta, float time)
        {
            if (currentEntry == null) return;
            score = Math.Max(0, score + delta);
            currentEntry.score_events.Add(new ScoreEventLog
            {
                event_type = eventType ?? string.Empty,
                delta = delta,
                score = score,
                time = time,
            });
            currentEntry.result.score = score;
        }

        public void SetScore(int value)
        {
            if (currentEntry == null) return;
            score = Math.Max(0, value);
            currentEntry.result.score = score;
        }

        // battle-score-formula unit 3 — 총점 + 3축 분해. 기존 SetScore(int) 는 지우지
        // 않는다: AddScoreEvent 경로가 여전히 result.score 를 갱신한다.
        //
        // 알려진 불일치 — score_events[] 는 처치당 +10 의 라이브 HUD 누적이고
        // result.score 는 최종 산식이라 같은 로그 안에서 두 값이 다르다.
        // 계약 12(HUD 표시 전용 존치)의 귀결이며 의도된 상태다.
        public void SetScore(int total, int timeScore, int stressScore, int killScore)
        {
            if (currentEntry == null) return;
            SetScore(total);
            currentEntry.result.time_score = Math.Max(0, timeScore);
            currentEntry.result.stress_score = Math.Max(0, stressScore);
            currentEntry.result.kill_score = Math.Max(0, killScore);
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

        // tournament-play-report Unit 1 — serialize the current entry WITHOUT
        // closing the session (file write stays in EndSession). Compact form for
        // network transport. Fills timestamp_end/duration_sec on the live entry;
        // safe because EndSession later overwrites both with final values.
        public string SnapshotJson()
        {
            if (currentEntry == null) return null;
            var now = DateTime.UtcNow;
            currentEntry.timestamp_end = now.ToString("o");
            currentEntry.result.duration_sec = (float)(now - startedAt).TotalSeconds;
            return JsonUtility.ToJson(currentEntry, prettyPrint: false);
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
