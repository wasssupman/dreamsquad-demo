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
        //
        // wave-concept-blocks unit 2 — laneCount(맵 스폰 수)를 안 주는 레거시 오버로드. 프리뷰·
        // 테스트용이며 2로 폴백한다. **라이브 경로는 반드시 laneCount 를 넘기는 오버로드를 쓴다**
        // (계약 6 — 브리핑과 런타임이 다른 값을 쓰면 예고와 실스폰이 갈린다).
        public static GeneratedWavePlan Generate(AttackDeck deck, int seedOverride)
            => Generate(deck, seedOverride, 2);

        public static GeneratedWavePlan Generate(AttackDeck deck, int seedOverride, int laneCount)
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
                deck.maxWaveIntervalSec,
                deck.waveSpawnLeadInSec,
                deck.bossPool,
                deck.unitGrowthPerWave,
                deck.waveConceptPool,
                deck.conceptHoldWaves,
                laneCount);
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
            // three-minute-survival unit 2 — 웨이브 간 상한 간격(구 fixedIntervalSec). 명목
            // triggerTimeSec 그리드(i × 이 값 = 최악 케이스 시각)도 여기서 나온다.
            float maxWaveIntervalSec = 0f,
            float spawnLeadInSec = 0f,
            // boss-jjangssen unit 0 — 보스 로테이션 풀. **맨 뒤에 추가**한 것은 의도다: 기존 boss 파라미터
            // 사이에 끼우면 positional 인자로 호출하는 테스트들이 조용히 다른 값을 받는다.
            IReadOnlyList<AttackUnitData> bossPool = null,
            // three-minute-survival unit 2 — 같은 이유로 맨 뒤에 추가. 1 = 성장 없음.
            float unitGrowthPerWave = 1f,
            // wave-concept-blocks unit 2 — 같은 이유로 맨 뒤에 추가. 풀이 비면(기본) 컨셉 경로를
            // 타지 않고 아래 레거시 2종 분기가 그대로 돈다 → 기존 편성과 byte-identical.
            IReadOnlyList<WaveConceptData> waveConceptPool = null,
            int conceptHoldWaves = 3,
            int laneCount = 2)
        {
            if (attackUnitPool == null) throw new ArgumentNullException(nameof(attackUnitPool));

            var pool = BuildDistinctPool(attackUnitPool);
            // boss-jjangssen unit 0 — 폴백 소유자는 생성기다(덱을 안 거치는 직접 호출자도 같은 규칙을
            // 받아야 하고, 두 곳에 두면 드리프트한다). bossPool 이 비면 bossUnit 단일.
            var bosses = BuildBossPool(bossPool, bossUnit);
            // boss-wave-cadence unit 0 — 보스는 잡몹 pool 과 분리가 계약. 실수로 pool 에 섞여도
            // 방어적으로 제외해 비-보스 웨이브 보스 오발화·escort 보스 중복(보스 2기)을 원천 차단.
            // 없으면 no-op → 비-보스 웨이브는 현행 생성기와 불변.
            // boss-jjangssen unit 0 — 단일 bossUnit 에서 pool 전체 루프로 확장(불변식 유지).
            for (int b = 0; b < bosses.Count; b++)
            {
                if (pool.Remove(bosses[b]))
                    Debug.LogWarning($"[WavePatternGenerator] boss '{bosses[b].id}' 가 attackUnitPool 에 포함돼 있어 생성 pool 에서 제외했습니다. 덱에서 pool 과 보스를 분리하세요.");
            }
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
            // three-minute-survival unit 2 — 상한 간격(>0)이 명목 그리드를 정한다. 런타임은 이
            // triggerTimeSec 을 읽지 않는다(전멸/상한 이벤트 구동) — 남겨두는 이유는 브리핑
            // 스트립·배틀로그가 "최악 케이스 시각"으로 읽을 수 있게 하는 것뿐이다.
            // 0 이면 레거시 파생(제한시간/웨이브수) — 작성 플랜·직접 호출자용 폴백.
            float interval = maxWaveIntervalSec > 0f
                ? maxWaveIntervalSec
                : (waveCount > 0 ? duration / waveCount : 0f);
            float spacing = intraWaveSpacingSec > 0f ? intraWaveSpacingSec : 0.35f;
            // 스폰 창 불변식 — 위반하면 _pending 이 영구히 비지 않아 "전멸 즉시 진행"이 죽는다.
            // 저작 실수를 조용히 넘기지 않는다(증상이 "웨이브가 항상 상한 간격으로만 온다"는
            // 형태라 원인 추적이 매우 어렵다).
            if (maxWaveIntervalSec > 0f)
            {
                float window = math.max(0f, spawnLeadInSec) + (maxUnits - 1) * spacing;
                if (window >= maxWaveIntervalSec)
                    Debug.LogWarning(
                        $"[WavePatternGenerator] 스폰 창 {window:0.##}s 가 상한 간격 {maxWaveIntervalSec:0.##}s 이상이다 " +
                        $"(leadIn {spawnLeadInSec:0.##} + (maxUnits {maxUnits}−1) × spacing {spacing:0.##}). " +
                        "전멸 즉시 진행이 성립하지 않는다 — intraWaveSpacingSec 을 내리거나 maxUnitsPerWave 를 줄여라.");
            }

            // wave-concept-blocks unit 2 — 컨셉은 웨이브가 아니라 **블록**의 속성이다(계약 1).
            // 블록 경계에서만 컨셉과 lane 배정을 뽑고 conceptHoldWaves 웨이브 동안 재사용한다.
            // 풀이 비면 concept 이 영구히 null 이라 rng 를 한 번도 안 쓰고 아래 레거시 분기만 돈다.
            int holdWaves = math.max(1, conceptHoldWaves);
            int lanes = math.max(1, laneCount);
            bool hasConceptPool = HasAnyConcept(waveConceptPool);
            if (laneCount <= 0)
                Debug.LogWarning("[WavePatternGenerator] laneCount 가 0 이하다 — 1 로 폴백한다. " +
                                 "라이브 경로는 맵의 스폰 수를 넘겨야 예고와 실스폰이 일치한다(계약 6).");

            WaveConceptData concept = null;
            int previousBlock = -1;

            // wave-concept-blocks unit 3 — 보스 후처리가 그 웨이브의 블록 컨셉을 읽는다.
            // 후처리를 이 루프 안으로 옮기지 않는 이유는 rng 소비 순서다: 옮기면 컨셉 없는
            // 레거시 덱의 스트림까지 흔들려 무회귀 경로가 깨진다. 그래서 블록별 스냅샷을 남긴다.
            var conceptByWave = new WaveConceptData[waveCount];
            var conceptSlotsByWave = new WaveConceptSlot[waveCount][];
            var conceptLanesByWave = new int[waveCount][];
            WaveConceptSlot[] blockSlots = null;
            int[] blockLanes = null;
            // wave-pull-revival unit 2 — 블록 가운데 웨이브가 쓸 변주 편성(저작 없으면 null).
            WaveConceptSlot[] blockVariantSlots = null;
            int[] blockVariantLanes = null;

            var waves = new List<GeneratedWave>(waveCount);
            for (int i = 0; i < waveCount; i++)
            {
                if (hasConceptPool && i / holdWaves != previousBlock)
                {
                    int block = i / holdWaves;
                    previousBlock = block;
                    concept = ResolveBlockConcept(
                        waveConceptPool, block * holdWaves + 1, lanes, concept,
                        ref rng, out blockSlots, out blockLanes,
                        out blockVariantSlots, out blockVariantLanes);
                }

                // wave-pull-revival unit 2 — 블록의 **두 번째** 웨이브만 변주를 입는다.
                // 첫 웨이브는 성격을 가르치는 자리(순수해야 한다), 마지막은 그 성격의 시험대다.
                // holdWaves < 3 이면 가운데가 없으므로 적용하지 않는다 — 2 에서 i%2==1 은
                // 마지막 웨이브라 시험대를 덮어쓴다.
                bool useVariant = holdWaves >= 3 && i % holdWaves == 1 &&
                                  blockVariantSlots != null && blockVariantSlots.Length > 0;
                var waveSlots = useVariant ? blockVariantSlots : blockSlots;
                var waveLanes = useVariant ? blockVariantLanes : blockLanes;

                conceptByWave[i] = concept;
                conceptSlotsByWave[i] = waveSlots;
                conceptLanesByWave[i] = waveLanes;

                if (concept != null)
                {
                    int conceptTotal = ExponentialWaveTotal(
                        i, minUnits, maxUnits, unitGrowthPerWave, waveCountJitter, rng.NextFloat());
                    var conceptGroups = BuildConceptGroups(
                        pool, waveSlots, waveLanes, conceptTotal, concept.countMul,
                        maxUnits, i + 1, concept.id, ref rng);

                    waves.Add(new GeneratedWave(
                        i, i * interval, conceptGroups, 0f, WaveExpandMode.RoundRobin,
                        concept.displayName));
                    continue;
                }

                // ---- 레거시 2종 경로 (컨셉 없음) ----
                // 여기는 손대지 않는다. 컨셉 풀이 빈 덱이 **현행과 byte-identical** 해야 무회귀
                // 경로가 데이터로 성립한다(rng 소비 순서까지 동일).
                int aIndex = rng.NextInt(0, pool.Count);
                int bIndex = rng.NextInt(0, pool.Count - 1);
                if (bIndex >= aIndex) bIndex++;

                // wave-pattern unit 12 — 등장 게이트. 뽑은 인덱스만 사후 보정하므로 rng 소비가
                // 불변이다 → 게이트에 안 걸리는 웨이브의 구성은 기존과 byte-identical.
                int waveNumber = i + 1;
                aIndex = ResolveWaveEligibleIndex(pool, aIndex, waveNumber);
                bIndex = ResolveWaveEligibleIndex(pool, bIndex, waveNumber, aIndex);

                // wave-pattern unit 7 — 수량 램프. NextFloat 1콜은 기존 NextInt 1콜과 rng
                // 소비 수가 같아 아래 countA·보스 후처리의 rng 정렬이 불변이다.
                float jitter01 = rng.NextFloat();
                int total = ExponentialWaveTotal(i, minUnits, maxUnits, unitGrowthPerWave, waveCountJitter, jitter01);
                int countA = rng.NextInt(1, total);
                int countB = total - countA;

                // structure-hunter-enemy unit 1 — 종류별 동시 등장 상한. rng 를 소비하지 않으므로
                // 상한 미저작(전부 0)이면 기존 웨이브와 byte-identical 이다.
                ClampGroupCounts(
                    pool[aIndex] != null ? pool[aIndex].maxPerWave : 0,
                    pool[bIndex] != null ? pool[bIndex].maxPerWave : 0,
                    ref countA, ref countB);

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
            if (bosses.Count > 0 && bossWaveInterval > 0)
            {
                int escortMin = math.max(1, math.min(bossEscortMin, bossEscortMax));
                int escortMax = math.max(escortMin, math.max(bossEscortMin, bossEscortMax));
                for (int i = 0; i < waves.Count; i++)
                {
                    if ((i + 1) % bossWaveInterval != 0) continue;
                    // boss-jjangssen unit 0 — 보스가 1종이면 rng 를 **소비하지 않는다**. 라이브 덱은
                    // 전부 단일 보스라, 이 가드가 rng 스트림을 byte-identical 하게 유지해 기존 맵들의
                    // 웨이브 편성이 무회귀가 된다. 2종+ 부터 선택에 rng 1콜을 쓴다.
                    var boss = bosses.Count == 1 ? bosses[0] : bosses[rng.NextInt(0, bosses.Count)];
                    int escortCount = rng.NextInt(escortMin, escortMax + 1);

                    // wave-concept-blocks unit 3 — 호위가 그 블록 컨셉의 **성질과 위상**을 입는다.
                    // 그래야 블록의 마지막 웨이브에서 컨셉이 깨지지 않고 보스가 그 블록의
                    // 클라이맥스가 된다(「원거리」 블록 = 보스가 사거리 호위를 끼고 협공으로 온다).
                    var blockConcept = conceptByWave[i];
                    var blockConceptSlots = conceptSlotsByWave[i];
                    var blockConceptLanes = conceptLanesByWave[i];

                    List<WaveSpawnGroup> groups;
                    if (blockConcept != null && blockConceptSlots != null && blockConceptSlots.Length > 0)
                    {
                        // ⚠ 호위 예산에는 countMul 을 적용하지 않는다(countMul: 1f). bossEscortMin/Max
                        // 가 이미 예산이라 배율을 다시 곱하면 이중 스케일이 된다 — 「중장」(0.4)이면
                        // 3 × 0.4 = 1.2 로 하한에 먹혀 컨셉마다 호위 수가 제멋대로 달라진다.
                        // 컨셉이 호위에 주는 것은 성질과 위상이고 수량은 보스 파라미터가 소유한다.
                        groups = BuildConceptGroups(
                            pool, blockConceptSlots, blockConceptLanes, escortCount, 1f,
                            maxUnits, i + 1, blockConcept.id, ref rng);
                        // 선봉: RoundRobin round 0 = 보스 먼저. lane 은 컨셉 첫 슬롯을 따른다 —
                        // 보스가 서 있는 쪽이 «본대»로 읽힌다.
                        groups.Insert(0, new WaveSpawnGroup(boss, 1, 0f, blockConceptLanes[0]));
                    }
                    else
                    {
                        // 컨셉 없는 덱(레거시/폴백) — 여기는 손대지 않는다. rng 소비까지 현행 그대로.
                        // pool 은 boss-free. 호위도 같은 등장 게이트를 따른다(unit 12).
                        var escortType = pool[ResolveWaveEligibleIndex(pool, rng.NextInt(0, pool.Count), i + 1)];
                        // structure-hunter-enemy unit 1 — 호위에도 같은 상한. 여기가 더 위험하다:
                        // 일반 웨이브는 종류가 둘로 나뉘지만 호위는 한 종류가 통째로 3~4기다.
                        if (escortType != null && escortType.maxPerWave > 0)
                            escortCount = math.min(escortCount, escortType.maxPerWave);
                        groups = new List<WaveSpawnGroup>
                        {
                            new WaveSpawnGroup(boss, 1),      // 선봉: RoundRobin round 0 = 보스 먼저
                            new WaveSpawnGroup(escortType, escortCount),
                        };
                    }

                    waves[i] = new GeneratedWave(
                        i, i * interval, groups, 0f, WaveExpandMode.RoundRobin,
                        blockConcept != null ? blockConcept.displayName : "");
                }
            }

            // wave-pattern unit 11 — 리드인은 플랜에 실려 QueueWave 의 스폰 base 에서만 쓰인다.
            // 위 triggerTimeSec(i*interval) 그리드는 이 값과 무관하게 불변이다.
            return new GeneratedWavePlan(resolvedSeed, generatorVersion, duration, interval, spacing, waves,
                math.max(0f, spawnLeadInSec));
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

            // spawnLeadInSec = 0 (기본값) — 작성 플랜은 그룹 상대 시각(triggerTimeSec ∈ [0,duration])
            // 으로 리드인을 직접 표현한다. 덱 값을 겹쳐 주면 이중 가산이다(unit 11).
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

        // three-minute-survival unit 2 — 웨이브 수량의 **완만한 지수 성장**(순수). 구
        // RampedWaveTotal(전체 웨이브 수로 나눈 선형 보간)을 대체한다: 웨이브 상한이 100(명목)이
        // 되면서 "전체 대비 진행률" 이라는 분모가 의미를 잃었다 — 3분 안에 도달하는 것은 앞의
        // 10~16개뿐이므로 성장은 **웨이브 번호의 절대 함수**여야 한다.
        //
        //   center_i = minUnits × growth^i          (growth=1 이면 상수 = 성장 없음)
        //   total_i  = clamp(round(center + jitter), minUnits, maxUnits)
        //
        // jitter01∈[0,1) 는 호출측이 뽑아 넘기는 plain 입력(rng 를 함수에 넣지 않아 EditMode 로
        // 결정론 검증 가능 — 제약 10). growth^i 는 i 가 커지면 폭발하므로 maxUnits 클램프가
        // 유일한 상한이다.
        public static int ExponentialWaveTotal(
            int waveIndex, int minUnits, int maxUnits, float growth, int jitterBand, float jitter01)
        {
            if (maxUnits < minUnits) { int t = minUnits; minUnits = maxUnits; maxUnits = t; }
            float g = growth > 1f ? growth : 1f;
            int i = waveIndex > 0 ? waveIndex : 0;
            // pow 로 한 번에 구한다 — 누적 곱은 웨이브 인덱스마다 값이 달라지는 것을 막지 못하고
            // (같은 결과) 호출당 루프만 늘린다.
            float center = minUnits * math.pow(g, i);
            // 지수 구간에서 center 가 maxUnits 를 크게 넘어가면 float 이 커져 jitter 가 묻힌다.
            // 클램프를 먼저 걸어 jitter 가 상한 근처에서도 살아 있게 한다.
            center = math.min(center, maxUnits);
            float jitter = jitterBand > 0 ? (jitter01 * 2f - 1f) * jitterBand : 0f;
            return math.clamp((int)math.round(center + jitter), minUnits, maxUnits);
        }

        // structure-hunter-enemy unit 1 — 종류별 동시 등장 상한(순수). 0 = 무제한.
        //
        // 상한을 넘은 몫은 잘라내고 **여유가 있는 쪽으로 넘겨 웨이브 총량을 보존**한다.
        // 둘 다 상한이면 총량이 준다 — 그게 상한의 목적이므로 억지로 채우지 않는다.
        //
        // rng 를 건드리지 않는 것이 계약이다: 이 함수가 rng 를 소비하면 상한을 저작하지 않은
        // 기존 덱의 웨이브 스트림까지 흔들려 6개 맵의 난이도 곡선이 조용히 바뀐다.
        public static void ClampGroupCounts(int maxA, int maxB, ref int countA, ref int countB)
        {
            int leftover = 0;
            if (maxA > 0 && countA > maxA) { leftover += countA - maxA; countA = maxA; }
            if (maxB > 0 && countB > maxB) { leftover += countB - maxB; countB = maxB; }
            if (leftover <= 0) return;

            int roomB = maxB > 0 ? math.max(0, maxB - countB) : leftover;
            int giveB = math.min(leftover, roomB);
            countB += giveB;
            leftover -= giveB;
            if (leftover <= 0) return;

            int roomA = maxA > 0 ? math.max(0, maxA - countA) : leftover;
            countA += math.min(leftover, roomA);
        }

        // wave-pattern unit 12 — 등장 게이트 해소(순수). pool[startIndex] 가 이 웨이브에 등장할 수
        // 없으면(unit.minWaveNumber > waveNumber) pool 순서로 다음 허용 유닛까지 순환해 그 인덱스를
        // 돌려준다. 인덱스 in → 인덱스 out 이라 rng 를 건드리지 않는다(제약 10 순수 함수).
        // excludeIndex(≥0) = 같은 웨이브의 다른 그룹. "한 웨이브 = 2종" 계약을 지키려 건너뛴다.
        // 허용 유닛이 하나도 없으면 startIndex 를 그대로 돌려준다 — 빈/단일 웨이브를 만드느니
        // 게이트를 여는 fail-open 이다(예: 풀의 모든 유닛이 첫 웨이브 금지인 경우).
        public static int ResolveWaveEligibleIndex(
            IReadOnlyList<AttackUnitData> pool, int startIndex, int waveNumber, int excludeIndex = -1)
        {
            if (pool == null || pool.Count == 0) return startIndex;

            int count = pool.Count;
            int start = ((startIndex % count) + count) % count;
            for (int step = 0; step < count; step++)
            {
                int index = (start + step) % count;
                if (index == excludeIndex) continue;
                var unit = pool[index];
                if (unit == null) continue;
                if (unit.minWaveNumber <= waveNumber) return index;
            }
            return startIndex;
        }

        // ================= wave-concept-blocks unit 1 — 컨셉 해석 순수 함수 3개 =================
        // 셋 다 rng 를 받지 않는다. 호출측이 뽑은 plain 값(rollValue/roll01)을 넘겨야 EditMode 로
        // 결정론을 검증할 수 있다(ExponentialWaveTotal 이 jitter01 을 받는 것과 같은 형태, 제약 10).

        // 곡선 총량 → 슬롯별 개체 수. 균등 분배 + 잔여는 앞 슬롯부터.
        //
        // 하한이 `slotCount` 인 것이 중요하다: minUnitsPerWave(5)를 하한으로 쓰면 countMul 0.4 인
        // 컨셉이 초반 웨이브에서 5기가 되어 배율이 무의미해진다. 상한은 maxUnits 를 존중한다.
        //
        // 반환값 = 실제 분배한 합(scaled). 잔여를 랜덤으로 흘리지 않는 것이 계약이다 — 흘리면
        // 같은 시드에서 결과가 갈린다.
        public static int DistributeSlotCounts(
            int total, float countMul, int slotCount, int maxUnits, int[] outCounts)
        {
            if (slotCount <= 0) return 0;

            int lo = slotCount;
            int hi = math.max(lo, maxUnits);
            float mul = countMul > 0f ? countMul : 1f;
            int scaled = math.clamp((int)math.round(total * mul), lo, hi);

            int baseShare = scaled / slotCount;
            int remainder = scaled - baseShare * slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                if (outCounts == null || i >= outCounts.Length) break;
                outCounts[i] = baseShare + (i < remainder ? 1 : 0);
            }
            return scaled;
        }

        // laneGroup 값들 → 실제 lane 인덱스. 계약 2(lane 은 위상으로만 말한다)의 구현.
        //
        // 같은 laneGroup 은 반드시 같은 lane, 다른 laneGroup 은 반드시 다른 lane. 이 두 불변식이
        // 「원거리」의 협공을 성립시킨다. -1(무지정)은 -1 로 통과해 호출측이 기존
        // EffectiveSpawnIndex 경로를 타게 한다.
        //
        // false = 이 컨셉이 요구하는 lane 수가 맵의 스폰 수를 넘는다 → 호출측이 후보에서 버린다.
        public static bool AssignLanes(
            IReadOnlyList<int> laneGroups, int laneCount, int rollValue, int[] outLaneIndex)
        {
            int slotCount = laneGroups != null ? laneGroups.Count : 0;
            if (slotCount == 0) return true;
            if (laneCount <= 0) return false;

            // 등장 순서대로 distinct 그룹을 세면서 lane 을 배정한다. 슬롯 수가 한 자리라 O(n²) 로 충분.
            int offset = ((rollValue % laneCount) + laneCount) % laneCount;
            int assigned = 0;
            for (int i = 0; i < slotCount; i++)
            {
                int group = laneGroups[i];
                if (group < 0)
                {
                    if (outLaneIndex != null && i < outLaneIndex.Length) outLaneIndex[i] = -1;
                    continue;
                }

                int firstSeen = -1;
                for (int j = 0; j < i; j++)
                    if (laneGroups[j] == group) { firstSeen = j; break; }

                int lane;
                if (firstSeen >= 0)
                {
                    lane = outLaneIndex != null && firstSeen < outLaneIndex.Length
                        ? outLaneIndex[firstSeen]
                        : -1;
                }
                else
                {
                    if (assigned >= laneCount) return false;   // 요구 lane 수 초과
                    lane = (offset + assigned) % laneCount;
                    assigned++;
                }
                if (outLaneIndex != null && i < outLaneIndex.Length) outLaneIndex[i] = lane;
            }
            return true;
        }

        // 블록 경계에서 컨셉 하나를 뽑는다. 가중치 룰렛 + 게이트 3종 + 직전 배제.
        //
        // lane 요구량 검사는 concept.RequiredLaneCount(slots 파생)를 쓴다 — 저작 필드로 두면
        // 파생값과 갈려 «저작은 2를 요구하는데 슬롯은 3 lane 을 쓰는» 컨셉이 통과한다.
        //
        // 직전 배제가 리듬 규칙이다: 같은 컨셉이 두 블록 연속이면 그것이 기본값이 되어 인상이
        // 죽는다. 배제 때문에 후보가 0이 되면(풀에 1개뿐인 경우) 배제를 풀고 다시 고른다 —
        // fail-open. null 반환은 «후보 없음»이고 호출측이 구조적 폴백으로 떨어진다.
        public static WaveConceptData PickConcept(
            IReadOnlyList<WaveConceptData> pool,
            int blockFirstWaveNumber,
            int laneCount,
            WaveConceptData previousConcept,
            float roll01)
        {
            if (pool == null || pool.Count == 0) return null;

            float totalWeight = SumEligibleWeight(pool, blockFirstWaveNumber, laneCount, previousConcept);
            if (totalWeight <= 0f)
            {
                previousConcept = null;   // 배제 fail-open
                totalWeight = SumEligibleWeight(pool, blockFirstWaveNumber, laneCount, null);
                if (totalWeight <= 0f) return null;
            }

            float target = math.clamp(roll01, 0f, 0.9999f) * totalWeight;
            float cumulative = 0f;
            WaveConceptData last = null;
            for (int i = 0; i < pool.Count; i++)
            {
                var candidate = pool[i];
                if (!IsConceptEligible(candidate, blockFirstWaveNumber, laneCount, previousConcept)) continue;
                last = candidate;
                cumulative += candidate.weight;
                if (cumulative > target) return candidate;
            }
            // float 누적 오차로 마지막 경계를 못 넘는 경우의 안전망.
            return last;
        }

        private static float SumEligibleWeight(
            IReadOnlyList<WaveConceptData> pool, int waveNumber, int laneCount, WaveConceptData exclude)
        {
            float sum = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                var candidate = pool[i];
                if (IsConceptEligible(candidate, waveNumber, laneCount, exclude)) sum += candidate.weight;
            }
            return sum;
        }

        private static bool IsConceptEligible(
            WaveConceptData concept, int waveNumber, int laneCount, WaveConceptData exclude)
        {
            if (concept == null) return false;
            if (ReferenceEquals(concept, exclude)) return false;
            if (concept.weight <= 0f) return false;
            if (concept.EffectiveSlotCount <= 0) return false;
            if (concept.minWaveNumber > waveNumber) return false;
            if (concept.RequiredLaneCount > laneCount) return false;
            return true;
        }

        // waypoint-routing unit 7 — 상세 펼침에서 (스웜, 실제 lane)별 첫 스폰을 접는다.
        // lane 하나로 접지 않으므로 같은 lane 의 서로 다른 스웜은 별도 가이드로 남는다.
        public static SpawnGuideForecast[] BuildSpawnGuideForecasts(
            IReadOnlyList<ExpandedWaveSpawn> entries)
        {
            var result = new List<SpawnGuideForecast>();
            if (entries == null) return Array.Empty<SpawnGuideForecast>();

            for (int i = 0; i < entries.Count; i++)
            {
                var expanded = entries[i];
                var entry = expanded.entry;
                if (entry == null || entry.unitType == null) continue;

                int existing = -1;
                for (int j = 0; j < result.Count; j++)
                    if (result[j].swarmIndex == expanded.swarmIndex
                        && result[j].laneIndex == expanded.laneIndex)
                    {
                        existing = j;
                        break;
                    }

                var forecast = new SpawnGuideForecast(
                    expanded.swarmIndex,
                    expanded.laneIndex,
                    entry.triggerTimeSec,
                    entry.unitType.waypointPathIndex,
                    (byte)entry.unitType.EffectiveTraversalLayers);
                if (existing < 0)
                {
                    result.Add(forecast);
                }
                else if (entry.triggerTimeSec < result[existing].firstSpawnSec)
                {
                    result[existing] = forecast;
                }
            }
            return result.ToArray();
        }

        // RoundRobin(seed): round 0,1,2... 마다 그룹 순서대로 1마리씩 emit, intraWaveSpacing 간격
        //   (2그룹이면 기존 A,B,A,B... 인터리브와 byte-identical).
        // PerGroupTimeline(작성): 그룹마다 triggerOffsetSec 부터 count 마리를 spawnIntervalSec 간격.
        public static List<ExpandedWaveSpawn> ExpandWave(
            GeneratedWave wave, float baseTriggerTimeSec, int laneCount, float intraWaveSpacingSec)
        {
            var entries = new List<ExpandedWaveSpawn>(wave.totalCount);
            var groups = wave.groups;
            if (groups == null || groups.Count == 0) return entries;

            int localIndex = 0;
            int baseDeckIndex = wave.waveIndex * DeckIndexStride;

            if (wave.expandMode == WaveExpandMode.PerGroupTimeline)
            {
                for (int g = 0; g < groups.Count; g++)
                {
                    var grp = groups[g];
                    if (grp.unit == null) continue;
                    for (int k = 0; k < grp.count; k++)
                    {
                        float t = baseTriggerTimeSec + grp.triggerOffsetSec + k * wave.spawnIntervalSec;
                        AddEntryAt(entries, grp.unit, g, t, laneCount, baseDeckIndex,
                            grp.laneIndex, ref localIndex);
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
                    AddEntry(entries, groups[g].unit, g, baseTriggerTimeSec, laneCount,
                        intraWaveSpacingSec, baseDeckIndex, groups[g].laneIndex, ref localIndex);
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
            List<ExpandedWaveSpawn> entries,
            AttackUnitData unit,
            int swarmIndex,
            float baseTriggerTimeSec,
            int laneCount,
            float intraWaveSpacingSec,
            int baseDeckIndex,
            int groupLaneIndex,
            ref int localIndex)
        {
            int lanes = math.max(1, laneCount);
            int authoredSpawnIndex = ResolveAuthoredLane(groupLaneIndex, localIndex, lanes);
            entries.Add(new ExpandedWaveSpawn(
                new SpawnEntry
                {
                    triggerTimeSec = baseTriggerTimeSec + localIndex * intraWaveSpacingSec,
                    unitType = unit,
                    spawnIndex = authoredSpawnIndex,
                },
                swarmIndex,
                ResolveEffectiveLane(groupLaneIndex, authoredSpawnIndex, baseDeckIndex + localIndex, lanes)));
            localIndex++;
        }

        // wave-concept-blocks unit 2 — 컨셉이 lane 을 지정했으면(≥0) 그 값을 쓰고, 무지정(-1)이면
        // 기존 «펼침 순번 % lane 수» 규칙을 그대로 쓴다.
        private static int ResolveAuthoredLane(int groupLaneIndex, int localIndex, int lanes)
            => groupLaneIndex >= 0
                ? math.clamp(groupLaneIndex, 0, lanes - 1)
                : localIndex % lanes;

        // 컨셉이 지정한 lane 은 EffectiveSpawnIndex 를 **우회**한다. 그 함수는 laneCount ≥ 3 에서
        // authored 값을 버리고 deckIndex 라운드로빈으로 돌리므로, 통과시키면 저작한 lane 이
        // 3레인 이상 맵에서 조용히 지워진다.
        private static int ResolveEffectiveLane(
            int groupLaneIndex, int authoredSpawnIndex, int deckIndex, int lanes)
            => groupLaneIndex >= 0
                ? math.clamp(groupLaneIndex, 0, lanes - 1)
                : EffectiveSpawnIndex(authoredSpawnIndex, deckIndex, lanes);

        // PerGroupTimeline 용 — 절대 시각을 직접 받아 엔트리 추가. lane 은 전역 localIndex 기준.
        private static void AddEntryAt(
            List<ExpandedWaveSpawn> entries,
            AttackUnitData unit,
            int swarmIndex,
            float timeSec,
            int laneCount,
            int baseDeckIndex,
            int groupLaneIndex,
            ref int localIndex)
        {
            int lanes = math.max(1, laneCount);
            int authoredSpawnIndex = ResolveAuthoredLane(groupLaneIndex, localIndex, lanes);
            entries.Add(new ExpandedWaveSpawn(
                new SpawnEntry
                {
                    triggerTimeSec = timeSec,
                    unitType = unit,
                    spawnIndex = authoredSpawnIndex,
                },
                swarmIndex,
                ResolveEffectiveLane(groupLaneIndex, authoredSpawnIndex, baseDeckIndex + localIndex, lanes)));
            localIndex++;
        }

        // wave-concept-blocks unit 2 — 종류별 동시 등장 상한의 N슬롯 일반화.
        //
        // 2슬롯 ref 버전(위)의 규칙을 그대로 잇는다: 잘린 몫은 **여유 있는 슬롯으로 넘겨 총량을
        // 보존**하고, 전부 상한이면 총량이 줄어든다(그게 상한의 목적이다). rng 를 소비하지 않는
        // 것도 계약 그대로 — 소비하면 상한을 저작하지 않은 덱의 스트림까지 흔들린다.
        public static void ClampGroupCounts(int[] maxPerSlot, int[] counts, int slotCount)
        {
            if (maxPerSlot == null || counts == null) return;
            int n = math.min(slotCount, math.min(maxPerSlot.Length, counts.Length));

            int leftover = 0;
            for (int i = 0; i < n; i++)
            {
                int max = maxPerSlot[i];
                if (max > 0 && counts[i] > max) { leftover += counts[i] - max; counts[i] = max; }
            }
            if (leftover <= 0) return;

            for (int i = 0; i < n && leftover > 0; i++)
            {
                int max = maxPerSlot[i];
                int room = max > 0 ? math.max(0, max - counts[i]) : leftover;
                int give = math.min(leftover, room);
                counts[i] += give;
                leftover -= give;
            }
        }

        private static bool HasAnyConcept(IReadOnlyList<WaveConceptData> pool)
        {
            if (pool == null) return false;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null) return true;
            return false;
        }

        // wave-concept-blocks unit 2 — 블록 경계에서 컨셉 하나를 확정한다.
        // 슬롯 배열과 lane 배정을 함께 내놓아 그 블록의 모든 웨이브(+보스 후처리)가 같은 값을 쓴다.
        // null 반환 = 폴백(구조적 레거시 편성).
        //
        // rng 소비: PickConcept 1회(NextFloat) + AssignLanes 1회(NextInt). 슬롯이 없으면
        // AssignLanes 를 부르지 않으므로 NextInt 도 소비하지 않는다 — 이 순서가 결정론의 일부다.
        private static WaveConceptData ResolveBlockConcept(
            IReadOnlyList<WaveConceptData> conceptPool,
            int blockFirstWaveNumber,
            int laneCount,
            WaveConceptData previousConcept,
            ref Unity.Mathematics.Random rng,
            out WaveConceptSlot[] slots,
            out int[] lanes,
            out WaveConceptSlot[] variantSlots,
            out int[] variantLanes)
        {
            slots = null;
            lanes = null;
            variantSlots = null;
            variantLanes = null;

            var concept = PickConcept(
                conceptPool, blockFirstWaveNumber, laneCount, previousConcept, rng.NextFloat());
            if (concept == null) return null;

            var effectiveSlots = CollectSlots(concept);
            if (effectiveSlots.Length == 0) return null;   // 슬롯이 없으면 편성을 만들 수 없다

            var laneGroups = new int[effectiveSlots.Length];
            for (int s = 0; s < effectiveSlots.Length; s++) laneGroups[s] = effectiveSlots[s].laneGroup;

            var assigned = new int[effectiveSlots.Length];
            // AssignLanes 가 false 면 PickConcept 의 게이트가 놓친 경우다(있어선 안 됨).
            // 조용히 lane 을 무지정으로 떨어뜨리면 «저작한 lane 이 사라지는» 침묵이 되므로
            // 컨셉 자체를 버리고 폴백으로 간다(계약 5 의 «침묵 금지»와 같은 판단).
            if (!AssignLanes(laneGroups, laneCount, rng.NextInt(), assigned))
            {
                Debug.LogWarning(
                    $"[WavePatternGenerator] 컨셉 '{concept.id}' 가 요구하는 lane 수" +
                    $"({concept.RequiredLaneCount})가 맵 스폰 수({laneCount})를 넘어 폴백한다.");
                return null;
            }

            slots = effectiveSlots;
            lanes = assigned;

            // wave-pull-revival unit 2 — 가운데 웨이브 편성 = **본 편성 + 끼어드는 슬롯**.
            //
            // 교체가 아니라 삽입인 이유: 교체하면 가운데 웨이브에 블록의 성격이 통째로 사라져
            // 「배우고 → 대응하고 → 겨우 버티고」의 압력 상승이 끊긴다. 삽입이면 탱커 블록은
            // 계속 탱커 블록이면서 「지금 당기면 탱커 위에 저격수가 얹힌다」가 생긴다.
            //
            // **rng 를 새로 쓰지 않는다**: 입구는 위에서 확정한 배정을 물려받으므로(계약 5)
            // AssignLanes 를 다시 부르지 않는다. 덕분에 변주를 저작하지 않은 컨셉은 rng 소비가
            // 완전히 동일하고, 저작한 컨셉만 편성이 달라진다.
            var extra = CollectSlots(concept, variant: true);
            if (extra.Length > 0)
            {
                var extraLanes = InheritLanes(extra, effectiveSlots, assigned);
                variantSlots = Concat(effectiveSlots, extra);
                variantLanes = Concat(assigned, extraLanes);
            }
            return concept;
        }

        private static T[] Concat<T>(T[] a, T[] b)
        {
            var result = new T[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }

        // 변주 슬롯의 입구 = 블록이 이미 확정한 배정. 같은 laneGroup 이 본 편성에 있으면 그
        // 입구를 쓰고, 없으면(또는 무지정) 본 편성의 입구를 순서대로 재사용한다.
        // 새로 뽑지 않는 것이 계약이다 — 블록 안에서 입구가 바뀌면 보강 결정이 보상받지 못한다.
        private static int[] InheritLanes(
            WaveConceptSlot[] variants, WaveConceptSlot[] mainSlots, int[] mainLanes)
        {
            var result = new int[variants.Length];
            for (int v = 0; v < variants.Length; v++)
            {
                int group = variants[v].laneGroup;
                if (group < 0)
                {
                    // 무지정(-1)은 무지정으로 남긴다. 구체 lane 으로 못박으면 「전 lane 분산」이라
                    // 저작한 슬롯이 한 입구로 몰려 저작 의미가 뒤집힌다(AssignLanes 의 -1 계약).
                    result[v] = -1;
                    continue;
                }
                int lane = -1;
                for (int m = 0; m < mainSlots.Length; m++)
                    if (mainSlots[m].laneGroup == group) { lane = mainLanes[m]; break; }
                result[v] = lane >= 0 ? lane : mainLanes[v % mainLanes.Length];
            }
            return result;
        }

        private static WaveConceptSlot[] CollectSlots(WaveConceptData concept, bool variant = false)
        {
            var source = variant ? concept.variantSlots : concept.slots;
            if (source == null) return Array.Empty<WaveConceptSlot>();
            int count = 0;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null) count++;
            if (count == 0) return Array.Empty<WaveConceptSlot>();

            var result = new WaveConceptSlot[count];
            int w = 0;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null) result[w++] = source[i];
            return result;
        }

        // wave-concept-blocks unit 2/3 — 슬롯 명세 + 예산 → WaveSpawnGroup 목록.
        // 일반 웨이브와 보스 호위가 **같은 규칙**을 쓰게 하는 단일 지점이다(두 곳에 두면 갈린다).
        //
        // 버퍼를 재사용하지 않고 매 웨이브 작은 배열을 새로 잡는다: 생성은 판당 1회(100웨이브)라
        // 재사용이 사는 자리가 아니고, 버퍼를 밖에서 들고 다니면 호출자마다 크기 관리가 새어나온다.
        private static List<WaveSpawnGroup> BuildConceptGroups(
            List<AttackUnitData> pool,
            WaveConceptSlot[] slots,
            int[] lanes,
            int total,
            float countMul,
            int maxUnits,
            int waveNumber,
            string conceptId,
            ref Unity.Mathematics.Random rng)
        {
            int slotCount = slots.Length;
            var counts = new int[slotCount];
            var maxPerSlot = new int[slotCount];
            var chosen = new List<int>(slotCount);
            var candidates = new List<int>(pool.Count);

            DistributeSlotCounts(total, countMul, slotCount, maxUnits, counts);

            for (int s = 0; s < slotCount; s++)
            {
                int picked = PickSlotUnitIndex(
                    pool, slots[s], waveNumber, chosen, candidates, conceptId, ref rng);
                chosen.Add(picked);
                maxPerSlot[s] = picked >= 0 && pool[picked] != null ? pool[picked].maxPerWave : 0;
            }

            ClampGroupCounts(maxPerSlot, counts, slotCount);

            var groups = new List<WaveSpawnGroup>(slotCount);
            for (int s = 0; s < slotCount; s++)
            {
                int picked = chosen[s];
                if (picked < 0 || counts[s] <= 0) continue;
                groups.Add(new WaveSpawnGroup(pool[picked], counts[s], 0f, lanes[s]));
            }
            return groups;
        }

        // wave-concept-blocks unit 2 — 슬롯 하나가 뽑을 유닛의 pool 인덱스.
        //
        // 완화 순서가 설계다(계약 5 fail-open). 가장 덜 해로운 것부터 버린다:
        //   1) class + altitude + 등장게이트 + 중복배제   ← 정상
        //   2) 중복배제를 버린다        — 같은 종이 두 슬롯에 들어간다. 무해
        //   3) 등장게이트를 버린다      — 후반 적이 초반에 나온다. 국소적
        //   4) class 를 버린다          — 컨셉이 정체성을 잃는다. 경고
        //   5) altitude 까지 버린다     — **마지막이다.** 지상 컨셉에 비행이 섞이면 대공 없이
        //                                막을 수 없는 적이 나온다(계약 10). 큰 경고와 함께만.
        // altitude 를 마지막에 두는 것이 「평소」가 웨이브 1~3 에 비행을 내놓지 않는 구조적 보장이다.
        private static int PickSlotUnitIndex(
            List<AttackUnitData> pool,
            WaveConceptSlot slot,
            int waveNumber,
            List<int> alreadyChosen,
            List<int> candidateBuffer,
            string conceptId,
            ref Unity.Mathematics.Random rng)
        {
            if (pool == null || pool.Count == 0) return -1;

            // 1) 정상 — 중복배제 + 등장게이트 + class + altitude
            if (CollectSlotCandidates(pool, slot, waveNumber, alreadyChosen, true, true, candidateBuffer) == 0
                // 2) 중복배제를 버린다 — 같은 종이 두 슬롯에 들어간다. 무해
                && CollectSlotCandidates(pool, slot, waveNumber, null, true, true, candidateBuffer) == 0
                // 3) 등장게이트를 버린다 — 후반 적이 초반에 나온다. 국소적
                && CollectSlotCandidates(pool, slot, 0, null, true, true, candidateBuffer) == 0)
            {
                // 4) class 를 버린다 — 컨셉이 정체성을 잃는다. 경고
                if (CollectSlotCandidates(pool, slot, 0, null, false, true, candidateBuffer) > 0)
                {
                    Debug.LogWarning(
                        $"[WavePatternGenerator] 컨셉 '{conceptId}' 슬롯의 성질 필터" +
                        $"({slot.classFilter})에 맞는 적이 pool 에 없어 성질을 무시했다. 컨셉이 흐려진다.");
                }
                // 5) altitude 까지 버린다 — **마지막이다.** 지상 컨셉에 비행이 섞이면 대공 없이
                //    막을 수 없는 적이 나온다(계약 10). 큰 경고와 함께만.
                else if (CollectSlotCandidates(pool, slot, 0, null, false, false, candidateBuffer) > 0)
                {
                    Debug.LogWarning(
                        $"[WavePatternGenerator] 컨셉 '{conceptId}' 슬롯의 고도({slot.altitude})에 맞는 적이" +
                        " pool 에 없어 **고도까지 무시**했다 — 지상 컨셉에 비행이 섞일 수 있다." +
                        " pool 에 해당 고도의 적을 넣어라.");
                }
                else return -1;   // pool 이 통째로 비었다
            }

            return candidateBuffer[rng.NextInt(0, candidateBuffer.Count)];
        }

        private static int CollectSlotCandidates(
            List<AttackUnitData> pool,
            WaveConceptSlot slot,
            int waveNumber,
            List<int> exclude,
            bool applyClassFilter,
            bool applyAltitudeFilter,
            List<int> outIndices)
        {
            outIndices.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                var unit = pool[i];
                if (unit == null) continue;
                if (exclude != null && exclude.Contains(i)) continue;
                if (waveNumber > 0 && unit.minWaveNumber > waveNumber) continue;
                if (applyClassFilter && slot.classFilter != EnemyClass.None
                    && unit.enemyClass != slot.classFilter) continue;
                if (applyAltitudeFilter && !MatchesAltitude(unit, slot.altitude)) continue;
                outIndices.Add(i);
            }
            return outIndices.Count;
        }

        // 고도 판정의 단일 지점. Air = 통행층에 Air 비트가 있다, Ground = 없다.
        public static bool MatchesAltitude(AttackUnitData unit, SlotAltitude altitude)
        {
            if (unit == null) return false;
            bool isAir = (unit.EffectiveTraversalLayers & PlacementLayer.Air) != 0;
            return altitude == SlotAltitude.Air ? isAir : !isAir;
        }

        // null source 허용 — 보스 pool 은 미저작(null)이 정상이다. 잡몹 pool 경로는 상류
        // (Generate 진입부)에서 이미 ArgumentNullException 을 던지므로 영향 없다.
        private static List<AttackUnitData> BuildDistinctPool(IReadOnlyList<AttackUnitData> source)
        {
            var result = new List<AttackUnitData>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                var unit = source[i];
                if (unit == null || result.Contains(unit)) continue;
                result.Add(unit);
            }
            return result;
        }

        // boss-jjangssen unit 0 — 보스 후보 확정. bossPool 이 실질 원소를 가지면 그것을(null/중복 제거),
        // 아니면 bossUnit 단일로 폴백한다. 폴백을 여기 둔 이유는 덱 경로와 직접 호출 경로가 같은 규칙을
        // 받아야 하기 때문이다 — 덱에도 폴백을 두면 두 곳이 드리프트한다.
        // 순서 보존이 계약이다: 보스 선택이 index 기반이므로 authoring 순서가 곧 결정론의 입력이다.
        private static List<AttackUnitData> BuildBossPool(
            IReadOnlyList<AttackUnitData> bossPool, AttackUnitData bossUnit)
        {
            // distinct 규칙(순서 보존 + null 제거)은 BuildDistinctPool 단일 소스에 둔다 —
            // 두 곳에 복제하면 한쪽만 바뀌어 조용히 갈린다. 여기 고유한 것은 폴백뿐이다.
            var result = BuildDistinctPool(bossPool);
            if (result.Count == 0 && bossUnit != null)
                result.Add(bossUnit);
            return result;
        }
    }
}
