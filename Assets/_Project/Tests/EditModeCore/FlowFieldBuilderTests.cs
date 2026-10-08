// 적응: Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs (battle-core-rebuild unit 2)
// 기대값은 그대로다 — 비용 단위(직교 10 / 대각 14)도 코너컷 규칙도 안 바뀌었다.
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class FlowFieldBuilderTests
    {
        private static byte[] OpenMask(int n)
        {
            var m = new byte[n];
            for (int i = 0; i < n; i++) m[i] = 1;
            return m;
        }

        [Test]
        public void 직선에서_모든_칸이_목적지를_가리킨다()
        {
            var grid = new int2(5, 1);
            var walk = OpenMask(5);
            var flow = new float2[5];
            var dist = new int[5];

            FlowFieldBuilder.Build(walk, grid, new int2(4, 0), flow, dist, new CellQueue());

            Assert.AreEqual(0, dist[4], "목적지 칸의 거리는 0");
            Assert.AreEqual(4 * FlowFieldBuilder.CostOrtho, dist[0]);
            Assert.AreEqual(new float2(1, 0), flow[0]);
            Assert.AreEqual(new float2(0, 0), flow[4], "목적지의 방향은 zero(정지 신호)");
        }

        [Test]
        public void 장애물이_있으면_돌아간다()
        {
            //  . . G
            //  . X .
            //  S . .
            var grid = new int2(3, 3);
            var walk = OpenMask(9);
            walk[1 * 3 + 1] = 0;
            var flow = new float2[9];
            var dist = new int[9];

            FlowFieldBuilder.Build(walk, grid, new int2(2, 2), flow, dist, new CellQueue());

            Assert.AreEqual(0, dist[2 * 3 + 2]);
            Assert.AreEqual(4 * FlowFieldBuilder.CostOrtho, dist[0],
                "중앙이 막히면 L 자 두 경로가 남고 길이는 같다");
            Assert.AreNotEqual(float2.zero, flow[0], "출발 칸에 방향이 있어야 한다");
        }

        [Test]
        public void 대각은_양쪽_직교가_열려_있을_때만_허용된다()
        {
            // 코너컷 방지 — 아니면 유닛이 벽 모서리를 관통한다(타일 정렬 벽이라 눈에 띈다).
            //  . X
            //  S .        (1,1) 로 가려면 (1,0) 또는 (0,1) 중 하나는 열려야 한다
            var grid = new int2(2, 2);
            var walk = OpenMask(4);
            walk[1 * 2 + 0] = 0;   // (0,1) 막힘
            walk[0 * 2 + 1] = 0;   // (1,0) 막힘
            var flow = new float2[4];
            var dist = new int[4];

            FlowFieldBuilder.Build(walk, grid, new int2(1, 1), flow, dist, new CellQueue());

            Assert.AreEqual(int.MaxValue, dist[0], "대각만 남으면 갈 수 없다");
        }

        [Test]
        public void 대각_비용이_직교보다_비싸다()
        {
            var grid = new int2(3, 3);
            var walk = OpenMask(9);
            var flow = new float2[9];
            var dist = new int[9];

            FlowFieldBuilder.Build(walk, grid, new int2(2, 2), flow, dist, new CellQueue());

            Assert.AreEqual(2 * FlowFieldBuilder.CostDiag, dist[0], "(0,0)→(2,2) 는 대각 2회");
            Assert.AreEqual(FlowFieldBuilder.CostOrtho, dist[2 * 3 + 1], "(1,2)→(2,2) 는 직교 1회");
        }

        [Test]
        public void 소스가_없으면_전_칸이_도달_불가다()
        {
            var grid = new int2(3, 3);
            var walk = OpenMask(9);
            var flow = new float2[9];
            var dist = new int[9];

            // 경계 밖 소스 하나 — 유효 소스 0 과 같은 결과여야 한다.
            FlowFieldBuilder.BuildFromSources(walk, grid, new[] { new int2(9, 9) }, 1,
                                              flow, dist, new CellQueue());

            for (int i = 0; i < 9; i++) Assert.AreEqual(int.MaxValue, dist[i]);
        }

        [Test]
        public void 다중_소스는_최근접_소스를_향한다()
        {
            var grid = new int2(5, 1);
            var walk = OpenMask(5);
            var flow = new float2[5];
            var dist = new int[5];

            FlowFieldBuilder.BuildFromSources(walk, grid, new[] { new int2(0, 0), new int2(4, 0) }, 2,
                                              flow, dist, new CellQueue());

            Assert.AreEqual(FlowFieldBuilder.CostOrtho, dist[1]);
            Assert.AreEqual(new float2(-1, 0), flow[1], "왼쪽 소스가 가깝다");
            Assert.AreEqual(new float2(1, 0), flow[3], "오른쪽 소스가 가깝다");
        }

        [Test]
        public void 소스_수집은_자기_칸을_빼고_디스크를_덮는다()
        {
            var grid = new int2(5, 5);
            var walk = OpenMask(25);
            var outSources = new int2[FlowFieldBuilder.DiscArea(1)];

            int n = FlowFieldBuilder.CollectDefenderSources(
                walk, grid, new[] { new int2(2, 2) }, 1, 1, outSources);

            Assert.AreEqual(8, n, "체비셰프 1 디스크에서 자기 칸을 뺀 8칸");
            for (int i = 0; i < n; i++)
                Assert.AreNotEqual(new int2(2, 2), outSources[i]);
        }

        [Test]
        public void 같은_입력이면_같은_필드다()
        {
            var grid = new int2(6, 6);
            var walk = OpenMask(36);
            walk[2 * 6 + 3] = 0;
            var q = new CellQueue();

            var flowA = new float2[36]; var distA = new int[36];
            var flowB = new float2[36]; var distB = new int[36];
            FlowFieldBuilder.Build(walk, grid, new int2(5, 5), flowA, distA, q);
            FlowFieldBuilder.Build(walk, grid, new int2(5, 5), flowB, distB, q);

            for (int i = 0; i < 36; i++)
            {
                Assert.AreEqual(distA[i], distB[i]);
                Assert.AreEqual(flowA[i], flowB[i]);
            }
        }
    }
}
