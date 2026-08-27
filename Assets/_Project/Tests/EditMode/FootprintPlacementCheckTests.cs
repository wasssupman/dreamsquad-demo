using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // defender-footprint unit 1 — footprint 공간 판정(SpatialFootprintCheck)과 재배치 자기 겹침
    // (RelocationFootprintCheck) 회귀 방지. 셀 규칙은 SpatialPlacementCheck 재사용이 계약이라
    // 여기서는 footprint 고유 축(다중 셀·per-cell 사유·종합 우선순위·자기 제외)만 겨눈다.
    public class FootprintPlacementCheckTests
    {
        // 5x4: 전부 Place, x=2 세로열이 Walk 경로.
        static GeneratedMap MakeMap()
        {
            var tiles = new NativeArray<MapTileType>(20, Allocator.Persistent);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
            for (int y = 0; y < 4; y++) tiles[y * 5 + 2] = MapTileType.Walk; // (2,*)
            spawns[0] = new int2(2, 0);
            return new GeneratedMap { tiles = tiles, spawns = spawns, gridSize = new int2(5, 4), goal = new int2(2, 3) };
        }

        [Test]
        public void OneByOne_MatchesSingleCellCheck()
        {
            var map = MakeMap();
            try
            {
                var occupied = new HashSet<Vector2Int> { new(3, 1) };
                for (int y = -1; y <= 4; y++)
                {
                    for (int x = -1; x <= 5; x++)
                    {
                        Assert.AreEqual(
                            BattleBridge.SpatialPlacementCheck(map, occupied, new int2(x, y), PlacementLayer.Ground),
                            BattleBridge.SpatialFootprintCheck(map, occupied, new Vector2Int(x, y), Vector2Int.one, PlacementLayer.Ground),
                            $"1×1 동치 ({x},{y})");
                    }
                }
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void MultiCell_AllValid_ReturnsNone_PerCellAllNone()
        {
            var map = MakeMap();
            try
            {
                var perCell = new List<FootprintCellReason>();
                var result = BattleBridge.SpatialFootprintCheck(
                    map, new HashSet<Vector2Int>(), new Vector2Int(0, 0), new Vector2Int(2, 3),
                    PlacementLayer.Ground, perCell);
                Assert.AreEqual(PlacementRejectReason.None, result);
                Assert.AreEqual(6, perCell.Count);
                foreach (var c in perCell) Assert.AreEqual(PlacementRejectReason.None, c.reason);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void WalkColumnInside_ReturnsNotBuildable_OnlyThoseCellsMarked()
        {
            var map = MakeMap();
            try
            {
                var perCell = new List<FootprintCellReason>();
                // (1,0)~(3,1): x=2 열 두 칸만 Walk.
                var result = BattleBridge.SpatialFootprintCheck(
                    map, new HashSet<Vector2Int>(), new Vector2Int(1, 0), new Vector2Int(3, 2),
                    PlacementLayer.Ground, perCell);
                Assert.AreEqual(PlacementRejectReason.NotBuildable, result);
                int bad = 0;
                foreach (var c in perCell)
                {
                    if (c.cell.x == 2)
                    {
                        Assert.AreEqual(PlacementRejectReason.NotBuildable, c.reason, $"{c.cell}");
                        bad++;
                    }
                    else Assert.AreEqual(PlacementRejectReason.None, c.reason, $"{c.cell}");
                }
                Assert.AreEqual(2, bad);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void PartiallyOutOfBounds_ReturnsOutOfBounds()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.OutOfBounds,
                    BattleBridge.SpatialFootprintCheck(
                        map, new HashSet<Vector2Int>(), new Vector2Int(4, 3), new Vector2Int(2, 2),
                        PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void OverallReason_OccupiedWinsOverNotBuildableAndOutOfBounds()
        {
            var map = MakeMap();
            try
            {
                // (1,3)~(3,4): Walk 열(NotBuildable) + y=4 행(OutOfBounds) + (3,3) 점유가 섞인다.
                var occupied = new HashSet<Vector2Int> { new(3, 3) };
                Assert.AreEqual(PlacementRejectReason.Occupied,
                    BattleBridge.SpatialFootprintCheck(
                        map, occupied, new Vector2Int(1, 3), new Vector2Int(3, 2),
                        PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Relocation_SelfOverlap_IsAllowed()
        {
            var map = MakeMap();
            try
            {
                // 2×2 가 (3,0) 앵커에 서 있고((3,0)~(4,1) 점유) 한 칸 아래 (3,1) 로 이동 —
                // (3,1)·(4,1) 은 자기 점유라 Occupied 로 치지 않아야 한다. x=2 열을 피해 우측 블록 사용.
                var occupied = new HashSet<Vector2Int> { new(3, 0), new(4, 0), new(3, 1), new(4, 1) };
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.RelocationFootprintCheck(
                        map, occupied, new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(2, 2),
                        fromHasDefender: true, fromBusy: false, PlacementLayer.Ground));
                // 남의 점유는 여전히 막는다: (3,2) 에 타 유닛.
                occupied.Add(new Vector2Int(3, 2));
                Assert.AreEqual(PlacementRejectReason.Occupied,
                    BattleBridge.RelocationFootprintCheck(
                        map, occupied, new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(2, 2),
                        fromHasDefender: true, fromBusy: false, PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Relocation_SameAnchor_IsInPlaceRefit()
        {
            var map = MakeMap();
            try
            {
                var occupied = new HashSet<Vector2Int> { new(3, 0), new(4, 0), new(3, 1), new(4, 1) };
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.RelocationFootprintCheck(
                        map, occupied, new Vector2Int(3, 0), new Vector2Int(3, 0), new Vector2Int(2, 2),
                        fromHasDefender: true, fromBusy: false, PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Relocation_SourceGates_KeepPriority()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.NoDefenderAtSource,
                    BattleBridge.RelocationFootprintCheck(
                        map, new HashSet<Vector2Int>(), new Vector2Int(0, 0), new Vector2Int(3, 0), Vector2Int.one,
                        fromHasDefender: false, fromBusy: false, PlacementLayer.Ground));
                Assert.AreEqual(PlacementRejectReason.SourceBusy,
                    BattleBridge.RelocationFootprintCheck(
                        map, new HashSet<Vector2Int>(), new Vector2Int(0, 0), new Vector2Int(3, 0), Vector2Int.one,
                        fromHasDefender: true, fromBusy: true, PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }
    }
}
