using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // placement-eligible-tile-highlight unit 2 — 공간 배치 술어(판정 CanPlaceDefenderAt 과 하이라이트
    // 수집이 공유하는 SpatialPlacementCheck) 회귀 방지. bounds/비-Place/점유/빈-Place/미생성 케이스.
    public class SpatialPlacementCheckTests
    {
        // 3x3: 전부 Place, x=1 세로열이 Walk 경로.
        static GeneratedMap MakeMap()
        {
            var tiles = new NativeArray<MapTileType>(9, Allocator.Persistent);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
            tiles[0 * 3 + 1] = MapTileType.Walk; // (1,0)
            tiles[1 * 3 + 1] = MapTileType.Walk; // (1,1)
            tiles[2 * 3 + 1] = MapTileType.Walk; // (1,2)
            spawns[0] = new int2(1, 0);
            return new GeneratedMap { tiles = tiles, spawns = spawns, gridSize = new int2(3, 3), goal = new int2(1, 2) };
        }

        [Test]
        public void EmptyPlaceCell_ReturnsNone()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(0, 0), PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void WalkCell_ReturnsNotBuildable()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.NotBuildable,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(1, 1), PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void OutOfBounds_ReturnsOutOfBounds()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.OutOfBounds,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(3, 0), PlacementLayer.Ground));
                Assert.AreEqual(PlacementRejectReason.OutOfBounds,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(-1, 0), PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void OccupiedPlaceCell_ReturnsOccupied()
        {
            var map = MakeMap();
            try
            {
                var occupied = new HashSet<Vector2Int> { new Vector2Int(0, 0) };
                Assert.AreEqual(PlacementRejectReason.Occupied,
                    BattleBridge.SpatialPlacementCheck(map, occupied, new int2(0, 0), PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void UncreatedMap_ReturnsMissingMap()
        {
            Assert.AreEqual(PlacementRejectReason.MissingMap,
                BattleBridge.SpatialPlacementCheck(default, new HashSet<Vector2Int>(), new int2(0, 0), PlacementLayer.Ground));
        }

        // ── placement-mask unit 1 — 마스크가 배치 가능성의 정본 ─────────────────

        // MakeMap 과 동일 3x3 + placeMask: Walk 셀 (1,1) 허용, Place 셀 (0,0) 금지.
        static GeneratedMap MakeMaskedMap()
        {
            var map = MakeMap();
            var mask = new NativeArray<byte>(9, Allocator.Persistent);
            for (int i = 0; i < 9; i++)
                mask[i] = (byte)(map.tiles[i] == MapTileType.Place ? 1 : 0);
            mask[1 * 3 + 1] = 1;   // Walk 셀 (1,1) 배치 허용 — B-1.
            mask[0 * 3 + 0] = 0;   // Place 셀 (0,0) 배치 금지.
            map.placeMask = mask;
            return map;
        }

        [Test]
        public void WalkCellWithMask_ReturnsNone()
        {
            var map = MakeMaskedMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(1, 1), PlacementLayer.Ground),
                    "Walk 셀이어도 mask=1 이면 배치 가능 (B-1)");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void PlaceCellMaskedOff_ReturnsNotBuildable()
        {
            var map = MakeMaskedMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.NotBuildable,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(0, 0), PlacementLayer.Ground),
                    "Place 셀이어도 mask=0 이면 배치 불가");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void MaskedWalkCell_Occupied_ReturnsOccupied()
        {
            var map = MakeMaskedMap();
            try
            {
                var occupied = new HashSet<Vector2Int> { new Vector2Int(1, 1) };
                Assert.AreEqual(PlacementRejectReason.Occupied,
                    BattleBridge.SpatialPlacementCheck(map, occupied, new int2(1, 1), PlacementLayer.Ground),
                    "마스크 셀도 점유 판정은 동일");
            }
            finally { map.Dispose(); }
        }
    }
}
