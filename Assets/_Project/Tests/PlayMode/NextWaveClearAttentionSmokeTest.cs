using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // nextwave-clear-attention unit 3 — Bridge의 pending/live 합집합과 View 어필 수명이
    // 한 프레임 폴링 경계에서 함께 전환되는지 확인하는 hybrid smoke.
    public class NextWaveClearAttentionSmokeTest
    {
        private World _world;
        private EntityManager _em;
        private GameObject _bridgeGo;
        private GameObject _dockGo;
        private BattleBridge _bridge;
        private NextWaveDock _dock;
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _world = new World("NextWaveClearAttentionSmokeTest");
            _em = _world.EntityManager;
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            _bridgeGo = new GameObject("BattleBridge_NextWaveClearAttentionSmokeTest");
            LogAssert.Expect(
                LogType.Error,
                "[BattleBridge] tilemapMapView reference missing — assign in Inspector.");
            LogAssert.Expect(
                LogType.Error,
                "[BattleBridge] placementInput reference missing — assign in Inspector.");
            LogAssert.Expect(
                LogType.Error,
                "[BattleBridge] SeasonRegistry / activeSeason / mapTheme 가 wiring 되지 않았다. BattleScene 에 SeasonRegistry.asset 을 연결하라.");
            _bridge = _bridgeGo.AddComponent<BattleBridge>();
            SetField(_bridge, "_wavePlan", new GeneratedWavePlan(
                seed: 1,
                generatorVersion: 2,
                timerDurationSec: 30f,
                waveIntervalSec: 10f,
                intraWaveSpacingSec: 1f,
                waves: new List<GeneratedWave>
                {
                    new GeneratedWave(0, 0f, _a, 1, _b, 1),
                    new GeneratedWave(1, 10f, _a, 1, _b, 1),
                    new GeneratedWave(2, 20f, _a, 1, _b, 1)
                }));
            SetField(_bridge, "_usingGeneratedWaves", true);
            SetField(_bridge, "_running", true);
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _em);
            SetField(
                _bridge,
                "_aliveAttackersQuery",
                _em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>()));
            SetField(_bridge, "_aliveAttackersQueryCreated", true);

            _dockGo = new GameObject("NextWaveDock_NextWaveClearAttentionSmokeTest");
            _dock = _dockGo.AddComponent<NextWaveDock>();
            SetField(_dock, "bridge", _bridge);
            // Full PlayMode assembly leaves a GameManager singleton between some cases.
            // This smoke owns its phase explicitly; prevent lazy subscription from
            // replacing Battle with unrelated prior-test phase state.
            SetField(_dock, "_subscribed", true);
            Invoke(_dock, "OnPhaseChanged", GamePhase.Battle);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dockGo != null) Object.DestroyImmediate(_dockGo);
            if (_bridgeGo != null) Object.DestroyImmediate(_bridgeGo);
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
            _world?.Dispose();
        }

        [UnityTest]
        public IEnumerator PendingAliveClearAndForceNextWave_DriveAttentionLifetime()
        {
            SetField(_bridge, "_battleClock", 0.0);
            Invoke(_bridge, "QueueDueWaves", 0f);
            Invoke(_bridge, "RefreshNextWaveClearReady");
            yield return null;

            Assert.Greater(PendingCount(), 0, "lead-in/stagger entries must remain queued");
            Assert.IsFalse(_bridge.NextWaveClearReady);
            Assert.IsFalse(ClearVisual());

            ((IList)GetField(_bridge, "_pending")).Clear();
            var attacker = _em.CreateEntity(typeof(AttackUnitTag));
            Invoke(_bridge, "RefreshNextWaveClearReady");
            yield return null;

            Assert.IsFalse(_bridge.NextWaveClearReady);
            Assert.IsFalse(ClearVisual(), "live attacker must suppress the CTA attention");

            _em.DestroyEntity(attacker);
            Invoke(_bridge, "RefreshNextWaveClearReady");
            yield return null;

            Assert.IsTrue(_bridge.NextWaveClearReady);
            Assert.IsTrue(ClearVisual());
            Assert.IsNotNull(GetField(_dock, "_attention"));
            AssertPulseRings(active: true);

            SetField(_bridge, "_battleClock", 1.0);
            _bridge.ForceNextWave();
            yield return null;

            Assert.IsFalse(_bridge.NextWaveClearReady);
            Assert.IsFalse(ClearVisual());
            Assert.IsNull(GetField(_dock, "_attention"));
            AssertPulseRings(active: false);
        }

        private int PendingCount() => ((IList)GetField(_bridge, "_pending")).Count;

        private bool ClearVisual() => (bool)GetField(_dock, "_clearReadyVisual");

        private void AssertPulseRings(bool active)
        {
            var rings = (RectTransform[])GetField(_dock, "_pulseRings");
            Assert.AreEqual(2, rings.Length);
            for (int i = 0; i < rings.Length; i++)
            {
                Assert.IsNotNull(rings[i]);
                Assert.AreEqual(active, rings[i].gameObject.activeSelf, $"pulse ring {i}");
            }
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
                field = type.GetField(
                    name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
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
