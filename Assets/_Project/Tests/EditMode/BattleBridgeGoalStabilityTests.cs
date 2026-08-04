using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Battle.Units;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // goal-stability unit 1 — 안정도(M>0) 골 엔티티 스폰/미스폰/멱등/teardown 계약.
    // fixture 는 BattleBridgeDraftMapTests 동형(라이브 경로: mapPool → BuildMapForBattle →
    // BuildFlowField → SpawnGoalEntities, 실 ECS World 주입).
    public class BattleBridgeGoalStabilityTests
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
            _world = new World("BattleBridgeGoalStabilityTests");

            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _doc  = BuildTwoGoalDocument(new[] { 30f, 0f });
            _pool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(_pool, _doc, deck: null);

            _go     = new GameObject("BattleBridge_GoalStabilityTest");
            _bridge = _go.AddComponent<BattleBridge>();

            SetField(_bridge, "deck",    _deck);
            SetField(_bridge, "mapPool", _pool);
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
            _world?.Dispose();
        }

        // 6×4, y=2 복도 Walk, 스폰 2, 골 2 (5,2)·(3,2) — stability 는 goals index 정렬.
        private static MapDocument BuildTwoGoalDocument(float[] stability)
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
                tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(5, 2), new Vector2Int(3, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: 42,
                version: 1,
                goalStabilityArr: stability);
            return doc;
        }

        private int CountGoalEntities()
        {
            using var q = _world.EntityManager.CreateEntityQuery(typeof(GoalPoint));
            return q.CalculateEntityCount();
        }

        // Case 1 — M>0 골만 스폰: 골 2개 중 stability {30, 0} → 엔티티 1, 컴포넌트 값 검증.
        [Test]
        public void Prepare_WithStability_SpawnsOnlyPositiveGoals()
        {
            CallPrepareDraftMapInternal(_bridge);

            using var q = _world.EntityManager.CreateEntityQuery(typeof(GoalPoint));
            var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            Assert.AreEqual(1, entities.Length, "M>0 골만 엔티티가 된다 (M=0 골 미스폰)");

            var em = _world.EntityManager;
            var gp = em.GetComponentData<GoalPoint>(entities[0]);
            Assert.AreEqual(new Unity.Mathematics.int2(5, 2), gp.cell);
            Assert.AreEqual(0, gp.goalIndex);

            var health = em.GetComponentData<Health>(entities[0]);
            Assert.AreEqual(30f, health.value);
            Assert.AreEqual(30f, health.max);

            Assert.AreEqual(Faction.Goal, em.GetComponentData<FactionTag>(entities[0]).value);
            Assert.IsTrue(em.HasBuffer<IncomingDamage>(entities[0]), "IncomingDamage 버퍼 사전 부착");
            Assert.IsTrue(em.HasComponent<Unity.Transforms.LocalTransform>(entities[0]));
            entities.Dispose();
        }

        // Case 2 — 안정도 미authored 문서(레거시) → 골 엔티티 0 = 현행 완전 동일.
        [Test]
        public void Prepare_WithoutStability_SpawnsNone()
        {
            var plainDoc = BuildTwoGoalDocument(stability: null);
            var plainPool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(plainPool, plainDoc, deck: null);
            SetField(_bridge, "mapPool", plainPool);
            try
            {
                CallPrepareDraftMapInternal(_bridge);
                Assert.AreEqual(0, CountGoalEntities(), "안정도 부재 = 전 골 0 = 엔티티 미스폰");
            }
            finally
            {
                Object.DestroyImmediate(plainDoc);
                Object.DestroyImmediate(plainPool);
            }
        }

        // Case 3 — 재빌드 멱등: RebuildDraftMap 후에도 엔티티 중복 없음.
        [Test]
        public void Rebuild_NoDuplicateGoalEntities()
        {
            CallPrepareDraftMapInternal(_bridge);
            Assert.AreEqual(1, CountGoalEntities());

            _bridge.RebuildDraftMap();
            Assert.AreEqual(1, CountGoalEntities(), "재빌드 시 기존 골 엔티티 제거 후 재생성 (중복 금지)");
        }

        // Case 4 — 매치 경계 정리: DestroyBattleEntities 가 골 엔티티를 잔존 없이 제거.
        [Test]
        public void DestroyBattleEntities_RemovesGoalEntities()
        {
            CallPrepareDraftMapInternal(_bridge);
            Assert.AreEqual(1, CountGoalEntities(), "pre-condition");

            CallPrivateMethod(_bridge, "DestroyBattleEntities");
            Assert.AreEqual(0, CountGoalEntities(), "앱-수명 월드 잔존 금지 (teardown 계약)");
        }

        // -----------------------------------------------------------------------
        // Helpers (BattleBridgeDraftMapTests 동형)

        private static void CallPrepareDraftMapInternal(BattleBridge bridge)
        {
            CallPrivateMethod(bridge, "EnsureQueriesAndQueues");
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

        private static void AddPoolEntry(MapDocumentPool pool, MapDocument doc, AttackDeck deck)
        {
            var fi = typeof(MapDocumentPool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "MapDocumentPool.entries field not found");
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapDocumentPool.Entry { document = doc, deck = deck });
        }
    }
}
