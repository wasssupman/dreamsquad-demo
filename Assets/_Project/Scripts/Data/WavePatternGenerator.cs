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
                deck.ResolveAttackUnitPool(),
                deck.bossUnit,
                deck.bossWaveInterval,
                deck.bossEscortMin,
                deck.bossEscortMax,
                deck.waveCountJitter,
                deck.fixedWaveIntervalSec);
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
            IReadOnlyList<AttackUnitData> attackUnitPool,
            AttackUnitData bossUnit = null,
            int bossWaveInterval = 0,
            int bossEscortMin = 0,
            int bossEscortMax = 0,
            int waveCountJitter = 1,
            float fixedIntervalSec = 0f)
        {
            if (attackUnitPool == null) throw new ArgumentNullException(nameof(attackUnitPool));

            var pool = BuildDistinctPool(attackUnitPool);
            // boss-wave-cadence unit 0 — 보스는 잡몹 pool 과 분리가 계약. 실수로 pool 에 섞여도
            // 방어적으로 제외해 비-보스 웨이브 보스 오발화·escort 보스 중복(보스 2기)을 원천 차단.
            // 없으면 no-op → 비-보스 웨이브는 현행 생성기와 불변.
            if (bossUnit != null && pool.Remove(bossUnit))
                Debug.LogWarning($"[WavePatternGenerator] bossUnit '{bossUnit.id}' 가 attackUnitPool 에 포함돼 있어 생성 pool 에서 제외했습니다. 덱에서 pool 과 bossUnit 을 분리하세요.");
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
            // endless-mode unit 1 — 고정 간격(>0)이면 웨이브수 의존 파생 대신 그 값을 쓴다.
            // triggerTimeSec = i*interval 계약은 불변 → 스케줄러(QueueDueWaves) 재사용.
            float interval = fixedIntervalSec > 0f
                ? fixedIntervalSec
                : (waveCount > 0 ? duration / waveCount : 0f);
            float spacing = intraWaveSpacingSec > 0f ? intraWaveSpacingSec : 0.35f;

            var waves = new List<GeneratedWave>(waveCount);
            for (int i = 0; i < waveCount; i++)
            {
                int aIndex = rng.NextInt(0, pool.Count);
                int bIndex = rng.NextInt(0, pool.Count - 1);
                if (bIndex >= aIndex) bIndex++;

                // wave-pattern unit 7 — 수량 램프. NextFloat 1콜은 기존 NextInt 1콜과 rng
                // 소비 수가 같아 아래 countA·보스 후처리의 rng 정렬이 불변이다.
                float jitter01 = rng.NextFloat();
                int total = RampedWaveTotal(i, waveCount, minUnits, maxUnits, waveCountJitter, jitter01);
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

            // boss-wave-cadence unit 0 — 매 bossWaveInterval 번째 웨이브를 보스×1(선봉) + 잡몹×[min,max]
            // 로 치환. 랜덤 루프 뒤 후처리라 비-보스 웨이브의 rng 소비는 현행과 byte-identical.
            if (bossUnit != null && bossWaveInterval > 0)
            {
                int escortMin = math.max(1, math.min(bossEscortMin, bossEscortMax));
                int escortMax = math.max(escortMin, math.max(bossEscortMin, bossEscortMax));
                for (int i = 0; i < waves.Count; i++)
                {
                    if ((i + 1) % bossWaveInterval != 0) continue;
                    int escortCount = rng.NextInt(escortMin, escortMax + 1);
                    var escortType = pool[rng.NextInt(0, pool.Count)]; // pool 은 boss-free
                    var groups = new List<WaveSpawnGroup>
                    {
                        new WaveSpawnGroup(bossUnit, 1),      // 선봉: RoundRobin round 0 = 보스 먼저
                        new WaveSpawnGroup(escortType, escortCount),
                    };
                    waves[i] = new GeneratedWave(i, i * interval, groups, 0f, WaveExpandMode.RoundRobin);
                }
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

        // spawn-point-alert unit 0 — QueueWave 의 deckIndex 부여(waveIndex*Stride + 엔트리 순번)
        // 규약을 예보와 실스폰이 공유한다. 이 상수·아래 두 함수의 공유가 "예보 = 실스폰" 보증.
        public const int DeckIndexStride = 1000;

        // BattleBridge.SpawnUnit 에서 이관(spawn-point-alert unit 0). laneCount<=2 는 authored
        // spawnIndex 존중, 3+ lane 은 deckIndex 기반 결정론 round-robin.
        public static int EffectiveSpawnIndex(int authoredIndex, int deckIndex, int laneCount)
        {
            if (laneCount <= 0) return 0;
            if (laneCount <= 2)
                return math.clamp(authoredIndex, 0, laneCount - 1);
            return math.abs(deckIndex) % laneCount;
        }

        // wave-pattern unit 7 — 웨이브 수량 램프(순수). total 을 웨이브 인덱스에 따라
        // minUnits(첫 웨이브)→maxUnits(마지막 웨이브) 선형 보간하고 ±jitterBand 정수 지터를
        // 더한 뒤 [minUnits,maxUnits] 로 클램프한다. jitter01∈[0,1) 는 호출측이 뽑아 넘기는
        // plain 입력(rng 를 함수에 넣지 않아 EditMode 로 결정론 검증 가능 — 제약 10).
        public static int RampedWaveTotal(
            int waveIndex, int waveCount, int minUnits, int maxUnits, int jitterBand, float jitter01)
        {
            if (maxUnits < minUnits) { int t = minUnits; minUnits = maxUnits; maxUnits = t; }
            float ramp = waveCount > 1 ? (float)waveIndex / (waveCount - 1) : 1f;
            float center = math.lerp(minUnits, maxUnits, math.saturate(ramp));
            float jitter = jitterBand > 0 ? (jitter01 * 2f - 1f) * jitterBand : 0f;
            return math.clamp((int)math.round(center + jitter), minUnits, maxUnits);
        }

        // 웨이브가 lane 별로 첫 적을 내보내는 절대 시각(스폰 없는 lane 은 -1).
        // ExpandWave + EffectiveSpawnIndex 를 실스폰 경로(QueueWave→SpawnUnit)와 동일 규약으로 호출.
        public static float[] FirstSpawnTimesPerLane(
            GeneratedWave wave, float baseTriggerTimeSec, int laneCount, float intraWaveSpacingSec)
        {
            int lanes = math.max(1, laneCount);
            var result = new float[lanes];
            for (int i = 0; i < lanes; i++) result[i] = -1f;

            var entries = ExpandWave(wave, baseTriggerTimeSec, laneCount, intraWaveSpacingSec);
            for (int i = 0; i < entries.Count; i++)
            {
                int lane = EffectiveSpawnIndex(entries[i].spawnIndex, wave.waveIndex * DeckIndexStride + i, lanes);
                if (result[lane] < 0f || entries[i].triggerTimeSec < result[lane])
                    result[lane] = entries[i].triggerTimeSec;
            }
            return result;
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
