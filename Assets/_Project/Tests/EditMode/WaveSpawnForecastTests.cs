using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // spawn-point-alert unit 0 — per-lane 첫 스폰 시각 예보.
    // 기대값은 손계산: RoundRobin 엔트리 i 의 시각 = base + i*spacing,
    // lane = (waveIndex*DeckIndexStride + i) % laneCount (3+ lane), authored clamp (<=2 lane).
    public class WaveSpawnForecastTests
    {
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _a = CreateUnit("A");
            _b = CreateUnit("B");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        [Test]
        public void EffectiveSpawnIndex_TwoLanesClampsAuthoredIndex()
        {
            Assert.AreEqual(0, WavePatternGenerator.EffectiveSpawnIndex(0, 999, 2));
            Assert.AreEqual(1, WavePatternGenerator.EffectiveSpawnIndex(5, 0, 2));
            Assert.AreEqual(0, WavePatternGenerator.EffectiveSpawnIndex(-1, 0, 2));
        }

        [Test]
        public void EffectiveSpawnIndex_ThreePlusLanesRoundRobinsDeckIndex()
        {
            Assert.AreEqual(2, WavePatternGenerator.EffectiveSpawnIndex(0, 1001, 3));
            Assert.AreEqual(0, WavePatternGenerator.EffectiveSpawnIndex(7, 3000, 3));
        }

        [Test]
        public void ThreeLanes_WaveIndexMultipleOfThree_LanesInEntryOrder()
        {
            // waveIndex 3 → base deckIndex 3000, 3000%3=0 → entry i 가 lane i%3.
            var wave = new GeneratedWave(3, 30f, _a, 5, _b, 5);
            var first = WavePatternGenerator.FirstSpawnTimesPerLane(wave, 30f, 3, 0.35f);

            Assert.AreEqual(30f, first[0], 1e-4);
            Assert.AreEqual(30.35f, first[1], 1e-4);
            Assert.AreEqual(30.7f, first[2], 1e-4);
        }

        [Test]
        public void ThreeLanes_LaneRotationFollowsDeckIndexConvention()
        {
            // waveIndex 1 → base deckIndex 1000, 1000%3=1 → entry0 lane1, entry1 lane2, entry2 lane0.
            var wave = new GeneratedWave(1, 0f, _a, 5, _b, 5);
            var first = WavePatternGenerator.FirstSpawnTimesPerLane(wave, 0f, 3, 0.35f);

            Assert.AreEqual(0.70f, first[0], 1e-4);
            Assert.AreEqual(0f, first[1], 1e-4);
            Assert.AreEqual(0.35f, first[2], 1e-4);
        }

        [Test]
        public void BossWave_BossVanguardLaneGetsBaseTime()
        {
            // 보스 선봉(RoundRobin round 0 = 보스 먼저) — waveIndex 4 → 4000%3=1 → 보스 lane1@base.
            var groups = new List<WaveSpawnGroup>
            {
                new WaveSpawnGroup(_a, 1), // 보스 역할(선봉)
                new WaveSpawnGroup(_b, 4),
            };
            var wave = new GeneratedWave(4, 90f, groups, 0f, WaveExpandMode.RoundRobin);
            var first = WavePatternGenerator.FirstSpawnTimesPerLane(wave, 90f, 3, 0.35f);

            Assert.AreEqual(90f, first[1], 1e-4);   // entry0 = 보스
            Assert.AreEqual(90.35f, first[2], 1e-4); // entry1
            Assert.AreEqual(90.7f, first[0], 1e-4);  // entry2
        }

        [Test]
        public void TwoLanes_UsesAuthoredSpawnIndex()
        {
            // ExpandWave 의 authored spawnIndex = localIndex % 2 → entry0 lane0, entry1 lane1.
            var wave = new GeneratedWave(2, 10f, _a, 2, _b, 2);
            var first = WavePatternGenerator.FirstSpawnTimesPerLane(wave, 10f, 2, 0.5f);

            Assert.AreEqual(10f, first[0], 1e-4);
            Assert.AreEqual(10.5f, first[1], 1e-4);
        }

        [Test]
        public void PerGroupTimeline_PerLaneMinIsNotWaveMin()
        {
            // g0: offset 1.0, 2마리 @ interval 0.5 → local0 @6.0, local1 @6.5
            // g1: offset 0.2, 1마리 → local2 @5.2
            // waveIndex 0 → lane = 엔트리 순번 % 3 → lane0@6.0, lane1@6.5, lane2@5.2.
            var groups = new List<WaveSpawnGroup>
            {
                new WaveSpawnGroup(_a, 2, 1f),
                new WaveSpawnGroup(_b, 1, 0.2f),
            };
            var wave = new GeneratedWave(0, 0f, groups, 0.5f, WaveExpandMode.PerGroupTimeline);
            var first = WavePatternGenerator.FirstSpawnTimesPerLane(wave, 5f, 3, 0.35f);

            Assert.AreEqual(6f, first[0], 1e-4);
            Assert.AreEqual(6.5f, first[1], 1e-4);
            Assert.AreEqual(5.2f, first[2], 1e-4);
        }

        [Test]
        public void LaneWithNoSpawnReturnsMinusOne()
        {
            // 엔트리 2 < lane 3 → lane2 는 스폰 없음.
            var groups = new List<WaveSpawnGroup>
            {
                new WaveSpawnGroup(_a, 1),
                new WaveSpawnGroup(_b, 1),
            };
            var wave = new GeneratedWave(0, 0f, groups, 0f, WaveExpandMode.RoundRobin);
            var first = WavePatternGenerator.FirstSpawnTimesPerLane(wave, 0f, 3, 0.35f);

            Assert.AreEqual(0f, first[0], 1e-4);
            Assert.AreEqual(0.35f, first[1], 1e-4);
            Assert.AreEqual(-1f, first[2]);
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
