// 적응: Assets/_Project/Tests/EditMode/Battle/PathSmoothingTests.cs (battle-core-rebuild unit 2)
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class PathSmoothingTests
    {
        private const float R = 0.25f;

        private static float2[] FlowToward(int w, int h, int2 goal, byte[] walk)
        {
            var flow = new float2[w * h];
            var dist = new int[w * h];
            FlowFieldBuilder.Build(walk, new int2(w, h), goal, flow, dist, new CellQueue());
            return flow;
        }

        [Test]
        public void 열린_격자에서_먼_가시점으로_직행한다()
        {
            var walk = new byte[10 * 3];
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            var nav = CoreMapFixtures.NavOf(walk, 10, 3);
            var flow = FlowToward(10, 3, new int2(9, 1), walk);

            Assert.IsTrue(PathSmoothing.TryStepTarget(new float3(0f, 0f, 1f), in nav, flow, R,
                                                      PathSmoothing.DefaultLookahead, out float3 target));
            Assert.Greater(target.x, 4f, "한 칸이 아니라 멀리 본다");
        }

        [Test]
        public void 첫_후보는_가시성과_무관하게_채택된다()
        {
            // 필드는 칸 중심 기준 방향을 준다. 유닛이 칸 안에서 치우쳐 있으면 그 방향으로
            // 몸이 안 들어갈 수 있는데, 방향 벡터에는 비켜설 성분이 없어 영구 교착이 난다.
            // 다음 칸 중심은 정의상 몸이 들어가는 자리다 — 되돌리지 말 것.
            var walk = new byte[5 * 5];
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            walk[2 * 5 + 2] = 0;                       // 바로 옆에 벽
            var nav = CoreMapFixtures.NavOf(walk, 5, 5);
            var flow = FlowToward(5, 5, new int2(4, 1), walk);

            Assert.IsTrue(PathSmoothing.TryStepTarget(new float3(1.4f, 0f, 1.4f), in nav, flow, R,
                                                      PathSmoothing.DefaultLookahead, out _),
                "벽 옆에서도 스텝이 나와야 한다(교착 금지)");
        }

        [Test]
        public void 선분은_뚫려도_몸통이_걸리면_막힘이다()
        {
            // 반지름을 함께 보지 않으면 몸통이 걸리는 통로로 직행해 매 틱 제자리 진동이 난다.
            var walk = new byte[5 * 3];
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            walk[0 * 5 + 2] = 0;   // (2,0)
            walk[2 * 5 + 2] = 0;   // (2,2) — (2,1) 만 열린 틈
            var nav = CoreMapFixtures.NavOf(walk, 5, 3);

            var a = new float3(0f, 0f, 1f);
            var b = new float3(4f, 0f, 1f);
            Assert.IsTrue(PathSmoothing.IsVisible(a, b, 0.2f, in nav), "작은 몸은 지난다");
            Assert.IsFalse(PathSmoothing.IsVisible(a, b, 0.6f, in nav), "큰 몸은 걸린다");
        }

        [Test]
        public void 목적지_칸에서는_스텝이_없다()
        {
            var walk = new byte[3 * 3];
            for (int i = 0; i < walk.Length; i++) walk[i] = 1;
            var nav = CoreMapFixtures.NavOf(walk, 3, 3);
            var flow = FlowToward(3, 3, new int2(1, 1), walk);

            Assert.IsFalse(PathSmoothing.TryStepTarget(new float3(1f, 0f, 1f), in nav, flow, R,
                                                       PathSmoothing.DefaultLookahead, out _));
        }
    }
}
