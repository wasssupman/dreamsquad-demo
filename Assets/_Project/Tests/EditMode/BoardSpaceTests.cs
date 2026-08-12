using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // 이 스위트가 지키는 계약 하나: **셀↔월드 정합의 권위는 주입된 GridLayout 이다.**
    // BoardSpace 는 셀 크기/회전/오프셋 수식을 스스로 갖지 않는다.
    //
    // map-view-deadcode-removal unit 1 — 예전엔 이 계약을 "Isometric cellLayout 에서도 맞나"로
    // 검증했다. iso 폐기 후에는 **회전 + 비균일 cellSize + 오프셋** rect 그리드로 같은 걸 겨눈다.
    // 회전은 장식이 아니라 프로덕션 구성이다(TilemapMapView 가 보드를 XZ 바닥에 90°X 로 눕힌다) —
    // 위임이 깨지면 바로 이 축에서 조용히 어긋난다.
    public class BoardSpaceTests
    {
        private GameObject _gridGo;

        [TearDown]
        public void TearDown()
        {
            // BoardSpace 는 정적 상태이나 "안전 idle 모드"는 없다(Legacy identity 폴백 제거).
            // 모든 사용처는 Configure 후 사용이 계약 — 각 테스트가 자체 Configure 로 시작한다.
            if (_gridGo != null) Object.DestroyImmediate(_gridGo);
        }

        private Grid CreateGrid(Vector3 cellSize, Vector3 position, Vector3 eulerAngles = default)
        {
            _gridGo = new GameObject("BoardSpaceTestGrid");
            _gridGo.transform.position = position;
            _gridGo.transform.rotation = Quaternion.Euler(eulerAngles);
            var grid = _gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = cellSize;
            return grid;
        }

        private static void AssertNear(float3 expected, float3 actual, string label)
        {
            Assert.Less(math.distance(expected, actual), 1e-3f,
                $"{label}: expected {expected}, got {actual}");
        }

        // --- 1. Configure 가드 (legacy-render-removal unit 3 — grid 없는 구성은 무시 + 에러) ---

        [Test]
        public void Configure_NullGrid_LogsErrorAndKeepsLastValidConfig()
        {
            var grid = CreateGrid(Vector3.one, Vector3.zero);
            BoardSpace.Configure(new float3(3f, 0f, 5f), 2f, grid);
            var before = BoardSpace.ToView(new float3(3f, 0f, 5f));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "[BoardSpace] Tilemap mode requires a GridLayout; ignoring Configure.");
            BoardSpace.Configure(float3.zero, 1f, null);

            // 마지막 유효 구성 유지 — 변환 결과 불변.
            AssertNear(before, BoardSpace.ToView(new float3(3f, 0f, 5f)), "config retained");
        }

        // --- 2. 라운드트립 (보드 평면 위 점, y = simOrigin.y) ---

        [Test]
        public void FlatGrid_RoundTrip_RecoversSimPosition()
        {
            var simOrigin = new float3(3f, 0f, 5f);
            var grid = CreateGrid(new Vector3(2f, 2f, 1f), new Vector3(-1f, 4f, 0f));
            BoardSpace.Configure(simOrigin, 2f, grid);

            foreach (var p in BoardPlanePoints(simOrigin, 2f))
                AssertNear(p, BoardSpace.ToSim(BoardSpace.ToView(p)), $"roundtrip {p}");
        }

        // 비자명 그리드(회전 + 비균일 cellSize + 오프셋)에서도 왕복이 성립해야 한다.
        // 90°X = 프로덕션 구성(보드를 XZ 바닥에 눕힘)이며, cellSize 를 비균일로 둬서
        // "두 축을 같은 수로 나눈다" 류의 숨은 가정을 잡는다.
        [Test]
        public void RotatedNonUniformGrid_RoundTrip_RecoversSimPosition()
        {
            var simOrigin = new float3(-2f, 0f, 1.5f);
            var grid = CreateGrid(new Vector3(2f, 3f, 1f), new Vector3(0.7f, -0.2f, 4f),
                new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(simOrigin, 2f, grid);

            foreach (var p in BoardPlanePoints(simOrigin, 2f))
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

        // --- 3. 정합 권위 = Grid: sim 셀 중심 ↔ Grid 셀 중심 일치 ---

        [Test]
        public void FlatGrid_SimCellCenter_MatchesGridCellCenter()
        {
            AssertCellCentersMatch(new Vector3(2f, 2f, 1f), 2f, Vector3.zero);
        }

        [Test]
        public void RotatedNonUniformGrid_SimCellCenter_MatchesGridCellCenter()
        {
            AssertCellCentersMatch(new Vector3(2f, 3f, 1f), 2f, new Vector3(90f, 0f, 0f));
        }

        private void AssertCellCentersMatch(Vector3 cellSize, float tileSize, Vector3 eulerAngles)
        {
            var simOrigin = new float3(2f, 0f, -3f);
            var grid = CreateGrid(cellSize, new Vector3(0.7f, -0.2f, 0f), eulerAngles);
            BoardSpace.Configure(simOrigin, tileSize, grid);

            foreach (var cell in new[] { new int2(0, 0), new int2(1, 0), new int2(0, 1), new int2(3, 2) })
            {
                float3 simCenter = GridMath.CellToWorldCenter(cell, tileSize, simOrigin.y, simOrigin);
                // **그리드 로컬 공간에서 비교한다.** 두 값은 로컬 Z(= cellSize.z 의 절반, Grid 가
                // 셀 중심에 더하는 깊이 오프셋)만큼만 달라야 하며, 그 차이는 회전과 무관하다.
                // 월드 XY 로 비교하면 그리드가 회전한 순간 축이 어긋나 테스트가 거짓 실패한다.
                Vector3 localView = grid.transform.InverseTransformPoint(
                    (Vector3)BoardSpace.ToView(simCenter));
                Vector3 localGrid = grid.transform.InverseTransformPoint(
                    grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0)));

                Assert.Less(math.distance(((float3)localGrid).xy, ((float3)localView).xy), 1e-3f,
                    $"cell {cell} (euler {eulerAngles}) grid-plane: grid {localGrid}, view {localView}");
            }
        }

        // --- 4. 방향 변환도 Grid 를 따른다 (facing/투사체 회전이 이 축에 걸려 있다) ---

        // sim +x / +z 각각이 **그리드 자신의 축**으로, **그리드 자신의 셀 스케일**을 달고 간다.
        // 90°X 회전에서 grid 로컬 +Y 는 월드 +Z 가 된다 — 그래서 sim +z 는 월드 +Z 로 나와야 한다.
        [Test]
        public void RotatedGrid_AxisDirections_FollowGridAxesAndCellScale()
        {
            var grid = CreateGrid(new Vector3(2f, 3f, 1f), Vector3.zero, new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(float3.zero, 2f, grid);

            float3 dirX = BoardSpace.ToViewVector(new float3(1f, 0f, 0f)); // sim +x
            float3 dirZ = BoardSpace.ToViewVector(new float3(0f, 0f, 1f)); // sim +z

            // sim 1유닛 = 0.5셀(tileSize 2) → 로컬 X 는 0.5×2=1, 로컬 Y 는 0.5×3=1.5.
            AssertNear(new float3(1f, 0f, 0f), dirX, "sim +x → grid local +X → world +X");
            AssertNear(new float3(0f, 0f, 1.5f), dirZ, "sim +z → grid local +Y → world +Z");
        }

        // --- 5. RaycastPlane = Grid 평면 ---

        [Test]
        public void RaycastPlane_MatchesGridPlane()
        {
            var grid = CreateGrid(Vector3.one, new Vector3(2f, 3f, 1f));
            BoardSpace.Configure(float3.zero, 1f, grid);
            var tilemap = BoardSpace.RaycastPlane();
            Assert.Less(math.abs(math.abs(tilemap.normal.z) - 1f), 1e-3f, "tilemap normal is ±Z");
            Assert.Less(math.abs(tilemap.GetDistanceToPoint(new Vector3(-5f, 7f, 1f))), 1e-3f,
                "tilemap plane passes through grid z");
        }

        // 회전한 그리드에서는 입력 평면도 같이 돈다 — 법선을 grid.transform.forward 에서
        // 유도하기 때문이다. 이게 깨지면 배치 탭이 엉뚱한 셀에 떨어진다.
        [Test]
        public void RaycastPlane_FollowsGridRotation()
        {
            var grid = CreateGrid(Vector3.one, new Vector3(0f, 5f, 0f), new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(float3.zero, 1f, grid);
            var plane = BoardSpace.RaycastPlane();
            Assert.Less(math.abs(math.abs(plane.normal.y) - 1f), 1e-3f, "90°X 회전 후 법선은 ±Y");
            Assert.Less(math.abs(plane.GetDistanceToPoint(new Vector3(-4f, 5f, 8f))), 1e-3f,
                "평면이 grid 높이(y=5)를 지난다");
        }
    }
}
