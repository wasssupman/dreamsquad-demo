using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // defender-footprint unit 0 — 대표 셀 규약(홀수 정중앙·짝수 floor)과 앵커 왕복 대칭.
    public class FootprintMathTests
    {
        [Test]
        public void Cells_AnchorIsMinCorner()
        {
            var r = FootprintMath.Cells(new Vector2Int(3, 4), new Vector2Int(2, 3));
            Assert.AreEqual(new Vector2Int(3, 4), r.min);
            Assert.AreEqual(new Vector2Int(5, 7), r.max, "max 는 exclusive — anchor + size");
        }

        [Test]
        public void Cells_ClampsSizeToOne()
        {
            var r = FootprintMath.Cells(Vector2Int.zero, new Vector2Int(0, -2));
            Assert.AreEqual(Vector2Int.one, r.size);
        }

        [TestCase(1, 1, 0, 0)]
        [TestCase(3, 3, 1, 1)]
        [TestCase(2, 2, 0, 0)]
        [TestCase(2, 3, 0, 1)]
        [TestCase(3, 2, 1, 0)]
        [TestCase(1, 2, 0, 0)]
        [TestCase(1, 3, 0, 1)]
        public void PrimaryOffset_OddCenters_EvenFloors(int w, int h, int ex, int ey)
        {
            Assert.AreEqual(new Vector2Int(ex, ey),
                FootprintMath.PrimaryOffset(new Vector2Int(w, h)));
        }

        [Test]
        public void PrimaryAndAnchor_RoundTrip_PrimaryInsideRect()
        {
            var sizes = new[]
            {
                new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3),
                new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(3, 2),
                new Vector2Int(3, 3),
            };
            var anchor = new Vector2Int(5, 7);
            foreach (var size in sizes)
            {
                var primary = FootprintMath.PrimaryCell(anchor, size);
                Assert.AreEqual(anchor, FootprintMath.AnchorFromPrimary(primary, size),
                    $"{size.x}x{size.y} 왕복 대칭");
                Assert.IsTrue(FootprintMath.Cells(anchor, size).Contains(primary),
                    $"{size.x}x{size.y} 대표 셀은 점유 rect 안");
            }
        }

        [Test]
        public void OneByOne_PrimaryEqualsAnchor()
        {
            Assert.AreEqual(new Vector2Int(4, 9),
                FootprintMath.PrimaryCell(new Vector2Int(4, 9), Vector2Int.one));
        }

        [TestCase(1, 1, 5, 7)]  // 항등
        [TestCase(2, 2, 5, 7)]  // (W-1)/2=0 — 손가락이 좌하단 열
        [TestCase(3, 3, 4, 7)]  // 손가락 = 하단 중앙
        [TestCase(3, 2, 4, 7)]
        [TestCase(1, 3, 5, 7)]
        public void AnchorFromBottomCenter_FingerIsBottomCenter(int w, int h, int ax, int ay)
        {
            Assert.AreEqual(new Vector2Int(ax, ay),
                FootprintMath.AnchorFromBottomCenter(new Vector2Int(5, 7), new Vector2Int(w, h)));
        }

        [TestCase(1, 1, 0f, 0f)]
        [TestCase(3, 3, 0f, 0f)]  // 홀수 변 = 대표 셀이 곧 기하 중심
        [TestCase(2, 2, 0.5f, 0.5f)]
        [TestCase(2, 3, 0.5f, 0f)]
        [TestCase(1, 2, 0f, 0.5f)]
        public void CenterOffsetFromPrimary_EvenSidesGetHalfCell(int w, int h, float ox, float oy)
        {
            var off = FootprintMath.CenterOffsetFromPrimary(new Vector2Int(w, h));
            Assert.AreEqual(ox, off.x, 1e-5f);
            Assert.AreEqual(oy, off.y, 1e-5f);
        }

        [Test]
        public void RectChebyshevDistance_OverlapTouchGap()
        {
            var a = FootprintMath.Cells(new Vector2Int(0, 0), new Vector2Int(2, 2)); // (0,0)~(1,1)
            Assert.AreEqual(0, FootprintMath.RectChebyshevDistance(a,
                FootprintMath.Cells(new Vector2Int(1, 1), new Vector2Int(2, 2))), "겹침 = 0");
            Assert.AreEqual(1, FootprintMath.RectChebyshevDistance(a,
                FootprintMath.Cells(new Vector2Int(2, 0), new Vector2Int(2, 2))), "옆면 접촉 = 1");
            Assert.AreEqual(1, FootprintMath.RectChebyshevDistance(a,
                FootprintMath.Cells(new Vector2Int(2, 2), new Vector2Int(3, 3))), "대각 접촉 = 1");
            Assert.AreEqual(2, FootprintMath.RectChebyshevDistance(a,
                FootprintMath.Cells(new Vector2Int(3, 0), new Vector2Int(1, 1))), "한 칸 이격 = 2");
        }

        [Test]
        public void RectChebyshevDistance_OneByOnePairs_MatchEightNeighborhood()
        {
            var center = FootprintMath.Cells(new Vector2Int(5, 5), Vector2Int.one);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    var other = FootprintMath.Cells(new Vector2Int(5 + dx, 5 + dy), Vector2Int.one);
                    int expected = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    Assert.AreEqual(expected, FootprintMath.RectChebyshevDistance(center, other),
                        $"1×1 쌍 ({dx},{dy}) — 셀 체비셰프와 동치(거리 1 = 8이웃)");
                }
            }
        }

        [Test]
        public void DefenderUnitData_Footprint_DefaultsToOne_ClampsAtRead()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            try
            {
                Assert.AreEqual(Vector2Int.one, so.Footprint, "기본값 = 1×1 (기존 유닛 무변)");
                so.footprintWidth = 0;
                so.footprintHeight = 3;
                Assert.AreEqual(new Vector2Int(1, 3), so.Footprint, "0/음수는 읽는 자리에서 1로 조임");
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }
    }
}
