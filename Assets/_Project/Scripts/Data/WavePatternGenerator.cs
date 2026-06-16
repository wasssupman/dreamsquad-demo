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
            return Generate(deck, deck.ResolveWaveSeed());
        }

        // match-seed-unification — 라이브 경로용. 시드를 외부(GameManager.matchSeed 파생)에서 주입.
        // 덱의 나머지 설정(풀/웨이브 수/spacing)은 그대로 사용.
        public static GeneratedWavePlan Generate(AttackDeck deck, int seedOverride)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return Generate(
                seedOverride,
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
            float interval = waveCount > 0 ? duration / waveCount : 0f;
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

        // wave-authoring-test-mode unit 2/6 — 에디터 작성 플랜을 런타임 GeneratedWavePlan 으로.
        // 각 웨이브는 durationSec 만큼의 구간이고, 웨이브 i 절대 시작 = 앞 웨이브 durationSec 합.
        // 그룹의 triggerTimeSec(웨이브 상대)은 WaveSpawnGroup.triggerOffsetSec 로, count 펼침
        // 간격은 GeneratedWave.spawnIntervalSec(=wave.intervalSec)로. expandMode=PerGroupTimeline.
        // seed=0/version=0 은 비-seed(작성) 마커.
        public static GeneratedWavePlan FromPlanAsset(WavePlanAsset plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var source = plan.waves;
            int count = source != null ? source.Count : 0;
            var waves = new List<GeneratedWave>(count);
            float cumulativeStart = 0f;
            for (int i = 0; i < count; i++)
            {
                var aw = source[i];
                var groups = new List<WaveSpawnGroup>();
                if (aw != null && aw.groups != null)
                    for (int g = 0; g < aw.groups.Count; g++)
                    {
                        var grp = aw.groups[g];
                        if (grp == null || grp.unit == null || grp.count <= 0) continue;
                        groups.Add(new WaveSpawnGroup(grp.unit, grp.count, math.max(0f, grp.triggerTimeSec)));
                    }
                float interval = aw != null ? math.max(0f, aw.intervalSec) : 0f;
                waves.Add(new GeneratedWave(i, cumulativeStart, groups, interval, WaveExpandMode.PerGroupTimeline));
                cumulativeStart += aw != null ? math.max(0f, aw.durationSec) : 0f;
            }

            return new GeneratedWavePlan(0, 0, plan.timerDurationSec, 0f, 0f, waves);
        }

        // RoundRobin(seed): round 0,1,2... 마다 그룹 순서대로 1마리씩 emit, intraWaveSpacing 간격
        //   (2그룹이면 기존 A,B,A,B... 인터리브와 byte-identical).
        // PerGroupTimeline(작성): 그룹마다 triggerOffsetSec 부터 count 마리를 spawnIntervalSec 간격.
        public static List<SpawnEntry> ExpandWave(GeneratedWave wave, float baseTriggerTimeSec, int laneCount, float intraWaveSpacingSec)
        {
            var entries = new List<SpawnEntry>(wave.totalCount);
            var groups = wave.groups;
            if (groups == null || groups.Count == 0) return entries;

            int localIndex = 0;

            if (wave.expandMode == WaveExpandMode.PerGroupTimeline)
            {
                for (int g = 0; g < groups.Count; g++)
                {
                    var grp = groups[g];
                    if (grp.unit == null) continue;
                    for (int k = 0; k < grp.count; k++)
                    {
                        float t = baseTriggerTimeSec + grp.triggerOffsetSec + k * wave.spawnIntervalSec;
                        AddEntryAt(entries, grp.unit, t, laneCount, ref localIndex);
                    }
                }
                return entries;
            }

            int maxCount = 0;
            for (int g = 0; g < groups.Count; g++) maxCount = math.max(maxCount, groups[g].count);
            for (int round = 0; round < maxCount; round++)
                for (int g = 0; g < groups.Count; g++)
                {
                    if (round >= groups[g].count) continue;
                    if (groups[g].unit == null) continue; // 빈 그룹은 스폰하지 않음(작성 데이터 방어)
                    AddEntry(entries, groups[g].unit, baseTriggerTimeSec, laneCount, intraWaveSpacingSec, ref localIndex);
                }
            return entries;
        }

        public static string FormatSummary(GeneratedWave wave)
        {
            var groups = wave.groups;
            if (groups == null || groups.Count == 0) return $"Wave {wave.waveIndex + 1} - (empty)";

            var sb = new System.Text.StringBuilder();
            sb.Append("Wave ").Append(wave.waveIndex + 1).Append(" - ");
            for (int g = 0; g < groups.Count; g++)
            {
                if (g > 0) sb.Append(", ");
                var unit = groups[g].unit;
                sb.Append(unit != null ? unit.displayName : "?").Append(' ').Append(groups[g].count);
            }
            return sb.ToString();
        }

        private static void AddEntry(
            List<SpawnEntry> entries,
            AttackUnitData unit,
            float baseTriggerTimeSec,
            int laneCount,
            float intraWaveSpacingSec,
            ref int localIndex)
        {
            int lanes = math.max(1, laneCount);
            entries.Add(new SpawnEntry
            {
                triggerTimeSec = baseTriggerTimeSec + localIndex * intraWaveSpacingSec,
                unitType = unit,
                spawnIndex = localIndex % lanes,
            });
            localIndex++;
        }

        // PerGroupTimeline 용 — 절대 시각을 직접 받아 엔트리 추가. lane 은 전역 localIndex 기준.
        private static void AddEntryAt(
            List<SpawnEntry> entries,
            AttackUnitData unit,
            float timeSec,
            int laneCount,
            ref int localIndex)
        {
            int lanes = math.max(1, laneCount);
            entries.Add(new SpawnEntry
            {
                triggerTimeSec = timeSec,
                unitType = unit,
                spawnIndex = localIndex % lanes,
            });
            localIndex++;
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
