using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    // nextwave-clear-attention unit 0 — 호출된 웨이브의 pending/live 합집합이
    // 실제로 빌 때만 다음 웨이브 강조가 켜지는지 검증한다.
    //
    // battle-sim-extraction unit 14 — 이 픽스처는 **Bridge 에 남는다**. 검증 대상이 "대기열(스케줄
    // 모듈) + 살아 있는 적(ECS 질의)의 합집합" 이라 두 소유자가 만나는 지점이 곧 Bridge 다.
    // 상태 주입은 `_waveSchedule` 을 꺼내 그 공개 API 로 한다 — private 필드 주입이 아니라
    // 실제 경로(Initialize/QueueDueWaves/TakeDueSpawns)를 지나므로 픽스처가 규칙을 우회하지 않는다.
    public class NextWaveClearReadyTests
    {
        private const float Interval = 10f;
        private const int LaneCount = 1;

        private World _world;
        private EntityManager _em;
        private GameObject _go;
        private BattleBridge _bridge;
        private MatchWaveSchedule _schedule;
        private AttackUnitData _a;
        private AttackUnitData _b;
        private readonly List<MatchWaveSchedule.PendingSpawnEntry> _drain = new();

        [SetUp]
        public void SetUp()
        {
            _world = new World("NextWaveClearReadyTests");
            _em = _world.EntityManager;
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            _go = new GameObject("BattleBridge_NextWaveClearReadyTests");
            _bridge = _go.AddComponent<BattleBridge>();

            _schedule = (MatchWaveSchedule)GetField(_bridge, "_waveSchedule");
            _schedule.Initialize(BuildPlan(), authored: false);

            SetField(_bridge, "_running", true);
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _em);
            SetField(_bridge, "_aliveAttackersQuery",
                _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>()));
            SetField(_bridge, "_aliveAttackersQueryCreated", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
            _world?.Dispose();
        }

        private GeneratedWavePlan BuildPlan()
        {
            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 3; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 1, _b, 1));
            return new GeneratedWavePlan(
                seed: 1,
                generatorVersion: 2,
                timerDurationSec: 30f,
                waveIntervalSec: Interval,
                intraWaveSpacingSec: 1f,
                waves: waves);
        }

        [Test]
        public void BeforeFirstWave_IsFalse()
        {
            RefreshClearReady();

            Assert.IsFalse(_bridge.NextWaveClearReady);
        }

        // 자동 큐잉은 직전의 ready 래치를 즉시 내린다(리드인·스태거로 아직 안 나온 적이 있다).
        [Test]
        public void PendingLeadInOrStagger_IsFalse()
        {
            QueueDueWaves(0f);
            ClearPending();
            RefreshClearReady();
            Assert.IsTrue(ClearReadyCache(), "wave 1 이 비면 강조가 켜진다(전제)");

            QueueDueWaves(Interval); // wave 2 자동 큐잉

            Assert.AreEqual(2, NextWaveIndex());
            Assert.Greater(PendingCount(), 0);
            Assert.IsFalse(ClearReadyCache(), "automatic QueueWave must clear the previous ready cache");
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady);
        }

        [Test]
        public void PendingEmpty_ButAliveAttacker_IsFalse_ThenBecomesTrue()
        {
            QueueDueWaves(0f);
            ClearPending();
            var attacker = _em.CreateEntity(typeof(AttackUnitTag));

            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady);

            _em.DestroyEntity(attacker);
            RefreshClearReady();
            Assert.IsTrue(_bridge.NextWaveClearReady);
        }

        [Test]
        public void KillAndGoalRemoval_ConvergeToSameEmptyState()
        {
            QueueDueWaves(0f);
            ClearPending();
            var killed = _em.CreateEntity(typeof(AttackUnitTag), typeof(DeadTag));
            var leaked = _em.CreateEntity(typeof(AttackUnitTag), typeof(PastGoalTag));

            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady);

            _em.DestroyEntity(killed);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "goal-reached attacker still remains");

            _em.DestroyEntity(leaked);
            RefreshClearReady();
            Assert.IsTrue(_bridge.NextWaveClearReady);
        }

        [Test]
        public void OverlappingForcedWaves_WaitForWholeQueuedUnion()
        {
            QueueDueWaves(0f);
            SetBattleClock(1.0);
            _bridge.ForceNextWave();
            Assert.AreEqual(2, NextWaveIndex());

            ClearPending();
            var fromFirstWave = _em.CreateEntity(typeof(AttackUnitTag));
            var fromSecondWave = _em.CreateEntity(typeof(AttackUnitTag));

            _em.DestroyEntity(fromSecondWave);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "older queued wave still has an attacker");

            _em.DestroyEntity(fromFirstWave);
            RefreshClearReady();
            Assert.IsTrue(_bridge.NextWaveClearReady);
        }

        [Test]
        public void ForceNextWave_ImmediatelyClearsReadyState()
        {
            QueueDueWaves(0f);
            ClearPending();
            RefreshClearReady();
            Assert.IsTrue(_bridge.NextWaveClearReady);

            SetBattleClock(1.0);
            _bridge.ForceNextWave();

            Assert.IsFalse(ClearReadyCache(), "forced QueueWave must clear the ready cache immediately");
            Assert.IsFalse(_bridge.NextWaveClearReady);
            Assert.Greater(PendingCount(), 0);
        }

        [Test]
        public void FinalWaveLegacyAndStoppedStates_AreFalse()
        {
            // ① 마지막 웨이브까지 큐잉된 상태 — 다음 액션이 없다. (인덱스를 주입하지 않고 실제
            //    큐잉으로 도달시킨다 — 픽스처가 규칙을 우회하지 않게.)
            QueueDueWaves(1000f);
            Assert.AreEqual(3, NextWaveIndex());
            ClearPending();
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "final wave has no next action");

            // ② legacy 전투(생성 웨이브 없음) — Next Wave 자체를 노출하지 않는다.
            _schedule.Initialize(default, authored: false);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "legacy battles do not expose Next Wave");

            // ③ 전투 종료 — 남은 웨이브가 있어도 강조를 노출하지 않는다.
            _schedule.Initialize(BuildPlan(), authored: false);
            QueueDueWaves(0f);
            ClearPending();
            SetField(_bridge, "_running", false);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "stopped battle must not expose attention");
        }

        // ---- helpers ----

        private void QueueDueWaves(float elapsedSec)
        {
            SetBattleClock(elapsedSec);
            _schedule.QueueDueWaves(elapsedSec, LaneCount, null);
        }

        private void SetBattleClock(double sec) => SetField(_bridge, "_battleClock", sec);

        private void RefreshClearReady() => Invoke(_bridge, "RefreshNextWaveClearReady");

        private int NextWaveIndex() => _schedule.NextWaveIndex;

        private bool ClearReadyCache() => _schedule.ClearReady;

        private int PendingCount() => _schedule.PendingCount;

        /// 대기열을 실제 경로로 비운다 — 트리거 시각이 지난 것을 전부 꺼내는 것이 곧 스폰이다.
        private void ClearPending()
        {
            _drain.Clear();
            _schedule.TakeDueSpawns(float.MaxValue, _drain);
            _drain.Clear();
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }

        private static FieldInfo FindField(object target, string name)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (field == null && type != null)
            {
                field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                           | BindingFlags.Public);
                type = type.BaseType;
            }

            Assert.IsNotNull(field, $"Field '{name}' not found on {target.GetType().Name}");
            return field;
        }

        private static void SetField(object target, string name, object value) =>
            FindField(target, name).SetValue(target, value);

        private static object GetField(object target, string name) =>
            FindField(target, name).GetValue(target);

        private static void Invoke(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, $"Method '{name}' not found on {target.GetType().Name}");
            method.Invoke(target, args);
        }
    }
}
