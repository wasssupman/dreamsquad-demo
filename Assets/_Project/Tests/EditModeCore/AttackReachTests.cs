// 적응: Assets/_Project/Tests/EditMode/AttackReachTests.cs (battle-core-rebuild unit 2)
// 도형(부채꼴·띠) 케이스는 unit 3 에서 저작·bake 가 붙을 때 함께 옮긴다 — 여기서는 **원 항**과
// 「이동이 같은 자를 쓴다」만 증언한다.
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore.Combat;

namespace Wassup.Tests.EditMode.Core
{
    public class AttackReachTests
    {
        [Test]
        public void 도달은_사거리에_양쪽_몸을_더한_원이다()
        {
            // 제약 13 — 도달 = |좌표 차| ≤ 범위 + 원점 항 + 대상의 몸.
            var a = new float3(0f, 0f, 0f);
            var b = new float3(2f, 0f, 0f);

            Assert.IsFalse(AttackReach.InReach(a, b, 1f, 1f, 0.25f, 0.25f), "1 + 0.25 + 0.25 = 1.5 < 2");
            Assert.IsTrue(AttackReach.InReach(a, b, 1.5f, 1f, 0.25f, 0.25f));
        }

        [Test]
        public void 대상_몸을_빼면_조용히_좁아진다()
        {
            // 보스처럼 몸이 큰 상대는 링보다 멀리서도 맞는다. 0 으로 두면 화면이 실제보다 좁다.
            var a = float3.zero;
            var b = new float3(1.9f, 0f, 0f);
            Assert.IsFalse(AttackReach.InReach(a, b, 1f, 1f, 0.25f, 0f));
            Assert.IsTrue(AttackReach.InReach(a, b, 1f, 1f, 0.25f, 1f), "보스 몸 1.0 이면 닿는다");
        }

        [Test]
        public void 대각_인접도_사거리_1_이다()
        {
            // 「대각 인접도 사거리 1」은 오래된 계약이다 — 표준 몸(0.25)끼리면 1.414 ≤ 1.5.
            Assert.IsTrue(AttackReach.InCellReach(int2.zero, new int2(1, 1), 1f, 0.25f, 0.25f));
        }

        [Test]
        public void 칸_판정과_월드_판정이_같은_본체를_지난다()
        {
            // 화면(칸)과 실제(월드)가 갈리면 「밝은 칸인데 안 때린다」가 된다.
            for (int dx = 0; dx <= 3; dx++)
            for (int dz = 0; dz <= 3; dz++)
            {
                bool cell = AttackReach.InCellReach(int2.zero, new int2(dx, dz), 2f, 0.25f, 0.25f);
                bool world = AttackReach.InReach(float3.zero, new float3(dx, 0f, dz), 2f, 1f, 0.25f, 0.25f);
                Assert.AreEqual(cell, world, $"({dx},{dz})");
            }
        }

        [Test]
        public void 칸_체비셰프는_격자_계층_전용이다()
        {
            // 사거리 판정에 쓰면 0.1 → 0, 2.5 → 3 이 된다. 남은 소비처는 순찰 이동뿐이다.
            Assert.IsTrue(AttackReach.InCellRange(int2.zero, new int2(2, 2), 2));
            Assert.IsFalse(AttackReach.InCellRange(int2.zero, new int2(3, 0), 2));
        }

        [Test]
        public void 타일_크기가_달라도_같은_칸수로_판정한다()
        {
            // 술어는 월드 단위를 모른다 — 「사거리 3」이 저작에서 칸 수이기 때문이다.
            var a = float3.zero;
            var b = new float3(3f, 0f, 0f);       // tileSize 3 → 1칸
            Assert.IsTrue(AttackReach.InReach(a, b, 1f, 3f, 0f, 0f));
            Assert.IsFalse(AttackReach.InReach(a, b, 1f, 1f, 0f, 0f));
        }

        [Test]
        public void 도형이_Omni_면_원_항만_본다()
        {
            var shape = AttackShapeBaked.Omni;
            Assert.IsTrue(AttackReach.InReachShaped(float3.zero, new float3(0f, 0f, 1f), 2f, 1f,
                                                    0.25f, 0.25f, in shape, new float2(1f, 0f)));
        }
    }
}
