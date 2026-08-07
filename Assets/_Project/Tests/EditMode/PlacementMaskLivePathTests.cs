using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // placement-mask unit 3 — 라이브 경로(mapPool → BuildMapForBattle) 에서 B-1 계약 검증.
    //   ① Walk 셀 mask=1 → 배치 가능, Place 셀 mask=0 → 배치 불가 (마스크가 정본)
    //   ② tiles(통행/walkMask 의 유일한 파생원) 는 마스크와 무관하게 불변 — "적 행동 불변" 의
    //      메커니즘 축. 도달 시간 e2e 는 Play 육안 검증(스펙 unit 3)이 담당한다.
    // 픽스처는 BattleBridgeDraftMapTests 와 동일한 reflection 패턴(풀 주입 — 실 풀 미오염).
    public class PlacementMaskLivePathTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private MapDocument _doc;
        private MapDocumentPool _pool;

        private const int W = 6, H = 4;
        private static readonly int2 MaskedWalkCell = new int2(3, 2);   // 복도 위 배치 허용 셀
        private static readonly int2 MaskedOffPlaceCell = new int2(0, 0);   // 배치 금지된 Place 셀

        [SetUp]
        public void SetUp()
        {
            _world = new World("PlacementMaskLivePathTests");
            _deck = ScriptableObject.CreateInstance<AttackDeck>();
            _doc = BuildMaskedDocument();
            _pool = ScriptableObject.CreateInstance<MapDocumentPool>();
            AddPoolEntry(_pool, _doc);

            _go = new GameObject("BattleBridge_MaskTest");
            _bridge = _go.AddComponent<BattleBridge>();
            SetField(_bridge, "deck", _deck);
            SetField(_bridge, "mapPool", _pool);
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_deck != null) Object.DestroyImmediate(_deck);
            if (_doc != null) Object.DestroyImmediate(_doc);
            if (_pool != null) Object.DestroyImmediate(_pool);
            _world?.Dispose();
        }

        // 6×4, y=2 복도 Walk, 스폰 2, 골 1 (BattleBridgeDraftMapTests 의 usable 문서와 동형)
        // + placeMask: 파생값 위에 Walk 셀 (3,2) 허용 / Place 셀 (0,0) 금지.
        private static MapDocument BuildMaskedDocument()
        {
            int n = W * H;
            var tiles = new MapTileType[n];
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < W; x++) tiles[2 * W + x] = MapTileType.Walk;

            var mask = new byte[n];
            for (int i = 0; i < n; i++) mask[i] = PlacementLayers.Derive(tiles[i]);
            mask[2 * W + 3] = (byte)PlacementLayer.Ground;   // MaskedWalkCell — 경로 셀에 Ground 층 개방
            mask[0 * W + 0] = 0;   // MaskedOffPlaceCell — 지면 셀을 닫음

            var doc = ScriptableObject.CreateInstance<MapDocument>();
            doc.SetFrom(
                W, H,
                tiles, new byte[n], new bool[n], new byte[n],
                new[] { new Vector2Int(W - 1, 2) },
                new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) },
                seed: 77, version: 1,
                placeMaskArr: mask);
            return doc;
        }

        [Test]
        public void LivePath_WalkCellMasked_IsPlaceable_PlaceCellMaskedOff_IsNot()
        {
            CallPrivateMethod(_bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(_bridge, "BuildMapForBattle");
            Assert.IsTrue(_bridge.HasGeneratedMap, "masked doc 은 usable — 맵 빌드 성공");

            var gm = GetGeneratedMap(_bridge);
            Assert.AreEqual(77, gm.seed, "풀 문서 경로 사용(폴백 아님)");

            var none = new HashSet<Vector2Int>();
            Assert.AreEqual(PlacementRejectReason.None,
                BattleBridge.SpatialPlacementCheck(gm, none, MaskedWalkCell, PlacementLayer.Ground),
                "Walk 셀 mask=1 → 배치 가능 (B-1)");
            Assert.AreEqual(PlacementRejectReason.NotBuildable,
                BattleBridge.SpatialPlacementCheck(gm, none, MaskedOffPlaceCell, PlacementLayer.Ground),
                "Place 셀 mask=0 → 배치 불가");
            Assert.AreEqual(PlacementRejectReason.None,
                BattleBridge.SpatialPlacementCheck(gm, none, new int2(2, 1), PlacementLayer.Ground),
                "마스크 안 건드린 Place 셀은 그대로 배치 가능");
        }

        [Test]
        public void LivePath_SpawnAndGoalCells_AreClosedForAllLayers()
        {
            // unit 4 리뷰 M-1 — Walk→Path 파생이 스폰·골 칸까지 열어버리는 걸 런타임 불변식이 막는다.
            // 문서/커빙 의미는 그대로고 라이브 맵에만 덮는다.
            CallPrivateMethod(_bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(_bridge, "BuildMapForBattle");
            var gm = GetGeneratedMap(_bridge);
            var none = new HashSet<Vector2Int>();

            for (int i = 0; i < gm.spawns.Length; i++)
                Assert.AreEqual(PlacementRejectReason.NotBuildable,
                    BattleBridge.SpatialPlacementCheck(gm, none, gm.spawns[i], PlacementLayer.All),
                    $"스폰 {gm.spawns[i]} 칸은 어느 층으로도 배치 불가");
            for (int i = 0; i < gm.goals.Length; i++)
                Assert.AreEqual(PlacementRejectReason.NotBuildable,
                    BattleBridge.SpatialPlacementCheck(gm, none, gm.goals[i], PlacementLayer.All),
                    $"골 {gm.goals[i]} 칸은 어느 층으로도 배치 불가");
        }

        [Test]
        public void LivePath_PathLayerOpensRoadCells_ButNotSpawnGoal()
        {
            CallPrivateMethod(_bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(_bridge, "BuildMapForBattle");
            var gm = GetGeneratedMap(_bridge);
            var none = new HashSet<Vector2Int>();

            // (2,2) 는 복도 한가운데 — 스폰(0,2)/(1,2) 도 골(5,2) 도 아니다.
            Assert.AreEqual(PlacementRejectReason.None,
                BattleBridge.SpatialPlacementCheck(gm, none, new int2(2, 2), PlacementLayer.Path),
                "경로 층 유닛은 파생만으로 도로 칸에 선다");
            Assert.AreEqual(PlacementRejectReason.NotBuildable,
                BattleBridge.SpatialPlacementCheck(gm, none, new int2(2, 2), PlacementLayer.Ground),
                "지면 층 유닛은 같은 도로 칸에 못 선다");
        }

        [Test]
        public void LivePath_TilesUnchangedByMask_WalkabilitySourceIntact()
        {
            CallPrivateMethod(_bridge, "EnsureQueriesAndQueues");
            CallPrivateMethod(_bridge, "BuildMapForBattle");

            var gm = GetGeneratedMap(_bridge);
            var docTiles = _doc.Tiles;
            for (int i = 0; i < docTiles.Count; i++)
                Assert.AreEqual(docTiles[i], gm.tiles[i],
                    $"tiles[{i}] — 마스크는 tiles(walkMask 파생원)를 건드리지 않는다 (B-1 통행 불변)");
        }

        // ── helpers (BattleBridgeDraftMapTests 미러) ─────────────────────────────

        private static void AddPoolEntry(MapDocumentPool pool, MapDocument doc)
        {
            var fi = typeof(MapDocumentPool).GetField("entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, "MapDocumentPool.entries field not found");
            var list = (System.Collections.IList)fi.GetValue(pool);
            list.Add(new MapDocumentPool.Entry { document = doc, deck = null });
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
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
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
    }
}
