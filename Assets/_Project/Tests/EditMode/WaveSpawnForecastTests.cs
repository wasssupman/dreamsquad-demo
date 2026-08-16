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
            var first = FirstSpawnTimes(wave, 30f, 3, 0.35f);

            Assert.AreEqual(30f, first[0], 1e-4);
            Assert.AreEqual(30.35f, first[1], 1e-4);
            Assert.AreEqual(30.7f, first[2], 1e-4);
        }

        [Test]
        public void ThreeLanes_LaneRotationFollowsDeckIndexConvention()
        {
            // waveIndex 1 → base deckIndex 1000, 1000%3=1 → entry0 lane1, entry1 lane2, entry2 lane0.
            var wave = new GeneratedWave(1, 0f, _a, 5, _b, 5);
            var first = FirstSpawnTimes(wave, 0f, 3, 0.35f);

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
            var first = FirstSpawnTimes(wave, 90f, 3, 0.35f);

            Assert.AreEqual(90f, first[1], 1e-4);   // entry0 = 보스
            Assert.AreEqual(90.35f, first[2], 1e-4); // entry1
            Assert.AreEqual(90.7f, first[0], 1e-4);  // entry2
        }

        [Test]
        public void TwoLanes_UsesAuthoredSpawnIndex()
        {
            // ExpandWave 의 authored spawnIndex = localIndex % 2 → entry0 lane0, entry1 lane1.
            var wave = new GeneratedWave(2, 10f, _a, 2, _b, 2);
            var first = FirstSpawnTimes(wave, 10f, 2, 0.5f);

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
            var first = FirstSpawnTimes(wave, 5f, 3, 0.35f);

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
            var first = FirstSpawnTimes(wave, 0f, 3, 0.35f);

            Assert.AreEqual(0f, first[0], 1e-4);
            Assert.AreEqual(0.35f, first[1], 1e-4);
            Assert.AreEqual(-1f, first[2]);
        }

        [Test]
        public void DifferentSwarmsOnSameLane_RemainSeparateGuides()
        {
            _a.waypointPathIndex = 0;
            _a.traversalLayers = PlacementLayer.Path;
            _b.waypointPathIndex = 1;
            _b.traversalLayers = PlacementLayer.Ground;
            var wave = new GeneratedWave(0, 0f, new[]
            {
                new WaveSpawnGroup(_a, 2),
                new WaveSpawnGroup(_b, 2),
            });
            var detailed = WavePatternGenerator.ExpandWave(wave, 4f, 1, 0.5f);

            var guides = WavePatternGenerator.BuildSpawnGuideForecasts(detailed);

            Assert.AreEqual(2, guides.Length, "같은 lane 이어도 스웜 기준으로 병합하지 않는다");
            Assert.AreEqual(0, guides[0].swarmIndex);
            Assert.AreEqual(0, guides[0].laneIndex);
            Assert.AreEqual(4f, guides[0].firstSpawnSec, 1e-4f);
            Assert.AreEqual(0, guides[0].waypointPathIndex);
            Assert.AreEqual((byte)PlacementLayer.Path, guides[0].traversalLayers);
            Assert.AreEqual(1, guides[1].swarmIndex);
            Assert.AreEqual(0, guides[1].laneIndex);
            Assert.AreEqual(4.5f, guides[1].firstSpawnSec, 1e-4f);
            Assert.AreEqual(1, guides[1].waypointPathIndex);
            Assert.AreEqual((byte)PlacementLayer.Ground, guides[1].traversalLayers);
        }

        // waypoint-flight-enemy unit 11 — 예보의 경로 해석 = 스폰의 경로 해석.
        // (SO 저작 > 레인 기본 > 최단거리) 우선순위를 WaypointRouting.ResolvePathIndex 로 공유한다.
        [Test]
        public void LaneDefaultRoute_ResolvesIntoForecast_WhenUnitHasNoAuthoredPath()
        {
            _a.waypointPathIndex = -1;
            var wave = new GeneratedWave(0, 0f, new[] { new WaveSpawnGroup(_a, 3) });
            var detailed = WavePatternGenerator.ExpandWave(wave, 0f, 2, 0.25f);

            var guides = WavePatternGenerator.BuildSpawnGuideForecasts(detailed, new[] { 2, -1 });

            Assert.AreEqual(2, guides.Length);
            Assert.AreEqual(0, guides[0].laneIndex);
            Assert.AreEqual(2, guides[0].waypointPathIndex, "레인 0 은 기본 경로 2 를 실어야 한다");
            Assert.AreEqual(1, guides[1].laneIndex);
            Assert.AreEqual(-1, guides[1].waypointPathIndex, "레인 1 은 미지정(-1) 그대로");
        }

        [Test]
        public void AuthoredUnitPath_BeatsLaneDefaultRoute()
        {
            _a.waypointPathIndex = 0;   // 종의 정체성(예: Skimmer Air 경로)이 레인 기본을 이긴다
            var wave = new GeneratedWave(0, 0f, new[] { new WaveSpawnGroup(_a, 2) });
            var detailed = WavePatternGenerator.ExpandWave(wave, 0f, 2, 0.25f);

            var guides = WavePatternGenerator.BuildSpawnGuideForecasts(detailed, new[] { 2, 2 });

            Assert.AreEqual(2, guides.Length);
            Assert.AreEqual(0, guides[0].waypointPathIndex);
            Assert.AreEqual(0, guides[1].waypointPathIndex);
        }

        [Test]
        public void NoLaneRoutes_KeepsShortestPathMarker()
        {
            _a.waypointPathIndex = -1;
            var wave = new GeneratedWave(0, 0f, new[] { new WaveSpawnGroup(_a, 2) });
            var detailed = WavePatternGenerator.ExpandWave(wave, 0f, 2, 0.25f);

            // null(미주입)과 짧은 배열(레인 수 부족) 모두 예외 없이 -1 로 남는다.
            var noRoutes = WavePatternGenerator.BuildSpawnGuideForecasts(detailed);
            var shortRoutes = WavePatternGenerator.BuildSpawnGuideForecasts(detailed, new[] { -1 });

            Assert.AreEqual(-1, noRoutes[0].waypointPathIndex);
            Assert.AreEqual(-1, noRoutes[1].waypointPathIndex);
            Assert.AreEqual(-1, shortRoutes[1].waypointPathIndex, "배열 밖 레인은 -1 폴백");
        }

        [Test]
        public void OneSwarmAcrossLanes_GetsGuidePerActualLane()
        {
            var wave = new GeneratedWave(0, 0f, new[] { new WaveSpawnGroup(_a, 3) });
            var detailed = WavePatternGenerator.ExpandWave(wave, 7f, 2, 0.25f);

            var guides = WavePatternGenerator.BuildSpawnGuideForecasts(detailed);

            Assert.AreEqual(2, guides.Length);
            Assert.AreEqual(0, guides[0].swarmIndex);
            Assert.AreEqual(0, guides[1].swarmIndex);
            Assert.AreEqual(0, guides[0].laneIndex);
            Assert.AreEqual(1, guides[1].laneIndex);
            Assert.AreEqual(7f, guides[0].firstSpawnSec, 1e-4f);
            Assert.AreEqual(7.25f, guides[1].firstSpawnSec, 1e-4f);
        }

        [Test]
        public void Expansion_PreservesEntryOrderSwarmOriginAndResolvedLane()
        {
            var wave = new GeneratedWave(0, 0f, _a, 2, _b, 1);

            var expanded = WavePatternGenerator.ExpandWave(wave, 3f, 2, 0.4f);

            Assert.AreEqual(3, expanded.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, new[]
            {
                expanded[0].swarmIndex,
                expanded[1].swarmIndex,
                expanded[2].swarmIndex,
            });
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, new[]
            {
                expanded[0].laneIndex,
                expanded[1].laneIndex,
                expanded[2].laneIndex,
            });
            Assert.AreSame(_a, expanded[0].entry.unitType);
            Assert.AreSame(_b, expanded[1].entry.unitType);
            Assert.AreSame(_a, expanded[2].entry.unitType);
            Assert.AreEqual(3f, expanded[0].entry.triggerTimeSec, 1e-4f);
            Assert.AreEqual(3.4f, expanded[1].entry.triggerTimeSec, 1e-4f);
            Assert.AreEqual(3.8f, expanded[2].entry.triggerTimeSec, 1e-4f);
        }

        private static float[] FirstSpawnTimes(
            GeneratedWave wave, float baseTriggerTimeSec, int laneCount, float spacing)
        {
            var result = new float[laneCount];
            for (int i = 0; i < result.Length; i++) result[i] = -1f;
            var expanded = WavePatternGenerator.ExpandWave(
                wave, baseTriggerTimeSec, laneCount, spacing);
            for (int i = 0; i < expanded.Count; i++)
            {
                int lane = expanded[i].laneIndex;
                float time = expanded[i].entry.triggerTimeSec;
                if (result[lane] < 0f || time < result[lane]) result[lane] = time;
            }
            return result;
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
