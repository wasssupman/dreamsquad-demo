using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Somnia.Battle.BattleCore.Map;
using Somnia.Battle.Core;

namespace Somnia.Battle.Tests.EditMode
{
    // battle-core-rebuild unit 9 — 옛 `BoardSpaceTests` 의 규칙을 옮긴 것. `BoardSpace` 는 남고 새 층(`CoreMapOverlay`·입력·뷰 풀)이 쓴다.
    // sim 셀 중심의 출처는 코어의 `Somnia.Battle.BattleCore.Map.GridMath`.
    //
    // 이 스위트가 지키는 계약 하나: **셀↔월드 정합의 권위는 주입된 보드 평면 Transform + tileSize 다.**
    // 셀 (0,0) 의 최소 모서리가 평면 원점, 평면 로컬 X/Y 가 셀 축(한 칸 = tileSize), 로컬 +Z 가 법선.
    // BoardSpace 는 그 밖의 셀 수식(회전·오프셋)을 스스로 갖지 않는다 — 평면 Transform 이 전부다. 그래서
    // **회전 + 오프셋** 평면으로 겨눈다 — 회전은 장식이 아니라 프로덕션 구성이다(보드를 XZ 바닥에 90°X 로 눕힌다).
    // tilemap-untangle 단위 1(2026-10-07): 옛 `GridLayout` 권위를 뗐다. 비균일 cellSize 케이스는 함께 사라졌다 —
    // tileSize 는 스칼라고 `CoreBoardPlane.Declare` 도 `(t, t)` 만 세웠다.
    public class BoardSpaceAuthorityTests
    {
        private GameObject _planeGo;

        [TearDown]
        public void TearDown()
        {
            // BoardSpace 는 정적 상태이나 "안전 idle 모드"는 없다 — 각 테스트가 자체 Configure 로 시작한다.
            if (_planeGo != null) Object.DestroyImmediate(_planeGo);
        }

        private Transform CreatePlane(Vector3 position, Vector3 eulerAngles = default)
        {
            _planeGo = new GameObject("BoardSpaceAuthorityTestPlane");
            _planeGo.transform.position = position;
            _planeGo.transform.rotation = Quaternion.Euler(eulerAngles);
            return _planeGo.transform;
        }

        private static void AssertNear(float3 expected, float3 actual, string label)
        {
            Assert.Less(math.distance(expected, actual), 1e-3f,
                $"{label}: expected {expected}, got {actual}");
        }

        // 평면 없는 구성은 에러 + 무시(마지막 유효 구성 유지)
        [Test]
        public void 평면_없는_구성은_에러를_내고_마지막_유효_구성을_지킨다()
        {
            var plane = CreatePlane(Vector3.zero);
            BoardSpace.Configure(new float3(3f, 0f, 5f), 2f, plane);
            var before = BoardSpace.ToView(new float3(3f, 0f, 5f));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "[BoardSpace] 보드 평면 Transform 이 필요하다; Configure 를 무시한다.");
            BoardSpace.Configure(float3.zero, 1f, null);

            AssertNear(before, BoardSpace.ToView(new float3(3f, 0f, 5f)), "config retained");
        }

        // 보드 평면 위 점은 ToView→ToSim 왕복으로 되돌아온다
        [Test]
        public void 평평한_평면에서_왕복하면_sim_자리가_돌아온다()
        {
            var simOrigin = new float3(3f, 0f, 5f);
            var plane = CreatePlane(new Vector3(-1f, 4f, 0f));
            BoardSpace.Configure(simOrigin, 2f, plane);

            foreach (var p in BoardPlanePoints(simOrigin, 2f))
                AssertNear(p, BoardSpace.ToSim(BoardSpace.ToView(p)), $"roundtrip {p}");
        }

        // 회전·오프셋 평면에서도 왕복 성립
        [Test]
        public void 회전_평면에서도_왕복하면_sim_자리가_돌아온다()
        {
            var simOrigin = new float3(-2f, 0f, 1.5f);
            var plane = CreatePlane(new Vector3(0.7f, -0.2f, 4f), new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(simOrigin, 2f, plane);

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

        // sim 셀 중심 ↔ 평면 로컬 셀 중심 ((x+0.5)·t, (y+0.5)·t) 일치 — 정합 권위 = 평면 + tileSize
        [Test]
        public void 평평한_평면에서_sim_셀_중심이_평면_셀_중심과_같다()
        {
            AssertCellCentersMatch(2f, Vector3.zero);
        }

        [Test]
        public void 회전_평면에서도_sim_셀_중심이_평면_셀_중심과_같다()
        {
            AssertCellCentersMatch(2f, new Vector3(90f, 0f, 0f));
        }

        private void AssertCellCentersMatch(float tileSize, Vector3 eulerAngles)
        {
            var simOrigin = new float3(2f, 0f, -3f);
            var plane = CreatePlane(new Vector3(0.7f, -0.2f, 0f), eulerAngles);
            BoardSpace.Configure(simOrigin, tileSize, plane);

            foreach (var cell in new[] { new int2(0, 0), new int2(1, 0), new int2(0, 1), new int2(3, 2) })
            {
                float3 simCenter = GridMath.CellToWorldCenter(cell, tileSize, simOrigin.y, simOrigin);
                // **평면 로컬 공간에서 비교한다.** 월드 XY 로 비교하면 회전한 순간 거짓 실패한다.
                Vector3 localView = plane.InverseTransformPoint((Vector3)BoardSpace.ToView(simCenter));
                var localCenter = new float2((cell.x + 0.5f) * tileSize, (cell.y + 0.5f) * tileSize);

                Assert.Less(math.distance(localCenter, ((float3)localView).xy), 1e-3f,
                    $"cell {cell} (euler {eulerAngles}) plane-local: expected {localCenter}, view {localView}");
                Assert.Less(math.abs(localView.z), 1e-3f, $"cell {cell}: 셀 중심은 평면 위(로컬 z=0)여야 한다");
            }
        }

        // 방향 변환도 평면 축을 따른다(sim 1 유닛 = 평면 로컬 1 유닛 — tileSize 는 셀 수와 길이 사이의 환산이라 길이는 보존된다)
        [Test]
        public void 회전_평면에서_방향_변환은_평면_축을_따른다()
        {
            var plane = CreatePlane(Vector3.zero, new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(float3.zero, 2f, plane);

            float3 dirX = BoardSpace.ToViewVector(new float3(1f, 0f, 0f)); // sim +x
            float3 dirZ = BoardSpace.ToViewVector(new float3(0f, 0f, 1f)); // sim +z

            // 90°X 회전에서 평면 로컬 +Y 는 월드 +Z 가 된다.
            AssertNear(new float3(1f, 0f, 0f), dirX, "sim +x → plane local +X → world +X");
            AssertNear(new float3(0f, 0f, 1f), dirZ, "sim +z → plane local +Y → world +Z");
        }

        // 입력 레이캐스트 평면 = 보드 평면
        [Test]
        public void 레이캐스트_평면은_보드_평면이다()
        {
            var plane = CreatePlane(new Vector3(2f, 3f, 1f));
            BoardSpace.Configure(float3.zero, 1f, plane);
            var p = BoardSpace.RaycastPlane();
            Assert.Less(math.abs(math.abs(p.normal.z) - 1f), 1e-3f, "평면 법선은 ±Z");
            Assert.Less(math.abs(p.GetDistanceToPoint(new Vector3(-5f, 7f, 1f))), 1e-3f,
                "평면이 plane z 를 지난다");
        }

        // 평면이 돌면 입력 평면도 같이 돈다(배치 탭이 엉뚱한 셀에 안 떨어진다)
        [Test]
        public void 레이캐스트_평면은_보드_평면_회전을_따른다()
        {
            var plane = CreatePlane(new Vector3(0f, 5f, 0f), new Vector3(90f, 0f, 0f));
            BoardSpace.Configure(float3.zero, 1f, plane);
            var p = BoardSpace.RaycastPlane();
            Assert.Less(math.abs(math.abs(p.normal.y) - 1f), 1e-3f, "90°X 회전 후 법선은 ±Y");
            Assert.Less(math.abs(p.GetDistanceToPoint(new Vector3(-4f, 5f, 8f))), 1e-3f,
                "평면이 plane 높이(y=5)를 지난다");
        }

        // tilemap-untangle 단위 1 의 동치 고정 테스트(옛 `Grid` 식 = 평면 × t, 회전 3종 × 임의 점 20, 1e-5)는
        // 단위 2 에서 Tilemap 모듈과 함께 지웠다 — Rectangle · 간격 0 · cellSize (t,t,1) 에서 `CellToLocalInterpolated(v)` 는
        // `v × cellSize` 라 두 식은 정의상 같다.
    }
}
