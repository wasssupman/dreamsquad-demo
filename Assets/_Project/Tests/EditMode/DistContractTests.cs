using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 6 — dist/flow 소비처가 기대는 계약을 못박는다.
    //
    // unit 4 가 dist 의 단위를 ×10 스케일로 바꿨고 flow 를 8방향으로 넓혔다. 이동 밖에서
    // 이 둘을 읽는 4곳(FrontmostTargeting · BlinkMath · 경로 추적 · 넉백 방향)이 절대값이
    // 아니라 **상대 비교와 센티넬**에만 의존한다는 것이 안전의 근거다. 그 근거를 테스트로 둔다.
    public class DistContractTests
    {
        private const int W = 5, H = 3;
        private static readonly int2 Grid = new int2(W, H);
        private static readonly int2 Goal = new int2(0, 1);

        private static void BuildCorridor(out NativeArray<int> dist, out NativeArray<float2> flow)
        {
            var walk = new NativeArray<byte>(W * H, Allocator.Temp);
            for (int x = 0; x < W; x++) walk[1 * W + x] = 1;
            flow = new NativeArray<float2>(W * H, Allocator.Temp);
            dist = new NativeArray<int>(W * H, Allocator.Temp);
            FlowFieldBuilder.Build(walk, Grid, Goal, flow, dist);
            walk.Dispose();
        }

        [Test]
        public void GoalCell_DistIsZero()
        {
            // BattleBridge 의 경로 추적이 `dist == 0` 으로 골 도달을 판정한다(:1807).
            // 스케일이 바뀌어도 0 은 0 이어야 한다.
            BuildCorridor(out var dist, out var flow);
            Assert.AreEqual(0, dist[GridMath.CellIndex(Goal, Grid)]);
        }

        [Test]
        public void UnreachableCell_KeepsMaxValueSentinel()
        {
            // BlinkMath.TryFindLandingCell 이 이 센티넬만 본다. 스케일 무관해야 한다.
            BuildCorridor(out var dist, out var flow);
            Assert.AreEqual(int.MaxValue, dist[GridMath.CellIndex(new int2(2, 0), Grid)],
                "통로 밖(비-Walk)은 도달 불가 센티넬");
            Assert.AreEqual(FrontmostTargeting.UnreachableDist, int.MaxValue,
                "FrontmostTargeting 의 센티넬이 필드와 같은 값이어야 한다");
        }

        [Test]
        public void Dist_IncreasesMonotonically_AwayFromGoal()
        {
            // FrontmostTargeting 은 절대값이 아니라 순서만 쓴다. 스케일이 바뀌어도
            // "골에 가까울수록 작다" 가 유지되면 그 소비는 안전하다.
            BuildCorridor(out var dist, out var flow);
            int prev = -1;
            for (int x = 0; x < W; x++)
            {
                int d = dist[GridMath.CellIndex(new int2(x, 1), Grid)];
                Assert.Greater(d, prev, $"x={x} 에서 단조 증가가 깨졌다");
                prev = d;
            }
        }

        [Test]
        public void Frontmost_PicksSmallerDist_RegardlessOfScale()
        {
            // 같은 순서를 스케일 1배와 10배에서 각각 확인 — 순위가 스케일에 불변임을 보인다.
            for (int scale = 1; scale <= 10; scale += 9)
            {
                var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
                cands[0] = new FrontmostTargeting.Candidate
                    { flowDist = 3 * scale, sqDist = 1f, simId = 1 };
                cands[1] = new FrontmostTargeting.Candidate
                    { flowDist = 1 * scale, sqDist = 9f, simId = 2 };

                Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2),
                    $"scale {scale}: dist 가 작은 쪽이 앞선다");
                cands.Dispose();
            }
        }

        [Test]
        public void Frontmost_ExcludesUnreachable()
        {
            var cands = new NativeArray<FrontmostTargeting.Candidate>(2, Allocator.Temp);
            cands[0] = new FrontmostTargeting.Candidate
                { flowDist = FrontmostTargeting.UnreachableDist, sqDist = 0f, simId = 1 };
            cands[1] = new FrontmostTargeting.Candidate
                { flowDist = 50, sqDist = 9f, simId = 2 };

            Assert.AreEqual(1, FrontmostTargeting.SelectFrontmost(cands, 2),
                "도달 불가 후보는 제외된다");
            cands.Dispose();
        }

        [Test]
        public void Flow_IsUnitLength_IncludingDiagonals()
        {
            // AttackSystem 의 넉백 방향이 flow 를 normalizesafe 한다. 필드가 이미 단위
            // 벡터라는 계약이 유지되면 대각이 들어와도 그 소비는 안전하다.
            int n = 9 * 7;
            var walk = new NativeArray<byte>(n, Allocator.Temp);
            for (int i = 0; i < n; i++) walk[i] = 1;
            var flow = new NativeArray<float2>(n, Allocator.Temp);
            var dist = new NativeArray<int>(n, Allocator.Temp);
            FlowFieldBuilder.Build(walk, new int2(9, 7), new int2(0, 0), flow, dist);

            bool sawDiagonal = false;
            for (int i = 0; i < n; i++)
            {
                float len2 = math.lengthsq(flow[i]);
                if (len2 < 1e-6f) continue;             // 골/고립 = zero-flow
                Assert.AreEqual(1f, len2, 1e-4f, $"index {i} 가 단위 벡터가 아니다");
                if (math.abs(flow[i].x) > 1e-4f && math.abs(flow[i].y) > 1e-4f) sawDiagonal = true;
            }
            Assert.IsTrue(sawDiagonal, "열린 격자인데 대각 flow 가 하나도 없다");

            walk.Dispose(); flow.Dispose(); dist.Dispose();
        }
    }
}
