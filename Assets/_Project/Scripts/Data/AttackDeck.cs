using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "AttackDeck", menuName = "Wassup/AttackDeck", order = 11)]
    public class AttackDeck : ScriptableObject
    {
        public string deckId = "WaveA";
        [Header("Generated Waves")]
        public bool useGeneratedWaves = true;
        // match-seed-unification(2026-06-10) DEPRECATED(라이브): 라이브 웨이브 시드는
        // GameManager.matchSeed 에서 파생(MatchSeed.DeriveWaveSeed). waveSeed/ResolveWaveSeed 는
        // 레거시 Generate(deck) 오버로드(테스트 등)에서만 쓰임. 재현 고정은 GameManager.debugFixedMatchSeed.
        public int waveSeed = 0;
        public int waveGeneratorVersion = 1;
        public AttackUnitData[] attackUnitPool;
        public int minWaveCount = 10;
        public int maxWaveCount = 15;
        public int minUnitsPerWave = 10;
        public int maxUnitsPerWave = 15;
        public float intraWaveSpacingSec = 0.35f;

        [Header("Legacy Spawns")]
        public List<SpawnEntry> spawns = new();
        public int defeatGoalReachedCount = 5;
        public float timerDurationSec = 180f;

        public int ResolveWaveSeed()
        {
            return waveSeed != 0 ? waveSeed : 1;
        }

        public AttackUnitData[] ResolveAttackUnitPool()
        {
            if (attackUnitPool != null && attackUnitPool.Length >= 2)
                return attackUnitPool;

            if (spawns == null || spawns.Count == 0)
                return Array.Empty<AttackUnitData>();

            var units = new List<AttackUnitData>();
            for (int i = 0; i < spawns.Count; i++)
            {
                var unit = spawns[i].unitType;
                if (unit == null || units.Contains(unit)) continue;
                units.Add(unit);
            }
            return units.ToArray();
        }
    }

    [Serializable]
    public class SpawnEntry
    {
        public float triggerTimeSec;
        public AttackUnitData unitType;
        [Tooltip("GeneratedMap.spawns array index. Out-of-range values fall back to index 0.")]
        public int spawnIndex;
    }
}
