using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 7 — 평활화가 이 spec 의 검증 질문("한 줄기 직선")에
    // 답하는 부분이다. 필드는 8방향으로 양자화돼 있고, 여기서 방향이 연속이 된다.
    public class PathSmoothingTests
    {
        private const float R = 0.35f;

        private static NavGrid Nav(NativeArray<byte> walk, int2 grid) => new NavGrid(
            staticWalk: walk, blockedCells: default, hasObstacles: false,
            gridSize: grid, tileSize: 1f, origin: float3.zero);

        private static NativeArray<byte> OpenField(int2 grid)
        {
            var walk = new NativeArray<byte>(grid.x * grid.y, Allocator.Temp);
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            return walk;
        }

        private static NativeArray<float2> BuildFlow(NativeArray<byte> walk, int2 grid, int2 goal)
        {
            var flow = new NativeArray<float2>(grid.x * grid.y, Allocator.Temp);
            var dist = new NativeArray<int>(grid.x * grid.y, Allocator.Temp);
            FlowFieldBuilder.Build(walk, grid, goal, flow, dist);
            dist.Dispose();
            return flow;
        }

        // ── 열린 공간 ───────────────────────────────────────────────────────────

        [Test]
        public void OpenField_ReachesFarLookaheadTarget()
        {
            var grid = new int2(9, 7);
            var walk = OpenField(grid);
            var flow = BuildFlow(walk, grid, new int2(0, 0));
            var nav = Nav(walk, grid);

            var from = new float3(8f, 0f, 6f);
            bool ok = PathSmoothing.TryFurthestVisible(
                from, nav, flow, R, PathSmoothing.DefaultLookahead, out var target);

            Assert.IsTrue(ok, "열린 공간이면 후보가 있어야 한다");
            // 8단계 양자화를 넘어선 지점이어야 의미가 있다 — 한 칸 앞이면 평활화가 아니다.
            float d = math.distance(new float2(from.x, from.z), new float2(target.x, target.z));
            Assert.Greater(d, 2f, "가시선이 뚫렸으면 멀리 잡아야 한다");
        }

        [Test]
        public void OpenField_TargetIsNotAxisAligned_ForNonFortyFiveSlope()
        {
            // 이 spec 의 검증 질문. 45°가 아닌 기울기에서 조준점이 축 정렬이면
            // 여전히 양자화된 방향을 따라가고 있는 것이다.
            var grid = new int2(9, 7);
            var walk = OpenField(grid);
            var flow = BuildFlow(walk, grid, new int2(0, 0));
            var nav = Nav(walk, grid);

            var from = new float3(8f, 0f, 3f);   // 기울기 8:3 — 45° 아님
            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                from, nav, flow, R, PathSmoothing.DefaultLookahead, out var target));

            float dx = math.abs(target.x - from.x);
            float dz = math.abs(target.z - from.z);
            Assert.Greater(dx, 0.5f, "x 성분 있음");
            Assert.Greater(dz, 0.5f, "z 성분 있음");
        }

        // ── 가시선 ──────────────────────────────────────────────────────────────

        [Test]
        public void IsVisible_True_AcrossOpenGround()
        {
            var grid = new int2(6, 6);
            var walk = OpenField(grid);
            var nav = Nav(walk, grid);

            Assert.IsTrue(PathSmoothing.IsVisible(
                new float3(0f, 0f, 0f), new float3(4f, 0f, 3f), R, nav));
        }

        [Test]
        public void IsVisible_False_ThroughWall()
        {
            var grid = new int2(6, 6);
            var walk = OpenField(grid);
            for (int y = 0; y < 6; y++) walk[y * 6 + 3] = 0;   // x=3 벽 열
            var nav = Nav(walk, grid);

            Assert.IsFalse(PathSmoothing.IsVisible(
                new float3(1f, 0f, 1f), new float3(5f, 0f, 1f), R, nav));
        }

        [Test]
        public void IsVisible_False_WhenBodyClipsWall_EvenIfSegmentIsClear()
        {
            // 이 unit 의 핵심 계약: 가시선은 **선분이 아니라 원**으로 판정한다.
            // 선분만 보면 뚫린 경로라도 몸통이 벽에 걸리면 막힌 것으로 봐야 한다.
            // true 를 주면 AgentCollision 이 매 프레임 막아 제자리 진동이 난다.
            var grid = new int2(6, 6);
            var walk = OpenField(grid);
            for (int y = 0; y < 6; y++) walk[y * 6 + 3] = 0;
            walk[2 * 6 + 3] = 1;                               // (3,2) 한 칸만 열린 틈
            var nav = Nav(walk, grid);

            // z=1.7 은 셀 (3,2) 안이라 **선분 자체는 뚫려 있다**.
            // 하지만 r=0.45 면 원이 z∈[1.25, 2.15] 를 덮어 벽 (3,1)(z∈[0.5,1.5])에 걸린다.
            Assert.IsFalse(PathSmoothing.IsVisible(
                new float3(1f, 0f, 1.7f), new float3(5f, 0f, 1.7f), 0.45f, nav),
                "선분은 뚫려도 몸통이 걸리면 막힌 것이다");

            // 같은 경로를 틈 정중앙으로 지나면 통과한다(지름 0.9 < 타일 1.0).
            Assert.IsTrue(PathSmoothing.IsVisible(
                new float3(1f, 0f, 2f), new float3(5f, 0f, 2f), 0.45f, nav),
                "정중앙이면 지나갈 수 있다");
        }

        [Test]
        public void IsVisible_True_ForSamePoint()
        {
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            var nav = Nav(walk, grid);
            Assert.IsTrue(PathSmoothing.IsVisible(
                new float3(1f, 0f, 1f), new float3(1f, 0f, 1f), R, nav));
        }

        // ── 종료 조건 ───────────────────────────────────────────────────────────

        [Test]
        public void IsolatedCell_ReturnsFalse()
        {
            var grid = new int2(4, 4);
            var walk = new NativeArray<byte>(16, Allocator.Temp);
            walk[0] = 1;                       // 골만 walkable, 나머지 벽
            var flow = BuildFlow(walk, grid, new int2(0, 0));
            var nav = Nav(walk, grid);

            Assert.IsFalse(PathSmoothing.TryFurthestVisible(
                new float3(3f, 0f, 3f), nav, flow, R, 8, out _),
                "고립 셀에선 후보가 없다 — 호출자는 기존 flow 를 쓴다");
        }

        // ── 교착 방지 (2026-08-08 사용자 제보 회귀) ─────────────────────────────

        [Test]
        public void OffCenterAgent_GetsLateralAim_WhenBodyDoesNotFitFieldDirection()
        {
            // 사고 재현: 장애물 모서리 옆 셀에서 필드는 "위로"라고 하는데, 유닛이 셀 안에서
            // 장애물 쪽으로 치우쳐 있어 몸이 이웃 열을 침범한다. 방향 벡터에 비켜설 성분이
            // 없으면 영구 교착이 나고, 뒤에서 다른 적이 밀어야 빠져나온다.
            //
            //   y=2  . # .      (1,2) 벽
            //   y=1  . # .      (1,1) 벽
            //   y=0  . . .
            // 셀 (0,1) 의 유닛이 오른쪽으로 치우쳐 있으면 위로 못 간다.
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            walk[1 * 4 + 1] = 0;
            walk[2 * 4 + 1] = 0;
            var flow = BuildFlow(walk, grid, new int2(0, 3));   // 골 = 좌상단
            var nav = Nav(walk, grid);

            var from = new float3(0.45f, 0f, 0.5f);   // 셀 (0,1) 안에서 벽 쪽으로 치우침

            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                from, nav, flow, R, PathSmoothing.DefaultLookahead, out var target),
                "조준점이 없으면 호출자가 필드 방향만 쓰게 되어 교착한다");

            Assert.Less(target.x, from.x,
                "조준점에 **왼쪽으로 비켜설 성분**이 있어야 몸이 통로에 들어간다");
        }

        [Test]
        public void FirstCandidate_IsAcceptedEvenWhenNotVisible()
        {
            // 위 성질의 근거를 직접 못박는다: 첫 후보(= 바로 다음 셀 중심)는 가시성과
            // 무관하게 채택한다. 그 자리는 정의상 몸이 들어가는 위치이기 때문이다.
            // 되돌리면 치우친 유닛이 다시 갇힌다.
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            walk[1 * 4 + 1] = 0;
            walk[2 * 4 + 1] = 0;
            var flow = BuildFlow(walk, grid, new int2(0, 3));
            var nav = Nav(walk, grid);

            var from = new float3(0.45f, 0f, 0.5f);
            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                from, nav, flow, R, PathSmoothing.DefaultLookahead, out var target));

            // 채택된 첫 후보는 셀 중심이므로 좌표가 정수여야 한다.
            Assert.AreEqual(math.round(target.x), target.x, 1e-4f, "셀 중심 조준");
            Assert.AreEqual(math.round(target.z), target.z, 1e-4f, "셀 중심 조준");
        }

        [Test]
        public void UncreatedFlow_ReturnsFalse()
        {
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            var nav = Nav(walk, grid);
            Assert.IsFalse(PathSmoothing.TryFurthestVisible(
                new float3(1f, 0f, 1f), nav, default, R, 8, out _));
        }

        [Test]
        public void Corridor_TargetStaysInsideCorridor()
        {
            // 1타일 복도에서는 직선화할 여지가 없다. 조준점이 복도를 벗어나면 안 된다.
            var grid = new int2(6, 3);
            var walk = new NativeArray<byte>(18, Allocator.Temp);
            for (int x = 0; x < 6; x++) walk[1 * 6 + x] = 1;
            var flow = BuildFlow(walk, grid, new int2(0, 1));
            var nav = Nav(walk, grid);

            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                new float3(5f, 0f, 1f), nav, flow, R, 8, out var target));
            Assert.AreEqual(1f, target.z, 1e-3f, "복도(z=1) 밖으로 나가지 않는다");
        }
    }
}
