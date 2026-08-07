using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class GeneratedMapTests
    {
        [Test]
        public void CellIndex_UsesRowMajorLayout()
        {
            var map = new GeneratedMap { gridSize = new int2(20, 10) };

            Assert.AreEqual(0, map.CellIndex(new int2(0, 0)));
            Assert.AreEqual(19, map.CellIndex(new int2(19, 0)));
            Assert.AreEqual(20, map.CellIndex(new int2(0, 1)));
            Assert.AreEqual(199, map.CellIndex(new int2(19, 9)));
        }

        [Test]
        public void Dispose_DisposesOwnedNativeArrays()
        {
            var map = new GeneratedMap
            {
                tiles = new NativeArray<MapTileType>(4, Allocator.Persistent),
                spawns = new NativeArray<int2>(1, Allocator.Persistent),
                placeMask = new NativeArray<byte>(4, Allocator.Persistent),
                gridSize = new int2(2, 2),
            };

            map.Dispose();

            Assert.IsFalse(map.tiles.IsCreated);
            Assert.IsFalse(map.spawns.IsCreated);
            Assert.IsFalse(map.placeMask.IsCreated);
        }

        // ── placement-mask unit 0 — PlaceableAt 폴백 사다리 ──────────────────────

        [Test]
        public void PlaceableAt_MaskNotCreated_FallsBackToPlaceTile()
        {
            // 직접 구성 픽스처(마스크 미생성) 보호 — tiles==Place 파생 폴백.
            var tiles = new NativeArray<MapTileType>(4, Allocator.Persistent);
            tiles[0] = MapTileType.Place;
            tiles[1] = MapTileType.Walk;
            var map = new GeneratedMap { tiles = tiles, gridSize = new int2(2, 2) };
            try
            {
                Assert.IsTrue(map.PlaceableAt(new int2(0, 0)), "Place 타일 → 배치 가능");
                Assert.IsFalse(map.PlaceableAt(new int2(1, 0)), "Walk 타일 → 배치 불가");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void PlaceableAt_MaskCreated_MaskWinsOverTiles()
        {
            // 마스크 생성 시 타일 종류 무시 — Walk 셀 mask=1 배치 가능, Place 셀 mask=0 배치 불가.
            var tiles = new NativeArray<MapTileType>(4, Allocator.Persistent);
            tiles[0] = MapTileType.Place;
            tiles[1] = MapTileType.Walk;
            var mask = new NativeArray<byte>(4, Allocator.Persistent);
            mask[0] = 0;
            mask[1] = 1;
            var map = new GeneratedMap { tiles = tiles, placeMask = mask, gridSize = new int2(2, 2) };
            try
            {
                Assert.IsFalse(map.PlaceableAt(new int2(0, 0)), "Place 타일이어도 mask=0 → 불가");
                Assert.IsTrue(map.PlaceableAt(new int2(1, 0)), "Walk 타일이어도 mask=1 → 가능");
            }
            finally { map.Dispose(); }
        }
    }
}
