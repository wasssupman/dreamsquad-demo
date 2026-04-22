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
                Assert.NotNull(wave.unitA);
                Assert.NotNull(wave.unitB);
                Assert.AreNotSame(wave.unitA, wave.unitB);
                Assert.GreaterOrEqual(wave.totalCount, 10);
                Assert.LessOrEqual(wave.totalCount, 15);
                Assert.GreaterOrEqual(wave.countA, 1);
                Assert.GreaterOrEqual(wave.countB, 1);
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
                Assert.AreEqual(12f + i * plan.intraWaveSpacingSec, entries[i].triggerTimeSec, 0.0001f);
                Assert.AreEqual(i % 3, entries[i].spawnIndex);
                Assert.NotNull(entries[i].unitType);
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

                Assert.AreEqual(a, entries[0].unitType);
                Assert.AreEqual(b, entries[1].unitType);
                Assert.AreEqual(a, entries[2].unitType);
                Assert.AreEqual(b, entries[3].unitType);
                Assert.AreEqual(b, entries[4].unitType);
                Assert.AreEqual(b, entries[5].unitType);
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
