using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // placement-mask unit 4 — 배치 층 비트필드. 판정은 (셀 층 & 유닛 층) != 0 하나이고
    // 코드는 유닛 클래스(role)를 보지 않는다 — 이 테스트도 비트로만 말한다.
    public class PlacementLayerTests
    {
        [Test]
        public void Derive_MapsTileTypesToLayers()
        {
            Assert.AreEqual((byte)PlacementLayer.Ground, PlacementLayers.Derive(MapTileType.Place));
            Assert.AreEqual((byte)PlacementLayer.Path, PlacementLayers.Derive(MapTileType.Walk));
            Assert.AreEqual(0, PlacementLayers.Derive(MapTileType.Deco), "장식은 어떤 층도 열지 않는다");
            Assert.AreEqual(0, PlacementLayers.Derive(MapTileType.Env));
        }

        [Test]
        public void Sanitize_DropsUndefinedBits_KeepsDefined()
        {
            Assert.AreEqual((byte)PlacementLayer.Ground, PlacementLayers.Sanitize(0x81), "0x80 미정의 비트 제거");
            Assert.AreEqual(PlacementLayers.CellBits, PlacementLayers.Sanitize(0xFF), "All(0xFF) 은 셀에선 정의된 층으로 접힘");
            Assert.AreEqual(0, PlacementLayers.Sanitize(0x80));
        }

        // 2x1 맵: (0,0)=Ground 층만, (1,0)=Path 층만.
        static GeneratedMap MakeLayeredMap()
        {
            var tiles = new NativeArray<MapTileType>(2, Allocator.Persistent);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            var mask = new NativeArray<byte>(2, Allocator.Persistent);
            tiles[0] = MapTileType.Place;
            tiles[1] = MapTileType.Walk;
            mask[0] = (byte)PlacementLayer.Ground;
            mask[1] = (byte)PlacementLayer.Path;
            return new GeneratedMap
            {
                tiles = tiles, spawns = spawns, placeMask = mask,
                gridSize = new int2(2, 1),
            };
        }

        [TestCase(0, (byte)PlacementLayer.Ground, true, TestName = "Ground유닛_지면셀_허용")]
        [TestCase(1, (byte)PlacementLayer.Ground, false, TestName = "Ground유닛_경로셀_거부")]
        [TestCase(0, (byte)PlacementLayer.Path, false, TestName = "Path유닛_지면셀_거부")]
        [TestCase(1, (byte)PlacementLayer.Path, true, TestName = "Path유닛_경로셀_허용")]
        [TestCase(0, (byte)PlacementLayer.All, true, TestName = "All유닛_지면셀_허용")]
        [TestCase(1, (byte)PlacementLayer.All, true, TestName = "All유닛_경로셀_허용")]
        public void Intersection_DecidesPlacement(int x, byte unitLayers, bool expected)
        {
            var map = MakeLayeredMap();
            try
            {
                var reason = BattleBridge.SpatialPlacementCheck(
                    map, new HashSet<Vector2Int>(), new int2(x, 0), (PlacementLayer)unitLayers);
                Assert.AreEqual(expected ? PlacementRejectReason.None : PlacementRejectReason.NotBuildable, reason);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void BothLayersOpen_AcceptsEitherUnit()
        {
            var map = MakeLayeredMap();
            try
            {
                map.placeMask[0] = (byte)(PlacementLayer.Ground | PlacementLayer.Path);
                var none = new HashSet<Vector2Int>();
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.SpatialPlacementCheck(map, none, new int2(0, 0), PlacementLayer.Ground));
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.SpatialPlacementCheck(map, none, new int2(0, 0), PlacementLayer.Path));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void UnitWithNoneLayers_FallsBackToGround()
        {
            // 기존 asset 은 신규 필드를 None(0) 으로 역직렬화한다 — 미지정 = Ground 폴백이라
            // SO 를 안 건드리면 units 0~3 동작 그대로여야 한다.
            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            try
            {
                unit.placementLayers = PlacementLayer.None;
                Assert.AreEqual(PlacementLayer.Ground, unit.EffectivePlacementLayers);

                unit.placementLayers = PlacementLayer.Path;
                Assert.AreEqual(PlacementLayer.Path, unit.EffectivePlacementLayers, "지정했으면 그대로");
            }
            finally { ScriptableObject.DestroyImmediate(unit); }
        }

        [Test]
        public void MaskAbsent_DerivedLayersStillSeparateUnits()
        {
            // 마스크 미저작 맵(라이브 6종)에서도 층은 타일 종류에서 파생된다.
            var tiles = new NativeArray<MapTileType>(2, Allocator.Persistent);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            tiles[0] = MapTileType.Place;
            tiles[1] = MapTileType.Walk;
            var map = new GeneratedMap { tiles = tiles, spawns = spawns, gridSize = new int2(2, 1) };
            try
            {
                var none = new HashSet<Vector2Int>();
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.SpatialPlacementCheck(map, none, new int2(0, 0), PlacementLayer.Ground));
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.SpatialPlacementCheck(map, none, new int2(1, 0), PlacementLayer.Path),
                    "마스크가 없어도 Walk 셀은 Path 층으로 파생된다");
                Assert.AreEqual(PlacementRejectReason.NotBuildable,
                    BattleBridge.SpatialPlacementCheck(map, none, new int2(1, 0), PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }
    }
}
