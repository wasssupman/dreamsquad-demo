using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 1 — 벽 질의 단일 진입점의 직접 커버리지.
    //
    // 이 파일이 존재하는 이유는 unit 2 다. unit 2 는 술어를 zero-flow → 정적 마스크로
    // 뒤집는데, 그때 마스크 경로가 프로덕션의 **유일한** 경로가 된다. 지금 그 경로는
    // 어떤 테스트도 실행하지 않으므로(ecs-review T2), 전환 시 회귀가 조용히 숨는다.
    // 두 경로를 지금 각각 못박아 두면 unit 2 의 diff 가 안전망 위에서 움직인다.
    public class NavGridTests
    {
        // 3x1: (0,0) flow=(1,0) / (1,0) flow=(1,0) / (2,0) = 골(zero flow)
        private static NavGrid FlowOnly(NativeArray<float2> flow)
            => new NavGrid(
                staticWalk:   default,
                blockedCells: default,
                hasObstacles: false,
                gridSize:     new int2(3, 1),
                tileSize:     1f,
                origin:       float3.zero,
                flow:         flow,
                goals:        default,
                goalCell:     new int2(2, 0));

        private static NativeArray<float2> MakeFlow()
        {
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            flow[0] = new float2(1, 0);
            flow[1] = new float2(1, 0);
            flow[2] = float2.zero;   // goal
            return flow;
        }

        private static NavGrid MaskOnly(NativeArray<byte> mask, NativeHashSet<int2> blocked = default, bool hasObstacles = false)
            => new NavGrid(
                staticWalk:   mask,
                blockedCells: blocked,
                hasObstacles: hasObstacles,
                gridSize:     new int2(3, 1),
                tileSize:     1f,
                origin:       float3.zero);

        // ── 경계 ────────────────────────────────────────────────────────────────

        [Test]
        public void IsBlocked_True_Outside_Grid()
        {
            var nav = FlowOnly(MakeFlow());
            Assert.IsTrue(nav.IsBlocked(new int2(-1, 0)), "왼쪽 밖");
            Assert.IsTrue(nav.IsBlocked(new int2(3, 0)),  "오른쪽 밖");
            Assert.IsTrue(nav.IsBlocked(new int2(0, 1)),  "위쪽 밖");
        }

        // ── flow 경로 (unit 1 의 현행 술어) ─────────────────────────────────────

        [Test]
        public void FlowPath_ZeroFlow_NonGoal_IsBlocked()
        {
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            flow[0] = float2.zero;        // 고립 — 도달 불가
            flow[1] = new float2(1, 0);
            flow[2] = float2.zero;        // 골
            var nav = FlowOnly(flow);
            Assert.IsTrue(nav.IsBlocked(new int2(0, 0)), "zero-flow 비골 = 벽");
        }

        [Test]
        public void FlowPath_GoalCell_NotBlocked_DespiteZeroFlow()
        {
            var nav = FlowOnly(MakeFlow());
            Assert.IsFalse(nav.IsBlocked(new int2(2, 0)), "골은 flow=0 이어도 통행 가능");
        }

        [Test]
        public void FlowPath_GoalsArray_TakesPrecedenceOverGoalCell()
        {
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            flow[0] = float2.zero;   // goals 에 포함 → 통행 가능
            flow[1] = new float2(1, 0);
            flow[2] = float2.zero;   // goalCell 이지만 goals 에 없음 → 벽
            var goals = new NativeArray<int2>(1, Allocator.Temp);
            goals[0] = new int2(0, 0);

            var nav = new NavGrid(
                staticWalk: default, blockedCells: default, hasObstacles: false,
                gridSize: new int2(3, 1), tileSize: 1f, origin: float3.zero,
                flow: flow, goals: goals, goalCell: new int2(2, 0));

            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)), "goals 멤버십이 골 판정");
            Assert.IsTrue(nav.IsBlocked(new int2(2, 0)),  "goals 있으면 goalCell 폴백은 안 쓴다");
        }

        // ── walkMask 경로 (unit 2 이후 프로덕션의 유일 경로) ────────────────────

        [Test]
        public void MaskPath_UsesStaticWalk_WhenFlowAbsent()
        {
            var mask = new NativeArray<byte>(3, Allocator.Temp);
            mask[0] = 1; mask[1] = 0; mask[2] = 1;
            var nav = MaskOnly(mask);

            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)), "walkable");
            Assert.IsTrue(nav.IsBlocked(new int2(1, 0)),  "비-Walk 타일");
            Assert.IsFalse(nav.IsBlocked(new int2(2, 0)), "walkable");
        }

        [Test]
        public void MaskPath_IsolatedWalkCell_IsNotBlocked()
        {
            // unit 2 가 만들 의미 변화를 여기서 못박는다: 골에서 도달 불가한 Walk 셀은
            // flow 술어에선 벽이지만(위 FlowPath_ZeroFlow_NonGoal_IsBlocked), 마스크
            // 술어에선 통행 가능이다. D1-b(봉쇄 시 차단 구역 전체가 벽이 되는 사고)를
            // 막는 것이 정확히 이 차이다.
            var mask = new NativeArray<byte>(3, Allocator.Temp);
            mask[0] = 1; mask[1] = 1; mask[2] = 1;
            var nav = MaskOnly(mask);
            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)));
        }

        // ── 동적 장애물 오버레이 ────────────────────────────────────────────────

        [Test]
        public void Obstacles_BlockWalkableCell()
        {
            var mask = new NativeArray<byte>(3, Allocator.Temp);
            mask[0] = 1; mask[1] = 1; mask[2] = 1;
            var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(1, 0));

            var nav = MaskOnly(mask, blocked, hasObstacles: true);
            Assert.IsFalse(nav.IsBlocked(new int2(0, 0)));
            Assert.IsTrue(nav.IsBlocked(new int2(1, 0)), "장애물이 walkable 셀을 막는다");
        }

        [Test]
        public void Obstacles_Ignored_When_HasObstacles_False()
        {
            var mask = new NativeArray<byte>(3, Allocator.Temp);
            mask[0] = 1; mask[1] = 1; mask[2] = 1;
            var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(1, 0));

            var nav = MaskOnly(mask, blocked, hasObstacles: false);
            Assert.IsFalse(nav.IsBlocked(new int2(1, 0)), "플래그가 false 면 집합을 보지 않는다");
        }

        // ── 술어 우선순위 (unit 1 의 동작 불변이 걸린 계약) ─────────────────────

        [Test]
        public void Flow_Wins_Over_Mask_When_Both_Present()
        {
            // unit 1 은 술어를 바꾸지 않는다. 마스크가 있어도 flow 가 있으면 flow 가 이긴다.
            // 이 계약이 깨지면 고립 Walk 셀 판정이 조용히 뒤집힌다.
            // ⚠ unit 2 는 이 우선순위를 **의도적으로 뒤집는다** — 그때 이 테스트를 반전시킨다.
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            flow[0] = float2.zero;   // flow 로는 벽
            flow[1] = new float2(1, 0);
            flow[2] = float2.zero;
            var mask = new NativeArray<byte>(3, Allocator.Temp);
            mask[0] = 1; mask[1] = 1; mask[2] = 1;   // 마스크로는 통행 가능

            var nav = new NavGrid(
                staticWalk: mask, blockedCells: default, hasObstacles: false,
                gridSize: new int2(3, 1), tileSize: 1f, origin: float3.zero,
                flow: flow, goals: default, goalCell: new int2(2, 0));

            Assert.IsTrue(nav.IsBlocked(new int2(0, 0)), "flow 우선 — unit 1 동작 불변 계약");
        }

        // ── MaterializeWalkMask ────────────────────────────────────────────────

        [Test]
        public void MaterializeWalkMask_FlowPath_MatchesIsBlocked()
        {
            var nav = FlowOnly(MakeFlow());
            var outMask = new NativeArray<byte>(3, Allocator.Temp);
            nav.MaterializeWalkMask(outMask);

            Assert.AreEqual(1, outMask[0]);
            Assert.AreEqual(1, outMask[1]);
            Assert.AreEqual(1, outMask[2], "골은 flow=0 이어도 walkable 로 나와야 한다");
        }

        [Test]
        public void MaterializeWalkMask_MaskPath_WithObstacles()
        {
            var mask = new NativeArray<byte>(3, Allocator.Temp);
            mask[0] = 1; mask[1] = 1; mask[2] = 0;
            var blocked = new NativeHashSet<int2>(4, Allocator.Temp);
            blocked.Add(new int2(0, 0));

            var nav = MaskOnly(mask, blocked, hasObstacles: true);
            var outMask = new NativeArray<byte>(3, Allocator.Temp);
            nav.MaterializeWalkMask(outMask);

            Assert.AreEqual(0, outMask[0], "장애물");
            Assert.AreEqual(1, outMask[1], "통행 가능");
            Assert.AreEqual(0, outMask[2], "비-Walk 타일");
        }
    }
}
