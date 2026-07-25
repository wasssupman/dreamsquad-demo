using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // endless-mode unit 1 — 생성기 고정 간격. fixedIntervalSec>0 이면 웨이브수 의존 파생 대신
    // 그 값으로 triggerTime[i]=i*interval 을 찍는다. 0 이면 기존 duration/waveCount 로 회귀.
    public class WaveFixedIntervalTests
    {
        private readonly List<AttackUnitData> _units = new();

        [SetUp]
        public void SetUp()
        {
            _units.Clear();
            _units.Add(CreateUnit("Basic"));
            _units.Add(CreateUnit("Swift"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _units)
                Object.DestroyImmediate(unit);
            _units.Clear();
        }

        [Test]
        public void FixedInterval_StampsUniformTriggerTimes()
        {
            var plan = WavePatternGenerator.Generate(
                seed: 1, generatorVersion: 1, timerDurationSec: 180f,
                minWaveCount: 30, maxWaveCount: 30, minUnitsPerWave: 8, maxUnitsPerWave: 8,
                intraWaveSpacingSec: 0.35f, attackUnitPool: _units, fixedIntervalSec: 10f);

            Assert.AreEqual(30, plan.waves.Count, "min==max==30 → 30 웨이브 고정");
            Assert.AreEqual(10f, plan.waveIntervalSec, 0.0001f, "고정 간격 10 이 plan 에 반영");
            for (int i = 0; i < plan.waves.Count; i++)
                Assert.AreEqual(i * 10f, plan.waves[i].triggerTimeSec, 0.0001f, $"wave {i} triggerTime");
        }

        // 무한 모드 전제: 10초×30 = 290초 > 180초. 당기기 없으면 마지막 웨이브들은 타이머 밖이라
        // 스케줄러가 안 낸다(실스폰 ~18). "당겨야 더 많이 나온다"는 설계 근거를 여기 고정.
        [Test]
        public void FixedInterval_LastWaveExceedsTimerWindow()
        {
            var plan = WavePatternGenerator.Generate(
                seed: 7, generatorVersion: 1, timerDurationSec: 180f,
                minWaveCount: 30, maxWaveCount: 30, minUnitsPerWave: 8, maxUnitsPerWave: 8,
                intraWaveSpacingSec: 0.35f, attackUnitPool: _units, fixedIntervalSec: 10f);

            Assert.AreEqual(290f, plan.waves[plan.waves.Count - 1].triggerTimeSec, 0.0001f);
            Assert.Greater(plan.waves[plan.waves.Count - 1].triggerTimeSec, plan.timerDurationSec);
        }

        [Test]
        public void FixedIntervalZero_FallsBackToDurationOverCount()
        {
            var plan = WavePatternGenerator.Generate(
                seed: 1, generatorVersion: 1, timerDurationSec: 180f,
                minWaveCount: 10, maxWaveCount: 10, minUnitsPerWave: 8, maxUnitsPerWave: 8,
                intraWaveSpacingSec: 0.35f, attackUnitPool: _units, fixedIntervalSec: 0f);

            Assert.AreEqual(10, plan.waves.Count);
            Assert.AreEqual(18f, plan.waveIntervalSec, 0.0001f, "180/10 = 18 (기존 파생 불변)");
            for (int i = 0; i < plan.waves.Count; i++)
                Assert.AreEqual(i * 18f, plan.waves[i].triggerTimeSec, 0.0001f, $"wave {i} triggerTime");
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
