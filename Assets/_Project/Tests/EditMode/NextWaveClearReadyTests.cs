using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // nextwave-clear-attention unit 0 — 호출된 웨이브의 pending/live 합집합이
    // 실제로 빌 때만 다음 웨이브 강조가 켜지는지 검증한다.
    public class NextWaveClearReadyTests
    {
        private const float Interval = 10f;

        private World _world;
        private EntityManager _em;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _world = new World("NextWaveClearReadyTests");
            _em = _world.EntityManager;
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            _go = new GameObject("BattleBridge_NextWaveClearReadyTests");
            _bridge = _go.AddComponent<BattleBridge>();

            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 3; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 1, _b, 1));

            SetField(_bridge, "_wavePlan", new GeneratedWavePlan(
                seed: 1,
                generatorVersion: 2,
                timerDurationSec: 30f,
                waveIntervalSec: Interval,
                intraWaveSpacingSec: 1f,
                waves: waves));
            SetField(_bridge, "_usingGeneratedWaves", true);
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

        [Test]
        public void BeforeFirstWave_IsFalse()
        {
            RefreshClearReady();

            Assert.IsFalse(_bridge.NextWaveClearReady);
        }

        [Test]
        public void PendingLeadInOrStagger_IsFalse()
        {
            SetField(_bridge, "_nextWaveClearReady", true);
            QueueDueWaves(0f);

            Assert.AreEqual(1, NextWaveIndex());
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
            SetField(_bridge, "_battleClock", 1.0);
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

            SetField(_bridge, "_battleClock", 1.0);
            _bridge.ForceNextWave();

            Assert.IsFalse(ClearReadyCache(), "forced QueueWave must clear the ready cache immediately");
            Assert.IsFalse(_bridge.NextWaveClearReady);
            Assert.Greater(PendingCount(), 0);
        }

        [Test]
        public void FinalWaveLegacyAndStoppedStates_AreFalse()
        {
            QueueDueWaves(0f);
            ClearPending();

            SetField(_bridge, "_nextWaveIndex", 3);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "final wave has no next action");

            SetField(_bridge, "_nextWaveIndex", 1);
            SetField(_bridge, "_usingGeneratedWaves", false);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "legacy battles do not expose Next Wave");

            SetField(_bridge, "_usingGeneratedWaves", true);
            SetField(_bridge, "_running", false);
            RefreshClearReady();
            Assert.IsFalse(_bridge.NextWaveClearReady, "stopped battle must not expose attention");
        }

        private void QueueDueWaves(float elapsedSec)
        {
            SetField(_bridge, "_battleClock", (double)elapsedSec);
            Invoke(_bridge, "QueueDueWaves", elapsedSec);
        }

        private void RefreshClearReady() => Invoke(_bridge, "RefreshNextWaveClearReady");

        private int NextWaveIndex() => (int)GetField(_bridge, "_nextWaveIndex");

        private bool ClearReadyCache() => (bool)GetField(_bridge, "_nextWaveClearReady");

        private int PendingCount() => ((IList)GetField(_bridge, "_pending")).Count;

        private void ClearPending() => ((IList)GetField(_bridge, "_pending")).Clear();

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
