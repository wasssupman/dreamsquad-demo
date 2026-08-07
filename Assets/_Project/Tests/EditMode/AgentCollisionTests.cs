using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 3 — 원형 충돌 + 벽 슬라이드 회귀.
    //
    // 4x5 맵. y=2 만 벽:
    //   y=4  . . . .
    //   y=3  . . . .
    //   y=2  # # # #      ← 벽. 진입면 z = 1.5
    //   y=1  . . . .
    //   y=0  . . . .
    // 타일 1, 원점 0 → 셀 (x,y) 중심 = (x, _, y), 타일 경계는 ±0.5.
    //
    // r=0.35 인 에이전트가 아래 구역에서 가질 수 있는 z 는 [-0.15, 1.15] 다
    // (아래는 격자 경계 -0.5, 위는 벽면 1.5, 각각 반지름만큼 안쪽).
    public class AgentCollisionTests
    {
        private const float R = 0.35f;
        private const float Eps = 2e-3f;
        private const int W = 4, H = 5;

        private NativeArray<byte> _walk;
        private NativeHashSet<int2> _blocked;

        [SetUp]
        public void SetUp()
        {
            _walk = new NativeArray<byte>(W * H, Allocator.Persistent);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                _walk[y * W + x] = (byte)(y == 2 ? 0 : 1);
            _blocked = new NativeHashSet<int2>(4, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_walk.IsCreated) _walk.Dispose();
            if (_blocked.IsCreated) _blocked.Dispose();
        }

        private NavGrid Nav(bool withObstacles = false) => new NavGrid(
            staticWalk: _walk,
            blockedCells: withObstacles ? _blocked : default,
            hasObstacles: withObstacles,
            gridSize: new int2(W, H), tileSize: 1f, origin: float3.zero);

        // ── 정면 충돌 ───────────────────────────────────────────────────────────

        [Test]
        public void HeadOn_StopsAtWallFace_WithRadiusGap()
        {
            var from = new float3(1f, 0f, 0.5f);
            var to   = new float3(1f, 0f, 1.4f);   // 벽(y=2)으로 직진

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(1.5f - R, r.z, Eps, "원 가장자리가 벽면(1.5)에 선다");
            Assert.AreEqual(1f, r.x, Eps, "직교축은 무변경");
        }

        [Test]
        public void NoWall_PassesThroughUnchanged()
        {
            var from = new float3(1f, 0f, 0.5f);
            var to   = new float3(1.4f, 0f, 0.9f);   // 원이 벽에 닿지 않는 범위

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(to.x, r.x, Eps);
            Assert.AreEqual(to.z, r.z, Eps);
        }

        // ── 슬라이드 (이 unit 의 핵심) ─────────────────────────────────────────

        [Test]
        public void Diagonal_IntoWall_SlidesAlongIt()
        {
            var from = new float3(1f, 0f, 0.5f);
            var to   = new float3(1.4f, 0f, 1.4f);   // 대각으로 벽에 비스듬히 접근

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(1.5f - R, r.z, Eps, "벽 축은 멈춘다");
            Assert.AreEqual(1.4f, r.x, Eps, "자유 축은 계속 간다 — 이것이 슬라이드");
        }

        [Test]
        public void Diagonal_Slide_PreservesFullTangentialDistance()
        {
            // 접선 성분이 깎이면 벽에 붙었을 때 눈에 띄게 느려진다. 온전히 보존돼야 한다.
            var from = new float3(0.5f, 0f, 1.0f);
            var to   = new float3(0.9f, 0f, 1.4f);

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(0.4f, r.x - from.x, Eps, "접선 변위 온전");
            Assert.AreEqual(1.5f - R, r.z, Eps);
        }

        // ── 겹침·경계 방어 ──────────────────────────────────────────────────────

        [Test]
        public void AlreadyOverlappingWall_DoesNotSnapBackwards()
        {
            // 외력·텔레포트로 벽에 겹쳐 들어간 상태. 뒤로 튕기지 않고 제자리에 머문다.
            var from = new float3(1f, 0f, 1.7f);    // 이미 벽 타일(y=2) 안
            var to   = new float3(1f, 0f, 1.9f);    // 더 깊이

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.LessOrEqual(r.z, to.z + Eps, "전진 한계를 넘지 않는다");
            Assert.GreaterOrEqual(r.z, from.z - Eps, "뒤로 튕기지 않는다");
        }

        [Test]
        public void MovingAwayFromWall_IsNeverBlocked()
        {
            var from = new float3(1f, 0f, 1.1f);
            var to   = new float3(1f, 0f, 0.7f);    // 벽 반대 방향

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(to.z, r.z, Eps);
        }

        [Test]
        public void GridBoundary_RespectsRadius()
        {
            // 격자 밖은 NavGrid 가 막힘으로 판정하므로, 가장자리도 반지름만큼 여유를 둔다.
            var from = new float3(1f, 0f, 0f);
            var to   = new float3(1f, 0f, -0.4f);

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(-0.5f + R, r.z, Eps, "아래 경계(-0.5)에서 반지름만큼 안쪽");
        }

        // ── 반지름 계약 ─────────────────────────────────────────────────────────

        [Test]
        public void ZeroRadius_MatchesPointClamp()
        {
            var nav = Nav();
            var from = new float3(1f, 0f, 0.5f);
            var to   = new float3(1f, 0f, 1.7f);

            var circle = AgentCollision.Resolve(from, to, 0f, nav);
            var point  = MovementCellTrim.Apply(
                to, GridMath.WorldToCell(from, 1f, nav.gridSize, origin: float3.zero), nav);

            Assert.AreEqual(point.x, circle.x, 1e-6f);
            Assert.AreEqual(point.z, circle.z, 1e-6f);
        }

        [Test]
        public void LargerRadius_StopsEarlier()
        {
            var from = new float3(1f, 0f, 0.5f);
            var to   = new float3(1f, 0f, 1.4f);

            var small = AgentCollision.Resolve(from, to, 0.2f, Nav());
            var large = AgentCollision.Resolve(from, to, 0.45f, Nav());

            Assert.Less(large.z, small.z, "반지름이 클수록 더 일찍 선다");
        }

        // ── 통로 통과 · 동적 장애물 ─────────────────────────────────────────────

        [Test]
        public void Radius035_FitsThroughOneTileCorridor()
        {
            // 위아래가 모두 벽인 1타일 폭 복도(y=1)를 가로로 통과한다.
            for (int x = 0; x < W; x++)
            {
                _walk[0 * W + x] = 0;
                _walk[1 * W + x] = 1;
                _walk[2 * W + x] = 0;
            }
            var from = new float3(0f, 0f, 1f);
            var to   = new float3(0.8f, 0f, 1f);

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.AreEqual(0.8f, r.x, Eps, "지름 0.7 < 1.0 이라 통과한다");
        }

        [Test]
        public void InnerCorner_DoesNotLeakThroughDiagonalGap()
        {
            // 두 벽이 대각으로 맞닿은 모서리. 그 사이 대각 틈으로 빠져나가면 안 된다
            // (ecs-review MEDIUM — 알고리즘은 맞지만 회귀 안전망이 없었다).
            //
            //   y=2  . # . .      (1,2) 벽
            //   y=1  . . # .      (2,1) 벽
            //   y=0  . . . .
            // 대각 통로가 (1.5, 1.5) 한 점뿐이라, 반대축 범위를 안 보면 그 점을 지나쳐 버린다.
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                _walk[y * W + x] = 1;
            _walk[2 * W + 1] = 0;   // (1,2)
            _walk[1 * W + 2] = 0;   // (2,1)

            var from = new float3(1f, 0f, 1f);   // 셀 (1,1) — 열려 있음
            var to   = new float3(2f, 0f, 2f);   // 셀 (2,2) — 열려 있지만 대각 틈으로만 닿는다

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.LessOrEqual(r.x, 1.5f - R + Eps, "(2,1) 벽면을 넘지 않는다");
            Assert.LessOrEqual(r.z, 1.5f - R + Eps, "(1,2) 벽면을 넘지 않는다");
        }

        [Test]
        public void FastStepFromCellBoundary_DoesNotSkipIntermediateWall()
        {
            // ecs-review M1 회귀. 셀 경계에 선 채 외력으로 전속 이동하면 전진 가장자리가
            // 최대 (0.5 + 0.9 + r) 타일까지 간다. 최종 위치만 검사하면 벽 셀 하나를 지나쳐
            // 그 너머 빈 셀에 도달한다 — 스윕이 그 구멍을 막아야 한다.
            //
            //   y=1 행: (0,1) 열림 / (1,1) 벽 / (2,1) 열림
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                _walk[y * W + x] = 1;
            _walk[1 * W + 1] = 0;

            // 시작은 벽에 닿지 않은 최대 전진 지점(중심 0.14, 가장자리 0.49).
            // 여기서 큰 변위를 주면 도착 가장자리는 셀 2 에 있어, 최종 위치만 보는 구현은
            // 중간의 벽 셀 1 을 못 본다.
            var from = new float3(0.14f, 0f, 1f);
            var to   = new float3(1.9f, 0f, 1f);    // 벽 셀 (1,1) 을 뛰어넘어 (2,1) 로

            var r = AgentCollision.Resolve(from, to, R, Nav());

            Assert.LessOrEqual(r.x, 0.5f - R + Eps, "중간 벽을 건너뛰지 않는다");
            Assert.GreaterOrEqual(r.x, from.x - Eps, "뒤로 튕기지 않는다");
        }

        [Test]
        public void ObstacleOverlay_BlocksLikeStaticWall()
        {
            _blocked.Add(new int2(2, 1));
            var from = new float3(1f, 0f, 1f);
            var to   = new float3(1.6f, 0f, 1f);

            var r = AgentCollision.Resolve(from, to, R, Nav(withObstacles: true));

            Assert.AreEqual(1.5f - R, r.x, Eps, "동적 장애물도 같은 면에서 막는다");
        }
    }
}
