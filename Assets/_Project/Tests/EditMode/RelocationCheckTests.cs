using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // defender-relocation unit 0 — 재배치 순수 판정(RelocationCheck) 회귀 방지.
    // 소스 부재/진행중/동일셀 + to 공간 판정(SpatialPlacementCheck 위임) 케이스.
    public class RelocationCheckTests
    {
        // 3x3: 전부 Place, x=1 세로열이 Walk 경로 (SpatialPlacementCheckTests 와 동일 지형).
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

        static HashSet<Vector2Int> OccupiedAt(params Vector2Int[] cells) => new HashSet<Vector2Int>(cells);

        [Test]
        public void ValidMove_ReturnsNone()
        {
            var map = MakeMap();
            try
            {
                var occupied = OccupiedAt(new Vector2Int(0, 0)); // from 은 점유 집합에 남아 있는 상태
                Assert.AreEqual(PlacementRejectReason.None,
                    BattleBridge.RelocationCheck(map, occupied, new int2(0, 0), new int2(2, 2),
                        fromHasDefender: true, fromBusy: false, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void EmptySource_ReturnsNoDefenderAtSource()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.NoDefenderAtSource,
                    BattleBridge.RelocationCheck(map, OccupiedAt(), new int2(0, 0), new int2(2, 2),
                        fromHasDefender: false, fromBusy: false, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void BusySource_ReturnsSourceBusy()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.SourceBusy,
                    BattleBridge.RelocationCheck(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(2, 2),
                        fromHasDefender: true, fromBusy: true, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        // unit 9 — 같은 칸은 **제자리 재정비 확정**이다(예전엔 SameCell 거부였다).
        // 두 가지를 한 번에 지킨다: ⑴ 통과한다 ⑵ Occupied 로 오판하지 않는다.
        // ⑵ 가 중요한 이유 — from 은 아직 점유 집합에 남아 있어서, from==to 검사가
        // SpatialPlacementCheck 뒤로 밀리면 자기 자리가 "이미 누가 있다" 로 튄다.
        [Test]
        public void SameCell_IsRefit_NotOccupied()
        {
            var map = MakeMap();
            try
            {
                var reason = BattleBridge.RelocationCheck(map, OccupiedAt(new Vector2Int(0, 0)),
                    new int2(0, 0), new int2(0, 0),
                    fromHasDefender: true, fromBusy: false, layers: PlacementLayer.Ground);
                Assert.AreNotEqual(PlacementRejectReason.Occupied, reason,
                    "자기 점유를 목적지 점유로 오판하면 안 된다 (from==to 검사가 공간 판정보다 앞)");
                Assert.AreEqual(PlacementRejectReason.None, reason, "같은 칸 = 제자리 재정비 확정");
            }
            finally { map.Dispose(); }
        }

        // 소스 사유는 제자리에서도 이긴다 — 재정비라고 죽은 칸/진행중 유닛을 통과시키지 않는다.
        [Test]
        public void SameCell_StillRejects_WhenSourceInvalid()
        {
            var map = MakeMap();
            try
            {
                var occ = OccupiedAt(new Vector2Int(0, 0));
                Assert.AreEqual(PlacementRejectReason.NoDefenderAtSource,
                    BattleBridge.RelocationCheck(map, occ, new int2(0, 0), new int2(0, 0),
                        fromHasDefender: false, fromBusy: false, layers: PlacementLayer.Ground));
                Assert.AreEqual(PlacementRejectReason.SourceBusy,
                    BattleBridge.RelocationCheck(map, occ, new int2(0, 0), new int2(0, 0),
                        fromHasDefender: true, fromBusy: true, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void OccupiedTarget_ReturnsOccupied()
        {
            var map = MakeMap();
            try
            {
                var occupied = OccupiedAt(new Vector2Int(0, 0), new Vector2Int(2, 2));
                Assert.AreEqual(PlacementRejectReason.Occupied,
                    BattleBridge.RelocationCheck(map, occupied, new int2(0, 0), new int2(2, 2),
                        fromHasDefender: true, fromBusy: false, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void WalkTarget_ReturnsNotBuildable()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.NotBuildable,
                    BattleBridge.RelocationCheck(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(1, 1),
                        fromHasDefender: true, fromBusy: false, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void OutOfBoundsTarget_ReturnsOutOfBounds()
        {
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.OutOfBounds,
                    BattleBridge.RelocationCheck(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(3, 0),
                        fromHasDefender: true, fromBusy: false, layers: PlacementLayer.Ground));
            }
            finally { map.Dispose(); }
        }
    }
}
