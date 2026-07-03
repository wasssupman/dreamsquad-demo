using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Battle.Movement;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // tilemap-view-backend unit 1 — Tilemap 페인트 위치와 BoardSpace(unit 0) 변환이
    // 같은 셀↔월드 정합을 공유함을 못 박는다. 이후 unit 에서 어긋나면 여기가 먼저 빨개진다.
    public class TilemapMapViewTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            // BoardSpace 리셋 불필요 — 안전 idle 모드 없음, 각 테스트가 자체 Configure 로 시작 (unit 3).
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private TilemapMapView CreateView(GridLayout.CellLayout layout, out Tilemap ground, Vector3 position)
        {
            _root = new GameObject("TilemapBoardTest");
            _root.transform.position = position;
            var grid = _root.AddComponent<Grid>();
            grid.cellLayout = layout;

            ground = CreateTilemapChild("Ground");
            var overlay = CreateTilemapChild("Overlay");

            var view = _root.AddComponent<TilemapMapView>();
            SetField(view, "grid", grid);
            SetField(view, "groundTilemap", ground);
            SetField(view, "overlayTilemap", overlay);
            return view;
        }

        private Tilemap CreateTilemapChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            return tilemap;
        }

        private static void SetField(object obj, string name, object value)
        {
            var field = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"field '{name}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static int CountPaintedCells(Tilemap tilemap, int w, int h)
        {
            var tiles = tilemap.GetTilesBlock(new BoundsInt(0, 0, 0, w, h, 1));
            int n = 0;
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] != null) n++;
            return n;
        }

        private static GeneratedMap BuildMap(int w, int h)
        {
            var map = new GeneratedMap
            {
                gridSize = new int2(w, h),
                tiles = new NativeArray<MapTileType>(w * h, Allocator.Persistent),
                spawns = new NativeArray<int2>(1, Allocator.Persistent),
                goal = new int2(w - 1, h - 1),
                seed = 1,
            };
            map.spawns[0] = new int2(0, 0);
            return map;
        }

        // --- 정합 고정: Tilemap.GetCellCenterWorld ≈ BoardSpace.ToView(셀 중심) (화면 평면) ---

        [Test]
        public void TilemapRect_PaintPositions_MatchBoardSpace()
            => AssertPaintMatchesBoardSpace(GridLayout.CellLayout.Rectangle, BoardViewMode.TilemapRect, 2f);

        [Test]
        public void TilemapIso_PaintPositions_MatchBoardSpace()
            => AssertPaintMatchesBoardSpace(GridLayout.CellLayout.Isometric, BoardViewMode.TilemapIso, 1f);

        private void AssertPaintMatchesBoardSpace(GridLayout.CellLayout layout, BoardViewMode mode, float tileSize)
        {
            var simOrigin = new float3(2f, 0f, -3f);
            var view = CreateView(layout, out var ground, new Vector3(0.7f, -0.2f, 0f));
            var tileSet = ScriptableObject.CreateInstance<TileSetData>();
            tileSet.isoCellSize = new Vector3(1f, 0.5f, 1f);

            var map = BuildMap(5, 4);
            try
            {
                // Initialize 가 Grid cellLayout/cellSize 를 모드에 맞춰 설정한다 (정합의 출발점).
                view.Initialize(map, tileSize, tileSet, mode);
                BoardSpace.Configure(mode, simOrigin, tileSize, view.Grid);

                foreach (var cell in new[] { new int2(0, 0), new int2(1, 0), new int2(0, 1), new int2(4, 3), new int2(2, 1) })
                {
                    float3 simCenter = GridMath.CellToWorldCenter(cell, tileSize, simOrigin.y, simOrigin);
                    float3 viewCenter = BoardSpace.ToView(simCenter);
                    float3 tileCenter = ground.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                    // 정합은 화면 평면(XY). Z(깊이/sorting) 는 의도적으로 다르다 (BoardSpaceTests 와 동일 규약).
                    Assert.Less(math.distance(tileCenter.xy, viewCenter.xy), 1e-3f,
                        $"cell {cell} ({layout}): tilemap {tileCenter}, boardspace {viewCenter}");
                }
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(tileSet);
            }
        }

        // --- Clear → Initialize 재진입에 잔상/누수 없음 ---

        [Test]
        public void ClearThenInitialize_Twice_LeavesNoStaleTiles()
        {
            var view = CreateView(GridLayout.CellLayout.Rectangle, out var ground, Vector3.zero);
            var tileSet = ScriptableObject.CreateInstance<TileSetData>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tileSet.walkTile = tile;
            tileSet.placeTile = tile;
            tileSet.envTile = tile;
            tileSet.decoTile = tile;

            var map = BuildMap(4, 4); // 전 셀 Walk(0) → walkTile 페인트
            try
            {
                view.Initialize(map, 1f, tileSet, BoardViewMode.TilemapRect);
                Assert.AreEqual(16, CountPaintedCells(ground, 4, 4), "첫 페인트 후 16셀");

                view.Clear();
                Assert.AreEqual(0, CountPaintedCells(ground, 4, 4), "Clear 후 잔상 0");

                view.Initialize(map, 1f, tileSet, BoardViewMode.TilemapRect);
                Assert.AreEqual(16, CountPaintedCells(ground, 4, 4), "재진입 페인트 후 16셀");

                view.Clear();
                Assert.AreEqual(0, CountPaintedCells(ground, 4, 4), "재진입 Clear 후 잔상 0");
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(tileSet);
            }
        }
    }
}
