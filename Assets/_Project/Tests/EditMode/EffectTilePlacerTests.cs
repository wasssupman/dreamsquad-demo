using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // effect-tiles unit 0 — SelectCells 결정론/필터/상한 회귀.
    public class EffectTilePlacerTests
    {
        [Test]
        public void SelectCells_IsDeterministicForSameSeed()
        {
            var a = CreateMap(new int2(6, 5), MapTileType.Place);
            var b = CreateMap(new int2(6, 5), MapTileType.Place);
            try
            {
                var cellsA = EffectTilePlacer.SelectCells(a, 1234, 4);
                var cellsB = EffectTilePlacer.SelectCells(b, 1234, 4);

                Assert.AreEqual(cellsA.Count, cellsB.Count);
                for (int i = 0; i < cellsA.Count; i++)
                    Assert.AreEqual(cellsA[i], cellsB[i]);
            }
            finally { a.Dispose(); b.Dispose(); }
        }

        [Test]
        public void SelectCells_DiffersForDifferentSeed()
        {
            var map = CreateMap(new int2(8, 8), MapTileType.Place);
            try
            {
                var cellsA = EffectTilePlacer.SelectCells(map, 1, 5);
                var cellsB = EffectTilePlacer.SelectCells(map, 2, 5);

                Assert.AreEqual(cellsA.Count, cellsB.Count);
                bool anyDifferent = false;
                for (int i = 0; i < cellsA.Count; i++)
                    if (!cellsA[i].Equals(cellsB[i])) { anyDifferent = true; break; }
                Assert.IsTrue(anyDifferent, "다른 seed 는 (64셀 중 5개 선정에서) 다른 결과를 내야 한다.");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SelectCells_ReturnsOnlyPlaceCells()
        {
            var map = CreateMap(new int2(5, 4), MapTileType.Deco);
            try
            {
                // Place 셀 3개만 지정 — 그 외(Deco/Walk)는 선정 불가.
                map.tiles[map.CellIndex(new int2(1, 1))] = MapTileType.Place;
                map.tiles[map.CellIndex(new int2(3, 2))] = MapTileType.Place;
                map.tiles[map.CellIndex(new int2(0, 3))] = MapTileType.Place;
                map.tiles[map.CellIndex(new int2(2, 0))] = MapTileType.Walk;

                var cells = EffectTilePlacer.SelectCells(map, 777, 10);

                Assert.AreEqual(3, cells.Count, "count 가 가용 Place 셀 수를 넘으면 전부 선정.");
                foreach (var cell in cells)
                    Assert.AreEqual(MapTileType.Place, map.TileAt(cell));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SelectCells_RespectsCountAndHasNoDuplicates()
        {
            var map = CreateMap(new int2(7, 7), MapTileType.Place);
            try
            {
                var cells = EffectTilePlacer.SelectCells(map, 42, 6);

                Assert.AreEqual(6, cells.Count);
                var seen = new HashSet<int2>(cells);
                Assert.AreEqual(cells.Count, seen.Count, "중복 셀 금지.");

                Assert.AreEqual(0, EffectTilePlacer.SelectCells(map, 42, 0).Count, "count<=0 은 빈 결과.");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SelectCells_MaskWinsOverTileType()
        {
            // placement-mask unit 1 — 마스크 생성 시 배치 정본은 마스크: Walk 셀 mask=1 선정 가능,
            // Place 셀 mask=0 선정 불가.
            var map = CreateMap(new int2(4, 4), MapTileType.Place);
            try
            {
                var mask = new NativeArray<byte>(16, Allocator.Temp);
                mask[map.CellIndex(new int2(1, 1))] = 1;
                mask[map.CellIndex(new int2(2, 2))] = 1;
                map.tiles[map.CellIndex(new int2(1, 1))] = MapTileType.Walk;   // Walk 인데 mask=1
                map.placeMask = mask;

                var cells = EffectTilePlacer.SelectCells(map, 99, 10);

                Assert.AreEqual(2, cells.Count, "mask=1 인 2셀만 선정");
                CollectionAssert.Contains(cells, new int2(1, 1), "Walk 셀도 mask=1 이면 선정");
                CollectionAssert.Contains(cells, new int2(2, 2));
            }
            finally { map.Dispose(); }
        }

        private static GeneratedMap CreateMap(int2 gridSize, MapTileType fill)
        {
            int count = gridSize.x * gridSize.y;
            var tiles = new NativeArray<MapTileType>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
                tiles[i] = fill;

            return new GeneratedMap
            {
                tiles = tiles,
                gridSize = gridSize,
                spawns = new NativeArray<int2>(1, Allocator.Temp),
                goal = new int2(gridSize.x - 1, gridSize.y - 1),
                seed = 1,
                generatorVersion = 1,
            };
        }
    }
}
