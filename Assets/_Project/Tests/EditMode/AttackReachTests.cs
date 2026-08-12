using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // 사거리 술어 회귀. 이 함수의 값어치는 «규칙이 하나»라는 데 있다 —
    // 공격(AttackSystem)·정지(EnemyAiStateSystem)·이동(PatrolAreaMath)이 같은 답을 받아야
    // «멈추는데 못 때리는» 교착이 안 생긴다(2026-08-12 실측 182프레임 교착의 교훈).
    public class AttackReachTests
    {
        private const float Tile = 1f;
        private static float3 At(float x, float z) => new float3(x, 0f, z);

        // 셀 중앙에 선 경우 = 타일 고정 유닛의 배치. 2차 게이트를 걸지 않는다.
        private static bool Locked(int2 a, int2 b, int range)
            => AttackReach.InReach(a, b, range, At(a.x, a.y), At(b.x, b.y), Tile, false);

        [Test]
        public void CellGate_IsChebyshev_DiagonalCountsAsOne()
        {
            var a = new int2(3, 3);
            Assert.IsTrue(Locked(a, new int2(4, 3), 1), "축 인접");
            Assert.IsTrue(Locked(a, new int2(4, 4), 1), "대각도 사거리 1 — 유클리드로 재지 않는다");
            Assert.IsFalse(Locked(a, new int2(5, 3), 1));
            Assert.IsFalse(Locked(a, new int2(5, 5), 1));
        }

        [Test]
        public void ZeroRange_SameCellOnly()
        {
            var a = new int2(3, 3);
            Assert.IsTrue(Locked(a, a, 0));
            Assert.IsFalse(Locked(a, new int2(4, 3), 0));
        }

        [Test]
        public void TileLockedPair_KeepsItsExistingSlack()
        {
            // 타일 고정 유닛은 한쪽만 칸 안에서 밀리므로 최대 1.49칸까지가 종전 동작이다.
            // 이 수정이 그걸 좁히면 출시된 유닛 전원의 체감 사거리가 바뀐다 = 회귀.
            Assert.IsTrue(AttackReach.InReach(
                new int2(3, 3), new int2(4, 3), 1, At(3f, 3f), At(4.49f, 3f), Tile, false));
        }

        [Test]
        public void BothContinuous_TwoTileSeparation_IsRejected()
        {
            // ★ 증상: 셀은 인접(1칸)인데 실제 1.98칸. 사거리 1이 2칸처럼 보이던 자리.
            Assert.IsFalse(AttackReach.InReach(
                new int2(3, 3), new int2(4, 3), 1, At(2.51f, 3f), At(4.49f, 3f), Tile, true));
        }

        [Test]
        public void BothContinuous_WithinCap_StillReaches()
        {
            // 상한은 타일 고정 유닛과 같은 1.5칸 — 정상 교전 거리는 그대로 닿는다.
            Assert.IsTrue(AttackReach.InReach(
                new int2(3, 3), new int2(4, 3), 1, At(3f, 3f), At(4.4f, 3f), Tile, true));
            Assert.IsTrue(AttackReach.InReach(
                new int2(3, 3), new int2(4, 3), 1, At(3.4f, 3f), At(3.9f, 3f), Tile, true));
        }

        [Test]
        public void WorldGate_IsChebyshevToo_SoDiagonalIsNotPenalized()
        {
            // 유클리드로 재면 대각 1.41 이 상한 1.5 에 아슬아슬해 «대각만 조용히 좁아지는»
            // 비대칭이 생긴다. 월드도 체비셰프라야 셀 규칙과 한 몸이다.
            Assert.IsTrue(AttackReach.InWorldReach(At(0f, 0f), At(1.4f, 1.4f), 1, Tile));
            Assert.IsFalse(AttackReach.InWorldReach(At(0f, 0f), At(1.6f, 1.6f), 1, Tile));
        }

        [Test]
        public void LongRange_ScalesTheCap()
        {
            Assert.IsTrue(AttackReach.InWorldReach(At(0f, 0f), At(4.4f, 0f), 4, Tile));
            Assert.IsFalse(AttackReach.InWorldReach(At(0f, 0f), At(4.6f, 0f), 4, Tile));
        }

        [Test]
        public void TileSize_ScalesTheCap()
        {
            // 상한은 타일 크기에 비례한다 — 셀 절반이라는 정의에서 나온 값이라 그래야 한다.
            Assert.IsTrue(AttackReach.InWorldReach(At(0f, 0f), At(2.9f, 0f), 1, 2f));
            Assert.IsFalse(AttackReach.InWorldReach(At(0f, 0f), At(3.1f, 0f), 1, 2f));
        }

        [Test]
        public void IsSymmetric()
        {
            // 비대칭이면 «A는 B를 때리는데 B는 A를 못 때리는» 상태가 생긴다.
            var a = new int2(2, 7); var b = new int2(4, 6);
            var pa = At(2.3f, 7.1f); var pb = At(4.2f, 5.8f);
            Assert.AreEqual(AttackReach.InReach(a, b, 2, pa, pb, Tile, true),
                            AttackReach.InReach(b, a, 2, pb, pa, Tile, true));
        }
    }
}
