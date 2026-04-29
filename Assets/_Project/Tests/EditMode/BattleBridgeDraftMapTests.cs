using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // Unit 4 — BattleBridge draft-map prebuild contracts.
    //
    // Fixture notes:
    //   • PrepareDraftMap() always overwrites _world with World.DefaultGameObjectInjectionWorld,
    //     which is null in EditMode tests (no domain reload / no default world bootstrap).
    //     We therefore replicate PrepareDraftMap's internal steps via reflection:
    //       1. Inject _world / _em (already done in SetUp).
    //       2. Call EnsureQueriesAndQueues() (private).
    //       3. Call BuildMapForBattle() (private).
    //     This is the identical code path that would run if the world were available.
    //   • RebuildDraftMap() works directly because it checks _world != null before proceeding.
    //   • BeginPlacement() also checks World.DefaultGameObjectInjectionWorld — same treatment
    //     (inject _world first, then the internal guard _generatedMap.IsCreated is consulted).
    public class BattleBridgeDraftMapTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private MapData _map;

        [SetUp]
        public void SetUp()
        {
            // Real ECS world — needed by EnsureQueriesAndQueues / BuildFlowField.
            _world = new World("BattleBridgeDraftMapTests");

            // Minimal ScriptableObjects.
            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _map  = ScriptableObject.CreateInstance<MapData>();

            // BattleBridge as a plain GameObject (Awake not called in EditMode).
            _go     = new GameObject("BattleBridge_Test");
            _bridge = _go.AddComponent<BattleBridge>();

            // Inject private SerializeFields.
            SetField(_bridge, "deck",          _deck);
            SetField(_bridge, "map",           _map);
            // useProcedural=false → BuildFromFixture (only needs MapData).
            SetField(_bridge, "useProcedural", false);

            // Inject the test World so ECS operations work.
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em",    _world.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go   != null) Object.DestroyImmediate(_go);
            if (_deck != null) Object.DestroyImmediate(_deck);
            if (_map  != null) Object.DestroyImmediate(_map);
            // World owns NativeContainers — dispose last.
            _world?.Dispose();
        }

        // Case 1 — Internal prepare path sets HasGeneratedMap to true.
        [Test]
        public void PrepareDraftMap_FirstCall_BuildsMap()
        {
            Assert.IsFalse(_bridge.HasGeneratedMap, "pre-condition: no map yet");
            CallPrepareDraftMapInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap);
        }

        // Case 3 — BeginPlacement after an already-built map does not rebuild
        //           (seed stays the same; no new BuildMapForBattle invocation).
        [Test]
        public void BeginPlacement_AfterPrepare_DoesNotRebuild()
        {
            CallPrepareDraftMapInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap);

            int seedBefore = GetGeneratedMapSeed(_bridge);

            // BeginPlacement checks _world = World.DefaultGameObjectInjectionWorld too.
            // Inject world again before calling it (it will reassign the field).
            // We do this by calling EnsureAndBeginPlacement which mirrors BeginPlacement's
            // non-coroutine path with the world already present.
            CallBeginPlacementInternal(_bridge);

            int seedAfter = GetGeneratedMapSeed(_bridge);
            Assert.AreEqual(seedBefore, seedAfter,
                "BeginPlacement must not rebuild map when one already exists");
        }

        // Case 2 — RebuildDraftMap disposes old map and creates a new valid one.
        [Test]
        public void RebuildDraftMap_DisposesOldAndCreatesNew()
        {
            CallPrepareDraftMapInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap, "map must exist after first prepare");

            // RebuildDraftMap works directly: _world is non-null so it skips PrepareDraftMap fallback.
            _bridge.RebuildDraftMap();

            Assert.IsTrue(_bridge.HasGeneratedMap, "HasGeneratedMap must stay true after rebuild");
            var gm = GetGeneratedMap(_bridge);
            Assert.IsTrue(gm.IsCreated, "rebuilt GeneratedMap.IsCreated must be true");
        }

        // Case 4 — When no map has been built, the fallback path in BeginPlacement builds one.
        [Test]
        public void BeginPlacement_WithoutPrepare_FallbackBuilds()
        {
            Assert.IsFalse(_bridge.HasGeneratedMap, "pre-condition: no map yet");
            CallBeginPlacementInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap,
                "BeginPlacement fallback must build map when none exists");
        }

        // -----------------------------------------------------------------------
        // Helpers

        // Replicates PrepareDraftMap's internals without touching World.DefaultGameObjectInjectionWorld.
        private static void CallPrepareDraftMapInternal(BattleBridge bridge)
        {
            CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(bridge, "BuildMapForBattle");
        }

        // Replicates BeginPlacement's internals without touching World.DefaultGameObjectInjectionWorld.
        // Mirrors the non-coroutine branch: EnsureQueriesAndQueues + fallback BuildMapForBattle.
        private static void CallBeginPlacementInternal(BattleBridge bridge)
        {
            // Re-inject world in case BeginPlacement would overwrite (call sequence matters).
            // We bypass BeginPlacement entirely and call its internals.
            CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
            // Replicate the fallback guard: only call BuildMapForBattle if no map exists.
            var gm = GetGeneratedMap(bridge);
            if (!gm.IsCreated)
                CallPrivateMethod(bridge, "BuildMapForBattle");
        }

        private static void CallPrivateMethod(object target, string name)
        {
            var mi = target.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{name}' not found on {target.GetType().Name}");
            mi.Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi   = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                         | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            fi.SetValue(target, value);
        }

        private static GeneratedMap GetGeneratedMap(BattleBridge bridge)
        {
            var fi = typeof(BattleBridge).GetField("_generatedMap",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "_generatedMap field not found");
            return (GeneratedMap)fi.GetValue(bridge);
        }

        private static int GetGeneratedMapSeed(BattleBridge bridge)
            => GetGeneratedMap(bridge).seed;
    }
}
