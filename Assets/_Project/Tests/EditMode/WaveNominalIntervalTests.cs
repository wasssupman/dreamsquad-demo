using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // three-minute-survival unit 2 — 생성기의 **명목** 트리거 그리드. maxWaveIntervalSec>0 이면
    // triggerTime[i] = i × 그 값(= 최악 케이스 시각)을 찍고, 0 이면 레거시 duration/waveCount 로
    // 회귀한다. 런타임 스케줄러는 이 값을 읽지 않는다(전멸/상한 이벤트 구동) — 이 표기는 브리핑
    // 스트립·배틀로그 전용이다. 구 endless-mode 고정 간격 스케줄은 은퇴했다.
    public class WaveNominalIntervalTests
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
        public void MaxInterval_StampsNominalTriggerTimes()
        {
            var plan = WavePatternGenerator.Generate(
                seed: 1, generatorVersion: 1, timerDurationSec: 180f,
                minWaveCount: 30, maxWaveCount: 30, minUnitsPerWave: 8, maxUnitsPerWave: 8,
                intraWaveSpacingSec: 0.35f, attackUnitPool: _units, maxWaveIntervalSec: 10f);

            Assert.AreEqual(30, plan.waves.Count, "min==max==30 → 30 웨이브 고정");
            Assert.AreEqual(10f, plan.waveIntervalSec, 0.0001f, "상한 간격 10 이 plan 에 반영");
            for (int i = 0; i < plan.waves.Count; i++)
                Assert.AreEqual(i * 10f, plan.waves[i].triggerTimeSec, 0.0001f, $"wave {i} triggerTime");
        }

        // 명목 그리드는 제한시간을 넘어도 된다(10초×30 = 290초 > 180초). 런타임이 이 값을 읽지
        // 않으므로 "타이머 밖 웨이브" 는 더 이상 미스폰 사유가 아니다 — 전멸시키면 그만큼
        // 앞당겨 나온다. 여기서는 그리드 산식만 고정한다.
        [Test]
        public void NominalGrid_MayExceedTimerWindow()
        {
            var plan = WavePatternGenerator.Generate(
                seed: 7, generatorVersion: 1, timerDurationSec: 180f,
                minWaveCount: 30, maxWaveCount: 30, minUnitsPerWave: 8, maxUnitsPerWave: 8,
                intraWaveSpacingSec: 0.35f, attackUnitPool: _units, maxWaveIntervalSec: 10f);

            Assert.AreEqual(290f, plan.waves[plan.waves.Count - 1].triggerTimeSec, 0.0001f);
            Assert.Greater(plan.waves[plan.waves.Count - 1].triggerTimeSec, plan.timerDurationSec);
        }

        [Test]
        public void MaxIntervalZero_FallsBackToDurationOverCount()
        {
            var plan = WavePatternGenerator.Generate(
                seed: 1, generatorVersion: 1, timerDurationSec: 180f,
                minWaveCount: 10, maxWaveCount: 10, minUnitsPerWave: 8, maxUnitsPerWave: 8,
                intraWaveSpacingSec: 0.35f, attackUnitPool: _units, maxWaveIntervalSec: 0f);

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
