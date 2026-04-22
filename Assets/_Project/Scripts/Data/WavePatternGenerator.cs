using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class WavePatternGenerator
    {
        public static GeneratedWavePlan Generate(AttackDeck deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return Generate(
                deck.ResolveWaveSeed(),
                deck.waveGeneratorVersion,
                deck.timerDurationSec,
                deck.minWaveCount,
                deck.maxWaveCount,
                deck.minUnitsPerWave,
                deck.maxUnitsPerWave,
                deck.intraWaveSpacingSec,
                deck.ResolveAttackUnitPool());
        }

        public static GeneratedWavePlan Generate(
            int seed,
            int generatorVersion,
            float timerDurationSec,
            int minWaveCount,
            int maxWaveCount,
            int minUnitsPerWave,
            int maxUnitsPerWave,
            float intraWaveSpacingSec,
            IReadOnlyList<AttackUnitData> attackUnitPool)
        {
            if (attackUnitPool == null) throw new ArgumentNullException(nameof(attackUnitPool));

            var pool = BuildDistinctPool(attackUnitPool);
            if (pool.Count < 2)
                throw new ArgumentException("Wave generation requires at least two distinct AttackUnitData entries.", nameof(attackUnitPool));

            int resolvedSeed = seed != 0 ? seed : 1;
            uint rngSeed = (uint)math.abs(resolvedSeed);
            if (rngSeed == 0u) rngSeed = 1u;
            var rng = new Unity.Mathematics.Random(rngSeed);

            int minWaves = math.max(2, math.min(minWaveCount, maxWaveCount));
            int maxWaves = math.max(minWaves, math.max(minWaveCount, maxWaveCount));
            int waveCount = rng.NextInt(minWaves, maxWaves + 1);

            int minUnits = math.max(2, math.min(minUnitsPerWave, maxUnitsPerWave));
            int maxUnits = math.max(minUnits, math.max(minUnitsPerWave, maxUnitsPerWave));

            float duration = timerDurationSec > 0f ? timerDurationSec : 180f;
            float interval = waveCount > 1 ? duration / (waveCount - 1) : 0f;
            float spacing = intraWaveSpacingSec > 0f ? intraWaveSpacingSec : 0.35f;

            var waves = new List<GeneratedWave>(waveCount);
            for (int i = 0; i < waveCount; i++)
            {
                int aIndex = rng.NextInt(0, pool.Count);
                int bIndex = rng.NextInt(0, pool.Count - 1);
                if (bIndex >= aIndex) bIndex++;

                int total = rng.NextInt(minUnits, maxUnits + 1);
                int countA = rng.NextInt(1, total);
                int countB = total - countA;

                waves.Add(new GeneratedWave(
                    i,
                    i * interval,
                    pool[aIndex],
                    countA,
                    pool[bIndex],
                    countB));
            }

            return new GeneratedWavePlan(resolvedSeed, generatorVersion, duration, interval, spacing, waves);
        }

        public static List<SpawnEntry> ExpandWave(GeneratedWave wave, float baseTriggerTimeSec, int laneCount, float intraWaveSpacingSec)
        {
            var entries = new List<SpawnEntry>(wave.totalCount);
            int localIndex = 0;
            AddEntries(entries, wave.unitA, wave.countA, baseTriggerTimeSec, laneCount, intraWaveSpacingSec, ref localIndex);
            AddEntries(entries, wave.unitB, wave.countB, baseTriggerTimeSec, laneCount, intraWaveSpacingSec, ref localIndex);
            return entries;
        }

        public static string FormatSummary(GeneratedWave wave)
        {
            string nameA = wave.unitA != null ? wave.unitA.displayName : "?";
            string nameB = wave.unitB != null ? wave.unitB.displayName : "?";
            return $"Wave {wave.waveIndex + 1} - {nameA} {wave.countA}, {nameB} {wave.countB}";
        }

        private static void AddEntries(
            List<SpawnEntry> entries,
            AttackUnitData unit,
            int count,
            float baseTriggerTimeSec,
            int laneCount,
            float intraWaveSpacingSec,
            ref int localIndex)
        {
            int lanes = math.max(1, laneCount);
            for (int i = 0; i < count; i++)
            {
                entries.Add(new SpawnEntry
                {
                    triggerTimeSec = baseTriggerTimeSec + localIndex * intraWaveSpacingSec,
                    unitType = unit,
                    spawnIndex = localIndex % lanes,
                });
                localIndex++;
            }
        }

        private static List<AttackUnitData> BuildDistinctPool(IReadOnlyList<AttackUnitData> source)
        {
            var result = new List<AttackUnitData>();
            for (int i = 0; i < source.Count; i++)
            {
                var unit = source[i];
                if (unit == null || result.Contains(unit)) continue;
                result.Add(unit);
            }
            return result;
        }
    }
}
