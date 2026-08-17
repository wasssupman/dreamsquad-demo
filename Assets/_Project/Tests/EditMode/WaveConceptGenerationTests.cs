using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-concept-blocks unit 2 — 생성기가 컨셉을 실제 편성으로 바꾸는지.
    //
    // 순수 함수(unit 1)가 초록인 것은 여기서 아무것도 보장하지 않는다. 「컨셉을 저작했는데
    // 편성이 안 바뀌는」 상태에서도 unit 1 테스트는 전부 통과한다 — 그래서 블록 유지·lane
    // 불변식·폴백 동일성을 **생성 결과에서** 확인한다.
    public class WaveConceptGenerationTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T New<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        private AttackUnitData Enemy(
            string id, EnemyClass cls,
            PlacementLayer layers = PlacementLayer.None,
            int minWave = 1, int maxPerWave = 0)
        {
            var unit = New<AttackUnitData>();
            unit.id = id;
            unit.displayName = id;
            unit.enemyClass = cls;
            unit.traversalLayers = layers;
            unit.minWaveNumber = minWave;
            unit.maxPerWave = maxPerWave;
            unit.health = 50;
            unit.moveSpeed = 2f;
            return unit;
        }

        private WaveConceptData Concept(
            string id, float countMul, int minWave, params (int lane, EnemyClass cls, SlotAltitude alt)[] slots)
        {
            var concept = New<WaveConceptData>();
            concept.id = id;
            concept.displayName = id;
            concept.weight = 1f;
            concept.minWaveNumber = minWave;
            concept.countMul = countMul;
            var built = new WaveConceptSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                built[i] = new WaveConceptSlot
                {
                    laneGroup = slots[i].lane,
                    classFilter = slots[i].cls,
                    altitude = slots[i].alt,
                };
            concept.slots = built;
            return concept;
        }

        // 라이브 덱과 같은 knob (보스는 끈다 — 보스 편성은 unit 3 소관).
        private AttackDeck Deck(AttackUnitData[] pool, WaveConceptData[] concepts, int waveCount = 12)
        {
            var deck = New<AttackDeck>();
            deck.deckId = "test";
            deck.useGeneratedWaves = true;
            deck.waveSeed = 20260813;
            deck.minWaveCount = waveCount;
            deck.maxWaveCount = waveCount;
            deck.minUnitsPerWave = 5;
            deck.maxUnitsPerWave = 24;
            deck.unitGrowthPerWave = 1.12f;
            deck.waveCountJitter = 1;
            deck.intraWaveSpacingSec = 0.5f;
            deck.maxWaveIntervalSec = 20f;
            deck.waveSpawnLeadInSec = 2f;
            deck.timerDurationSec = 180f;
            deck.bossWaveInterval = 0;
            deck.bossUnit = null;
            deck.bossPool = new AttackUnitData[0];
            deck.attackUnitPool = pool;
            deck.waveConceptPool = concepts;
            deck.conceptHoldWaves = 3;
            return deck;
        }

        private AttackUnitData[] GroundPool() => new[]
        {
            Enemy("basic", EnemyClass.Bruiser),
            Enemy("swift", EnemyClass.Runner),
            Enemy("runner", EnemyClass.Runner),
            Enemy("tanker", EnemyClass.Tanker),
            Enemy("sniper", EnemyClass.Shooter),
            Enemy("needler", EnemyClass.Shooter),
        };

        private static string Signature(GeneratedWavePlan plan)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < plan.waves.Count; i++)
            {
                var w = plan.waves[i];
                sb.Append(w.conceptLabel).Append('|');
                for (int g = 0; g < w.groups.Count; g++)
                    sb.Append(w.groups[g].unit != null ? w.groups[g].unit.id : "-")
                      .Append(':').Append(w.groups[g].count)
                      .Append('@').Append(w.groups[g].laneIndex).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        // ---------------- 블록 유지 ----------------

        [Test]
        public void Block_HoldsConceptAndLanesForThreeWaves()
        {
            var pincer = Concept("pincer", 1f, 1,
                (0, EnemyClass.Shooter, SlotAltitude.Ground),
                (1, EnemyClass.Shooter, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { pincer }), 20260813, 4);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("pincer", plan.waves[i].conceptLabel, $"웨이브 {i + 1} 컨셉");
                Assert.AreEqual(plan.waves[0].groups[0].laneIndex, plan.waves[i].groups[0].laneIndex,
                    "블록 안에서 lane 이 바뀌면 «여기를 보강하자»가 보상받지 못한다");
                Assert.AreEqual(plan.waves[0].groups[1].laneIndex, plan.waves[i].groups[1].laneIndex);
            }
        }

        [Test]
        public void Block_SwitchesConceptAtBoundary_AndNeverRepeatsBackToBack()
        {
            var a = Concept("a", 1f, 1, (0, EnemyClass.Runner, SlotAltitude.Ground));
            var b = Concept("b", 1f, 1, (0, EnemyClass.Shooter, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { a, b }), 20260813, 3);

            // 블록 = 3웨이브. 라벨은 3개씩 같고 블록마다 달라야 한다(직전 배제).
            for (int i = 0; i < plan.waves.Count; i++)
            {
                string expected = plan.waves[(i / 3) * 3].conceptLabel;
                Assert.AreEqual(expected, plan.waves[i].conceptLabel, $"웨이브 {i + 1} 은 블록 라벨을 따른다");
            }
            for (int block = 1; block * 3 < plan.waves.Count; block++)
                Assert.AreNotEqual(
                    plan.waves[(block - 1) * 3].conceptLabel,
                    plan.waves[block * 3].conceptLabel,
                    "같은 컨셉이 두 블록 연속이면 그것이 기본값이 되어 인상이 죽는다");
        }

        [Test]
        public void Block_TotalsRiseWithinTheBlock()
        {
            var spread = Concept("spread", 1f, 1,
                (-1, EnemyClass.None, SlotAltitude.Ground),
                (-1, EnemyClass.None, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { spread }, 15), 20260813, 2);

            Assert.Greater(plan.waves[14].totalCount, plan.waves[0].totalCount,
                "컨셉이 유지되는 동안 수량 곡선은 계속 올라야 «배우고 → 겨우 버티고» 가 성립한다");
        }

        // ---------------- lane 불변식 ----------------

        [Test]
        public void Lanes_SameGroupSharesLane_DifferentGroupsSplit()
        {
            var pincer = Concept("pincer", 1f, 1,
                (0, EnemyClass.Shooter, SlotAltitude.Ground),
                (1, EnemyClass.Shooter, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { pincer }), 20260813, 4);

            var w = plan.waves[0];
            Assert.AreEqual(2, w.groups.Count);
            Assert.AreNotEqual(w.groups[0].laneIndex, w.groups[1].laneIndex, "협공은 서로 다른 두 lane");
            Assert.GreaterOrEqual(w.groups[0].laneIndex, 0);
            Assert.Less(w.groups[1].laneIndex, 4);
        }

        [Test]
        public void Lanes_ConceptLaneSurvivesExpansionOnThreeLaneMaps()
        {
            // EffectiveSpawnIndex 는 laneCount >= 3 에서 authored 값을 버린다.
            // 컨셉 lane 이 그 규칙을 우회하지 않으면 여기서 무너진다.
            var single = Concept("single", 1f, 1, (0, EnemyClass.Tanker, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { single }), 20260813, 4);

            int lane = plan.waves[0].groups[0].laneIndex;
            var entries = WavePatternGenerator.ExpandWave(plan.waves[0], 0f, 4, 0.5f);

            Assert.IsNotEmpty(entries);
            foreach (var e in entries)
                Assert.AreEqual(lane, e.laneIndex,
                    "3레인 이상 맵에서 컨셉이 지정한 lane 이 deckIndex 라운드로빈에 지워졌다");
        }

        [Test]
        public void Lanes_ConceptTooWideForMap_FallsBackInsteadOfSilentlyDropping()
        {
            var pincer = Concept("pincer", 1f, 1,
                (0, EnemyClass.Shooter, SlotAltitude.Ground),
                (1, EnemyClass.Shooter, SlotAltitude.Ground));
            // 스폰 1개 맵 — 협공은 성립하지 않는다.
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { pincer }), 20260813, 1);

            Assert.AreEqual("", plan.waves[0].conceptLabel, "성립 불가 컨셉은 폴백으로 떨어진다");
            foreach (var g in plan.waves[0].groups)
                Assert.AreEqual(-1, g.laneIndex, "폴백은 lane 무지정");
        }

        // ---------------- 폴백(무회귀 경로) ----------------

        [Test]
        public void EmptyConceptPool_UsesLegacyShape()
        {
            var plan = WavePatternGenerator.Generate(
                Deck(GroundPool(), new WaveConceptData[0]), 20260813, 4);

            foreach (var w in plan.waves)
            {
                Assert.AreEqual("", w.conceptLabel, "컨셉 없음 = 라벨 없음");
                Assert.AreEqual(2, w.groups.Count, "레거시 경로는 항상 2종");
                foreach (var g in w.groups)
                    Assert.AreEqual(-1, g.laneIndex, "레거시 경로는 lane 무지정(기존 분산 규칙)");
            }
        }

        [Test]
        public void EmptyConceptPool_MatchesTheNoPoolOverload()
        {
            var pool = GroundPool();
            var withEmpty = Deck(pool, new WaveConceptData[0]);
            var withNull = Deck(pool, null);

            Assert.AreEqual(
                Signature(WavePatternGenerator.Generate(withEmpty, 20260813, 2)),
                Signature(WavePatternGenerator.Generate(withNull, 20260813, 2)),
                "빈 풀과 null 풀은 같은 rng 스트림을 써야 한다(무회귀 경로)");
        }

        // ---------------- 펼침 케이던스 (계약 8) ----------------

        [Test]
        public void Expansion_KeepsRoundRobinLastSpawnFormula()
        {
            var pincer = Concept("pincer", 1f, 1,
                (0, EnemyClass.Shooter, SlotAltitude.Ground),
                (1, EnemyClass.Shooter, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { pincer }), 20260813, 4);

            var wave = plan.waves[0];
            var entries = WavePatternGenerator.ExpandWave(wave, 0f, 4, 0.5f);

            Assert.AreEqual(wave.totalCount, entries.Count, "펼침 수 = 총량");
            float last = 0f;
            foreach (var e in entries) last = Mathf.Max(last, e.entry.triggerTimeSec);
            Assert.AreEqual((wave.totalCount - 1) * 0.5f, last, 0.0001f,
                "마지막 스폰이 (total-1)×spacing 이어야 스폰 창 불변식을 손대지 않는다");
        }

        // ---------------- 필터 ----------------

        [Test]
        public void AltitudeFilter_KeepsAirOutOfGroundConcepts()
        {
            var pool = new List<AttackUnitData>(GroundPool());
            pool.Insert(3, Enemy("skimmer", EnemyClass.Bruiser, PlacementLayer.Air));

            var ground = Concept("ground", 1f, 1,
                (-1, EnemyClass.None, SlotAltitude.Ground),
                (-1, EnemyClass.None, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(
                Deck(pool.ToArray(), new[] { ground }, 15), 20260813, 2);

            foreach (var w in plan.waves)
                foreach (var g in w.groups)
                    Assert.AreNotEqual("skimmer", g.unit.id,
                        "지상 컨셉에 비행이 섞이면 대공 없이 막을 수 없는 적이 나온다(계약 10)");
        }

        [Test]
        public void AltitudeFilter_AirConceptPicksOnlyAir()
        {
            var pool = new List<AttackUnitData>(GroundPool());
            pool.Insert(3, Enemy("skimmer", EnemyClass.Bruiser, PlacementLayer.Air));

            var air = Concept("air", 0.3f, 1, (0, EnemyClass.None, SlotAltitude.Air));
            var plan = WavePatternGenerator.Generate(
                Deck(pool.ToArray(), new[] { air }, 9), 20260813, 2);

            foreach (var w in plan.waves)
                foreach (var g in w.groups)
                    Assert.AreEqual("skimmer", g.unit.id, "공습은 비행만 받는다");
        }

        [Test]
        public void ClassFilter_HeavyConceptOnlyPicksTankers()
        {
            var heavy = Concept("heavy", 0.4f, 1, (0, EnemyClass.Tanker, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(GroundPool(), new[] { heavy }, 9), 20260813, 2);

            foreach (var w in plan.waves)
            {
                Assert.AreEqual(1, w.groups.Count, "중장은 단일 슬롯이라 한 종류만 온다");
                Assert.AreEqual("tanker", w.groups[0].unit.id);
            }
        }

        [Test]
        public void WaveGate_IsRespectedByConceptSlots()
        {
            var pool = new[]
            {
                Enemy("early", EnemyClass.Shooter),
                Enemy("late", EnemyClass.Shooter, PlacementLayer.None, minWave: 8),
                Enemy("filler", EnemyClass.Runner),
            };
            var shooters = Concept("shooters", 1f, 1, (0, EnemyClass.Shooter, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(pool, new[] { shooters }, 6), 20260813, 2);

            foreach (var w in plan.waves)
                foreach (var g in w.groups)
                    Assert.AreNotEqual("late", g.unit.id, "minWaveNumber 8 인 적이 웨이브 6 이전에 나왔다");
        }

        [Test]
        public void MaxPerWave_IsRespectedByConceptSlots()
        {
            var pool = new[]
            {
                Enemy("capped", EnemyClass.Shooter, PlacementLayer.None, maxPerWave: 2),
                Enemy("free", EnemyClass.Runner),
                Enemy("filler", EnemyClass.Bruiser),
            };
            var capped = Concept("capped", 1f, 1, (0, EnemyClass.Shooter, SlotAltitude.Ground));
            var plan = WavePatternGenerator.Generate(Deck(pool, new[] { capped }, 12), 20260813, 2);

            foreach (var w in plan.waves)
                foreach (var g in w.groups)
                    if (g.unit.id == "capped")
                        Assert.LessOrEqual(g.count, 2, "maxPerWave 상한이 컨셉 경로에서 빠졌다");
        }

        // ---------------- countMul ----------------

        [Test]
        public void CountMul_ScalesTheWaveTotal()
        {
            var pool = GroundPool();
            var heavy = Concept("heavy", 0.4f, 1, (0, EnemyClass.Tanker, SlotAltitude.Ground));
            var swarm = Concept("swarm", 1.3f, 1, (0, EnemyClass.Runner, SlotAltitude.Ground));

            var heavyPlan = WavePatternGenerator.Generate(Deck(pool, new[] { heavy }, 12), 20260813, 2);
            var swarmPlan = WavePatternGenerator.Generate(Deck(pool, new[] { swarm }, 12), 20260813, 2);

            Assert.Less(heavyPlan.waves[11].totalCount, swarmPlan.waves[11].totalCount,
                "성질을 통일하면 난이도가 성질에 끌려간다 — countMul 이 그걸 되돌린다(계약 4)");
        }

        // ---------------- 결정론 ----------------

        [Test]
        public void Generation_IsDeterministicForSameLaneCount()
        {
            var pool = GroundPool();
            var concepts = new[]
            {
                Concept("a", 1f, 1, (0, EnemyClass.Runner, SlotAltitude.Ground)),
                Concept("b", 0.7f, 1,
                    (0, EnemyClass.Shooter, SlotAltitude.Ground),
                    (1, EnemyClass.Shooter, SlotAltitude.Ground)),
            };
            var deck = Deck(pool, concepts);

            var first = Signature(WavePatternGenerator.Generate(deck, 20260813, 3));
            var second = Signature(WavePatternGenerator.Generate(deck, 20260813, 3));
            var third = Signature(WavePatternGenerator.Generate(deck, 20260813, 3));

            Assert.AreEqual(first, second);
            Assert.AreEqual(first, third);
        }

        // laneCount 는 결정론 키의 일부다 — lane 요구량 게이트가 후보 집합을 바꾼다.
        // 이것이 계약 6(브리핑과 런타임이 같은 값을 넘긴다)이 필요한 이유다.
        // wave-ramp-two-phase unit 0 — 수량 곡선은 rng 를 소비하지 않는다는 계약의 생성 결과 pin.
        // 곡선(두 단계)이 켜져도 컨셉 시퀀스와 유닛 추첨은 그대로여야 한다 — 흔들리면 시드
        // 재선정(unit 3)의 술어가 곡선 튜닝 때마다 다시 깨진다.
        [Test]
        public void RampCurve_DoesNotDisturbConceptSequenceOrPicks()
        {
            var pool = GroundPool();
            var concepts = new[]
            {
                Concept("a", 1f, 1, (0, EnemyClass.Runner, SlotAltitude.Ground)),
                Concept("b", 1f, 1, (0, EnemyClass.Shooter, SlotAltitude.Ground)),
            };
            var deck = Deck(pool, concepts, waveCount: 21);
            var plain = WavePatternGenerator.Generate(deck, 20260813, 2);
            deck.waveRampBreakWave = 15;
            deck.waveRampBreakUnits = 12;
            var ramped = WavePatternGenerator.Generate(deck, 20260813, 2);

            Assert.AreEqual(plain.waves.Count, ramped.waves.Count);
            bool anyCountDiffers = false;
            for (int i = 0; i < plain.waves.Count; i++)
            {
                Assert.AreEqual(plain.waves[i].conceptLabel, ramped.waves[i].conceptLabel,
                    $"웨이브 {i + 1}: 곡선이 컨셉 시퀀스를 흔들었다 — rng 소비가 갈렸다");
                Assert.AreEqual(plain.waves[i].groups.Count, ramped.waves[i].groups.Count);
                for (int g = 0; g < plain.waves[i].groups.Count; g++)
                {
                    Assert.AreEqual(plain.waves[i].groups[g].unit, ramped.waves[i].groups[g].unit,
                        $"웨이브 {i + 1} 그룹 {g}: 유닛 추첨이 갈렸다");
                    Assert.AreEqual(plain.waves[i].groups[g].laneIndex, ramped.waves[i].groups[g].laneIndex);
                    if (plain.waves[i].groups[g].count != ramped.waves[i].groups[g].count)
                        anyCountDiffers = true;
                }
            }
            Assert.IsTrue(anyCountDiffers, "수량이 하나도 안 달라졌다 — 곡선이 적용되지 않은 것이다");
        }

        [Test]
        public void LaneCount_IsPartOfTheDeterminismKey()
        {
            var pool = GroundPool();
            var concepts = new[]
            {
                Concept("wide", 1f, 1,
                    (0, EnemyClass.Shooter, SlotAltitude.Ground),
                    (1, EnemyClass.Shooter, SlotAltitude.Ground)),
                Concept("narrow", 1f, 1, (0, EnemyClass.Runner, SlotAltitude.Ground)),
            };
            var deck = Deck(pool, concepts);

            Assert.AreNotEqual(
                Signature(WavePatternGenerator.Generate(deck, 20260813, 1)),
                Signature(WavePatternGenerator.Generate(deck, 20260813, 4)),
                "스폰 1개 맵은 협공을 못 받으므로 편성 자체가 달라진다");
        }
    }
}
