// 적응: Assets/_Project/Tests/EditMode/Battle/PatrolAreaMathTests.cs (battle-core-rebuild unit 2)
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;
using Somnia.Battle.BattleCore.Move;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class PatrolAreaMathTests
    {
        private const int W = 7, H = 7;

        private static byte[] OpenMask()
        {
            var m = new byte[W * H];
            for (int i = 0; i < m.Length; i++) m[i] = 1;
            return m;
        }

        [Test]
        public void 구역_안팎을_박스로_가른다()
        {
            Assert.IsTrue(PatrolAreaMath.IsInArea(new int2(3, 4), new int2(3, 3), 1));
            Assert.IsFalse(PatrolAreaMath.IsInArea(new int2(3, 5), new int2(3, 3), 1));
        }

        [Test]
        public void 구역_마스크는_스스로_0_으로_시작한다()
        {
            // M11 — 호출자 계약으로 뒀더니 순찰병 **2기 이상에서만** 재현되는
            // 「거점을 벗어나 걸어나감」이 났다. 말로 된 계약 대신 함수가 보장한다.
            var full = OpenMask();
            var outMask = new byte[W * H];
            for (int i = 0; i < outMask.Length; i++) outMask[i] = 1;   // 앞 유닛의 잔재

            PatrolAreaMath.FillAreaMask(full, new int2(W, H), new int2(1, 1), 1, outMask);

            Assert.AreEqual(0, outMask[GridMath.CellIndex(new int2(5, 5), new int2(W, H))],
                "박스 밖은 반드시 0 이어야 한다");
            Assert.AreEqual(1, outMask[GridMath.CellIndex(new int2(1, 1), new int2(W, H))]);
        }

        [Test]
        public void 구역_안_적을_향해_간다()
        {
            var full = OpenMask();
            var area = new byte[W * H];
            var scratch = new PatrolScratch(W * H);
            PatrolAreaMath.FillAreaMask(full, new int2(W, H), new int2(3, 3), 2, area);

            var enemies = new[] { new int2(5, 3) };
            var positions = new[] { new float3(5f, 0f, 3f) };
            var radii = new[] { 0.25f };

            var dir = PatrolAreaMath.StepDir(area, full, new int2(W, H),
                                             anchorCell: new int2(3, 3), homeCell: new int2(3, 2),
                                             tileRadius: 2, selfCell: new int2(3, 3),
                                             selfPos: new float3(3f, 0f, 3f), selfBodyRadiusTiles: 0.25f,
                                             attackTileRange: 1, tileSize: 1f,
                                             enemyCells: enemies, enemyPositions: positions,
                                             enemyBodyRadii: radii, enemyCount: 1, scratch: scratch);

            Assert.AreEqual(new float2(1f, 0f), dir, "적 쪽(+x)으로 한 칸");
        }

        [Test]
        public void 구역_안에_적이_없으면_집으로_돌아간다()
        {
            var full = OpenMask();
            var area = new byte[W * H];
            var scratch = new PatrolScratch(W * H);
            PatrolAreaMath.FillAreaMask(full, new int2(W, H), new int2(3, 3), 1, area);

            var dir = PatrolAreaMath.StepDir(area, full, new int2(W, H),
                                             new int2(3, 3), new int2(3, 2), 1, new int2(3, 4),
                                             new float3(3f, 0f, 4f), 0.25f, 1, 1f,
                                             System.Array.Empty<int2>(), System.Array.Empty<float3>(),
                                             System.Array.Empty<float>(), 0, scratch);

            Assert.AreEqual(new float2(0f, -1f), dir, "집(3,2) 쪽으로 내려간다");
        }

        [Test]
        public void 집에_서_있으면_정지한다()
        {
            var full = OpenMask();
            var area = new byte[W * H];
            var scratch = new PatrolScratch(W * H);
            PatrolAreaMath.FillAreaMask(full, new int2(W, H), new int2(3, 3), 1, area);

            var dir = PatrolAreaMath.StepDir(area, full, new int2(W, H),
                                             new int2(3, 3), new int2(3, 2), 1, new int2(3, 2),
                                             new float3(3f, 0f, 2f), 0.25f, 1, 1f,
                                             System.Array.Empty<int2>(), System.Array.Empty<float3>(),
                                             System.Array.Empty<float>(), 0, scratch);
            Assert.AreEqual(float2.zero, dir);
        }

        [Test]
        public void 박스_밖으로_밀려나면_마스크_없는_필드로_복귀한다()
        {
            // 포탈·회오리·넉백은 진영을 안 보므로 순찰병을 박스 밖으로 민다. 구역 마스크로는
            // 박스 밖 칸의 거리가 무한이라 하강이 zero 가 되어 영구 정지한다.
            var full = OpenMask();
            var area = new byte[W * H];
            var scratch = new PatrolScratch(W * H);
            PatrolAreaMath.FillAreaMask(full, new int2(W, H), new int2(3, 3), 1, area);

            var dir = PatrolAreaMath.StepDir(area, full, new int2(W, H),
                                             new int2(3, 3), new int2(3, 3), 1, new int2(6, 3),
                                             new float3(6f, 0f, 3f), 0.25f, 1, 1f,
                                             System.Array.Empty<int2>(), System.Array.Empty<float3>(),
                                             System.Array.Empty<float>(), 0, scratch);
            Assert.AreEqual(new float2(-1f, 0f), dir, "집 쪽으로 돌아온다");
        }
    }
}
