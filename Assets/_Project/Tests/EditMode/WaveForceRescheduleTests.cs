using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pattern unit 9 — ForceNextWave 가 남은 웨이브 스케줄을 함께 앞당기는지.
    //
    // Fixture notes:
    //   • ECS world 불필요. QueueWave 는 _generatedMap 미생성 시 laneCount=1 로 떨어지고
    //     GameManager.Instance?.Logger 는 null-safe 라 순수 스케줄 경로만 돈다.
    //   • _wavePlan / _running / _usingGeneratedWaves / _battleClock 을 리플렉션으로 주입해
    //     StartBattle(ECS 의존) 없이 스케줄러만 격리 검증한다.
    //   • 검증 대상은 "다음 웨이브가 강제 호출 시점 + 원래 간격에 나온다"는 불변식이다.
    public class WaveForceRescheduleTests
    {
        private const float Interval = 10f;

        private GameObject _go;
        private BattleBridge _bridge;
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            _go = new GameObject("BattleBridge_WaveForceTest");
            _bridge = _go.AddComponent<BattleBridge>();

            // 0, 10, 20, 30초 등간격 4웨이브 — seed 경로가 만드는 모양과 동일(i * interval).
            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 4; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 2, _b, 2));

            var plan = new GeneratedWavePlan(
                seed: 1, generatorVersion: 2, timerDurationSec: 40f,
                waveIntervalSec: Interval, intraWaveSpacingSec: 1f, waves: waves);

            SetField(_bridge, "_wavePlan", plan);
            SetField(_bridge, "_usingGeneratedWaves", true);
            SetField(_bridge, "_running", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        // 기준선 — 강제 호출이 없으면 플랜의 시각을 그대로 따른다(오프셋 초기값 0).
        [Test]
        public void NoForce_KeepsPlannedSchedule()
        {
            QueueDueWaves(0f);
            Assert.AreEqual(1, NextWaveIndex(), "0초에 wave 1 만 큐잉");

            QueueDueWaves(9.9f);
            Assert.AreEqual(1, NextWaveIndex(), "9.9초는 아직 wave 2 예정 시각 전");

            QueueDueWaves(10f);
            Assert.AreEqual(2, NextWaveIndex(), "10초에 wave 2 큐잉");
        }

        // 핵심 — 3초에 wave 2 를 강제 호출하면 wave 3 은 13초(= 3 + 간격)에 나와야 한다.
        // 수정 전에는 원래 예정대로 20초에 나와 "당긴 만큼의 공백"이 생겼다.
        [Test]
        public void ForceNextWave_ShiftsFollowingWaveByPulledAmount()
        {
            QueueDueWaves(0f);            // wave 1 자동 큐잉 → index 1
            SetBattleClock(3f);
            _bridge.ForceNextWave();      // wave 2 를 10초 → 3초로 당김
            Assert.AreEqual(2, NextWaveIndex());

            QueueDueWaves(12.9f);
            Assert.AreEqual(2, NextWaveIndex(), "wave 3 은 3+10=13초 전에 나오면 안 된다");

            QueueDueWaves(13f);
            Assert.AreEqual(3, NextWaveIndex(), "wave 3 은 강제 호출 시점 + 간격에 나온다");
        }

        // 오프셋이 균일해 남은 웨이브 전체의 간격이 보존된다(마지막 웨이브까지).
        [Test]
        public void ForceNextWave_PreservesIntervalForAllRemainingWaves()
        {
            QueueDueWaves(0f);
            SetBattleClock(3f);
            _bridge.ForceNextWave();      // shift = -7

            QueueDueWaves(13f);           // wave 3
            Assert.AreEqual(3, NextWaveIndex());

            QueueDueWaves(22.9f);
            Assert.AreEqual(3, NextWaveIndex(), "wave 4 는 13+10=23초 전에 나오면 안 된다");

            QueueDueWaves(23f);
            Assert.AreEqual(4, NextWaveIndex(), "마지막 웨이브도 같은 간격을 유지한다");
        }

        // 연타 — 매 호출이 그 시점 기준으로 재기준되고, 추가 웨이브는 생기지 않는다.
        [Test]
        public void ForceNextWave_RepeatedTaps_RebaseFromEachCall()
        {
            QueueDueWaves(0f);
            SetBattleClock(3f);
            _bridge.ForceNextWave();      // wave 2 @3
            _bridge.ForceNextWave();      // wave 3 @3
            Assert.AreEqual(3, NextWaveIndex(), "연타는 추가 웨이브를 만들지 않는다");

            QueueDueWaves(12.9f);
            Assert.AreEqual(3, NextWaveIndex(), "wave 4 는 마지막 호출 시점 + 간격 전에 안 나온다");

            QueueDueWaves(13f);
            Assert.AreEqual(4, NextWaveIndex());
        }

        // 플랜 자체는 불변 — 브리핑 스트립·로그가 읽는 triggerTimeSec 은 오염되지 않는다.
        [Test]
        public void ForceNextWave_DoesNotMutatePlanTriggerTimes()
        {
            QueueDueWaves(0f);
            SetBattleClock(3f);
            _bridge.ForceNextWave();

            var plan = (GeneratedWavePlan)GetField(_bridge, "_wavePlan");
            for (int i = 0; i < plan.waves.Count; i++)
                Assert.AreEqual(i * Interval, plan.waves[i].triggerTimeSec, 0.0001f,
                    $"wave {i} 의 플랜 시각은 강제 호출과 무관하게 유지되어야 한다");
        }

        // ---- helpers ----

        private void QueueDueWaves(float elapsedSec)
        {
            SetBattleClock(elapsedSec);
            Invoke(_bridge, "QueueDueWaves", elapsedSec);
        }

        private void SetBattleClock(float sec) => SetField(_bridge, "_battleClock", (double)sec);

        private int NextWaveIndex() => (int)GetField(_bridge, "_nextWaveIndex");

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }

        private static FieldInfo FindField(object target, string name)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            return fi;
        }

        private static void SetField(object target, string name, object value) =>
            FindField(target, name).SetValue(target, value);

        private static object GetField(object target, string name) =>
            FindField(target, name).GetValue(target);

        private static void Invoke(object target, string name, params object[] args)
        {
            var mi = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance
                                                    | BindingFlags.Public);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, args);
        }
    }
}
