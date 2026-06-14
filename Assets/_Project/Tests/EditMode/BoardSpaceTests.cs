using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class BoardSpaceTests
    {
        private GameObject _gridGo;

        [TearDown]
        public void TearDown()
        {
            // BoardSpace 는 정적 상태 — 테스트 간 누수 방지를 위해 Legacy 로 리셋.
            BoardSpace.Configure(BoardViewMode.Legacy3D, float3.zero, 1f, null);
            if (_gridGo != null) Object.DestroyImmediate(_gridGo);
        }

        private Grid CreateGrid(GridLayout.CellLayout layout, Vector3 cellSize, Vector3 position)
        {
            _gridGo = new GameObject("BoardSpaceTestGrid");
            _gridGo.transform.position = position;
            var grid = _gridGo.AddComponent<Grid>();
            grid.cellLayout = layout;
            grid.cellSize = cellSize;
            return grid;
        }

        private static void AssertNear(float3 expected, float3 actual, string label)
        {
            Assert.Less(math.distance(expected, actual), 1e-3f,
                $"{label}: expected {expected}, got {actual}");
        }

        // --- 1. Legacy3D = identity ---

        [Test]
        public void Legacy3D_AllConversions_AreIdentity()
        {
            BoardSpace.Configure(BoardViewMode.Legacy3D, new float3(3f, 0.5f, -2f), 2f, null);

            var p = new float3(7.3f, 1.2f, -4.8f);
            AssertNear(p, BoardSpace.ToView(p), "ToView");
            AssertNear(p, BoardSpace.ToSim(p), "ToSim");

            var d = new float3(0.6f, 0f, -0.8f);
            AssertNear(d, BoardSpace.ToViewVector(d), "ToViewVector");
        }

        // --- 2. 라운드트립 (보드 평면 위 점, y = simOrigin.y) ---

        [Test]
        public void TilemapRect_RoundTrip_RecoversSimPosition()
        {
            var simOrigin = new float3(3f, 0f, 5f);
            var grid = CreateGrid(GridLayout.CellLayout.Rectangle, new Vector3(2f, 2f, 1f), new Vector3(-1f, 4f, 0f));
            BoardSpace.Configure(BoardViewMode.TilemapRect, simOrigin, 2f, grid);

            foreach (var p in BoardPlanePoints(simOrigin, 2f))
                AssertNear(p, BoardSpace.ToSim(BoardSpace.ToView(p)), $"roundtrip {p}");
        }

        [Test]
        public void TilemapIso_RoundTrip_RecoversSimPosition()
        {
            var simOrigin = new float3(0f, 0f, 0f);
            var grid = CreateGrid(GridLayout.CellLayout.Isometric, new Vector3(1f, 0.5f, 1f), Vector3.zero);
            BoardSpace.Configure(BoardViewMode.TilemapIso, simOrigin, 1f, grid);

            foreach (var p in BoardPlanePoints(simOrigin, 1f))
                AssertNear(p, BoardSpace.ToSim(BoardSpace.ToView(p)), $"roundtrip {p}");
        }

        private static float3[] BoardPlanePoints(float3 origin, float tileSize)
        {
            // 셀 중심(정수배), 셀 경계 부근, 비대칭 좌표를 섞는다.
            return new[]
            {
                origin,
                origin + new float3(tileSize * 1f, 0f, 0f),
                origin + new float3(0f, 0f, tileSize * 3f),
                origin + new float3(tileSize * 4.5f, 0f, tileSize * 2.25f),
                origin + new float3(tileSize * 0.49f, 0f, tileSize * 7.51f),
            };
        }

        // --- 3. 정합 권위 = Grid: sim 셀 중심 ↔ Tilemap 셀 중심 일치 ---

        [Test]
        public void TilemapRect_SimCellCenter_MatchesGridCellCenter()
        {
            AssertCellCentersMatch(GridLayout.CellLayout.Rectangle, new Vector3(2f, 2f, 1f), 2f);
        }

        [Test]
        public void TilemapIso_SimCellCenter_MatchesGridCellCenter()
        {
            AssertCellCentersMatch(GridLayout.CellLayout.Isometric, new Vector3(1f, 0.5f, 1f), 1f);
        }

        private void AssertCellCentersMatch(GridLayout.CellLayout layout, Vector3 cellSize, float tileSize)
        {
            var simOrigin = new float3(2f, 0f, -3f);
            var grid = CreateGrid(layout, cellSize, new Vector3(0.7f, -0.2f, 0f));
            var mode = layout == GridLayout.CellLayout.Rectangle
                ? BoardViewMode.TilemapRect : BoardViewMode.TilemapIso;
            BoardSpace.Configure(mode, simOrigin, tileSize, grid);

            foreach (var cell in new[] { new int2(0, 0), new int2(1, 0), new int2(0, 1), new int2(3, 2) })
            {
                float3 simCenter = GridMath.CellToWorldCenter(cell, tileSize, simOrigin.y, simOrigin);
                float3 viewCenter = BoardSpace.ToView(simCenter);
                float3 gridCenter = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                // 정합은 화면 평면(XY) 기준. Z 는 깊이/sorting 축이라 의도적으로 다르다
                // (Grid.GetCellCenterWorld 는 cellSize.z 의 절반을 더하지만 ToView 는
                //  보드 콘텐츠를 grid Z 평면에 둔다 — sorting 은 BoardSortOrder 가 담당).
                Assert.Less(math.distance(gridCenter.xy, viewCenter.xy), 1e-3f,
                    $"cell {cell} ({layout}) screen-plane: grid {gridCenter}, view {viewCenter}");
            }
        }

        // --- 4. iso 대각 방향: +x 셀과 +z 셀이 서로 다른 마름모 대각으로 ---

        [Test]
        public void TilemapIso_AxisDirections_MapToDistinctDiagonals()
        {
            var grid = CreateGrid(GridLayout.CellLayout.Isometric, new Vector3(1f, 0.5f, 1f), Vector3.zero);
            BoardSpace.Configure(BoardViewMode.TilemapIso, float3.zero, 1f, grid);

            float3 dirX = BoardSpace.ToViewVector(new float3(1f, 0f, 0f)); // sim +x
            float3 dirZ = BoardSpace.ToViewVector(new float3(0f, 0f, 1f)); // sim +z

            // 둘 다 화면 위쪽(y > 0) 대각이되, 좌우가 갈린다.
            Assert.Greater(dirX.y, 0f, "sim +x must rise on screen");
            Assert.Greater(dirZ.y, 0f, "sim +z must rise on screen");
            Assert.Greater(dirX.x, 0f, "sim +x must go screen-right");
            Assert.Less(dirZ.x, 0f, "sim +z must go screen-left");
            Assert.Greater(math.distance(dirX, dirZ), 0.1f, "diagonals must be distinct");
        }

        // --- 5. RaycastPlane 모드별 방향 ---

        [Test]
        public void RaycastPlane_MatchesViewMode()
        {
            BoardSpace.Configure(BoardViewMode.Legacy3D, new float3(0f, 1.5f, 0f), 1f, null);
            var legacy = BoardSpace.RaycastPlane();
            AssertNear(new float3(0f, 1f, 0f), legacy.normal, "legacy normal is +Y");
            Assert.Less(math.abs(legacy.GetDistanceToPoint(new Vector3(9f, 1.5f, -4f))), 1e-3f,
                "legacy plane passes through board height");

            var grid = CreateGrid(GridLayout.CellLayout.Rectangle, Vector3.one, new Vector3(2f, 3f, 1f));
            BoardSpace.Configure(BoardViewMode.TilemapRect, float3.zero, 1f, grid);
            var tilemap = BoardSpace.RaycastPlane();
            Assert.Less(math.abs(math.abs(tilemap.normal.z) - 1f), 1e-3f, "tilemap normal is ±Z");
            Assert.Less(math.abs(tilemap.GetDistanceToPoint(new Vector3(-5f, 7f, 1f))), 1e-3f,
                "tilemap plane passes through grid z");
        }
    }
}
