// 적응: Assets/_Project/Tests/EditMode/Battle/{NavGridTests, MovementCellTrimTests, FillWalkMaskTests}.cs
// (battle-core-rebuild unit 2)
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    public class NavGridAndTrimTests
    {
        [Test]
        public void 경계_밖은_항상_막힘이다()
        {
            var nav = CoreMapFixtures.FlatNav(3, 3);
            Assert.IsTrue(nav.IsBlocked(new int2(-1, 0)));
            Assert.IsTrue(nav.IsBlocked(new int2(3, 0)));
            Assert.IsFalse(nav.IsBlocked(new int2(1, 1)));
        }

        [Test]
        public void 마스크_미생성은_평지로_본다()
        {
            // 픽스처 보호 규약 — 프로덕션은 항상 채우므로 해당 없다.
            var nav = new NavGrid(null, null, false, new int2(3, 3), 1f);
            Assert.IsFalse(nav.IsBlocked(new int2(1, 1)));
        }

        [Test]
        public void 장애물은_정적_벽과_합쳐진다()
        {
            var walk = new byte[9];
            for (int i = 0; i < 9; i++) walk[i] = 1;
            var blocked = new bool[9];
            blocked[1 * 3 + 1] = true;

            var nav = new NavGrid(walk, blocked, true, new int2(3, 3), 1f);
            Assert.IsTrue(nav.IsBlocked(new int2(1, 1)));

            var without = new NavGrid(walk, blocked, false, new int2(3, 3), 1f);
            Assert.IsFalse(without.IsBlocked(new int2(1, 1)), "hasObstacles 가 꺼지면 안 본다");
        }

        [Test]
        public void 통행_마스크는_칸_층과_슬롯_마스크의_교집합이다()
        {
            // 걸을 수 있다 ⇔ (칸 층 & 슬롯 마스크) != 0 — 정의식은 여기 한 곳뿐이다.
            var layers = new byte[] { LayerBits.Path | LayerBits.Air, LayerBits.Air, LayerBits.Ground | LayerBits.Air };
            var outMask = new byte[3];

            TraversalSlots.FillWalkMask(layers, LayerBits.Path, outMask);
            Assert.AreEqual(new byte[] { 1, 0, 0 }, outMask);

            TraversalSlots.FillWalkMask(layers, LayerBits.Air, outMask);
            Assert.AreEqual(new byte[] { 1, 1, 1 }, outMask, "비행은 지상 벽을 안 본다");
        }

        [Test]
        public void 비행_슬롯은_지상_장애물을_벽으로_보지_않는다()
        {
            var layers = new byte[4];
            for (int i = 0; i < 4; i++) layers[i] = LayerBits.Path | LayerBits.Air;
            var blocked = new bool[4];
            blocked[2] = true;
            var outMask = new byte[4];

            MovementCellTrim.FillWalkMask(layers, new int2(2, 2), LayerBits.Path, true, blocked, outMask);
            Assert.AreEqual(0, outMask[2], "지상은 장애물에 막힌다");

            MovementCellTrim.FillWalkMask(layers, new int2(2, 2), LayerBits.Air, true, blocked, outMask);
            Assert.AreEqual(1, outMask[2], "비행은 안 막힌다");
        }

        [Test]
        public void 변위_상한이_터널링을_막는다()
        {
            var moved = MovementCellTrim.ClampDisplacement(float3.zero, new float3(5f, 0f, 0f), 1f);
            Assert.AreEqual(0.9f, moved.x, 1e-5f, "한 틱 변위는 0.9칸을 못 넘는다");
        }

        [Test]
        public void 막힌_칸으로_넘어가면_현재_칸_경계로_접힌다()
        {
            var walk = new byte[9];
            for (int i = 0; i < 9; i++) walk[i] = 1;
            walk[1 * 3 + 2] = 0;   // (2,1) 막힘
            var nav = CoreMapFixtures.NavOf(walk, 3, 3);

            var desired = new float3(2.0f, 0f, 1f);
            var trimmed = MovementCellTrim.Apply(desired, new int2(1, 1), in nav);

            Assert.Less(trimmed.x, 1.5f, "현재 칸 안에 남아야 한다");
        }
    }
}
