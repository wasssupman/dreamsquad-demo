using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // waypoint-routing unit 5 — 페인터 Bake 가 타는 경로(WriteToDocument)의 왕복 검증.
    //
    // «페인터로 저작 → 저장 → 재로드 왕복이 저작을 보존»의 데이터 계층이다. 창(UI)은
    // 단위 테스트 불가하므로, 창이 호출하는 유일한 쓰기 경로를 고정한다. 핵심 계약 둘:
    //   비-null = 통째 교체(빈 배열 = 삭제) / null = 기존 경로 보존 —
    //   후자가 깨지면 경로를 모르는 다른 Bake 호출자가 저작을 조용히 지운다.
    public class WaypointPathBakeTests
    {
        private MapDocument _doc;

        [SetUp]
        public void SetUp() => _doc = ScriptableObject.CreateInstance<MapDocument>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_doc);

        private static GeneratedMap MakeMap(int w, int h)
        {
            int n = w * h;
            var map = new GeneratedMap
            {
                tiles = new NativeArray<MapTileType>(n, Allocator.Temp),
                mergeDegree = new NativeArray<byte>(n, Allocator.Temp),
                chokepoint = new NativeArray<byte>(n, Allocator.Temp),
                propLayerId = new NativeArray<byte>(n, Allocator.Temp),
                gridSize = new int2(w, h),
                spawns = new NativeArray<int2>(1, Allocator.Temp),
                goal = new int2(w - 1, 0),
                seed = -1,
                generatorVersion = 0,
            };
            for (int i = 0; i < n; i++) map.tiles[i] = MapTileType.Walk;
            return map;
        }

        [Test]
        public void Write_WithPaths_RoundTripsCellsInOrder()
        {
            var map = MakeMap(6, 4);
            try
            {
                var authored = new[]
                {
                    new WaypointPath(new[] { new Vector2Int(1, 1), new Vector2Int(3, 2), new Vector2Int(5, 3) }),
                    new WaypointPath(new[] { new Vector2Int(0, 3), new Vector2Int(2, 0) }),
                };
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, authored);

                Assert.AreEqual(2, _doc.WaypointPaths.Count);
                Assert.AreEqual(3, _doc.WaypointPaths[0].Cells.Count);
                Assert.AreEqual(new Vector2Int(3, 2), _doc.WaypointPaths[0].Cells[1], "순서 보존");
                Assert.AreEqual(new Vector2Int(2, 0), _doc.WaypointPaths[1].Cells[1]);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Write_WithNull_PreservesExistingPaths()
        {
            var map = MakeMap(6, 4);
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null,
                    new[] { new WaypointPath(new[] { new Vector2Int(2, 2) }) });
                // 경로를 모르는 호출자의 재-Bake (타일만 다시 씀)
                MapDocumentBuilder.WriteToDocument(_doc, in map);

                Assert.AreEqual(1, _doc.WaypointPaths.Count, "null = 보존 — 조용한 삭제 금지");
                Assert.AreEqual(new Vector2Int(2, 2), _doc.WaypointPaths[0].Cells[0]);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Write_WithEmptyArray_ClearsPaths()
        {
            var map = MakeMap(6, 4);
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null,
                    new[] { new WaypointPath(new[] { new Vector2Int(2, 2) }) });
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, new WaypointPath[0]);

                Assert.AreEqual(0, _doc.WaypointPaths.Count, "빈 배열 = 명시적 삭제");
            }
            finally { map.Dispose(); }
        }
    }
}
