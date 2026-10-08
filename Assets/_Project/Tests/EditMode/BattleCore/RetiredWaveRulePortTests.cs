using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Map;
using Somnia.Battle.BattleCore.Wave;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 9 구현 2 — 옛 웨이브 컨셉 테스트 4벌(`WaveConceptGenerationTests` ·
    // `WaveConceptMathTests` · `WaveConceptVariantTests` · `WaveConceptBossTests`)이 증언하던 **규칙**을
    // 코어 생성기(`WaveGenerator`) 위로 옮긴다. 옛 쪽은 SO(`AttackDeck`·`WaveConceptData`)를 만들어
    // `WavePatternGenerator` 를 돌렸다 — 여기는 같은 저작을 plain(`WaveDeckDef`·`WaveConceptDef`)으로 준다.
    //
    // `WaveGeneratorTests` 와 겹치지 않게 했다: 그쪽은 **컨셉 없는** 레거시 경로의 rng 소비 순서와
    // 순수 함수 몇 개(분배 잔여 4,3,3 · 배율 0.4 · 같은 위상 = 같은 입구 · 입구 수 초과)를 진다.
    // 여기는 **컨셉 경로**(블록·입구·필터·변주·보스×컨셉)와 컨셉 선택(`PickConcept`)이다.
    //
    // ⚠ 수치는 게임 값이 아니라 픽스처다(옛 테스트의 덱 knob 을 그대로 옮겼다 — 시트가 덮는 값이 아니다).
    public class RetiredWaveRulePortTests
    {
        // `Somnia.Battle.Data.EnemyClass` 의 값과 같다(코어는 그 열거형을 모른다 — 빌더가 `(int)` 로 싣는다).
        private const int ClsNone = 0, Tanker = 1, Runner = 2, Bruiser = 3, Shooter = 4;
        private const int Seed = 20260813;

        // ── 픽스처 ────────────────────────────────────────────────────────────

        private static EnemyDef E(string id, int cls, bool air = false, int minWave = 1, int maxPerWave = 0)
        {
            var e = CoreMatchFixtures.Enemy(id, health: 50f, cls: cls, reward: 1);
            if (air) e.TraversalLayers = LayerBits.Air;
            e.MinWaveNumber = minWave;
            e.MaxPerWave = maxPerWave;
            return e;
        }

        /// <summary>옛 `GroundPool()` — 지상 6종.</summary>
        private static List<EnemyDef> GroundPool() => new List<EnemyDef>
        {
            E("basic", Bruiser), E("swift", Runner), E("runner", Runner),
            E("tanker", Tanker), E("sniper", Shooter), E("needler", Shooter),
        };

        private static WaveSlotDef Slot(int lane, int cls, SlotAltitude alt = SlotAltitude.Ground)
            => new WaveSlotDef { LaneGroup = lane, ClassFilter = cls, Altitude = alt, PathIndex = -1 };

        private static WaveConceptDef Concept(string id, float countMul, int minWave, WaveSlotDef[] slots,
                                              WaveSlotDef[] variants = null, float weight = 1f)
            => new WaveConceptDef
            {
                Id = id,
                DisplayName = id,
                Weight = weight,
                MinWaveNumber = minWave,
                CountMul = countMul,
                Slots = slots,
                VariantSlots = variants ?? System.Array.Empty<WaveSlotDef>(),
            };

        private static WaveConceptDef Concept(string id, float countMul, int minWave, params WaveSlotDef[] slots)
            => Concept(id, countMul, minWave, slots, null);

        /// <summary>
        /// 옛 테스트들의 덱 knob. `enemyCount` 개를 풀로 쓰고, `bossIndex` 가 0 이상이면 그 줄은 풀에서 빼고
        /// 보스 풀에 둔다(코어는 보스도 `Enemies` 의 한 줄이다).
        /// </summary>
        private static WaveDeckDef Deck(int enemyCount, WaveConceptDef[] concepts, int waveCount = 12,
                                        int bossIndex = -1, int bossInterval = 0, int holdWaves = 3)
        {
            var d = WaveDeckDef.Empty();
            d.GeneratorVersion = 1;
            d.WaveSeed = Seed;
            d.MinWaveCount = waveCount;
            d.MaxWaveCount = waveCount;
            d.MinUnitsPerWave = 5;
            d.MaxUnitsPerWave = 24;
            d.UnitGrowthPerWave = 1.12f;
            d.WaveCountJitter = 1;
            d.IntraWaveSpacingSec = 0.5f;
            d.MaxWaveIntervalSec = 20f;
            d.SpawnLeadInSec = 2f;
            d.TimerDurationSec = 180f;
            var pool = new List<int>();
            for (int i = 0; i < enemyCount; i++) if (i != bossIndex) pool.Add(i);
            d.EnemyPool = pool.ToArray();
            d.BossPool = bossIndex >= 0 ? new[] { bossIndex } : System.Array.Empty<int>();
            d.BossWaveInterval = bossIndex >= 0 ? bossInterval : 0;
            d.BossEscortMin = 3;
            d.BossEscortMax = 4;
            d.Concepts = concepts;
            d.ConceptHoldWaves = holdWaves;
            return d;
        }

        private static WavePlan Gen(in WaveDeckDef deck, List<EnemyDef> enemies, int lanes)
            => WaveGenerator.Generate(in deck, enemies.ToArray(), Seed, lanes);

        private static string Signature(WavePlan plan, List<EnemyDef> enemies)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < plan.WaveCount; i++)
            {
                var w = plan.Waves[i];
                sb.Append(w.ConceptLabel).Append('|');
                for (int g = 0; g < w.Groups.Length; g++)
                    sb.Append(enemies[w.Groups[g].EnemyIndex].Id).Append(':').Append(w.Groups[g].Count)
                      .Append('@').Append(w.Groups[g].LaneIndex).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        private static bool HasClass(in PlannedWave w, List<EnemyDef> enemies, int cls)
        {
            for (int g = 0; g < w.Groups.Length; g++)
                if (enemies[w.Groups[g].EnemyIndex].EnemyClass == cls) return true;
            return false;
        }

        private static WaveSlotDef[] Groups(params int[] laneGroups)
        {
            var s = new WaveSlotDef[laneGroups.Length];
            for (int i = 0; i < s.Length; i++) s[i] = Slot(laneGroups[i], ClsNone);
            return s;
        }

        // ════════════════════════════════════════════════════════════════════
        // 20 · 컨셉 경로 전체 (옛 WaveConceptGenerationTests)
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveConceptGenerationTests::Block_HoldsConceptAndLanesForThreeWaves — 블록 3웨이브 동안 컨셉·입구 유지
        [Test]
        public void 블록은_3웨이브_동안_컨셉과_입구를_유지한다()
        {
            var enemies = GroundPool();
            var pincer = Concept("pincer", 1f, 1, Slot(0, Shooter), Slot(1, Shooter));
            var plan = Gen(Deck(enemies.Count, new[] { pincer }), enemies, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("pincer", plan.Waves[i].ConceptLabel, $"웨이브 {i + 1} 컨셉");
                Assert.AreEqual(plan.Waves[0].Groups[0].LaneIndex, plan.Waves[i].Groups[0].LaneIndex,
                    "블록 안에서 입구가 바뀌면 «여기를 보강하자»가 보상받지 못한다");
                Assert.AreEqual(plan.Waves[0].Groups[1].LaneIndex, plan.Waves[i].Groups[1].LaneIndex);
            }
        }

        // 옛 WaveConceptGenerationTests::Block_SwitchesConceptAtBoundary_AndNeverRepeatsBackToBack — 경계 전환·연속 금지
        [Test]
        public void 컨셉은_블록_경계에서만_바뀌고_두_블록_연속되지_않는다()
        {
            var enemies = GroundPool();
            var a = Concept("a", 1f, 1, Slot(0, Runner));
            var b = Concept("b", 1f, 1, Slot(0, Shooter));
            var plan = Gen(Deck(enemies.Count, new[] { a, b }), enemies, 3);

            for (int i = 0; i < plan.WaveCount; i++)
                Assert.AreEqual(plan.Waves[(i / 3) * 3].ConceptLabel, plan.Waves[i].ConceptLabel,
                    $"웨이브 {i + 1} 은 블록 라벨을 따른다");
            for (int block = 1; block * 3 < plan.WaveCount; block++)
                Assert.AreNotEqual(plan.Waves[(block - 1) * 3].ConceptLabel, plan.Waves[block * 3].ConceptLabel,
                    "같은 컨셉이 두 블록 연속이면 그것이 기본값이 되어 인상이 죽는다");
        }

        // 옛 WaveConceptGenerationTests::Block_TotalsRiseWithinTheBlock — 컨셉 유지 중에도 수량 곡선은 오른다
        [Test]
        public void 컨셉이_유지되는_동안에도_수량은_오른다()
        {
            var enemies = GroundPool();
            var spread = Concept("spread", 1f, 1, Slot(-1, ClsNone), Slot(-1, ClsNone));
            var plan = Gen(Deck(enemies.Count, new[] { spread }, 15), enemies, 2);

            Assert.Greater(plan.Waves[14].TotalCount, plan.Waves[0].TotalCount,
                "«배우고 → 겨우 버티고» 가 성립하려면 곡선이 계속 올라야 한다");
        }

        // 옛 WaveConceptGenerationTests::Lanes_SameGroupSharesLane_DifferentGroupsSplit — 협공은 서로 다른 두 입구
        [Test]
        public void 협공_컨셉은_서로_다른_두_입구로_갈린다()
        {
            var enemies = GroundPool();
            var pincer = Concept("pincer", 1f, 1, Slot(0, Shooter), Slot(1, Shooter));
            var w = Gen(Deck(enemies.Count, new[] { pincer }), enemies, 4).Waves[0];

            Assert.AreEqual(2, w.Groups.Length);
            Assert.AreNotEqual(w.Groups[0].LaneIndex, w.Groups[1].LaneIndex, "협공은 서로 다른 두 lane");
            Assert.GreaterOrEqual(w.Groups[0].LaneIndex, 0);
            Assert.Less(w.Groups[1].LaneIndex, 4);
        }

        // 옛 WaveConceptGenerationTests::Lanes_ConceptLaneSurvivesExpansionOnThreeLaneMaps — 3레인+ 에서 컨셉 입구가 펼침에 안 지워진다
        [Test]
        public void 삼레인_이상에서도_컨셉_입구가_펼침에_지워지지_않는다()
        {
            var enemies = GroundPool();
            var single = Concept("single", 1f, 1, Slot(0, Tanker));
            var plan = Gen(Deck(enemies.Count, new[] { single }), enemies, 4);

            int lane = plan.Waves[0].Groups[0].LaneIndex;
            var into = new List<PlannedSpawn>();
            WaveGenerator.Expand(in plan.Waves[0], 0f, 4, 0.5f, into);

            Assert.IsNotEmpty(into);
            foreach (var s in into)
                Assert.AreEqual(lane, s.LaneIndex,
                    "3레인 이상 맵에서 컨셉이 지정한 입구가 순번 라운드로빈에 지워졌다");
        }

        // 옛 WaveConceptGenerationTests::Lanes_ConceptTooWideForMap_FallsBackInsteadOfSilentlyDropping — 성립 불가 컨셉은 폴백
        [Test]
        public void 맵_입구보다_넓은_컨셉은_조용히_입구를_버리지_않고_폴백한다()
        {
            var enemies = GroundPool();
            var pincer = Concept("pincer", 1f, 1, Slot(0, Shooter), Slot(1, Shooter));
            var plan = Gen(Deck(enemies.Count, new[] { pincer }), enemies, 1);

            Assert.AreEqual("", plan.Waves[0].ConceptLabel, "성립 불가 컨셉은 폴백으로 떨어진다");
            foreach (var g in plan.Waves[0].Groups)
                Assert.AreEqual(-1, g.LaneIndex, "폴백은 입구 무지정");
        }

        // 옛 WaveConceptGenerationTests::EmptyConceptPool_UsesLegacyShape — 풀이 비면 레거시 2종·입구 무지정
        [Test]
        public void 컨셉_풀이_비면_레거시_2종_편성이다()
        {
            var enemies = GroundPool();
            var plan = Gen(Deck(enemies.Count, System.Array.Empty<WaveConceptDef>()), enemies, 4);

            foreach (var w in plan.Waves)
            {
                Assert.AreEqual("", w.ConceptLabel, "컨셉 없음 = 라벨 없음");
                Assert.AreEqual(2, w.Groups.Length, "레거시 경로는 항상 2종");
                foreach (var g in w.Groups)
                    Assert.AreEqual(-1, g.LaneIndex, "레거시 경로는 입구 무지정(기존 분산 규칙)");
            }
        }

        // 옛 WaveConceptGenerationTests::EmptyConceptPool_MatchesTheNoPoolOverload — 빈 풀 = null 풀(같은 rng 스트림)
        [Test]
        public void 빈_컨셉_풀과_null_풀은_같은_편성이다()
        {
            var enemies = GroundPool();
            Assert.AreEqual(
                Signature(Gen(Deck(enemies.Count, System.Array.Empty<WaveConceptDef>()), enemies, 2), enemies),
                Signature(Gen(Deck(enemies.Count, null), enemies, 2), enemies),
                "빈 풀과 null 풀은 같은 rng 스트림을 써야 한다(무회귀 경로)");
        }

        // 옛 WaveConceptGenerationTests::Expansion_KeepsRoundRobinLastSpawnFormula — 펼침 수 = 총량, 마지막 = (total−1)×spacing
        [Test]
        public void 컨셉_웨이브_펼침의_마지막_스폰은_총량_빼기_1_곱하기_간격이다()
        {
            var enemies = GroundPool();
            var pincer = Concept("pincer", 1f, 1, Slot(0, Shooter), Slot(1, Shooter));
            var wave = Gen(Deck(enemies.Count, new[] { pincer }), enemies, 4).Waves[0];
            const float spacing = 0.5f;

            var into = new List<PlannedSpawn>();
            WaveGenerator.Expand(in wave, 0f, 4, spacing, into);

            Assert.AreEqual(wave.TotalCount, into.Count, "펼침 수 = 총량");
            float last = 0f;
            foreach (var s in into) last = System.Math.Max(last, s.TriggerTimeSec);
            Assert.AreEqual((wave.TotalCount - 1) * spacing, last, 1e-4f,
                "마지막 스폰이 (total-1)×spacing 이어야 스폰 창 불변식을 손대지 않는다");
        }

        // 옛 WaveConceptGenerationTests::AltitudeFilter_KeepsAirOutOfGroundConcepts — 지상 컨셉에 비행 없음
        [Test]
        public void 지상_컨셉에는_비행이_섞이지_않는다()
        {
            var enemies = GroundPool();
            enemies.Insert(3, E("skimmer", Bruiser, air: true));
            var ground = Concept("ground", 1f, 1, Slot(-1, ClsNone), Slot(-1, ClsNone));
            var plan = Gen(Deck(enemies.Count, new[] { ground }, 15), enemies, 2);

            foreach (var w in plan.Waves)
                foreach (var g in w.Groups)
                    Assert.AreNotEqual("skimmer", enemies[g.EnemyIndex].Id,
                        "지상 컨셉에 비행이 섞이면 대공 없이 막을 수 없는 적이 나온다");
        }

        // 옛 WaveConceptGenerationTests::AltitudeFilter_AirConceptPicksOnlyAir — 공습은 비행만
        [Test]
        public void 공중_컨셉은_비행만_뽑는다()
        {
            var enemies = GroundPool();
            enemies.Insert(3, E("skimmer", Bruiser, air: true));
            var air = Concept("air", 0.3f, 1, Slot(0, ClsNone, SlotAltitude.Air));
            var plan = Gen(Deck(enemies.Count, new[] { air }, 9), enemies, 2);

            foreach (var w in plan.Waves)
                foreach (var g in w.Groups)
                    Assert.AreEqual("skimmer", enemies[g.EnemyIndex].Id, "공습은 비행만 받는다");
        }

        // 옛 WaveConceptGenerationTests::ClassFilter_HeavyConceptOnlyPicksTankers — 성질 필터(단일 탱커 슬롯)
        [Test]
        public void 성질_필터_단일_탱커_슬롯은_탱커_한_종류만_낸다()
        {
            var enemies = GroundPool();
            var heavy = Concept("heavy", 0.4f, 1, Slot(0, Tanker));
            var plan = Gen(Deck(enemies.Count, new[] { heavy }, 9), enemies, 2);

            foreach (var w in plan.Waves)
            {
                Assert.AreEqual(1, w.Groups.Length, "중장은 단일 슬롯이라 한 종류만 온다");
                Assert.AreEqual("tanker", enemies[w.Groups[0].EnemyIndex].Id);
            }
        }

        // 옛 WaveConceptGenerationTests::WaveGate_IsRespectedByConceptSlots — 컨셉 슬롯도 등장 게이트를 지킨다
        [Test]
        public void 컨셉_슬롯도_등장_게이트를_지킨다()
        {
            var enemies = new List<EnemyDef>
            {
                E("early", Shooter), E("late", Shooter, minWave: 8), E("filler", Runner),
            };
            var shooters = Concept("shooters", 1f, 1, Slot(0, Shooter));
            var plan = Gen(Deck(enemies.Count, new[] { shooters }, 6), enemies, 2);

            foreach (var w in plan.Waves)
                foreach (var g in w.Groups)
                    Assert.AreNotEqual("late", enemies[g.EnemyIndex].Id,
                        "등장 웨이브 8 인 적이 웨이브 6 이전에 나왔다");
        }

        // 옛 WaveConceptGenerationTests::MaxPerWave_IsRespectedByConceptSlots — 컨셉 경로도 동시 등장 상한
        [Test]
        public void 컨셉_경로도_동시_등장_상한을_지킨다()
        {
            const int cap = 2;
            var enemies = new List<EnemyDef>
            {
                E("capped", Shooter, maxPerWave: cap), E("free", Runner), E("filler", Bruiser),
            };
            var capped = Concept("capped", 1f, 1, Slot(0, Shooter));
            var plan = Gen(Deck(enemies.Count, new[] { capped }, 12), enemies, 2);

            foreach (var w in plan.Waves)
                foreach (var g in w.Groups)
                    if (enemies[g.EnemyIndex].Id == "capped")
                        Assert.LessOrEqual(g.Count, cap, "상한이 컨셉 경로에서 빠졌다");
        }

        // 옛 WaveConceptGenerationTests::CountMul_ScalesTheWaveTotal — 수량 배율이 총량을 가른다
        [Test]
        public void 수량_배율이_웨이브_총량을_가른다()
        {
            var enemies = GroundPool();
            var heavy = Concept("heavy", 0.4f, 1, Slot(0, Tanker));
            var swarm = Concept("swarm", 1.3f, 1, Slot(0, Runner));

            var heavyPlan = Gen(Deck(enemies.Count, new[] { heavy }, 12), enemies, 2);
            var swarmPlan = Gen(Deck(enemies.Count, new[] { swarm }, 12), enemies, 2);

            Assert.Less(heavyPlan.Waves[11].TotalCount, swarmPlan.Waves[11].TotalCount,
                "성질을 통일하면 난이도가 성질에 끌려간다 — 배율이 그걸 되돌린다");
        }

        // 옛 WaveConceptGenerationTests::Generation_IsDeterministicForSameLaneCount — 같은 입구 수 = 같은 편성
        [Test]
        public void 컨셉_경로는_같은_입구_수에서_결정론이다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("a", 1f, 1, Slot(0, Runner)),
                Concept("b", 0.7f, 1, Slot(0, Shooter), Slot(1, Shooter)),
            });

            string first = Signature(Gen(deck, enemies, 3), enemies);
            Assert.AreEqual(first, Signature(Gen(deck, enemies, 3), enemies));
            Assert.AreEqual(first, Signature(Gen(deck, enemies, 3), enemies));
        }

        // 옛 WaveConceptGenerationTests::RampCurve_DoesNotDisturbConceptSequenceOrPicks — 수량 곡선은 컨셉·추첨의 rng 를 안 흔든다
        [Test]
        public void 두_단계_곡선은_컨셉_시퀀스와_유닛_추첨을_흔들지_않는다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("a", 1f, 1, Slot(0, Runner)),
                Concept("b", 1f, 1, Slot(0, Shooter)),
            }, waveCount: 21);
            var plain = Gen(deck, enemies, 2);
            deck.RampBreakWave = 15;
            deck.RampBreakUnits = 12;
            var ramped = Gen(deck, enemies, 2);

            Assert.AreEqual(plain.WaveCount, ramped.WaveCount);
            bool anyCountDiffers = false;
            for (int i = 0; i < plain.WaveCount; i++)
            {
                Assert.AreEqual(plain.Waves[i].ConceptLabel, ramped.Waves[i].ConceptLabel,
                    $"웨이브 {i + 1}: 곡선이 컨셉 시퀀스를 흔들었다 — rng 소비가 갈렸다");
                Assert.AreEqual(plain.Waves[i].Groups.Length, ramped.Waves[i].Groups.Length);
                for (int g = 0; g < plain.Waves[i].Groups.Length; g++)
                {
                    Assert.AreEqual(plain.Waves[i].Groups[g].EnemyIndex, ramped.Waves[i].Groups[g].EnemyIndex,
                        $"웨이브 {i + 1} 그룹 {g}: 유닛 추첨이 갈렸다");
                    Assert.AreEqual(plain.Waves[i].Groups[g].LaneIndex, ramped.Waves[i].Groups[g].LaneIndex);
                    if (plain.Waves[i].Groups[g].Count != ramped.Waves[i].Groups[g].Count) anyCountDiffers = true;
                }
            }
            Assert.IsTrue(anyCountDiffers, "수량이 하나도 안 달라졌다 — 곡선이 적용되지 않은 것이다");
        }

        // 옛 WaveConceptGenerationTests::LaneCount_IsPartOfTheDeterminismKey — 입구 수는 결정론 키의 일부
        [Test]
        public void 입구_수는_결정론_키의_일부다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("wide", 1f, 1, Slot(0, Shooter), Slot(1, Shooter)),
                Concept("narrow", 1f, 1, Slot(0, Runner)),
            });

            Assert.AreNotEqual(Signature(Gen(deck, enemies, 1), enemies), Signature(Gen(deck, enemies, 4), enemies),
                "스폰 1개 맵은 협공을 못 받으므로 편성 자체가 달라진다");
        }

        // ════════════════════════════════════════════════════════════════════
        // 21 · 컨셉 순수 함수 (옛 WaveConceptMathTests) — WaveGeneratorTests 에 없는 것만
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveConceptMathTests::Distribute_SumsToScaledTotal_AndEverySlotGetsAtLeastOne — 합 = scaled, 빈 슬롯 없음
        [Test]
        public void 분배_합은_배율_총량이고_빈_슬롯이_없다()
        {
            var counts = new int[3];
            int scaled = WaveGenerator.DistributeSlotCounts(19, 1f, 3, 24, counts);
            Assert.AreEqual(19, scaled);
            Assert.AreEqual(scaled, counts[0] + counts[1] + counts[2], "합이 scaled 와 같아야 한다");
            foreach (var c in counts) Assert.GreaterOrEqual(c, 1, "빈 슬롯을 만들지 않는다");
        }

        // 옛 WaveConceptMathTests::Distribute_CountMulScalesDown — 배율 축소 + 상한 절단
        [Test]
        public void 분배_배율은_총량을_줄이고_상한에서_잘린다()
        {
            const int total = 19, maxUnits = 24;
            var heavy = new int[1];
            var swarm = new int[1];
            WaveGenerator.DistributeSlotCounts(total, 0.4f, 1, maxUnits, heavy);
            WaveGenerator.DistributeSlotCounts(total, 1.3f, 1, maxUnits, swarm);
            Assert.AreEqual((int)System.Math.Round(total * 0.4f), heavy[0], "19 × 0.4 = 7.6 → 8");
            Assert.AreEqual(maxUnits, swarm[0], "19 × 1.3 = 24.7 는 상한에서 잘린다");
        }

        // 옛 WaveConceptMathTests::Distribute_LowerBoundIsSlotCount_NotMinUnits — 하한은 슬롯 수
        [Test]
        public void 분배_하한은_웨이브_최소_수량이_아니라_슬롯_수다()
        {
            var counts = new int[2];
            int scaled = WaveGenerator.DistributeSlotCounts(5, 0.3f, 2, 24, counts);
            Assert.AreEqual(2, scaled, "5 × 0.3 = 1.5 → 슬롯 수 2 로만 올라간다");
            Assert.AreEqual(new[] { 1, 1 }, counts);
        }

        // 옛 WaveConceptMathTests::Distribute_RespectsUpperBound — 상한
        [Test]
        public void 분배는_상한을_지킨다()
        {
            const int maxUnits = 24;
            var counts = new int[2];
            Assert.AreEqual(maxUnits, WaveGenerator.DistributeSlotCounts(100, 1f, 2, maxUnits, counts));
            Assert.AreEqual(maxUnits, counts[0] + counts[1]);
        }

        // 옛 WaveConceptMathTests::Distribute_IsDeterministic — 같은 입력 = 같은 출력
        [Test]
        public void 분배는_결정론이다()
        {
            var a = new int[3];
            var b = new int[3];
            WaveGenerator.DistributeSlotCounts(17, 0.7f, 3, 24, a);
            WaveGenerator.DistributeSlotCounts(17, 0.7f, 3, 24, b);
            Assert.AreEqual(a, b);
        }

        // 옛 WaveConceptMathTests::AssignLanes_SameGroupSameLane_DifferentGroupDifferentLane — 교차 위상 [0,1,0,1]
        [Test]
        public void 입구_배정_교차_위상도_같은_위상끼리_묶인다()
        {
            var lanes = new int[4];
            Assert.IsTrue(WaveGenerator.AssignLanes(Groups(0, 1, 0, 1), 3, 0, lanes));
            Assert.AreEqual(lanes[0], lanes[2], "같은 위상은 같은 입구");
            Assert.AreEqual(lanes[1], lanes[3], "같은 위상은 같은 입구");
            Assert.AreNotEqual(lanes[0], lanes[1], "다른 위상은 다른 입구");
        }

        // 옛 WaveConceptMathTests::AssignLanes_UnassignedStaysMinusOne — 전부 무지정 = 요구 없음
        [Test]
        public void 입구_배정_전부_무지정이면_요구가_없고_전부_마이너스1이다()
        {
            var lanes = new int[3];
            Assert.IsTrue(WaveGenerator.AssignLanes(Groups(-1, -1, -1), 2, 1, lanes), "무지정만 있으면 입구 요구가 없다");
            Assert.AreEqual(new[] { -1, -1, -1 }, lanes, "무지정은 -1 로 통과해 순번 라운드로빈을 탄다");
        }

        // 옛 WaveConceptMathTests::AssignLanes_IsDeterministicForSameRoll — 같은 롤 = 같은 배정
        [Test]
        public void 입구_배정은_같은_롤이면_같다()
        {
            var a = new int[2];
            var b = new int[2];
            WaveGenerator.AssignLanes(Groups(0, 1), 4, 7, a);
            WaveGenerator.AssignLanes(Groups(0, 1), 4, 7, b);
            Assert.AreEqual(a, b);
        }

        // 옛 WaveConceptMathTests::AssignLanes_RollShiftsTheLanePair — 롤이 복도 쌍을 옮긴다
        [Test]
        public void 입구_배정은_롤이_다르면_다른_복도_쌍을_준다()
        {
            var a = new int[2];
            var b = new int[2];
            WaveGenerator.AssignLanes(Groups(0, 1), 4, 0, a);
            WaveGenerator.AssignLanes(Groups(0, 1), 4, 1, b);
            Assert.AreNotEqual(a, b);
        }

        // 옛 WaveConceptMathTests::AssignLanes_NegativeRollIsHandled — 음수 롤
        [Test]
        public void 입구_배정은_음수_롤도_범위_안으로_접는다()
        {
            var lanes = new int[1];
            Assert.IsTrue(WaveGenerator.AssignLanes(Groups(0), 3, -5, lanes));
            Assert.GreaterOrEqual(lanes[0], 0);
            Assert.Less(lanes[0], 3);
        }

        private static WaveConceptDef PickC(string id, float weight, int minWave, params int[] laneGroups)
            => Concept(id, 1f, minWave, Groups(laneGroups), null, weight);

        // 옛 WaveConceptMathTests::Pick_MinWaveNumberGate_KeepsLateConceptsOut — 등장 게이트
        [Test]
        public void 컨셉_선택_등장_게이트가_늦은_컨셉을_막는다()
        {
            var pool = new[] { PickC("early", 1f, 1, -1), PickC("late", 1f, 7, -1) };
            Assert.AreEqual(0, WaveGenerator.PickConcept(pool, 1, 2, -1, 0.99f));
        }

        // 옛 WaveConceptMathTests::Pick_LaneRequirementGate_UsesDerivedCount — 파생 입구 요구량 게이트
        [Test]
        public void 컨셉_선택_입구_요구량은_슬롯에서_파생해_게이트한다()
        {
            var pool = new[] { PickC("pincer", 1f, 1, 0, 1), PickC("single", 1f, 1, 0) };
            Assert.AreEqual(1, WaveGenerator.PickConcept(pool, 1, 1, -1, 0.99f), "스폰 1개 맵에서는 협공이 빠진다");
        }

        // 옛 WaveConceptMathTests::Pick_ZeroWeight_IsNeverChosen — 가중치 0 은 안 뽑힌다
        [Test]
        public void 컨셉_선택_가중치_0은_뽑히지_않는다()
        {
            var pool = new[] { PickC("off", 0f, 1, -1), PickC("on", 1f, 1, -1) };
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(1, WaveGenerator.PickConcept(pool, 1, 2, -1, i / 10f));
        }

        // 옛 WaveConceptMathTests::Pick_ExcludesPreviousConcept — 직전 배제
        [Test]
        public void 컨셉_선택은_직전_컨셉을_배제한다()
        {
            var pool = new[] { PickC("a", 1f, 1, -1), PickC("b", 1f, 1, -1) };
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(1, WaveGenerator.PickConcept(pool, 1, 2, 0, i / 10f), "직전 컨셉은 후보에서 빠진다");
        }

        // 옛 WaveConceptMathTests::Pick_ExclusionFailsOpenWhenPoolHasOnlyOne — 배제 fail-open
        [Test]
        public void 컨셉_선택_배제로_후보가_0이면_배제를_푼다()
        {
            var pool = new[] { PickC("only", 1f, 1, -1) };
            Assert.AreEqual(0, WaveGenerator.PickConcept(pool, 1, 2, 0, 0.5f),
                "후보가 0 이 되면 배제를 풀어야 웨이브가 비지 않는다");
        }

        // 옛 WaveConceptMathTests::Pick_ReturnsNullWhenNothingIsEligible — 후보 없음 = -1
        [Test]
        public void 컨셉_선택_후보가_없으면_마이너스1이다()
        {
            var pool = new[] { PickC("late", 1f, 99, -1) };
            Assert.AreEqual(-1, WaveGenerator.PickConcept(pool, 1, 2, -1, 0.5f), "호출측이 구조적 폴백으로 떨어진다");
        }

        // 옛 WaveConceptMathTests::Pick_EmptySlots_IsNotEligible — 슬롯 없는 컨셉은 후보 아님
        [Test]
        public void 컨셉_선택_슬롯_없는_컨셉은_후보가_아니다()
        {
            var pool = new[] { PickC("hollow", 1f, 1) };
            Assert.AreEqual(-1, WaveGenerator.PickConcept(pool, 1, 2, -1, 0.5f), "슬롯 없는 컨셉은 편성을 만들 수 없다");
        }

        // 옛 WaveConceptMathTests::Pick_WeightsSkewTheDistribution — 가중치 1:9 ≈ 10%
        [Test]
        public void 컨셉_선택_가중치가_분포를_기울인다()
        {
            const float rare = 1f, common = 9f;
            var pool = new[] { PickC("rare", rare, 1, -1), PickC("common", common, 1, -1) };
            int hits = 0;
            for (int i = 0; i < 100; i++)
                if (WaveGenerator.PickConcept(pool, 1, 2, -1, i / 100f) == 0) hits++;
            Assert.AreEqual(100f * rare / (rare + common), hits, 1f, "가중치 1:9 면 대략 10% 만 rare");
        }

        // 옛 WaveConceptMathTests::Pick_IsDeterministicForSameRoll — 같은 롤 = 같은 선택
        [Test]
        public void 컨셉_선택은_같은_롤이면_같다()
        {
            var pool = new[] { PickC("a", 2f, 1, -1), PickC("b", 3f, 1, -1) };
            Assert.AreEqual(WaveGenerator.PickConcept(pool, 1, 2, -1, 0.42f),
                            WaveGenerator.PickConcept(pool, 1, 2, -1, 0.42f));
        }

        // 옛 WaveConceptMathTests::Pick_NullPool_ReturnsNull — null 풀
        [Test]
        public void 컨셉_선택_null_풀은_마이너스1이다()
        {
            Assert.AreEqual(-1, WaveGenerator.PickConcept(null, 1, 2, -1, 0.5f));
        }

        // ════════════════════════════════════════════════════════════════════
        // 22 · 묶음 가운데 변주 (옛 WaveConceptVariantTests)
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveConceptVariantTests::변주를_저작하지_않으면_편성이_완전히_동일하다 — 미저작 = byte-identical
        [Test]
        public void 변주를_저작하지_않으면_편성이_완전히_동일하다()
        {
            var enemies = GroundPool();
            var without = Deck(enemies.Count, new[] { Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, null) });
            var explicitEmpty = Deck(enemies.Count, new[]
            {
                Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, System.Array.Empty<WaveSlotDef>()),
            });
            var nullVariant = without;
            nullVariant.Concepts = new[] { without.Concepts[0] };
            nullVariant.Concepts[0].VariantSlots = null;

            string before = Signature(Gen(without, enemies, 3), enemies);
            Assert.AreEqual(before, Signature(Gen(explicitEmpty, enemies, 3), enemies),
                "변주 저작이 없는데 편성이 달라졌다 — 무회귀 경로가 깨졌다");
            Assert.AreEqual(before, Signature(Gen(nullVariant, enemies, 3), enemies));
        }

        // 옛 WaveConceptVariantTests::변주는_묶음_두번째_웨이브에만_삽입된다 — 가운데에만 · 삽입(교체 아님)
        [Test]
        public void 변주는_묶음_두번째_웨이브에만_삽입된다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, new[] { Slot(0, Shooter) }),
            });
            var plan = Gen(deck, enemies, 3);

            for (int i = 0; i < plan.WaveCount; i++)
            {
                Assert.IsTrue(HasClass(plan.Waves[i], enemies, Runner),
                    $"웨이브 {i + 1}: 본 편성(Runner)이 사라졌다 — 변주가 «교체»로 동작했다");
                if (i % 3 == 1)
                    Assert.IsTrue(HasClass(plan.Waves[i], enemies, Shooter), $"웨이브 {i + 1}(가운데): 변주가 끼지 않았다");
                else
                    Assert.IsFalse(HasClass(plan.Waves[i], enemies, Shooter), $"웨이브 {i + 1}: 가운데가 아닌데 변주가 들어왔다");
            }
        }

        // 옛 WaveConceptVariantTests::같은_laneGroup은_본편성과_같은_입구로_나온다 — 변주는 위상으로 입구를 물려받는다
        [Test]
        public void 같은_위상의_변주는_본_편성과_같은_입구로_나온다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("ranged", 1f, 1,
                    new[] { Slot(0, Shooter), Slot(1, Tanker) },
                    new[] { Slot(1, Runner) }),   // 본 편성의 **두 번째** 위상 — 순서로 배정하면 첫 입구로 떨어진다
            });
            var plan = Gen(deck, enemies, 4);

            int checkedBlocks = 0;
            for (int block = 0; block * 3 + 1 < plan.WaveCount; block++)
            {
                var first = plan.Waves[block * 3];
                var mid = plan.Waves[block * 3 + 1];
                int tankerLane = -999;
                for (int g = 0; g < first.Groups.Length; g++)
                    if (enemies[first.Groups[g].EnemyIndex].EnemyClass == Tanker) tankerLane = first.Groups[g].LaneIndex;
                if (tankerLane == -999) continue;

                for (int g = 0; g < mid.Groups.Length; g++)
                {
                    if (enemies[mid.Groups[g].EnemyIndex].EnemyClass != Runner) continue;
                    checkedBlocks++;
                    Assert.AreEqual(tankerLane, mid.Groups[g].LaneIndex,
                        $"묶음 {block}: 변주가 위상 1 을 저작했는데 본 편성 위상 1 과 다른 입구로 나왔다");
                }
            }
            Assert.Greater(checkedBlocks, 0, "검사가 공회전했다");
        }

        // 옛 WaveConceptVariantTests::변주_슬롯의_입구가_묶음_배정을_벗어나지_않는다 — 변주는 새 입구를 열지 않는다(게이트 off)
        [Test]
        public void 변주_슬롯의_입구는_묶음_배정을_벗어나지_않는다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("ranged", 1f, 1,
                    new[] { Slot(0, Shooter), Slot(1, Shooter) },
                    new[] { Slot(0, Tanker) }),
            });
            var plan = Gen(deck, enemies, 4);

            for (int block = 0; block * 3 + 1 < plan.WaveCount; block++)
            {
                var blockLanes = new HashSet<int>();
                var first = plan.Waves[block * 3];
                for (int g = 0; g < first.Groups.Length; g++) blockLanes.Add(first.Groups[g].LaneIndex);
                var mid = plan.Waves[block * 3 + 1];
                for (int g = 0; g < mid.Groups.Length; g++)
                    Assert.IsTrue(blockLanes.Contains(mid.Groups[g].LaneIndex),
                        $"묶음 {block} 가운데 웨이브가 새 입구(lane {mid.Groups[g].LaneIndex})를 열었다");
            }
        }

        // 옛 WaveConceptVariantTests::클라이맥스에서는_변주가_상시다 — break 이후 매 웨이브 변주
        [Test]
        public void 클라이맥스에서는_변주가_상시다()
        {
            const int breakWave = 7;
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, new[] { Slot(0, Shooter) }),
            });
            deck.RampBreakWave = breakWave;
            deck.RampBreakUnits = 8;
            var plan = Gen(deck, enemies, 2);

            for (int i = 0; i < plan.WaveCount; i++)
            {
                bool hasShooter = HasClass(plan.Waves[i], enemies, Shooter);
                if (i + 1 >= breakWave)
                    Assert.IsTrue(hasShooter, $"웨이브 {i + 1}: 클라이맥스인데 변주가 빠졌다");
                else if (i % 3 != 1)
                    Assert.IsFalse(hasShooter, $"웨이브 {i + 1}: 본편 비-가운데 웨이브에 변주가 붙었다");
            }
        }

        private static void VariantLanes(WavePlan plan, List<EnemyDef> enemies, bool expectNewLane, string label)
        {
            bool saw = false;
            for (int i = 0; i < plan.WaveCount; i++)
            {
                int mainLane = -999, variantLane = -999;
                foreach (var g in plan.Waves[i].Groups)
                {
                    int cls = enemies[g.EnemyIndex].EnemyClass;
                    if (cls == Runner) mainLane = g.LaneIndex;
                    if (cls == Shooter) variantLane = g.LaneIndex;
                }
                if (variantLane == -999 || mainLane == -999) continue;
                saw = true;
                if (expectNewLane)
                    Assert.AreNotEqual(mainLane, variantLane, $"{label} 웨이브 {i + 1}: 게이트 on 인데 변주가 본 레인으로 접혔다");
                else
                    Assert.AreEqual(mainLane, variantLane, $"{label} 웨이브 {i + 1}: 게이트 off 인데 변주가 새 레인을 열었다");
            }
            Assert.IsTrue(saw, $"{label}: 변주 웨이브가 하나도 없다 — 검사가 공회전했다");
        }

        // 옛 WaveConceptVariantTests::미지_laneGroup_변주는_게이트가_켜진_덱에서만_새_레인을_연다 — 새 전선은 break 게이트
        [Test]
        public void 본_편성에_없는_위상의_변주는_break_덱에서만_새_레인을_연다()
        {
            var enemies = GroundPool();
            WaveConceptDef Swarm() => Concept("swarm", 1f, 1,
                new[] { Slot(0, Runner), Slot(0, Runner) },
                new[] { Slot(1, Shooter) });

            var off = Deck(enemies.Count, new[] { Swarm() });
            var on = Deck(enemies.Count, new[] { Swarm() });
            on.RampBreakWave = 4;
            on.RampBreakUnits = 8;

            VariantLanes(Gen(off, enemies, 2), enemies, expectNewLane: false, "off");
            VariantLanes(Gen(on, enemies, 2), enemies, expectNewLane: true, "on");
        }

        // 옛 WaveConceptVariantTests::미지_그룹_다중_변주슬롯_접힘은_게이트_상태를_따른다 — 미지 위상 2+ 슬롯 접힘
        [Test]
        public void 미지_위상_다중_변주_슬롯의_접힘은_게이트_상태를_따른다()
        {
            var enemies = GroundPool();
            WaveConceptDef C() => Concept("pincer", 1f, 1,
                new[] { Slot(0, Runner), Slot(1, Tanker) },
                new[] { Slot(2, Shooter), Slot(2, Shooter) });

            void Lanes(WavePlan plan, out int runner, out int tanker, List<int> shooters)
            {
                shooters.Clear(); runner = -999; tanker = -999;
                for (int i = 0; i < plan.WaveCount; i++)
                {
                    var sl = new List<int>();
                    int r = -999, t = -999;
                    foreach (var g in plan.Waves[i].Groups)
                    {
                        int cls = enemies[g.EnemyIndex].EnemyClass;
                        if (cls == Runner) r = g.LaneIndex;
                        if (cls == Tanker) t = g.LaneIndex;
                        if (cls == Shooter) sl.Add(g.LaneIndex);
                    }
                    if (sl.Count == 2 && r != -999 && t != -999)
                    { runner = r; tanker = t; shooters.AddRange(sl); return; }
                }
                Assert.Fail("변주 2슬롯이 함께 뽑힌 웨이브가 없다 — 검사가 공회전했다");
            }

            var offShooters = new List<int>();
            Lanes(Gen(Deck(enemies.Count, new[] { C() }), enemies, 3), out int offRunner, out int offTanker, offShooters);
            Assert.AreEqual(offRunner, offShooters[0], "off: 첫 변주는 본 편성 첫 입구로 접힌다");
            Assert.AreEqual(offTanker, offShooters[1], "off: 둘째 변주는 본 편성 둘째 입구로 접힌다");

            var onDeck = Deck(enemies.Count, new[] { C() });
            onDeck.RampBreakWave = 4;
            onDeck.RampBreakUnits = 8;
            var onShooters = new List<int>();
            Lanes(Gen(onDeck, enemies, 3), out int onRunner, out int onTanker, onShooters);
            Assert.AreEqual(onShooters[0], onShooters[1], "on: 같은 위상은 같은 레인을 공유한다");
            Assert.AreNotEqual(onRunner, onShooters[0], "on: 새 전선은 본 편성 레인이 아니다");
            Assert.AreNotEqual(onTanker, onShooters[0]);
        }

        // 옛 WaveConceptVariantTests::holdWaves가_3미만이면_변주가_적용되지_않는다 — 블록 < 3 은 가운데가 없다
        [Test]
        public void 블록이_3웨이브_미만이면_변주가_적용되지_않는다()
        {
            var enemies = GroundPool();
            foreach (int hold in new[] { 1, 2 })
            {
                var deck = Deck(enemies.Count, new[]
                {
                    Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, new[] { Slot(0, Shooter) }),
                }, holdWaves: hold);
                var plan = Gen(deck, enemies, 3);
                for (int i = 0; i < plan.WaveCount; i++)
                    Assert.IsFalse(HasClass(plan.Waves[i], enemies, Shooter),
                        $"블록 {hold} 인데 웨이브 {i + 1} 에 변주가 들어왔다");
            }
        }

        // 옛 WaveConceptVariantTests::보스가_변주_자리에_와도_변주가_증발하지_않는다 — 보스 후처리는 그 웨이브의 슬롯을 읽는다
        [Test]
        public void 보스가_변주_자리에_와도_변주가_증발하지_않는다()
        {
            var enemies = GroundPool();
            enemies.Add(E("boss", Bruiser));
            int bossIndex = enemies.Count - 1;
            // 주기 4 · 묶음 3 → 보스 웨이브 i=3,7,11 중 i=7 은 i%3==1 = 변주 자리다.
            var deck = Deck(enemies.Count, new[]
            {
                Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, new[] { Slot(0, Shooter) }),
            }, bossIndex: bossIndex, bossInterval: 4);
            var plan = Gen(deck, enemies, 3);
            Assert.Greater(plan.WaveCount, 7, "전제: 보스가 변주 자리에 오는 웨이브까지 생성돼야 한다");

            var w = plan.Waves[7];
            bool sawBoss = false;
            foreach (var g in w.Groups) if (g.EnemyIndex == bossIndex) sawBoss = true;
            Assert.IsTrue(sawBoss && w.IsBoss, "웨이브 8 이 보스 웨이브여야 한다(전제)");
            Assert.IsTrue(HasClass(w, enemies, Shooter),
                "보스가 변주 자리에 오자 변주가 사라졌다 — 보스 후처리가 블록 본 편성을 읽고 있다");
        }

        // 옛 WaveConceptVariantTests::변주가_있어도_같은_시드는_같은_편성을_낸다 — 변주 포함 결정론
        [Test]
        public void 변주가_있어도_같은_시드는_같은_편성을_낸다()
        {
            var enemies = GroundPool();
            var deck = Deck(enemies.Count, new[]
            {
                Concept("swarm", 1f, 1, new[] { Slot(0, Runner) }, new[] { Slot(0, Shooter) }),
                Concept("heavy", 1f, 1, new[] { Slot(0, Tanker) }, new[] { Slot(0, Runner) }),
            });
            string first = Signature(Gen(deck, enemies, 3), enemies);
            for (int run = 0; run < 3; run++)
                Assert.AreEqual(first, Signature(Gen(deck, enemies, 3), enemies), $"{run + 2}회차가 다르다");
        }

        // ════════════════════════════════════════════════════════════════════
        // 23 · 보스 × 컨셉 (옛 WaveConceptBossTests)
        // ════════════════════════════════════════════════════════════════════

        private const int BossInterval = 9;

        /// <summary>옛 보스 테스트 풀(5종) + 보스 1줄(마지막).</summary>
        private static List<EnemyDef> BossEnemies(out int bossIndex)
        {
            var list = new List<EnemyDef>
            {
                E("basic", Bruiser), E("swift", Runner), E("tanker", Tanker),
                E("sniper", Shooter), E("needler", Shooter), E("boss", Bruiser),
            };
            bossIndex = list.Count - 1;
            return list;
        }

        private static bool HasEnemy(in PlannedWave w, int index)
        {
            for (int g = 0; g < w.Groups.Length; g++) if (w.Groups[g].EnemyIndex == index) return true;
            return false;
        }

        // 옛 WaveConceptBossTests::BossWaves_LandOnNine_NotFiveOrTen — 보스는 간격 9 에만
        [Test]
        public void 보스_웨이브는_간격의_배수에만_온다()
        {
            var enemies = BossEnemies(out int boss);
            var deck = Deck(enemies.Count, new[] { Concept("ranged", 0.7f, 1, Slot(0, Shooter)) }, 18,
                            bossIndex: boss, bossInterval: BossInterval);
            var plan = Gen(deck, enemies, 3);

            for (int i = 0; i < plan.WaveCount; i++)
            {
                bool expected = (i + 1) % BossInterval == 0;
                Assert.AreEqual(expected, HasEnemy(plan.Waves[i], boss), $"웨이브 {i + 1} 보스 여부");
                Assert.AreEqual(expected, plan.Waves[i].IsBoss, $"웨이브 {i + 1} 보스 표식");
            }
        }

        // 옛 WaveConceptBossTests::BossWave_IsTheLastWaveOfItsBlock — 간격 9 = 블록(3) 마지막 칸
        [Test]
        public void 간격_9의_보스는_블록의_마지막_웨이브다()
        {
            const int hold = 3;
            Assert.AreEqual(2, (BossInterval - 1) / hold, "보스 웨이브가 속한 블록");
            Assert.AreEqual(hold - 1, (BossInterval - 1) % hold, "보스 웨이브는 블록의 마지막 칸이어야 한다");
        }

        // 옛 WaveConceptBossTests::Escort_FollowsBlockConceptClassFilter — 호위는 블록 컨셉의 성질을 입는다
        [Test]
        public void 보스_호위는_블록_컨셉의_성질_필터를_따른다()
        {
            var enemies = BossEnemies(out int boss);
            var deck = Deck(enemies.Count, new[] { Concept("ranged", 0.7f, 1, Slot(0, Shooter)) }, BossInterval,
                            bossIndex: boss, bossInterval: BossInterval);
            var w = Gen(deck, enemies, 3).Waves[BossInterval - 1];

            Assert.IsTrue(HasEnemy(w, boss), "웨이브 9 는 보스 웨이브");
            foreach (var g in w.Groups)
            {
                if (g.EnemyIndex == boss) continue;
                Assert.AreEqual(Shooter, enemies[g.EnemyIndex].EnemyClass, "「원거리」 블록의 보스는 사거리 호위를 끼고 와야 한다");
            }
        }

        // 옛 WaveConceptBossTests::Escort_KeepsBudget_AndDoesNotApplyCountMul — 호위 예산에 배율 미적용
        [Test]
        public void 보스_호위_수량은_보스_파라미터가_소유하고_배율을_안_곱한다()
        {
            var enemies = BossEnemies(out int boss);
            var deck = Deck(enemies.Count, new[] { Concept("heavy", 0.4f, 1, Slot(0, Tanker)) }, BossInterval,
                            bossIndex: boss, bossInterval: BossInterval);
            var w = Gen(deck, enemies, 3).Waves[BossInterval - 1];

            int escorts = 0;
            foreach (var g in w.Groups) if (g.EnemyIndex != boss) escorts += g.Count;
            Assert.GreaterOrEqual(escorts, deck.BossEscortMin, "배율을 곱하면 하한에 먹혀 컨셉마다 호위 수가 달라진다");
            Assert.LessOrEqual(escorts, deck.BossEscortMax);
        }

        // 옛 WaveConceptBossTests::Boss_TakesTheLaneOfTheConceptsFirstSlot — 보스는 선봉 · 첫 슬롯 입구
        [Test]
        public void 보스는_선봉이고_컨셉_첫_슬롯의_입구에_선다()
        {
            var enemies = BossEnemies(out int boss);
            var deck = Deck(enemies.Count, new[] { Concept("pincer", 0.7f, 1, Slot(0, Shooter), Slot(1, Shooter)) },
                            BossInterval, bossIndex: boss, bossInterval: BossInterval);
            var w = Gen(deck, enemies, 4).Waves[BossInterval - 1];

            Assert.AreEqual(boss, w.Groups[0].EnemyIndex, "선봉 = 보스(RoundRobin round 0)");
            Assert.GreaterOrEqual(w.Groups[0].LaneIndex, 0, "협공 컨셉의 보스는 입구가 지정돼야 «본대»가 읽힌다");
            var lanes = new HashSet<int>();
            for (int g = 1; g < w.Groups.Length; g++) lanes.Add(w.Groups[g].LaneIndex);
            Assert.IsTrue(lanes.Contains(w.Groups[0].LaneIndex), "보스 입구가 호위 입구 중 하나(첫 슬롯)와 같아야 한다");
        }

        // 옛 WaveConceptBossTests::BossWave_CarriesTheConceptLabel — 보스 웨이브도 블록 라벨
        [Test]
        public void 보스_웨이브도_블록의_컨셉_라벨을_유지한다()
        {
            var enemies = BossEnemies(out int boss);
            var deck = Deck(enemies.Count, new[] { Concept("ranged", 0.7f, 1, Slot(0, Shooter)) }, BossInterval,
                            bossIndex: boss, bossInterval: BossInterval);
            Assert.AreEqual("ranged", Gen(deck, enemies, 3).Waves[BossInterval - 1].ConceptLabel,
                "보스 웨이브도 블록의 라벨을 유지해야 «강화판»으로 읽힌다");
        }

        // 옛 WaveConceptBossTests::NoConcept_BossWaveKeepsLegacyShape — 컨셉 없는 덱의 보스 웨이브는 레거시 2그룹
        [Test]
        public void 컨셉_없는_덱의_보스_웨이브는_레거시_모양이다()
        {
            var enemies = BossEnemies(out int boss);
            var deck = Deck(enemies.Count, System.Array.Empty<WaveConceptDef>(), BossInterval,
                            bossIndex: boss, bossInterval: BossInterval);
            var w = Gen(deck, enemies, 3).Waves[BossInterval - 1];

            Assert.AreEqual(2, w.Groups.Length, "레거시 보스 웨이브는 2그룹");
            Assert.AreEqual(boss, w.Groups[0].EnemyIndex);
            Assert.AreEqual(1, w.Groups[0].Count);
            Assert.AreEqual("", w.ConceptLabel);
            foreach (var g in w.Groups) Assert.AreEqual(-1, g.LaneIndex, "레거시 경로는 입구 무지정");
        }
    }
}
