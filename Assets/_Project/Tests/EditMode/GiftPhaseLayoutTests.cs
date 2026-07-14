using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // gift-phase-presentation unit 0 — 안무가 소비하는 순수 레이아웃 수학.
    // 그리드/부채꼴 좌표, 리플 인터리브 순열, 흡수 가속 케이던스, 스택 지터 결정론이 계약.
    public class GiftPhaseLayoutTests
    {
        // ── GridSlot ──────────────────────────────────────────────────────

        [Test]
        public void GridSlot_10Cards_5Cols_CentersBothAxes()
        {
            var cell = new Vector2(200f, 270f);

            // 첫 행 좌단/우단, 둘째 행 좌단/우단 — 중앙 대칭.
            Assert.AreEqual(new Vector2(-400f, 135f), GiftPhaseLayout.GridSlot(0, 10, 5, cell));
            Assert.AreEqual(new Vector2(400f, 135f), GiftPhaseLayout.GridSlot(4, 10, 5, cell));
            Assert.AreEqual(new Vector2(-400f, -135f), GiftPhaseLayout.GridSlot(5, 10, 5, cell));
            Assert.AreEqual(new Vector2(400f, -135f), GiftPhaseLayout.GridSlot(9, 10, 5, cell));
        }

        [Test]
        public void GridSlot_RowMajor_SecondCardStaysInFirstRow()
        {
            var cell = new Vector2(200f, 270f);
            var slot1 = GiftPhaseLayout.GridSlot(1, 10, 5, cell);
            Assert.AreEqual(135f, slot1.y, 1e-4f, "행 우선 — k=1 은 첫 행");
            Assert.AreEqual(-200f, slot1.x, 1e-4f);
        }

        [Test]
        public void GridSlot_CenterOfMass_IsOrigin()
        {
            var cell = new Vector2(180f, 252f);
            Vector2 sum = Vector2.zero;
            for (int k = 0; k < 10; k++) sum += GiftPhaseLayout.GridSlot(k, 10, 5, cell);
            Assert.AreEqual(0f, sum.x, 1e-3f);
            Assert.AreEqual(0f, sum.y, 1e-3f);
        }

        // ── FanSlot ───────────────────────────────────────────────────────

        [Test]
        public void FanSlot_LeftRight_Mirror_PositionAndRotation()
        {
            var (posL, rotL) = GiftPhaseLayout.FanSlot(0, 12, 900f, 40f, 30f);
            var (posR, rotR) = GiftPhaseLayout.FanSlot(11, 12, 900f, 40f, 30f);

            Assert.AreEqual(-posR.x, posL.x, 1e-3f, "좌우 대칭");
            Assert.AreEqual(posR.y, posL.y, 1e-3f);
            Assert.AreEqual(-rotR, rotL, 1e-3f, "회전 부호 반대");
            Assert.Greater(rotL, 0f, "왼쪽 카드는 왼쪽으로 기움(CCW, +z)");
        }

        [Test]
        public void FanSlot_MiddleRises_AboveEndpoints()
        {
            var (posEnd, _) = GiftPhaseLayout.FanSlot(0, 12, 900f, 40f, 30f);
            var (posMidL, _) = GiftPhaseLayout.FanSlot(5, 12, 900f, 40f, 30f);
            var (posMidR, _) = GiftPhaseLayout.FanSlot(6, 12, 900f, 40f, 30f);

            Assert.Greater(posMidL.y, posEnd.y, "아치 — 중앙이 끝단보다 솟음");
            Assert.AreEqual(posMidL.y, posMidR.y, 1e-3f, "짝수 n 중앙 쌍은 같은 높이");
        }

        [Test]
        public void FanSlot_SingleCard_CentersAtBase()
        {
            var (pos, rot) = GiftPhaseLayout.FanSlot(0, 1, 900f, 40f, 30f);
            Assert.AreEqual(0f, pos.x, 1e-4f);
            Assert.AreEqual(30f, pos.y, 1e-4f, "n=1 은 baseY 정점");
            Assert.AreEqual(0f, rot, 1e-4f);
        }

        // ── RiffleOrder ───────────────────────────────────────────────────

        [Test]
        public void RiffleOrder_IsPermutation()
        {
            foreach (int n in new[] { 2, 5, 12 })
            {
                var order = GiftPhaseLayout.RiffleOrder(n);
                Assert.AreEqual(n, order.Length);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, n), order, $"n={n} 전원소 정확히 1회");
            }
        }

        [Test]
        public void RiffleOrder_Alternates_BetweenHalves()
        {
            var order = GiftPhaseLayout.RiffleOrder(12);
            // 좌뭉치 = 0..5, 우뭉치 = 6..11. 지퍼 — 앞 두 장은 서로 다른 뭉치.
            bool first = order[0] < 6, second = order[1] < 6;
            Assert.AreNotEqual(first, second, "첫 두 장은 좌/우 교차");
            for (int i = 0; i + 1 < order.Length; i++)
                Assert.AreNotEqual(order[i] < 6, order[i + 1] < 6, $"i={i} 교차 유지 (짝수 n)");
        }

        // ── AbsorbDelay ───────────────────────────────────────────────────

        [Test]
        public void AbsorbDelay_StartsAtZero_AndIsMonotonic()
        {
            Assert.AreEqual(0f, GiftPhaseLayout.AbsorbDelay(0, 0.3f, 0.08f, 0.8f), 1e-5f);
            float prev = 0f;
            for (int i = 1; i < 12; i++)
            {
                float d = GiftPhaseLayout.AbsorbDelay(i, 0.3f, 0.08f, 0.8f);
                Assert.Greater(d, prev, $"i={i} 단조 증가");
                prev = d;
            }
        }

        [Test]
        public void AbsorbDelay_Gaps_ShrinkAndClampAtMin()
        {
            float prevGap = float.MaxValue;
            for (int i = 0; i < 11; i++)
            {
                float gap = GiftPhaseLayout.AbsorbDelay(i + 1, 0.3f, 0.08f, 0.6f)
                            - GiftPhaseLayout.AbsorbDelay(i, 0.3f, 0.08f, 0.6f);
                Assert.LessOrEqual(gap, prevGap + 1e-5f, $"i={i} 간격 단조 감소");
                Assert.GreaterOrEqual(gap, 0.08f - 1e-5f, $"i={i} min 클램프");
                prevGap = gap;
            }
            // 강한 감쇠면 꼬리 간격은 min 에 도달.
            float tail = GiftPhaseLayout.AbsorbDelay(11, 0.3f, 0.08f, 0.6f)
                         - GiftPhaseLayout.AbsorbDelay(10, 0.3f, 0.08f, 0.6f);
            Assert.AreEqual(0.08f, tail, 1e-4f);
        }

        // ── StackJitter ───────────────────────────────────────────────────

        [Test]
        public void StackJitter_Deterministic_SameInputSameOutput()
        {
            for (int k = 0; k < 12; k++)
            {
                var (rotA, offA) = GiftPhaseLayout.StackJitter(k, 6f, 8f);
                var (rotB, offB) = GiftPhaseLayout.StackJitter(k, 6f, 8f);
                Assert.AreEqual(rotA, rotB);
                Assert.AreEqual(offA, offB);
            }
        }

        [Test]
        public void StackJitter_Bounded_AndVaried()
        {
            var rots = new float[12];
            for (int k = 0; k < 12; k++)
            {
                var (rot, off) = GiftPhaseLayout.StackJitter(k, 6f, 8f);
                Assert.LessOrEqual(Mathf.Abs(rot), 6f + 1e-4f, $"k={k} 회전 바운드");
                Assert.LessOrEqual(Mathf.Abs(off.x), 8f + 1e-4f, $"k={k} 오프셋 x 바운드");
                Assert.LessOrEqual(Mathf.Abs(off.y), 8f + 1e-4f, $"k={k} 오프셋 y 바운드");
                rots[k] = rot;
            }
            Assert.GreaterOrEqual(rots.Distinct().Count(), 3, "지터가 실제로 다양함");
        }
    }
}
