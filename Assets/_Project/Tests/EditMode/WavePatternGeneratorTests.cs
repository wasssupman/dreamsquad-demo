using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class WavePatternGeneratorTests
    {
        private readonly List<AttackUnitData> _units = new();

        [SetUp]
        public void SetUp()
        {
            _units.Clear();
            _units.Add(CreateUnit("Basic"));
            _units.Add(CreateUnit("Swift"));
            _units.Add(CreateUnit("Tanker"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _units)
                Object.DestroyImmediate(unit);
            _units.Clear();
        }

        [Test]
        public void SameSeedProducesSameWaveSummary()
        {
            var a = Generate(1234);
            var b = Generate(1234);

            Assert.AreEqual(a.waves.Count, b.waves.Count);
            for (int i = 0; i < a.waves.Count; i++)
                Assert.AreEqual(WavePatternGenerator.FormatSummary(a.waves[i]), WavePatternGenerator.FormatSummary(b.waves[i]));
        }

        [Test]
        public void WaveCountAndUnitCountsStayInConfiguredRange()
        {
            var plan = Generate(55);

            Assert.GreaterOrEqual(plan.waves.Count, 10);
            Assert.LessOrEqual(plan.waves.Count, 15);
            foreach (var wave in plan.waves)
            {
                // seed 경로는 정확히 2그룹 불변(N-entry 일반화 후에도 유지).
                Assert.AreEqual(2, wave.groups.Count);
                Assert.NotNull(wave.groups[0].unit);
                Assert.NotNull(wave.groups[1].unit);
                Assert.AreNotSame(wave.groups[0].unit, wave.groups[1].unit);
                Assert.GreaterOrEqual(wave.totalCount, 10);
                Assert.LessOrEqual(wave.totalCount, 15);
                Assert.GreaterOrEqual(wave.groups[0].count, 1);
                Assert.GreaterOrEqual(wave.groups[1].count, 1);
            }
        }

        // 결정론 회귀: 같은 seed 2회 생성 → 전 웨이브를 펼친 SpawnEntry 시퀀스가
        // (unit ref / triggerTime / spawnIndex) 까지 완전히 동일해야 한다.
        [Test]
        public void SameSeedProducesByteIdenticalExpandedSequence()
        {
            var a = Generate(4242);
            var b = Generate(4242);

            Assert.AreEqual(a.waves.Count, b.waves.Count);
            for (int i = 0; i < a.waves.Count; i++)
            {
                var ea = WavePatternGenerator.ExpandWave(a.waves[i], i * 7f, 3, a.intraWaveSpacingSec);
                var eb = WavePatternGenerator.ExpandWave(b.waves[i], i * 7f, 3, b.intraWaveSpacingSec);
                Assert.AreEqual(ea.Count, eb.Count, $"wave {i} entry count");
                for (int e = 0; e < ea.Count; e++)
                {
                    Assert.AreSame(ea[e].entry.unitType, eb[e].entry.unitType, $"wave {i} entry {e} unit");
                    Assert.AreEqual(ea[e].entry.triggerTimeSec, eb[e].entry.triggerTimeSec, 0.0001f, $"wave {i} entry {e} time");
                    Assert.AreEqual(ea[e].laneIndex, eb[e].laneIndex, $"wave {i} entry {e} lane");
                    Assert.AreEqual(ea[e].swarmIndex, eb[e].swarmIndex, $"wave {i} entry {e} swarm");
                }
            }
        }

        // N>2 그룹 round-robin: A,B,C,A,B,C... 순서로 펼쳐지고 소진된 그룹은 건너뛴다.
        [Test]
        public void ExpandedWaveRoundRobinsAcrossNGroups()
        {
            var a = CreateUnit("A");
            var b = CreateUnit("B");
            var c = CreateUnit("C");
            try
            {
                var wave = new GeneratedWave(0, 0f, new[]
                {
                    new WaveSpawnGroup(a, 1),
                    new WaveSpawnGroup(b, 3),
                    new WaveSpawnGroup(c, 2),
                });
                var entries = WavePatternGenerator.ExpandWave(wave, 0f, 2, 0.35f);

                Assert.AreEqual(6, entries.Count);
                Assert.AreEqual(a, entries[0].entry.unitType); // round 0: A,B,C
                Assert.AreEqual(b, entries[1].entry.unitType);
                Assert.AreEqual(c, entries[2].entry.unitType);
                Assert.AreEqual(b, entries[3].entry.unitType); // round 1: (A done) B,C
                Assert.AreEqual(c, entries[4].entry.unitType);
                Assert.AreEqual(b, entries[5].entry.unitType); // round 2: only B
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void WaveIntervalUsesDurationDividedByWaveCount()
        {
            var plan = Generate(9876);
            float expected = plan.timerDurationSec / plan.waves.Count;

            Assert.AreEqual(expected, plan.waveIntervalSec, 0.0001f);
            Assert.AreEqual(0f, plan.waves[0].triggerTimeSec, 0.0001f);
            Assert.Less(plan.waves[plan.waves.Count - 1].triggerTimeSec, plan.timerDurationSec);
        }

        [Test]
        public void ExpandedWaveDistributesEntriesAcrossLanes()
        {
            var plan = Generate(77);
            var wave = plan.waves[0];

            var entries = WavePatternGenerator.ExpandWave(wave, 12f, 3, plan.intraWaveSpacingSec);

            Assert.AreEqual(wave.totalCount, entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(12f + i * plan.intraWaveSpacingSec, entries[i].entry.triggerTimeSec, 0.0001f);
                Assert.AreEqual(i % 3, entries[i].laneIndex);
                Assert.NotNull(entries[i].entry.unitType);
            }
        }

        [Test]
        public void ExpandedWaveInterleavesUnitTypesUntilOneSideExhausts()
        {
            var a = CreateUnit("A");
            var b = CreateUnit("B");
            try
            {
                var wave = new GeneratedWave(0, 0f, a, 2, b, 4);
                var entries = WavePatternGenerator.ExpandWave(wave, 0f, 2, 0.35f);

                Assert.AreEqual(a, entries[0].entry.unitType);
                Assert.AreEqual(b, entries[1].entry.unitType);
                Assert.AreEqual(a, entries[2].entry.unitType);
                Assert.AreEqual(b, entries[3].entry.unitType);
                Assert.AreEqual(b, entries[4].entry.unitType);
                Assert.AreEqual(b, entries[5].entry.unitType);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        // wave-authoring-test-mode unit 6 — 작성 플랜 변환: 웨이브 누적 시작 + 그룹 상대
        // offset + spawnInterval + PerGroupTimeline 모드, (unit==null||count<=0) 필터.
        [Test]
        public void FromPlanAssetMapsCumulativeStartAndGroupOffsets()
        {
            var a = CreateUnit("A");
            var b = CreateUnit("B");
            var plan = ScriptableObject.CreateInstance<WavePlanAsset>();
            plan.timerDurationSec = 0f;
            plan.waves = new List<AuthoredWave>
            {
                new AuthoredWave
                {
                    durationSec = 10f, intervalSec = 0.5f,
                    groups = new List<AuthoredSpawnGroup>
                    {
                        new AuthoredSpawnGroup { triggerTimeSec = 0f, unit = a, count = 3 },
                        new AuthoredSpawnGroup { triggerTimeSec = 4f, unit = b, count = 2 },
                    },
                },
                new AuthoredWave
                {
                    durationSec = 8f, intervalSec = 1f,
                    groups = new List<AuthoredSpawnGroup>
                    {
                        new AuthoredSpawnGroup { triggerTimeSec = 0f, unit = a, count = 0 },     // 필터
                        new AuthoredSpawnGroup { triggerTimeSec = 2f, unit = null, count = 5 },  // 필터
                        new AuthoredSpawnGroup { triggerTimeSec = 3f, unit = b, count = 4 },
                    },
                },
            };

            try
            {
                var gen = WavePatternGenerator.FromPlanAsset(plan);

                Assert.AreEqual(0f, gen.timerDurationSec, 0.0001f);
                Assert.AreEqual(2, gen.waves.Count);

                // wave 0: 절대 시작 0, interval 0.5, 그룹 2개(offset 0/4)
                Assert.AreEqual(0f, gen.waves[0].triggerTimeSec, 0.0001f);
                Assert.AreEqual(WaveExpandMode.PerGroupTimeline, gen.waves[0].expandMode);
                Assert.AreEqual(0.5f, gen.waves[0].spawnIntervalSec, 0.0001f);
                Assert.AreEqual(2, gen.waves[0].groups.Count);
                Assert.AreEqual(4f, gen.waves[0].groups[1].triggerOffsetSec, 0.0001f);
                Assert.AreEqual(5, gen.waves[0].totalCount);

                // wave 1: 절대 시작 = 앞 웨이브 durationSec(10), 필터 후 그룹 1개(b @offset 3)
                Assert.AreEqual(10f, gen.waves[1].triggerTimeSec, 0.0001f);
                Assert.AreEqual(1, gen.waves[1].groups.Count);
                Assert.AreEqual(b, gen.waves[1].groups[0].unit);
                Assert.AreEqual(3f, gen.waves[1].groups[0].triggerOffsetSec, 0.0001f);
                Assert.AreEqual(4, gen.waves[1].totalCount);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(plan);
            }
        }

        // PerGroupTimeline 펼침: 그룹 absolute 시각 = base + offset + k*interval.
        [Test]
        public void ExpandWavePerGroupTimelineUsesAbsoluteTimes()
        {
            var a = CreateUnit("A");
            var b = CreateUnit("B");
            try
            {
                var groups = new[]
                {
                    new WaveSpawnGroup(a, 3, 0f),
                    new WaveSpawnGroup(b, 2, 4f),
                };
                var wave = new GeneratedWave(0, 100f, groups, 0.5f, WaveExpandMode.PerGroupTimeline);
                var entries = WavePatternGenerator.ExpandWave(wave, 100f, 2, 0.35f); // intraWaveSpacing 무시됨

                Assert.AreEqual(5, entries.Count);
                Assert.AreEqual(a, entries[0].entry.unitType); Assert.AreEqual(100f, entries[0].entry.triggerTimeSec, 0.0001f);
                Assert.AreEqual(a, entries[1].entry.unitType); Assert.AreEqual(100.5f, entries[1].entry.triggerTimeSec, 0.0001f);
                Assert.AreEqual(a, entries[2].entry.unitType); Assert.AreEqual(101f, entries[2].entry.triggerTimeSec, 0.0001f);
                Assert.AreEqual(b, entries[3].entry.unitType); Assert.AreEqual(104f, entries[3].entry.triggerTimeSec, 0.0001f);
                Assert.AreEqual(b, entries[4].entry.unitType); Assert.AreEqual(104.5f, entries[4].entry.triggerTimeSec, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        private GeneratedWavePlan Generate(int seed)
        {
            return WavePatternGenerator.Generate(
                seed,
                1,
                180f,
                10,
                15,
                10,
                15,
                0.35f,
                _units);
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
