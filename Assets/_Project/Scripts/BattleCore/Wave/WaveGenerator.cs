// salvaged from Assets/_Project/Scripts/Data/WavePatternGenerator.cs (battle-core-rebuild unit 4)
//
// 이식 시 바뀐 것 — **셋뿐이고, 셋 다 rng 를 건드리지 않는다**:
//   ① 입력이 SO(`AttackDeck`·`WaveConceptData`·`AttackUnitData`)에서 plain(`WaveDeckDef`·
//      `WaveConceptDef`·`EnemyDef[]`)으로. 풀의 원소는 SO 참조가 아니라 **인덱스**다.
//   ② `UnityEngine.Mathf`/`Debug.LogWarning` → `Unity.Mathematics.math` / `Action<string> report`.
//      (`report` 가 null 이면 버린다 — 코어는 로거를 소유하지 않는다.)
//   ③ 반환 타입 이름(`GeneratedWavePlan` → `WavePlan` 등). Unity 층이 두 어휘를 동시에 보기 때문.
//
// ⚠⚠ **rng 소비 순서가 계약이다.** 이 파일에서 `NextInt`/`NextFloat` 의 **횟수와 차례**를
// 한 번이라도 바꾸면 같은 시드가 다른 편성을 낸다 — 라이브 6개 맵의 난이도 곡선이 통째로
// 밀리고, 증상은 「웨이브를 안 건드렸는데 판이 달라졌다」로만 보인다. 원본이 곳곳에 세워 둔
// byte-identical 장치를 그대로 옮겼다:
//   · 보스가 1종이면 선택 rng 를 **소비하지 않는다**
//   · 램프 지터는 `NextFloat` 1콜 = 옛 `NextInt` 1콜
//   · 등장 게이트·동시 등장 상한·슬롯 분배는 **순수**(rng 미소비)
//   · 컨셉 풀이 비면 rng 를 한 번도 안 쓰고 레거시 2종 분기만 돈다
// 그 순서를 증언하는 것이 `WaveGeneratorRngOrderTests` 의 오라클이다.
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore.Map;
// `System.Random` 과 이름이 겹친다. 플랫폼·런타임 버전에 매인 그쪽은 결정론 보장이 없으므로
// **이 별칭이 그 혼동을 구조적으로 막는다** — 이 파일에서 `Random` 은 xorshift 값 타입뿐이다.
using Random = Unity.Mathematics.Random;

namespace Wassup.BattleCore.Wave
{
    public static class WaveGenerator
    {
        /// <summary>펼침 순번 규약. 예보와 실스폰이 이 상수를 **공유**해야 둘이 갈리지 않는다.</summary>
        public const int DeckIndexStride = 1000;

        /// <summary>
        /// 시드 생성. `seed` 는 이미 파생된 웨이브 시드다(덱의 고정 오버라이드 판정은 호출측).
        /// `laneCount` 는 맵의 스폰 수 — 브리핑과 런타임이 다른 값을 쓰면 예고와 실스폰이 갈린다.
        /// </summary>
        public static WavePlan Generate(in WaveDeckDef deck, EnemyDef[] enemies, int seed,
                                        int laneCount, Action<string> report = null)
        {
            var pool = BuildDistinctPool(deck.EnemyPool, enemies);
            // 폴백 소유자는 생성기다 — 덱을 안 거치는 직접 호출자도 같은 규칙을 받아야 하고,
            // 두 곳에 두면 드리프트한다.
            var bosses = BuildBossPool(deck.BossPool, enemies);
            // 보스는 잡몹 풀과 분리가 계약이다. 실수로 섞여도 방어적으로 제외해 비-보스 웨이브의
            // 보스 오발화와 호위 보스 중복을 원천 차단한다. 없으면 no-op → 현행 불변.
            for (int b = 0; b < bosses.Count; b++)
            {
                int at = pool.IndexOf(bosses[b]);
                if (at < 0) continue;
                pool.RemoveAt(at);
                report?.Invoke($"[WaveGenerator] 보스 '{Id(enemies, bosses[b])}' 가 잡몹 풀에 들어 있어 생성 풀에서 제외했다. 덱에서 풀과 보스를 분리하라.");
            }
            if (pool.Count < 2)
                throw new ArgumentException(
                    "웨이브 생성은 서로 다른 적 2종 이상을 요구한다.", nameof(deck));

            int resolvedSeed = seed != 0 ? seed : 1;
            uint rngSeed = (uint)math.abs(resolvedSeed);
            if (rngSeed == 0u) rngSeed = 1u;
            var rng = new Random(rngSeed);

            int minWaves = math.max(2, math.min(deck.MinWaveCount, deck.MaxWaveCount));
            int maxWaves = math.max(minWaves, math.max(deck.MinWaveCount, deck.MaxWaveCount));
            int waveCount = rng.NextInt(minWaves, maxWaves + 1);

            int minUnits = math.max(2, math.min(deck.MinUnitsPerWave, deck.MaxUnitsPerWave));
            int maxUnits = math.max(minUnits, math.max(deck.MinUnitsPerWave, deck.MaxUnitsPerWave));

            float duration = deck.TimerDurationSec > 0f ? deck.TimerDurationSec : 180f;
            // 상한 간격(>0)이 명목 그리드를 정한다. 런타임은 이 시각을 읽지 않는다
            // (전멸/상한 이벤트 구동) — 남기는 이유는 브리핑이 「최악 케이스 시각」으로 읽는 것뿐.
            float interval = deck.MaxWaveIntervalSec > 0f
                ? deck.MaxWaveIntervalSec
                : (waveCount > 0 ? duration / waveCount : 0f);
            float spacing = deck.IntraWaveSpacingSec > 0f ? deck.IntraWaveSpacingSec : 0.35f;
            // 스폰 창 불변식 — 위반하면 대기열이 영영 안 비어 「전멸 즉시 진행」이 죽는다.
            // 증상이 「웨이브가 항상 상한 간격으로만 온다」라 원인 추적이 매우 어렵다.
            if (deck.MaxWaveIntervalSec > 0f)
            {
                float window = math.max(0f, deck.SpawnLeadInSec) + (maxUnits - 1) * spacing;
                if (window >= deck.MaxWaveIntervalSec)
                    report?.Invoke(
                        $"[WaveGenerator] 스폰 창 {window:0.##}s 가 상한 간격 {deck.MaxWaveIntervalSec:0.##}s 이상이다. "
                        + "전멸 즉시 진행이 성립하지 않는다 — 내부 간격을 내리거나 웨이브 최대 수량을 줄여라.");
            }

            // 컨셉은 웨이브가 아니라 **블록**의 속성이다. 블록 경계에서만 컨셉과 입구 배정을
            // 뽑고 `ConceptHoldWaves` 웨이브 동안 재사용한다. 풀이 비면 컨셉이 영영 null 이라
            // rng 를 한 번도 안 쓰고 아래 레거시 분기만 돈다.
            int holdWaves = math.max(1, deck.ConceptHoldWaves);
            int lanes = math.max(1, laneCount);
            bool hasConceptPool = HasAnyConcept(deck.Concepts);
            if (laneCount <= 0)
                report?.Invoke("[WaveGenerator] laneCount 가 0 이하다 — 1 로 폴백한다. "
                               + "라이브 경로는 맵의 스폰 수를 넘겨야 예고와 실스폰이 일치한다.");

            int concept = -1;
            int previousBlock = -1;
            // break 필드 하나가 클라이맥스 전체(곡선 전환·변주 상시·변주 신규 레인)의 게이트다.
            bool rampActive = deck.RampBreakWave >= 2 && deck.RampBreakUnits > 0;

            // 보스 후처리가 그 웨이브의 블록 컨셉을 읽는다. 후처리를 이 루프 안으로 옮기지
            // 않는 이유는 rng 소비 순서다 — 옮기면 컨셉 없는 덱의 스트림까지 흔들린다.
            var conceptByWave = new int[waveCount];
            var conceptSlotsByWave = new WaveSlotDef[waveCount][];
            var conceptLanesByWave = new int[waveCount][];
            WaveSlotDef[] blockSlots = null;
            int[] blockLanes = null;
            WaveSlotDef[] blockVariantSlots = null;
            int[] blockVariantLanes = null;

            var waves = new PlannedWave[waveCount];
            for (int i = 0; i < waveCount; i++)
            {
                if (hasConceptPool && i / holdWaves != previousBlock)
                {
                    int block = i / holdWaves;
                    previousBlock = block;
                    concept = ResolveBlockConcept(
                        deck.Concepts, block * holdWaves + 1, lanes, concept, rampActive, report,
                        ref rng, out blockSlots, out blockLanes,
                        out blockVariantSlots, out blockVariantLanes);
                }

                // 블록의 **두 번째** 웨이브만 변주를 입는다. 첫 웨이브는 성격을 가르치는 자리고
                // 마지막은 그 성격의 시험대다. 클라이맥스(break 이후)는 **매 웨이브**가 변주다.
                bool inClimax = rampActive && i + 1 >= deck.RampBreakWave;
                bool useVariant = (inClimax || (holdWaves >= 3 && i % holdWaves == 1))
                                  && blockVariantSlots != null && blockVariantSlots.Length > 0;
                var waveSlots = useVariant ? blockVariantSlots : blockSlots;
                var waveLanes = useVariant ? blockVariantLanes : blockLanes;

                conceptByWave[i] = concept;
                conceptSlotsByWave[i] = waveSlots;
                conceptLanesByWave[i] = waveLanes;

                if (concept >= 0)
                {
                    ref var c = ref deck.Concepts[concept];
                    int conceptTotal = ExponentialWaveTotal(
                        i, minUnits, maxUnits, deck.UnitGrowthPerWave, deck.WaveCountJitter,
                        rng.NextFloat(), deck.RampBreakWave, deck.RampBreakUnits);
                    var conceptGroups = BuildConceptGroups(
                        pool, enemies, waveSlots, waveLanes, conceptTotal, c.CountMul,
                        maxUnits, i + 1, c.Id, report, ref rng);

                    waves[i] = new PlannedWave(
                        i, i * interval, conceptGroups, 0f, WaveLayout.RoundRobin, c.DisplayName);
                    continue;
                }

                // ---- 레거시 2종 경로 (컨셉 없음) ----
                // 여기는 손대지 않는다. 컨셉 풀이 빈 덱이 **현행과 byte-identical** 해야
                // 무회귀 경로가 데이터로 성립한다(rng 소비 순서까지 동일).
                int aIndex = rng.NextInt(0, pool.Count);
                int bIndex = rng.NextInt(0, pool.Count - 1);
                if (bIndex >= aIndex) bIndex++;

                // 등장 게이트. 뽑은 인덱스만 **사후 보정**하므로 rng 소비가 불변이다.
                int waveNumber = i + 1;
                aIndex = ResolveWaveEligibleIndex(pool, enemies, aIndex, waveNumber);
                bIndex = ResolveWaveEligibleIndex(pool, enemies, bIndex, waveNumber, aIndex);

                // 수량 램프. `NextFloat` 1콜은 옛 `NextInt` 1콜과 소비 수가 같아 아래
                // countA·보스 후처리의 정렬이 불변이다.
                float jitter01 = rng.NextFloat();
                int total = ExponentialWaveTotal(i, minUnits, maxUnits, deck.UnitGrowthPerWave,
                                                 deck.WaveCountJitter, jitter01,
                                                 deck.RampBreakWave, deck.RampBreakUnits);
                int countA = rng.NextInt(1, total);
                int countB = total - countA;

                // 종류별 동시 등장 상한. rng 를 소비하지 않으므로 상한 미저작이면 byte-identical.
                ClampGroupCounts(MaxPerWave(enemies, pool[aIndex]), MaxPerWave(enemies, pool[bIndex]),
                                 ref countA, ref countB);

                waves[i] = new PlannedWave(i, i * interval, new[]
                {
                    new PlannedGroup(pool[aIndex], countA),
                    new PlannedGroup(pool[bIndex], countB),
                });
            }

            // 매 `BossWaveInterval` 번째 웨이브를 보스×1(선봉) + 잡몹×[min,max] 로 치환.
            // 랜덤 루프 **뒤** 후처리라 비-보스 웨이브의 rng 소비가 현행과 byte-identical 이다.
            if (bosses.Count > 0 && deck.BossWaveInterval > 0)
            {
                int escortMin = math.max(1, math.min(deck.BossEscortMin, deck.BossEscortMax));
                int escortMax = math.max(escortMin, math.max(deck.BossEscortMin, deck.BossEscortMax));
                for (int i = 0; i < waves.Length; i++)
                {
                    if ((i + 1) % deck.BossWaveInterval != 0) continue;
                    // 보스가 1종이면 rng 를 **소비하지 않는다**. 라이브 덱이 전부 단일 보스라
                    // 이 가드가 스트림을 byte-identical 하게 유지한다. 2종+ 부터 1콜.
                    int boss = bosses.Count == 1 ? bosses[0] : bosses[rng.NextInt(0, bosses.Count)];
                    int escortCount = rng.NextInt(escortMin, escortMax + 1);

                    int blockConcept = conceptByWave[i];
                    var blockConceptSlots = conceptSlotsByWave[i];
                    var blockConceptLanes = conceptLanesByWave[i];

                    PlannedGroup[] groups;
                    if (blockConcept >= 0 && blockConceptSlots != null && blockConceptSlots.Length > 0)
                    {
                        // ⚠ 호위 예산에는 `CountMul` 을 적용하지 않는다(1f). min/max 가 이미
                        // 예산이라 배율을 다시 곱하면 이중 스케일이다 — 「중장」(0.4)이면
                        // 3 × 0.4 = 1.2 로 하한에 먹혀 컨셉마다 호위 수가 제멋대로 달라진다.
                        var escort = BuildConceptGroups(
                            pool, enemies, blockConceptSlots, blockConceptLanes, escortCount, 1f,
                            maxUnits, i + 1, deck.Concepts[blockConcept].Id, report, ref rng);
                        groups = new PlannedGroup[escort.Length + 1];
                        // 선봉: RoundRobin round 0 = 보스 먼저. 입구는 컨셉 첫 슬롯을 따른다 —
                        // 보스가 서 있는 쪽이 «본대»로 읽힌다.
                        groups[0] = new PlannedGroup(boss, 1, 0f, blockConceptLanes[0]);
                        Array.Copy(escort, 0, groups, 1, escort.Length);
                    }
                    else
                    {
                        // 컨셉 없는 덱 — 여기는 손대지 않는다. rng 소비까지 현행 그대로.
                        int escortType = pool[ResolveWaveEligibleIndex(
                            pool, enemies, rng.NextInt(0, pool.Count), i + 1)];
                        // 호위가 더 위험하다: 일반 웨이브는 종류가 둘로 나뉘지만 호위는
                        // 한 종류가 통째로 3~4기다.
                        int cap = MaxPerWave(enemies, escortType);
                        if (cap > 0) escortCount = math.min(escortCount, cap);
                        groups = new[]
                        {
                            new PlannedGroup(boss, 1),
                            new PlannedGroup(escortType, escortCount),
                        };
                    }

                    waves[i] = new PlannedWave(
                        i, i * interval, groups, 0f, WaveLayout.RoundRobin,
                        blockConcept >= 0 ? deck.Concepts[blockConcept].DisplayName : "",
                        isBoss: true);
                }
            }

            // 리드인은 플랜에 실려 **스폰 기준시각에서만** 쓰인다. 위 그리드는 불변이다(X10).
            return new WavePlan
            {
                Seed = resolvedSeed,
                GeneratorVersion = deck.GeneratorVersion,
                TimerDurationSec = duration,
                WaveIntervalSec = interval,
                IntraWaveSpacingSec = spacing,
                SpawnLeadInSec = math.max(0f, deck.SpawnLeadInSec),
                Waves = waves,
            };
        }

        /// <summary>
        /// 저작 플랜 → 런타임 플랜. 각 웨이브는 `DurationSec` 만큼의 구간이고, 웨이브 i 의
        /// 절대 시작 = 앞 웨이브 `DurationSec` 의 합이다. `Seed`/`GeneratorVersion` 0 = 비-시드 마커.
        ///
        /// 리드인은 **0** 이다 — 저작 플랜은 그룹 상대 시각으로 같은 표현이 가능해서
        /// 덱 값을 겹쳐 주면 이중 가산이 된다(X10 의 짝).
        /// </summary>
        public static WavePlan FromAuthored(in WavePlanDef plan)
        {
            int count = plan.Waves != null ? plan.Waves.Length : 0;
            var waves = new PlannedWave[count];
            float cumulativeStart = 0f;
            for (int i = 0; i < count; i++)
            {
                var aw = plan.Waves[i];
                var src = aw.Groups;
                int n = 0;
                if (src != null)
                    for (int g = 0; g < src.Length; g++)
                        if (src[g].EnemyIndex >= 0 && src[g].Count > 0) n++;

                var groups = new PlannedGroup[n];
                int w = 0;
                if (src != null)
                    for (int g = 0; g < src.Length; g++)
                    {
                        var grp = src[g];
                        if (grp.EnemyIndex < 0 || grp.Count <= 0) continue;
                        groups[w++] = new PlannedGroup(grp.EnemyIndex, grp.Count,
                            math.max(0f, grp.TriggerTimeSec), grp.LaneIndex, grp.PathIndex);
                    }

                waves[i] = new PlannedWave(i, cumulativeStart, groups,
                    math.max(0f, aw.IntervalSec), WaveLayout.Timeline);
                cumulativeStart += math.max(0f, aw.DurationSec);
            }

            return new WavePlan
            {
                Seed = 0,
                GeneratorVersion = 0,
                TimerDurationSec = plan.TimerDurationSec,
                WaveIntervalSec = 0f,
                IntraWaveSpacingSec = 0f,
                SpawnLeadInSec = 0f,
                Waves = waves,
            };
        }

        // ── 펼침 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 웨이브 하나를 스폰 목록으로 편다.
        ///   · `RoundRobin` — round 0,1,2… 마다 그룹 순서로 1기씩, `intraWaveSpacingSec` 간격.
        ///   · `Timeline` — 그룹마다 상대 시각부터 `SpawnIntervalSec` 간격.
        /// </summary>
        public static void Expand(in PlannedWave wave, float baseTriggerTimeSec, int laneCount,
                                  float intraWaveSpacingSec, List<PlannedSpawn> into)
        {
            into.Clear();
            var groups = wave.Groups;
            if (groups == null || groups.Length == 0) return;

            int localIndex = 0;
            int baseDeckIndex = wave.WaveIndex * DeckIndexStride;
            int lanes = math.max(1, laneCount);

            if (wave.Layout == WaveLayout.Timeline)
            {
                for (int g = 0; g < groups.Length; g++)
                {
                    var grp = groups[g];
                    if (grp.EnemyIndex < 0) continue;
                    for (int k = 0; k < grp.Count; k++)
                    {
                        float t = baseTriggerTimeSec + grp.TriggerOffsetSec + k * wave.SpawnIntervalSec;
                        Add(into, grp, g, t, lanes, baseDeckIndex, ref localIndex);
                    }
                }
                return;
            }

            int maxCount = 0;
            for (int g = 0; g < groups.Length; g++) maxCount = math.max(maxCount, groups[g].Count);
            for (int round = 0; round < maxCount; round++)
                for (int g = 0; g < groups.Length; g++)
                {
                    if (round >= groups[g].Count) continue;
                    if (groups[g].EnemyIndex < 0) continue;   // 빈 그룹은 스폰하지 않는다
                    Add(into, groups[g], g,
                        baseTriggerTimeSec + localIndex * intraWaveSpacingSec,
                        lanes, baseDeckIndex, ref localIndex);
                }
        }

        private static void Add(List<PlannedSpawn> into, in PlannedGroup grp, int swarmIndex,
                                float timeSec, int lanes, int baseDeckIndex, ref int localIndex)
        {
            int authored = ResolveAuthoredLane(grp.LaneIndex, localIndex, lanes);
            into.Add(new PlannedSpawn
            {
                TriggerTimeSec = timeSec,
                EnemyIndex = grp.EnemyIndex,
                SpawnIndex = authored,
                LaneIndex = ResolveEffectiveLane(grp.LaneIndex, authored, baseDeckIndex + localIndex, lanes),
                SwarmIndex = swarmIndex,
                PathIndex = grp.PathIndex,
            });
            localIndex++;
        }

        /// <summary>
        /// 저작 입구 번호. `laneCount <= 2` 는 저작값을 존중하고, 3+ 는 순번 기반 결정론 라운드로빈.
        /// </summary>
        public static int EffectiveSpawnIndex(int authoredIndex, int deckIndex, int laneCount)
        {
            if (laneCount <= 0) return 0;
            if (laneCount <= 2) return math.clamp(authoredIndex, 0, laneCount - 1);
            return math.abs(deckIndex) % laneCount;
        }

        private static int ResolveAuthoredLane(int groupLaneIndex, int localIndex, int lanes)
            => groupLaneIndex >= 0 ? math.clamp(groupLaneIndex, 0, lanes - 1) : localIndex % lanes;

        // 컨셉이 지정한 입구는 `EffectiveSpawnIndex` 를 **우회**한다. 그 함수는 3레인 이상에서
        // 저작값을 버리고 라운드로빈으로 돌리므로, 통과시키면 저작한 입구가 조용히 지워진다.
        private static int ResolveEffectiveLane(int groupLaneIndex, int authoredSpawnIndex,
                                                int deckIndex, int lanes)
            => groupLaneIndex >= 0
                ? math.clamp(groupLaneIndex, 0, lanes - 1)
                : EffectiveSpawnIndex(authoredSpawnIndex, deckIndex, lanes);

        // ── 순수 함수들 (rng 미소비) ──────────────────────────────────────────

        /// <summary>
        /// 웨이브 수량의 **완만한 지수 성장**.
        ///   center_i = minUnits × growth^i      (growth = 1 이면 성장 없음)
        ///   total_i  = clamp(round(center + jitter), minUnits, maxUnits)
        /// `jitter01` 은 호출측이 뽑아 넘기는 plain 입력이라 rng 를 함수에 넣지 않는다(제약 10).
        /// break 저작이 있으면 본편은 평탄 상승, break 웨이브부터 그 값을 기점으로 지수다.
        /// </summary>
        public static int ExponentialWaveTotal(int waveIndex, int minUnits, int maxUnits,
                                               float growth, int jitterBand, float jitter01,
                                               int rampBreakWave = 0, int rampBreakUnits = 0)
        {
            if (maxUnits < minUnits) { int t = minUnits; minUnits = maxUnits; maxUnits = t; }
            float g = growth > 1f ? growth : 1f;
            int i = waveIndex > 0 ? waveIndex : 0;
            float center;
            if (rampBreakWave >= 2 && rampBreakUnits > 0)
            {
                int breakIdx = rampBreakWave - 1;   // 웨이브 N = 인덱스 N−1
                float breakUnits = math.max(minUnits, rampBreakUnits);
                center = i < breakIdx
                    ? math.lerp(minUnits, breakUnits, (float)i / breakIdx)
                    : breakUnits * math.pow(g, i - breakIdx);
            }
            else
                center = minUnits * math.pow(g, i);
            // 지수 구간에서 center 가 maxUnits 를 크게 넘으면 float 이 커져 jitter 가 묻힌다.
            // 클램프를 먼저 걸어 상한 근처에서도 jitter 가 살아 있게 한다.
            center = math.min(center, maxUnits);
            float jitter = jitterBand > 0 ? (jitter01 * 2f - 1f) * jitterBand : 0f;
            return math.clamp((int)math.round(center + jitter), minUnits, maxUnits);
        }

        /// <summary>
        /// 종류별 동시 등장 상한(2슬롯). 잘린 몫은 **여유 있는 쪽으로 넘겨 총량을 보존**한다.
        /// 둘 다 상한이면 총량이 준다 — 그게 상한의 목적이라 억지로 채우지 않는다.
        /// </summary>
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

        /// <summary>N슬롯 일반화. 2슬롯 버전과 같은 규칙이고 rng 를 소비하지 않는다.</summary>
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

        /// <summary>
        /// 등장 게이트 해소. 그 적이 이 웨이브에 못 나오면 풀 순서로 다음 허용까지 순환한다.
        /// 인덱스 in → 인덱스 out 이라 rng 를 건드리지 않는다. 허용이 하나도 없으면 입력을
        /// 그대로 돌려준다 — 빈/단일 웨이브를 만드느니 게이트를 여는 fail-open 이다.
        /// </summary>
        public static int ResolveWaveEligibleIndex(List<int> pool, EnemyDef[] enemies,
                                                   int startIndex, int waveNumber,
                                                   int excludeIndex = -1)
        {
            if (pool == null || pool.Count == 0) return startIndex;

            int count = pool.Count;
            int start = ((startIndex % count) + count) % count;
            for (int step = 0; step < count; step++)
            {
                int index = (start + step) % count;
                if (index == excludeIndex) continue;
                int e = pool[index];
                if (e < 0 || e >= enemies.Length) continue;
                if (enemies[e].MinWaveNumber <= waveNumber) return index;
            }
            return startIndex;
        }

        /// <summary>
        /// 곡선 총량 → 슬롯별 개체 수. 균등 분배 + 잔여는 앞 슬롯부터.
        /// 하한이 `slotCount` 인 것이 중요하다 — 웨이브 최소 수량을 하한으로 쓰면
        /// 배율 0.4 인 컨셉이 초반 웨이브에서 하한에 먹혀 배율이 무의미해진다.
        /// 잔여를 랜덤으로 흘리지 않는 것이 계약이다(흘리면 같은 시드에서 결과가 갈린다).
        /// </summary>
        public static int DistributeSlotCounts(int total, float countMul, int slotCount,
                                               int maxUnits, int[] outCounts)
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

        /// <summary>
        /// 위상 값들 → 실제 입구 번호. 같은 위상은 반드시 같은 입구, 다른 위상은 반드시 다른 입구 —
        /// 이 두 불변식이 「원거리」의 협공을 성립시킨다. -1(무지정)은 -1 로 통과한다.
        /// false = 이 컨셉이 요구하는 입구 수가 맵의 스폰 수를 넘는다 → 호출측이 후보에서 버린다.
        /// </summary>
        public static bool AssignLanes(WaveSlotDef[] slots, int laneCount, int rollValue, int[] outLaneIndex)
        {
            int slotCount = slots != null ? slots.Length : 0;
            if (slotCount == 0) return true;
            if (laneCount <= 0) return false;

            int offset = ((rollValue % laneCount) + laneCount) % laneCount;
            int assigned = 0;
            for (int i = 0; i < slotCount; i++)
            {
                int group = slots[i].LaneGroup;
                if (group < 0)
                {
                    if (outLaneIndex != null && i < outLaneIndex.Length) outLaneIndex[i] = -1;
                    continue;
                }

                int firstSeen = -1;
                for (int j = 0; j < i; j++)
                    if (slots[j].LaneGroup == group) { firstSeen = j; break; }

                int lane;
                if (firstSeen >= 0)
                {
                    lane = outLaneIndex != null && firstSeen < outLaneIndex.Length
                        ? outLaneIndex[firstSeen] : -1;
                }
                else
                {
                    if (assigned >= laneCount) return false;   // 요구 입구 수 초과
                    lane = (offset + assigned) % laneCount;
                    assigned++;
                }
                if (outLaneIndex != null && i < outLaneIndex.Length) outLaneIndex[i] = lane;
            }
            return true;
        }

        /// <summary>
        /// 블록 경계에서 컨셉 하나를 뽑는다. 가중치 룰렛 + 게이트 3종 + 직전 배제.
        /// 직전 배제가 리듬 규칙이다 — 같은 컨셉이 두 블록 연속이면 그것이 기본값이 되어
        /// 인상이 죽는다. 배제로 후보가 0이 되면 배제를 풀고 다시 고른다(fail-open).
        /// -1 반환 = 후보 없음 → 호출측이 구조적 폴백으로 떨어진다.
        /// </summary>
        public static int PickConcept(WaveConceptDef[] pool, int blockFirstWaveNumber,
                                      int laneCount, int previousConcept, float roll01)
        {
            if (pool == null || pool.Length == 0) return -1;

            float totalWeight = SumEligibleWeight(pool, blockFirstWaveNumber, laneCount, previousConcept);
            if (totalWeight <= 0f)
            {
                previousConcept = -1;   // 배제 fail-open
                totalWeight = SumEligibleWeight(pool, blockFirstWaveNumber, laneCount, -1);
                if (totalWeight <= 0f) return -1;
            }

            float target = math.clamp(roll01, 0f, 0.9999f) * totalWeight;
            float cumulative = 0f;
            int last = -1;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!IsConceptEligible(pool, i, blockFirstWaveNumber, laneCount, previousConcept)) continue;
                last = i;
                cumulative += pool[i].Weight;
                if (cumulative > target) return i;
            }
            // float 누적 오차로 마지막 경계를 못 넘는 경우의 안전망.
            return last;
        }

        private static float SumEligibleWeight(WaveConceptDef[] pool, int waveNumber,
                                               int laneCount, int exclude)
        {
            float sum = 0f;
            for (int i = 0; i < pool.Length; i++)
                if (IsConceptEligible(pool, i, waveNumber, laneCount, exclude)) sum += pool[i].Weight;
            return sum;
        }

        private static bool IsConceptEligible(WaveConceptDef[] pool, int index, int waveNumber,
                                              int laneCount, int exclude)
        {
            if (index == exclude) return false;
            ref var c = ref pool[index];
            if (c.Weight <= 0f) return false;
            if (c.EffectiveSlotCount <= 0) return false;
            if (c.MinWaveNumber > waveNumber) return false;
            if (c.RequiredLaneCount > laneCount) return false;
            return true;
        }

        /// <summary>고도 판정의 단일 지점. `Air` = 통행층에 Air 비트가 있다.</summary>
        public static bool MatchesAltitude(in EnemyDef e, SlotAltitude altitude)
        {
            bool isAir = (e.TraversalLayers & LayerBits.Air) != 0;
            return altitude == SlotAltitude.Air ? isAir : !isAir;
        }

        // ── 내부 ─────────────────────────────────────────────────────────────

        private static string Id(EnemyDef[] enemies, int index)
            => index >= 0 && index < enemies.Length ? enemies[index].Id : "?";

        private static int MaxPerWave(EnemyDef[] enemies, int index)
            => index >= 0 && index < enemies.Length ? enemies[index].MaxPerWave : 0;

        private static bool HasAnyConcept(WaveConceptDef[] pool)
        {
            if (pool == null) return false;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].EffectiveSlotCount > 0) return true;
            return false;
        }

        // 블록 경계에서 컨셉 하나를 확정한다. 슬롯 배열과 입구 배정을 함께 내놓아 그 블록의
        // 모든 웨이브(+보스 후처리)가 같은 값을 쓴다. -1 반환 = 구조적 폴백.
        //
        // rng 소비: `PickConcept` 1회(NextFloat) + `AssignLanes` 1회(NextInt). 슬롯이 없으면
        // `AssignLanes` 를 부르지 않으므로 NextInt 도 소비하지 않는다 — 이 순서가 결정론의 일부다.
        private static int ResolveBlockConcept(
            WaveConceptDef[] conceptPool, int blockFirstWaveNumber, int laneCount,
            int previousConcept, bool openVariantLanes, Action<string> report,
            ref Random rng,
            out WaveSlotDef[] slots, out int[] lanes,
            out WaveSlotDef[] variantSlots, out int[] variantLanes)
        {
            slots = null;
            lanes = null;
            variantSlots = null;
            variantLanes = null;

            int concept = PickConcept(conceptPool, blockFirstWaveNumber, laneCount,
                                      previousConcept, rng.NextFloat());
            if (concept < 0) return -1;

            var effectiveSlots = conceptPool[concept].Slots;
            if (effectiveSlots == null || effectiveSlots.Length == 0) return -1;

            var assigned = new int[effectiveSlots.Length];
            // false 면 `PickConcept` 의 게이트가 놓친 경우다(있어선 안 됨). 조용히 입구를
            // 무지정으로 떨어뜨리면 «저작한 입구가 사라지는» 침묵이 되므로 컨셉을 버린다.
            if (!AssignLanes(effectiveSlots, laneCount, rng.NextInt(), assigned))
            {
                report?.Invoke(
                    $"[WaveGenerator] 컨셉 '{conceptPool[concept].Id}' 가 요구하는 입구 수"
                    + $"({conceptPool[concept].RequiredLaneCount})가 맵 스폰 수({laneCount})를 넘어 폴백한다.");
                return -1;
            }

            slots = effectiveSlots;
            lanes = assigned;

            // 가운데 웨이브 편성 = **본 편성 + 끼어드는 슬롯**. 교체가 아니라 삽입인 이유:
            // 교체하면 가운데 웨이브에서 블록의 성격이 통째로 사라져 압력 상승이 끊긴다.
            //
            // **rng 를 새로 쓰지 않는다**: 입구는 위에서 확정한 배정을 물려받는다. 덕분에
            // 변주를 저작하지 않은 컨셉은 rng 소비가 완전히 동일하다.
            var extra = conceptPool[concept].VariantSlots;
            if (extra != null && extra.Length > 0)
            {
                var extraLanes = InheritLanes(extra, effectiveSlots, assigned, laneCount, openVariantLanes);
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

        // 변주 슬롯의 입구 = 블록이 이미 확정한 배정. 새로 뽑지 않는 것이 계약이다 —
        // 블록 안에서 입구가 바뀌면 보강 결정이 보상받지 못한다.
        // `openNewLanes`(덱 break 게이트)가 켜지면 본 편성에 없는 위상은 **미사용 입구**를
        // 연다(낮은 번호부터, rng 무소비 결정론). off 면 기존 접힘 그대로다.
        private static int[] InheritLanes(WaveSlotDef[] variants, WaveSlotDef[] mainSlots,
                                          int[] mainLanes, int laneCount, bool openNewLanes)
        {
            var result = new int[variants.Length];
            int probe = 0;   // 미사용 입구 스캔 커서 — 앞으로만 이동(결정론)
            for (int v = 0; v < variants.Length; v++)
            {
                int group = variants[v].LaneGroup;
                if (group < 0)
                {
                    // 무지정은 무지정으로 남긴다. 구체 입구로 못박으면 「전 입구 분산」이라
                    // 저작한 슬롯이 한 입구로 몰려 저작 의미가 뒤집힌다.
                    result[v] = -1;
                    continue;
                }
                int lane = -1;
                for (int m = 0; m < mainSlots.Length; m++)
                    if (mainSlots[m].LaneGroup == group) { lane = mainLanes[m]; break; }
                if (lane < 0 && openNewLanes)
                {
                    // 앞선 변주 슬롯이 같은 위상이면 그 배정을 공유한다. **게이트 안이어야
                    // 한다** — 밖에 두면 off 덱에서 미지 위상 2+ 슬롯의 접힘이 기존과 달라져
                    // byte-identical 이 깨진다.
                    for (int p = 0; p < v; p++)
                        if (variants[p].LaneGroup == group && result[p] >= 0) { lane = result[p]; break; }
                }
                if (lane < 0 && openNewLanes)
                {
                    for (; probe < laneCount; probe++)
                    {
                        bool used = false;
                        for (int m = 0; m < mainLanes.Length && !used; m++)
                            if (mainLanes[m] == probe) used = true;
                        for (int p = 0; p < v && !used; p++)
                            if (result[p] == probe) used = true;
                        if (!used) { lane = probe++; break; }
                    }
                }
                result[v] = lane >= 0 ? lane : mainLanes[v % mainLanes.Length];
            }
            return result;
        }

        // 슬롯 명세 + 예산 → 그룹 목록. 일반 웨이브와 보스 호위가 **같은 규칙**을 쓰게 하는
        // 단일 지점이다(두 곳에 두면 갈린다).
        private static PlannedGroup[] BuildConceptGroups(
            List<int> pool, EnemyDef[] enemies, WaveSlotDef[] slots, int[] lanes,
            int total, float countMul, int maxUnits, int waveNumber, string conceptId,
            Action<string> report, ref Random rng)
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
                    pool, enemies, slots[s], waveNumber, chosen, candidates, conceptId, report, ref rng);
                chosen.Add(picked);
                maxPerSlot[s] = picked >= 0 ? MaxPerWave(enemies, pool[picked]) : 0;
            }

            ClampGroupCounts(maxPerSlot, counts, slotCount);

            int n = 0;
            for (int s = 0; s < slotCount; s++)
                if (chosen[s] >= 0 && counts[s] > 0) n++;

            var groups = new PlannedGroup[n];
            int w = 0;
            for (int s = 0; s < slotCount; s++)
            {
                int picked = chosen[s];
                if (picked < 0 || counts[s] <= 0) continue;
                groups[w++] = new PlannedGroup(pool[picked], counts[s], 0f, lanes[s], slots[s].PathIndex);
            }
            return groups;
        }

        // 슬롯 하나가 뽑을 풀 인덱스. **완화 순서가 설계다**(fail-open) — 가장 덜 해로운 것부터 버린다:
        //   1) 성질 + 고도 + 등장게이트 + 중복배제   ← 정상
        //   2) 중복배제를 버린다   — 같은 종이 두 슬롯에. 무해
        //   3) 등장게이트를 버린다 — 후반 적이 초반에. 국소적
        //   4) 성질을 버린다       — 컨셉이 정체성을 잃는다. 경고
        //   5) 고도까지 버린다     — **마지막이다.** 지상 컨셉에 비행이 섞이면 대공 없이
        //                            막을 수 없는 적이 나온다. 큰 경고와 함께만.
        private static int PickSlotUnitIndex(
            List<int> pool, EnemyDef[] enemies, in WaveSlotDef slot, int waveNumber,
            List<int> alreadyChosen, List<int> candidateBuffer, string conceptId,
            Action<string> report, ref Random rng)
        {
            if (pool == null || pool.Count == 0) return -1;

            if (Collect(pool, enemies, slot, waveNumber, alreadyChosen, true, true, candidateBuffer) == 0
                && Collect(pool, enemies, slot, waveNumber, null, true, true, candidateBuffer) == 0
                && Collect(pool, enemies, slot, 0, null, true, true, candidateBuffer) == 0)
            {
                if (Collect(pool, enemies, slot, 0, null, false, true, candidateBuffer) > 0)
                {
                    report?.Invoke($"[WaveGenerator] 컨셉 '{conceptId}' 슬롯의 성질 필터"
                                   + $"({slot.ClassFilter})에 맞는 적이 풀에 없어 성질을 무시했다. 컨셉이 흐려진다.");
                }
                else if (Collect(pool, enemies, slot, 0, null, false, false, candidateBuffer) > 0)
                {
                    report?.Invoke($"[WaveGenerator] 컨셉 '{conceptId}' 슬롯의 고도({slot.Altitude})에 맞는 적이"
                                   + " 풀에 없어 **고도까지 무시**했다 — 지상 컨셉에 비행이 섞일 수 있다.");
                }
                else return -1;   // 풀이 통째로 비었다
            }

            return candidateBuffer[rng.NextInt(0, candidateBuffer.Count)];
        }

        private static int Collect(List<int> pool, EnemyDef[] enemies, in WaveSlotDef slot,
                                   int waveNumber, List<int> exclude, bool applyClassFilter,
                                   bool applyAltitudeFilter, List<int> outIndices)
        {
            outIndices.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                int e = pool[i];
                if (e < 0 || e >= enemies.Length) continue;
                if (exclude != null && exclude.Contains(i)) continue;
                ref var def = ref enemies[e];
                if (waveNumber > 0 && def.MinWaveNumber > waveNumber) continue;
                if (applyClassFilter && slot.ClassFilter != 0 && def.EnemyClass != slot.ClassFilter) continue;
                if (applyAltitudeFilter && !MatchesAltitude(in def, slot.Altitude)) continue;
                outIndices.Add(i);
            }
            return outIndices.Count;
        }

        // 순서 보존 + 중복/범위 밖 제거. distinct 규칙은 **한 소스**에 둔다 — 두 곳에
        // 복제하면 한쪽만 바뀌어 조용히 갈린다.
        private static List<int> BuildDistinctPool(int[] source, EnemyDef[] enemies)
        {
            var result = new List<int>(source != null ? source.Length : 0);
            if (source == null) return result;
            for (int i = 0; i < source.Length; i++)
            {
                int e = source[i];
                if (e < 0 || e >= enemies.Length) continue;
                if (result.Contains(e)) continue;
                result.Add(e);
            }
            return result;
        }

        private static List<int> BuildBossPool(int[] bossPool, EnemyDef[] enemies)
            => BuildDistinctPool(bossPool, enemies);
    }
}
