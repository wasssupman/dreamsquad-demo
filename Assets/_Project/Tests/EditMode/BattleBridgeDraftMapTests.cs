using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // Unit 4 — BattleBridge draft-map prebuild contracts.
    // map-pipeline-cleanup unit 2 — fixture 를 legacy MapData 주입에서 **라이브 경로**
    // (mapPool → MapGridBattleAdapter → ToGeneratedMap)로 재작성. 검증하는 라이프사이클
    // 계약(빌드/재빌드/BeginPlacement 무재빌드/폴백 빌드)은 동일하다.
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
    public class BattleBridgeDraftMapTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private MapDocument _doc;
        private MapDocumentPool _pool;

        [SetUp]
        public void SetUp()
        {
            // Real ECS world — needed by EnsureQueriesAndQueues / BuildFlowField.
            _world = new World("BattleBridgeDraftMapTests");

            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _doc  = BuildUsableDocument();
            _pool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(_pool, _doc, deck: null); // deck null → serialized deck 폴백 계약

            // BattleBridge as a plain GameObject (Awake not called in EditMode).
            _go     = new GameObject("BattleBridge_Test");
            _bridge = _go.AddComponent<BattleBridge>();

            SetField(_bridge, "deck",    _deck);
            SetField(_bridge, "mapPool", _pool);

            // Inject the test World so ECS operations work.
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em",    _world.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go   != null) Object.DestroyImmediate(_go);
            if (_deck != null) Object.DestroyImmediate(_deck);
            if (_doc  != null) Object.DestroyImmediate(_doc);
            if (_pool != null) Object.DestroyImmediate(_pool);
            // World owns NativeContainers — dispose last.
            _world?.Dispose();
        }

        // 최소 usable 문서: 6×4, y=2 복도 Walk, 스폰 2(connectivity 는 스폰 ≥2 요구), 골 1.
        private static MapDocument BuildUsableDocument()
        {
            const int w = 6;
            const int h = 4;
            int n = w * h;

            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                w, h,
                tiles,
                new[] { new Vector2Int(w - 1, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: 42,
                version: 1);
            return doc;
        }

        private static void AddPoolEntry(MapDocumentPool pool, MapDocument doc, AttackDeck deck)
        {
            var fi = typeof(MapDocumentPool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "MapDocumentPool.entries field not found");
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapDocumentPool.Entry { document = doc, deck = deck });
        }

        // Case 1 — Internal prepare path sets HasGeneratedMap to true.
        [Test]
        public void PrepareDraftMap_FirstCall_BuildsMap()
        {
            Assert.IsFalse(_bridge.HasGeneratedMap, "pre-condition: no map yet");
            CallPrepareDraftMapInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap);
        }

        // Case 2 — 풀 문서 경로가 실제로 쓰였다(폴백 리니어가 아니라 doc authoringSeed).
        [Test]
        public void PrepareDraftMap_UsesPoolDocument()
        {
            CallPrepareDraftMapInternal(_bridge);
            Assert.AreEqual(42, GetGeneratedMapSeed(_bridge),
                "GeneratedMap must come from the pool document (authoringSeed), not a fallback");
        }

        // Case 3 — BeginPlacement after an already-built map does not rebuild
        //           (seed stays the same; no new BuildMapForBattle invocation).
        [Test]
        public void BeginPlacement_AfterPrepare_DoesNotRebuild()
        {
            CallPrepareDraftMapInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap);

            int seedBefore = GetGeneratedMapSeed(_bridge);
            CallBeginPlacementInternal(_bridge);
            int seedAfter = GetGeneratedMapSeed(_bridge);

            Assert.AreEqual(seedBefore, seedAfter,
                "BeginPlacement must not rebuild map when one already exists");
        }

        // Case 4 — RebuildDraftMap disposes old map and creates a new valid one.
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

        // Case 5 — When no map has been built, the fallback path in BeginPlacement builds one.
        [Test]
        public void BeginPlacement_WithoutPrepare_FallbackBuilds()
        {
            Assert.IsFalse(_bridge.HasGeneratedMap, "pre-condition: no map yet");
            CallBeginPlacementInternal(_bridge);
            Assert.IsTrue(_bridge.HasGeneratedMap,
                "BeginPlacement fallback must build map when none exists");
        }

        // ── map-pipeline-cleanup feature-end 리뷰 반영 — 리팩터가 중앙화한 안전망 계약 ──

        // Case 6 — connectivity 실패 문서 → FallbackLinear 대체(절차 폴백 제거 후 유일한 런타임 안전망).
        [Test]
        public void BuildMap_UnconnectedDocument_FallsBackToLinear()
        {
            var badDoc = BuildUnconnectedDocument();
            var badPool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(badPool, badDoc, deck: null);
            SetField(_bridge, "mapPool", badPool);
            try
            {
                LogAssert.Expect(LogType.Warning,
                    "[BattleBridge] GeneratedMap connectivity failed; using fallback linear map.");
                CallPrepareDraftMapInternal(_bridge);

                Assert.IsTrue(_bridge.HasGeneratedMap, "fallback must still produce a map");
                var gm = GetGeneratedMap(_bridge);
                Assert.AreEqual(new Unity.Mathematics.int2(20, 10), gm.gridSize,
                    "fallback linear must use FallbackGridSize (20x10)");
                Assert.AreEqual(2, gm.spawns.Length, "fallback linear must use FallbackSpawnLaneCount (2)");
                Assert.IsTrue(MapConnectivity.AllSpawnsReachGoal(gm), "fallback map must be playable");
            }
            finally
            {
                Object.DestroyImmediate(badDoc);
                Object.DestroyImmediate(badPool);
            }
        }

        // Case 7 — 선택된 풀 엔트리가 unusable → hard-fail: 크래시 없이 맵 없음(LogError + 정지).
        [Test]
        public void BuildMap_UnusableSelectedEntry_HardFails_NoMap()
        {
            var emptyDoc = ScriptableObject.CreateInstance<MapDocument>(); // Width 0 → unusable
            var badPool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(badPool, emptyDoc, deck: null);
            SetField(_bridge, "mapPool", badPool);
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                    "usable MapDocument"));
                CallPrepareDraftMapInternal(_bridge);

                Assert.IsFalse(_bridge.HasGeneratedMap,
                    "unusable entry must hard-fail with no map (no silent fallback)");
            }
            finally
            {
                Object.DestroyImmediate(emptyDoc);
                Object.DestroyImmediate(badPool);
            }
        }

        // Case 8 — 풀 미배선 가드: 실제 PrepareDraftMap 이 world 접근 전에 명확히 거부.
        [Test]
        public void PrepareDraftMap_WithoutPool_GuardBlocks()
        {
            SetField(_bridge, "mapPool", null);

            LogAssert.Expect(LogType.Error, "[BattleBridge] deck or map pool reference missing.");
            _bridge.PrepareDraftMap(); // 가드가 world 참조보다 앞이라 EditMode 에서 안전

            Assert.IsFalse(_bridge.HasGeneratedMap, "guard must block map build without a pool");
        }

        // 좌측 복도(스폰 2)와 골이 단절된 문서 — ToGeneratedMap 은 성공하지만 connectivity 실패.
        private static MapDocument BuildUnconnectedDocument()
        {
            const int w = 6;
            const int h = 4;
            int n = w * h;

            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x <= 2; x++) tiles[2 * w + x] = MapTileType.Walk; // 좌측 섬
            tiles[2 * w + (w - 1)] = MapTileType.Walk;                        // 골 섬(단절)

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                w, h,
                tiles,
                new[] { new Vector2Int(w - 1, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: 43,
                version: 1);
            return doc;
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
            CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
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
