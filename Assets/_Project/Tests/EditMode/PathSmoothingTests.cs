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
        // 아래 IsVisible_* 는 프로덕션이 부르지 않는 술어를 검증한다(평활화는 차단 셀까지
        // 필요해 내부 경로를 쓴다). 그래도 여기서 직접 부르는 이유는 이 술어가 곧 가시선
        // 계약이라, TryFurthestVisible 을 통해 간접 검증하면 실패 원인이 흐려지기 때문이다.

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
        public void EscapeAim_IsCornerVertex_WhenSecondCandidateBlocked()
        {
            // unit 10 계약 진화 — 구 버전은 "탈출 조준 = 첫 후보 셀 중심(정수 좌표)" 을
            // 박았지만, 이제 둘째 후보가 막히면 **막은 벽의 코너 오프셋 꼭짓점**(비정수,
            // 월드 고정점)이 조준된다. 탈출의 본질(측면 성분)은 위 OffCenterAgent 케이스가
            // 계속 지키고, 여기서는 꼭짓점의 정확한 좌표를 못박는다.
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            walk[1 * 4 + 1] = 0;   // (1,1) 벽
            walk[2 * 4 + 1] = 0;   // (1,2) 벽
            var flow = BuildFlow(walk, grid, new int2(0, 3));
            var nav = Nav(walk, grid);

            var from = new float3(0.45f, 0f, 0.5f);
            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                from, nav, flow, R, PathSmoothing.DefaultLookahead, out var target));

            // apex 규칙: 차단 셀 (1,1) 의 4꼭짓점 중 **마지막 가시점(셀 (0,2) 중심)에
            // 최근접**한 (0.5, 1.5) 에서 벽 중심 반대 방향 (-,+) 으로 (R+skin) 오프셋.
            // 1차 키가 "에이전트 최근접"이면 이미 지난 코너를 뒤로 조준해 동결된다(실측).
            float off = R + AgentCollision.Skin;
            Assert.AreEqual(0.5f - off, target.x, 1e-3f, "apex x 오프셋");
            Assert.AreEqual(1.5f + off, target.z, 1e-3f, "apex z 오프셋");
        }

        [Test]
        public void CornerAim_IsObserverIndependent()
        {
            // unit 10 의 존재 이유 — 조준점이 (차단 셀, 꼭짓점) 쌍에서만 파생되는 월드
            // 고정점이라, 유닛이 코너 근처 어디에 있든 같은 점이 나온다. 이 성질이 깨지면
            // 매 프레임 재계산이 다시 왕복(덜컹임)을 만든다.
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            walk[1 * 4 + 1] = 0;
            walk[2 * 4 + 1] = 0;
            var flow = BuildFlow(walk, grid, new int2(0, 3));
            var nav = Nav(walk, grid);

            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                new float3(0.45f, 0f, 0.50f), nav, flow, R, PathSmoothing.DefaultLookahead, out var a));
            Assert.IsTrue(PathSmoothing.TryFurthestVisible(
                new float3(0.42f, 0f, 0.62f), nav, flow, R, PathSmoothing.DefaultLookahead, out var b));

            Assert.AreEqual(a.x, b.x, 1e-4f, "관찰자 위치가 달라도 같은 코너면 같은 조준점");
            Assert.AreEqual(a.z, b.z, 1e-4f);
        }

        [Test]
        public void CornerAim_InvalidOffset_ReturnsFalse()
        {
            // 좁은 대각 틈 — 오프셋 점에 반지름 원이 안 들어가면 채택하지 않는다
            // (호출자는 마지막 가시 셀 중심 폴백). 여기서 true 를 주면 유닛이 벽에 겹치는
            // 자리를 조준해 AgentCollision 과 매 프레임 싸운다.
            var grid = new int2(4, 4);
            var walk = OpenField(grid);
            walk[1 * 4 + 1] = 0;   // (1,1) — 차단 셀
            walk[2 * 4 + 0] = 0;   // (0,2) — 오프셋 점 (0.149, 1.851) 이 이 셀에 겹친다
            var nav = Nav(walk, grid);

            Assert.IsFalse(PathSmoothing.TryCornerAim(
                new int2(1, 1), new float3(0f, 0f, 2f), new float3(0.45f, 0f, 0.5f), R, nav, out _));
        }


        [Test]
        public void CornerAim_Tie_ResolvesToAgentSide()
        {
            // 아레나 입구 164프레임 정체의 최소 재현(2026-08-09). 두 코너가 꺾임점에서
            // 등거리(동률)일 때 벽 건너편으로 풀리면, 조준 방향이 막힌 축 위주가 되어
            // 충돌 해결 후 0.1배속 크리프가 남는다. 동률은 에이전트 쪽으로 풀려야 한다.
            //
            //   y=1  . . .        blocker (1,0). lastVisible = (1,1) 중심 →
            //   y=0  . # .        코너 (0.5,0.5) 와 (1.5,0.5) 가 등거리 동률.
            var grid = new int2(3, 2);
            var walk = OpenField(grid);
            walk[0 * 3 + 1] = 0;
            var nav = Nav(walk, grid);

            float off = R + AgentCollision.Skin;

            // 에이전트가 오른쪽에 있으면 → 오른쪽 코너 (1.5,0.5)
            Assert.IsTrue(PathSmoothing.TryCornerAim(
                new int2(1, 0), new float3(1f, 0f, 1f), new float3(1.8f, 0f, 0.2f), R, nav, out var right));
            Assert.AreEqual(1.5f + off, right.x, 1e-3f, "동률은 에이전트 쪽으로");

            // 에이전트가 왼쪽에 있으면 → 왼쪽 코너 (0.5,0.5)
            Assert.IsTrue(PathSmoothing.TryCornerAim(
                new int2(1, 0), new float3(1f, 0f, 1f), new float3(0.2f, 0f, 0.2f), R, nav, out var left));
            Assert.AreEqual(0.5f - off, left.x, 1e-3f, "동률은 에이전트 쪽으로");
        }

        [Test]
        public void TryStepTarget_FallsBackToFieldStep_AndStopsAtGoal()
        {
            // 이동·예고 라인이 공유하는 선택 규칙: 평활화 → 필드 스텝 → 골/고립 false.
            var grid = new int2(6, 3);
            var walk = new NativeArray<byte>(18, Allocator.Temp);
            for (int x = 0; x < 6; x++) walk[1 * 6 + x] = 1;
            var flow = BuildFlow(walk, grid, new int2(0, 1));
            var nav = Nav(walk, grid);

            Assert.IsTrue(PathSmoothing.TryStepTarget(
                new float3(5f, 0f, 1f), nav, flow, R, PathSmoothing.DefaultLookahead, out var t),
                "복도 안 — 목표점이 있어야 한다");
            Assert.AreEqual(1f, t.z, 1e-3f, "복도 밖으로 나가지 않는다");

            Assert.IsFalse(PathSmoothing.TryStepTarget(
                new float3(0f, 0f, 1f), nav, flow, R, PathSmoothing.DefaultLookahead, out _),
                "골 셀 위 — 더 갈 곳이 없다");
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
