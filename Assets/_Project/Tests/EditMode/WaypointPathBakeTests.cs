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
                // mergeDegree·chokepoint·propLayerId 는 은퇴했다 — 소비자가 없어
                // map-view-deadcode-removal unit 2 가 GeneratedMap 에서 제거했다.
                gridSize = new int2(w, h),
                spawns = new NativeArray<int2>(1, Allocator.Temp),
                goal = new int2(w - 1, 0),
                seed = -1,
                generatorVersion = 0,
            };
            for (int i = 0; i < n; i++) map.tiles[i] = MapTileType.Walk;
            return map;
        }

        // waypoint-routing unit 8 — spawnRoutes 정규화 테스트는 스폰 개수를 바꿔가며 확인해야
        // 해서 레인 수를 고르는 오버로드가 필요하다. spawns 좌표 자체는 이 테스트들의 관심사가
        // 아니라 (0,0) 그대로 둔다.
        private static GeneratedMap MakeMap(int w, int h, int spawnCount)
        {
            var map = MakeMap(w, h);
            map.spawns.Dispose();
            map.spawns = new NativeArray<int2>(spawnCount, Allocator.Temp);
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

        // waypoint-routing unit 8 — spawnRoutes 저작 왕복. waypointPaths 와 같은 규약
        // (null = 보존, 빈 배열 = 삭제) 이 spawnRoutes 에도 그대로 적용되는지 확인한다.

        [Test]
        public void Write_WithSpawnRoutes_RoundTrips()
        {
            var map = MakeMap(6, 4, 2);
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new[] { 1, -1 });

                Assert.AreEqual(2, _doc.SpawnRoutes.Count);
                Assert.AreEqual(1, _doc.SpawnRoutes[0]);
                Assert.AreEqual(-1, _doc.SpawnRoutes[1]);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Write_WithNull_PreservesExistingSpawnRoutes()
        {
            var map = MakeMap(6, 4, 2);
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new[] { 0, 1 });
                // spawnRoutes 를 모르는 호출자의 재-Bake (타일만 다시 씀)
                MapDocumentBuilder.WriteToDocument(_doc, in map);

                Assert.AreEqual(2, _doc.SpawnRoutes.Count, "null = 보존 — 조용한 삭제 금지");
                Assert.AreEqual(0, _doc.SpawnRoutes[0]);
                Assert.AreEqual(1, _doc.SpawnRoutes[1]);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Write_WithEmptySpawnRoutes_Clears()
        {
            var map = MakeMap(6, 4, 2);
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new[] { 0, 1 });
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new int[0]);

                Assert.AreEqual(0, _doc.SpawnRoutes.Count, "빈 배열 = 명시적 삭제");
            }
            finally { map.Dispose(); }
        }

        // waypoint-routing unit 8 — ToGeneratedMap 정규화(길이 맞추기)와 RouteForSpawn 폴백.

        [Test]
        public void ToGeneratedMap_NormalizesSpawnRoutes_PadsShortWithMinusOne()
        {
            var map = MakeMap(6, 4, 3);
            GeneratedMap projected = default;
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new[] { 2 });
                projected = MapDocumentBuilder.ToGeneratedMap(_doc, Allocator.Temp);

                Assert.IsTrue(projected.spawnRoutes.IsCreated);
                Assert.AreEqual(3, projected.spawnRoutes.Length, "spawns 개수로 정규화");
                Assert.AreEqual(2, projected.RouteForSpawn(0));
                Assert.AreEqual(-1, projected.RouteForSpawn(1), "짧은 문서 배열 -1 패딩");
                Assert.AreEqual(-1, projected.RouteForSpawn(2), "짧은 문서 배열 -1 패딩");
            }
            finally { map.Dispose(); projected.Dispose(); }
        }

        [Test]
        public void ToGeneratedMap_NormalizesSpawnRoutes_TruncatesLong()
        {
            var map = MakeMap(6, 4, 2);
            GeneratedMap projected = default;
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new[] { 0, 1, 2, 3, 4 });
                projected = MapDocumentBuilder.ToGeneratedMap(_doc, Allocator.Temp);

                Assert.AreEqual(2, projected.spawnRoutes.Length, "초과분 절삭");
                Assert.AreEqual(0, projected.RouteForSpawn(0));
                Assert.AreEqual(1, projected.RouteForSpawn(1));
            }
            finally { map.Dispose(); projected.Dispose(); }
        }

        [Test]
        public void ToGeneratedMap_NoAuthoredSpawnRoutes_LeavesUncreated()
        {
            var map = MakeMap(6, 4, 2);
            GeneratedMap projected = default;
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map); // spawnRoutes 안 넘김 — 저작 없는 기존 문서와 동형
                projected = MapDocumentBuilder.ToGeneratedMap(_doc, Allocator.Temp);

                Assert.IsFalse(projected.spawnRoutes.IsCreated, "미저작 = 미생성(폴백 모양 유지)");
                Assert.AreEqual(-1, projected.RouteForSpawn(0));
                Assert.AreEqual(-1, projected.RouteForSpawn(1));
            }
            finally { map.Dispose(); projected.Dispose(); }
        }

        [Test]
        public void RouteForSpawn_OutOfRangeOrNegativeIndex_ReturnsMinusOne()
        {
            var map = MakeMap(6, 4, 2);
            GeneratedMap projected = default;
            try
            {
                MapDocumentBuilder.WriteToDocument(_doc, in map, null, null, new[] { 5, 6 });
                projected = MapDocumentBuilder.ToGeneratedMap(_doc, Allocator.Temp);

                Assert.AreEqual(-1, projected.RouteForSpawn(-1));
                Assert.AreEqual(-1, projected.RouteForSpawn(2));
            }
            finally { map.Dispose(); projected.Dispose(); }
        }
    }
}
