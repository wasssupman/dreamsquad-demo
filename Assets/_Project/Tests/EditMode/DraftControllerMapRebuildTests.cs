using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // Unit 4 — DraftController triggers BattleBridge.RebuildDraftMap on option changes.
    // Uses a real BattleBridge (with test counter) wired to a DraftController instance.
    public class DraftControllerMapRebuildTests
    {
        private World _world;
        private GameObject _bridgeGo;
        private GameObject _controllerGo;
        private BattleBridge _bridge;
        private DraftController _controller;
        private AttackDeck _deck;
        private MapData _map;
        private DefenderUnitData[] _catalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("DraftControllerMapRebuildTests");

            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _map  = ScriptableObject.CreateInstance<MapData>();

            // Build a small catalog so BeginDraft does not error.
            _catalog = new DefenderUnitData[5];
            for (int i = 0; i < _catalog.Length; i++)
            {
                _catalog[i] = ScriptableObject.CreateInstance<DefenderUnitData>();
                _catalog[i].displayName = $"TestUnit_{i}";
            }

            // BattleBridge.
            _bridgeGo = new GameObject("BattleBridge_DraftCtrl_Test");
            _bridge   = _bridgeGo.AddComponent<BattleBridge>();
            SetPrivateField(_bridge, "deck",           _deck);
            SetPrivateField(_bridge, "map",            _map);
            SetPrivateField(_bridge, "useProcedural",  false);
            SetPrivateField(_bridge, "_world",         _world);
            SetPrivateField(_bridge, "_em",            _world.EntityManager);

            // Replicate PrepareDraftMap internals (avoid DefaultGameObjectInjectionWorld=null path).
            CallPrivateMethod(_bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(_bridge, "BuildMapForBattle");

            // DraftController.
            _controllerGo = new GameObject("DraftController_Test");
            _controller   = _controllerGo.AddComponent<DraftController>();
            SetPrivateField(_controller, "catalog",      _catalog);
            SetPrivateField(_controller, "poolSize",     5);
            SetPrivateField(_controller, "discardCount", 2);
            SetPrivateField(_controller, "battleBridge", _bridge);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerGo != null) Object.DestroyImmediate(_controllerGo);
            if (_bridgeGo     != null) Object.DestroyImmediate(_bridgeGo);
            if (_deck  != null) Object.DestroyImmediate(_deck);
            if (_map   != null) Object.DestroyImmediate(_map);
            foreach (var u in _catalog) if (u != null) Object.DestroyImmediate(u);
            _world?.Dispose();
        }

        // Case 1 — SetMapGenerationOptions calls RebuildDraftMap exactly once.
        [Test]
        public void SetMapGenerationOptions_TriggersBridgeRebuild()
        {
            int before = _bridge.RebuildDraftMapCallCount;
            _controller.SetMapGenerationOptions(MapGenerationOptions.Default);
            Assert.AreEqual(before + 1, _bridge.RebuildDraftMapCallCount,
                "SetMapGenerationOptions must call RebuildDraftMap once");
        }

        // Case 2 — SetMapPathShape calls RebuildDraftMap exactly once.
        [Test]
        public void SetMapPathShape_TriggersBridgeRebuild()
        {
            int before = _bridge.RebuildDraftMapCallCount;
            _controller.SetMapPathShape(MapPathShape.Free);
            Assert.AreEqual(before + 1, _bridge.RebuildDraftMapCallCount,
                "SetMapPathShape must call RebuildDraftMap once");
        }

        // Case 3 — BeginDraft itself does NOT call RebuildDraftMap.
        [Test]
        public void BeginDraft_DoesNotTriggerRebuild()
        {
            int before = _bridge.RebuildDraftMapCallCount;
            _controller.BeginDraft(seed: 42);
            Assert.AreEqual(before, _bridge.RebuildDraftMapCallCount,
                "BeginDraft must not trigger RebuildDraftMap");
        }

        // -----------------------------------------------------------------------
        private static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi   = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            fi.SetValue(target, value);
        }
    }
}
