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
