using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore.Map;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 9 — 옛 `BoardSpaceTests` 의 규칙을 그대로 옮긴 것(옛 파일은 퇴역과 함께 지워진다).
    // `BoardSpace` 는 남고 새 층(`CoreMapOverlay`·입력·뷰 풀)이 쓴다. 바뀐 것은 sim 셀 중심의 출처 하나 —
    // 옛 `Wassup.Battle.Movement.GridMath` 대신 코어의 `Wassup.BattleCore.Map.GridMath`(같은 산식)를 부른다.
    //
    // 이 스위트가 지키는 계약 하나: **셀↔월드 정합의 권위는 주입된 GridLayout 이다.**
    // BoardSpace 는 셀 크기/회전/오프셋 수식을 스스로 갖지 않는다. 그래서 **회전 + 비균일 cellSize + 오프셋**
    // rect 그리드로 겨눈다 — 회전은 장식이 아니라 프로덕션 구성이다(보드를 XZ 바닥에 90°X 로 눕힌다).
    public class BoardSpaceAuthorityTests
    {
        private GameObject _gridGo;

        [TearDown]
        public void TearDown()
        {
            // BoardSpace 는 정적 상태이나 "안전 idle 모드"는 없다 — 각 테스트가 자체 Configure 로 시작한다.
            if (_gridGo != null) Object.DestroyImmediate(_gridGo);
        }

        private Grid CreateGrid(Vector3 cellSize, Vector3 position, Vector3 eulerAngles = default)
        {
            _gridGo = new GameObject("BoardSpaceAuthorityTestGrid");
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

        // 옛 BoardSpaceTests::Configure_NullGrid_LogsErrorAndKeepsLastValidConfig — grid 없는 구성은 에러 + 무시(마지막 유효 구성 유지)
        [Test]
        public void 그리드_없는_구성은_에러를_내고_마지막_유효_구성을_지킨다()
        {
            var grid = CreateGrid(Vector3.one, Vector3.zero);
            BoardSpace.Configure(new float3(3f, 0f, 5f), 2f, grid);
            var before = BoardSpace.ToView(new float3(3f, 0f, 5f));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "[BoardSpace] Tilemap mode requires a GridLayout; ignoring Configure.");
            BoardSpace.Configure(float3.zero, 1f, null);

            AssertNear(before, BoardSpace.ToView(new float3(3f, 0f, 5f)), "config retained");
        }

        // 옛 BoardSpaceTests::FlatGrid_RoundTrip_RecoversSimPosition — 보드 평면 위 점은 ToView→ToSim 왕복으로 되돌아온다
        [Test]
        public void 평평한_그리드에서_왕복하면_sim_자리가_돌아온다()
        {
            var simOrigin = new float3(3f, 0f, 5f);
            var grid = CreateGrid(new Vector3(2f, 2f, 1f), new Vector3(-1f, 4f, 0f));
            BoardSpace.Configure(simOrigin, 2f, grid);

            foreach (var p in BoardPlanePoints(simOrigin, 2f))
                AssertNear(p, BoardSpace.ToSim(BoardSpace.ToView(p)), $"roundtrip {p}");
        }

        // 옛 BoardSpaceTests::RotatedNonUniformGrid_RoundTrip_RecoversSimPosition — 회전·비균일·오프셋 그리드에서도 왕복 성립
        [Test]
        public void 회전_비균일_그리드에서도_왕복하면_sim_자리가_돌아온다()
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

        // 옛 BoardSpaceTests::FlatGrid_SimCellCenter_MatchesGridCellCenter — sim 셀 중심 ↔ Grid 셀 중심 일치(정합 권위 = Grid)
        [Test]
        public void 평평한_그리드에서_sim_셀_중심이_그리드_셀_중심과_같다()
        {
            AssertCellCentersMatch(new Vector3(2f, 2f, 1f), 2f, Vector3.zero);
        }

        // 옛 BoardSpaceTests::RotatedNonUniformGrid_SimCellCenter_MatchesGridCellCenter — 회전·비균일에서도 셀 중심 일치
        [Test]
        public void 회전_비균일_그리드에서도_sim_셀_중심이_그리드_셀_중심과_같다()
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
                // **그리드 로컬 공간에서 비교한다.** 두 값은 로컬 Z(Grid 가 셀 중심에 더하는 깊이 오프셋)만큼만
                // 달라야 하며 그 차이는 회전과 무관하다. 월드 XY 로 비교하면 회전한 순간 거짓 실패한다.
                Vector3 localView = grid.transform.InverseTransformPoint(
                    (Vector3)BoardSpace.ToView(simCenter));
                Vector3 localGrid = grid.transform.InverseTransformPoint(
                    grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0)));

                Assert.Less(math.distance(((float3)localGrid).xy, ((float3)localView).xy), 1e-3f,
                    $"cell {cell} (euler {eulerAngles}) grid-plane: grid {localGrid}, view {localView}");
            }
        }

        // 옛 BoardSpaceTests::RotatedGrid_AxisDirections_FollowGridAxesAndCellScale — 방향 변환도 그리드 축·셀 스케일을 따른다
        [Test]
        public void 회전_그리드에서_방향_변환은_그리드_축과_셀_스케일을_따른다()
        {
            var grid = CreateGrid(new Vector3(2f, 3f, 1f), Vector3.zero, new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(float3.zero, 2f, grid);

            float3 dirX = BoardSpace.ToViewVector(new float3(1f, 0f, 0f)); // sim +x
            float3 dirZ = BoardSpace.ToViewVector(new float3(0f, 0f, 1f)); // sim +z

            // sim 1유닛 = 0.5셀(tileSize 2) → 로컬 X 는 0.5×2=1, 로컬 Y 는 0.5×3=1.5.
            // 90°X 회전에서 grid 로컬 +Y 는 월드 +Z 가 된다.
            AssertNear(new float3(1f, 0f, 0f), dirX, "sim +x → grid local +X → world +X");
            AssertNear(new float3(0f, 0f, 1.5f), dirZ, "sim +z → grid local +Y → world +Z");
        }

        // 옛 BoardSpaceTests::RaycastPlane_MatchesGridPlane — 입력 레이캐스트 평면 = Grid 평면
        [Test]
        public void 레이캐스트_평면은_그리드_평면이다()
        {
            var grid = CreateGrid(Vector3.one, new Vector3(2f, 3f, 1f));
            BoardSpace.Configure(float3.zero, 1f, grid);
            var plane = BoardSpace.RaycastPlane();
            Assert.Less(math.abs(math.abs(plane.normal.z) - 1f), 1e-3f, "평면 법선은 ±Z");
            Assert.Less(math.abs(plane.GetDistanceToPoint(new Vector3(-5f, 7f, 1f))), 1e-3f,
                "평면이 grid z 를 지난다");
        }

        // 옛 BoardSpaceTests::RaycastPlane_FollowsGridRotation — 그리드가 돌면 입력 평면도 같이 돈다(배치 탭이 엉뚱한 셀에 안 떨어진다)
        [Test]
        public void 레이캐스트_평면은_그리드_회전을_따른다()
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
