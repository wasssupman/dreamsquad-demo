using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    // wave-pattern unit 9 — ForceNextWave 가 남은 웨이브 스케줄을 함께 앞당기는지.
    //
    // battle-sim-extraction unit 14 — 규칙이 `MatchWaveSchedule` 로 이사해서 이 픽스처는
    // **BattleBridge·GameObject·리플렉션을 전부 버렸다.** 예전에는 private 필드 3개를 주입하고
    // private 메서드를 리플렉션으로 불러야 스케줄러만 격리할 수 있었다. 지금은 그냥 객체다 —
    // 규칙을 sim 후보 모듈로 옮긴 값이 여기서 바로 보인다.
    //
    // 검증 대상은 "다음 웨이브가 강제 호출 시점 + 원래 간격에 나온다"는 불변식이다.
    public class WaveForceRescheduleTests
    {
        private const float Interval = 10f;
        private const int LaneCount = 1;

        private MatchWaveSchedule _schedule;
        private GeneratedWavePlan _plan;
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            // 0, 10, 20, 30초 등간격 4웨이브 — seed 경로가 만드는 모양과 동일(i * interval).
            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 4; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 2, _b, 2));

            _plan = new GeneratedWavePlan(
                seed: 1, generatorVersion: 2, timerDurationSec: 40f,
                waveIntervalSec: Interval, intraWaveSpacingSec: 1f, waves: waves);

            _schedule = new MatchWaveSchedule();
            _schedule.Initialize(_plan, authored: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        // 기준선 — 강제 호출이 없으면 플랜의 시각을 그대로 따른다(오프셋 초기값 0).
        [Test]
        public void NoForce_KeepsPlannedSchedule()
        {
            QueueDueWaves(0f);
            Assert.AreEqual(1, _schedule.NextWaveIndex, "0초에 wave 1 만 큐잉");

            QueueDueWaves(9.9f);
            Assert.AreEqual(1, _schedule.NextWaveIndex, "9.9초는 아직 wave 2 예정 시각 전");

            QueueDueWaves(10f);
            Assert.AreEqual(2, _schedule.NextWaveIndex, "10초에 wave 2 큐잉");
        }

        // 핵심 — 3초에 wave 2 를 강제 호출하면 wave 3 은 13초(= 3 + 간격)에 나와야 한다.
        // 수정 전에는 원래 예정대로 20초에 나와 "당긴 만큼의 공백"이 생겼다.
        [Test]
        public void ForceNextWave_ShiftsFollowingWaveByPulledAmount()
        {
            QueueDueWaves(0f);            // wave 1 자동 큐잉 → index 1
            ForceNextWave(3f);            // wave 2 를 10초 → 3초로 당김
            Assert.AreEqual(2, _schedule.NextWaveIndex);

            QueueDueWaves(12.9f);
            Assert.AreEqual(2, _schedule.NextWaveIndex, "wave 3 은 3+10=13초 전에 나오면 안 된다");

            QueueDueWaves(13f);
            Assert.AreEqual(3, _schedule.NextWaveIndex, "wave 3 은 강제 호출 시점 + 간격에 나온다");
        }

        // 오프셋이 균일해 남은 웨이브 전체의 간격이 보존된다(마지막 웨이브까지).
        [Test]
        public void ForceNextWave_PreservesIntervalForAllRemainingWaves()
        {
            QueueDueWaves(0f);
            ForceNextWave(3f);            // shift = -7

            QueueDueWaves(13f);           // wave 3
            Assert.AreEqual(3, _schedule.NextWaveIndex);

            QueueDueWaves(22.9f);
            Assert.AreEqual(3, _schedule.NextWaveIndex, "wave 4 는 13+10=23초 전에 나오면 안 된다");

            QueueDueWaves(23f);
            Assert.AreEqual(4, _schedule.NextWaveIndex, "마지막 웨이브도 같은 간격을 유지한다");
        }

        // 연타 — 매 호출이 그 시점 기준으로 재기준되고, 추가 웨이브는 생기지 않는다(비멱등 계약).
        [Test]
        public void ForceNextWave_RepeatedTaps_RebaseFromEachCall()
        {
            QueueDueWaves(0f);
            ForceNextWave(3f);            // wave 2 @3
            ForceNextWave(3f);            // wave 3 @3
            Assert.AreEqual(3, _schedule.NextWaveIndex, "연타는 추가 웨이브를 만들지 않는다");

            QueueDueWaves(12.9f);
            Assert.AreEqual(3, _schedule.NextWaveIndex, "wave 4 는 마지막 호출 시점 + 간격 전에 안 나온다");

            QueueDueWaves(13f);
            Assert.AreEqual(4, _schedule.NextWaveIndex);
        }

        // 플랜 자체는 불변 — 브리핑 스트립·로그가 읽는 triggerTimeSec 은 오염되지 않는다.
        [Test]
        public void ForceNextWave_DoesNotMutatePlanTriggerTimes()
        {
            QueueDueWaves(0f);
            ForceNextWave(3f);

            GeneratedWavePlan plan = _schedule.Plan;
            for (int i = 0; i < plan.waves.Count; i++)
                Assert.AreEqual(i * Interval, plan.waves[i].triggerTimeSec, 0.0001f,
                    $"wave {i} 의 플랜 시각은 강제 호출과 무관하게 유지되어야 한다");
        }

        // 마지막 웨이브까지 큐잉된 뒤의 강제 호출은 거절된다(인덱스도 스케줄도 안 움직인다).
        [Test]
        public void ForceNextWave_AfterLastWave_IsRejected()
        {
            QueueDueWaves(1000f);
            Assert.AreEqual(4, _schedule.NextWaveIndex, "전부 큐잉된 상태");
            Assert.IsFalse(_schedule.TryForceNextWave(1000f, LaneCount, null));
            Assert.AreEqual(4, _schedule.NextWaveIndex);
        }

        // ---- helpers ----

        private void QueueDueWaves(float elapsedSec)
            => _schedule.QueueDueWaves(elapsedSec, LaneCount, null);

        private void ForceNextWave(float elapsedSec)
            => Assert.IsTrue(_schedule.TryForceNextWave(elapsedSec, LaneCount, null),
                $"{elapsedSec}초의 강제 호출이 받아들여져야 한다");

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }
    }
}
