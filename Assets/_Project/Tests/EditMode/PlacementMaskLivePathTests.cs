using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // placement-mask unit 3 → map-diorama-stage unit 2 재작성 — 라이브 경로
    // (mapPool → BuildMapForBattle, 스테이지 스캔)에서 B-1/마스크 계약 검증:
    //   ① 열린 셀 = 기본 Ground|Path|Air 개방(계약 3) — Ground 유닛도 Path 유닛도 선다
    //   ② PlacementBlockZone = 옛 마스크 브러시의 후계 — 차감 셀은 전 층 배치 불가,
    //      단 tiles(walkMask 파생원)는 불변 = 통행 불변 (critic C-2 전선 가드의 승계)
    //   ③ 스폰·골 칸은 런타임 불변식(CloseCellLayers)이 전 층 폐쇄 (unit 4 리뷰 M-1 승계)
    // 픽스처는 BattleBridgeDraftMapTests 의 스테이지 템플릿 헬퍼를 공유한다.
    public class PlacementMaskLivePathTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private AttackDeck _deck;
        private MapStage _stageTemplate;
        private MapStagePool _pool;

        private static readonly int2 OpenCell = new int2(2, 2);
        private static readonly int2 BlockedCell = new int2(3, 3);      // PropFootprint (픽스처 내장)
        private static readonly Vector2Int ZoneCell = new Vector2Int(5, 1); // PlacementBlockZone

        [SetUp]
        public void SetUp()
        {
            _world = new World("PlacementMaskLivePathTests");
            _deck = ScriptableObject.CreateInstance<AttackDeck>();

            _stageTemplate = BattleBridgeDraftMapTests.BuildUsableStage("MapStage_MaskFixture");
            BattleBridgeDraftMapTests.AddMarker<PlacementBlockZone>(
                _stageTemplate.gameObject, ZoneCell, z => z.size = Vector2Int.one);

            _pool = ScriptableObject.CreateInstance<MapStagePool>();
            BattleBridgeDraftMapTests.AddPoolEntry(_pool, _stageTemplate, deck: null);

            _go = new GameObject("BattleBridge_MaskTest");
            _bridge = _go.AddComponent<BattleBridge>();
            BattleBridgeDraftMapTests.SetField(_bridge, "deck", _deck);
            BattleBridgeDraftMapTests.SetField(_bridge, "mapPool", _pool);
            BattleBridgeDraftMapTests.SetField(_bridge, "_world", _world);
            BattleBridgeDraftMapTests.SetField(_bridge, "_em", _world.EntityManager);

            BattleBridgeDraftMapTests.CallPrivateMethod(_bridge, "EnsureQueriesAndQueues");
            BattleBridgeDraftMapTests.CallPrivateMethod(_bridge, "BuildMapForBattle");
            Assert.IsTrue(_bridge.HasGeneratedMap, "픽스처 스테이지는 usable — 맵 빌드 성공");
        }

        [TearDown]
        public void TearDown()
        {
            if (_bridge != null)
                BattleBridgeDraftMapTests.CallPrivateMethod(_bridge, "TeardownGeneratedMap");
            if (_go != null) Object.DestroyImmediate(_go);
            if (_deck != null) Object.DestroyImmediate(_deck);
            if (_stageTemplate != null) Object.DestroyImmediate(_stageTemplate.gameObject);
            if (_pool != null) Object.DestroyImmediate(_pool);
            _world?.Dispose();
        }

        [Test]
        public void LivePath_OpenCell_OpensGroundAndPath_BlockedCell_ClosesAll()
        {
            var gm = BattleBridgeDraftMapTests.GetGeneratedMap(_bridge);
            var none = new HashSet<Vector2Int>();

            Assert.AreEqual(PlacementRejectReason.None,
                BattleBridge.SpatialPlacementCheck(gm, none, OpenCell, PlacementLayer.Ground),
                "열린 마당 셀 — Ground 유닛 배치 가능 (계약 3 기본 개방)");
            Assert.AreEqual(PlacementRejectReason.None,
                BattleBridge.SpatialPlacementCheck(gm, none, OpenCell, PlacementLayer.Path),
                "열린 마당 셀 — Path 유닛(가디언, D7 결정 (a))도 배치 가능");
            Assert.AreEqual(PlacementRejectReason.NotBuildable,
                BattleBridge.SpatialPlacementCheck(gm, none, BlockedCell, PlacementLayer.Ground),
                "차단 프랍 셀 — 배치 불가");
        }

        [Test]
        public void LivePath_BlockZone_ClosesPlacement_KeepsTraversal()
        {
            var gm = BattleBridgeDraftMapTests.GetGeneratedMap(_bridge);
            var none = new HashSet<Vector2Int>();
            var zone = new int2(ZoneCell.x, ZoneCell.y);

            Assert.AreEqual(PlacementRejectReason.NotBuildable,
                BattleBridge.SpatialPlacementCheck(gm, none, zone, PlacementLayer.All),
                "BlockZone 셀 — 전 층 배치 불가 (옛 마스크 브러시/전선 가드의 후계, critic C-2)");
            Assert.AreEqual(MapTileType.Walk, gm.TileAt(zone),
                "BlockZone 은 배치만 닫는다 — tiles(walkMask 파생원)는 Walk 그대로 = 통행 불변");
        }

        [Test]
        public void LivePath_SpawnAndGoalCells_AreClosedForAllLayers()
        {
            var gm = BattleBridgeDraftMapTests.GetGeneratedMap(_bridge);
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
        public void LivePath_TilesSynthesis_OpenWalk_BlockedDeco()
        {
            var gm = BattleBridgeDraftMapTests.GetGeneratedMap(_bridge);
            Assert.AreEqual(MapTileType.Walk, gm.TileAt(OpenCell), "열린 셀 = Walk (계약 2 합성)");
            Assert.AreEqual(MapTileType.Deco, gm.TileAt(BlockedCell), "차단 셀 = Deco (계약 2 합성)");
        }
    }
}
