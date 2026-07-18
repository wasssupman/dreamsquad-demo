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
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(0, 0)));
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
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(1, 1)));
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
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(3, 0)));
                Assert.AreEqual(PlacementRejectReason.OutOfBounds,
                    BattleBridge.SpatialPlacementCheck(map, new HashSet<Vector2Int>(), new int2(-1, 0)));
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
                    BattleBridge.SpatialPlacementCheck(map, occupied, new int2(0, 0)));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void UncreatedMap_ReturnsMissingMap()
        {
            Assert.AreEqual(PlacementRejectReason.MissingMap,
                BattleBridge.SpatialPlacementCheck(default, new HashSet<Vector2Int>(), new int2(0, 0)));
        }
    }
}
