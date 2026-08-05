using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Sim.Match;

// battle-sim-extraction unit 15-C - 판정 본체가 `Sim/Match/MatchPlacementRules.Relocation` 으로
// 이사했으므로 sim 규칙을 직접 부른다. `BattleBridge.RelocationCheck` 는 프로덕션 호출처를 위한
// 포워더로만 남는다 - 테스트가 그 이름을 계속 쓰면 unit 17 asmdef 분리 후에도 테스트
// 어셈블리가 Bridge 를 참조하게 된다(SpatialPlacementCheckTests 와 같은 처분).
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
                    MatchPlacementRules.Relocation(map, occupied, new int2(0, 0), new int2(2, 2),
                        fromHasDefender: true, fromBusy: false));
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
                    MatchPlacementRules.Relocation(map, OccupiedAt(), new int2(0, 0), new int2(2, 2),
                        fromHasDefender: false, fromBusy: false));
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
                    MatchPlacementRules.Relocation(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(2, 2),
                        fromHasDefender: true, fromBusy: true));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SameCell_ReturnsSameCell_NotOccupied()
        {
            // from 은 점유 집합에 있다 — from == to 검사가 선행돼야 Occupied 로 오판하지 않는다.
            var map = MakeMap();
            try
            {
                Assert.AreEqual(PlacementRejectReason.SameCell,
                    MatchPlacementRules.Relocation(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(0, 0),
                        fromHasDefender: true, fromBusy: false));
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
                    MatchPlacementRules.Relocation(map, occupied, new int2(0, 0), new int2(2, 2),
                        fromHasDefender: true, fromBusy: false));
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
                    MatchPlacementRules.Relocation(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(1, 1),
                        fromHasDefender: true, fromBusy: false));
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
                    MatchPlacementRules.Relocation(map, OccupiedAt(new Vector2Int(0, 0)), new int2(0, 0), new int2(3, 0),
                        fromHasDefender: true, fromBusy: false));
            }
            finally { map.Dispose(); }
        }
    }
}
