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
        // ── unit 10: 대표 셀이 은퇴하고 **기하 중심**이 그 자리를 받았다 ──────────
        //
        // 짝수 변 footprint 는 **중심 칸이 없다**(2×3 의 중심은 셀 경계 x=.5). 대표 셀은
        // 그걸 정수 나눗셈으로 고른 동전 던지기였고, 플레이어에겐 안 보이는데 사거리를
        // 반 칸 옮겼다. 이제 저장값은 **앵커 + 크기**뿐이고 중심은 실수로 파생된다.
        [TestCase(1, 1, 0f, 0f)]
        [TestCase(3, 3, 1f, 1f)]      // 홀수 → 정수(중앙 칸 위)
        [TestCase(2, 2, 0.5f, 0.5f)]  // 짝수 → 셀 경계
        [TestCase(2, 3, 0.5f, 1f)]    // 캐논 2×3 — 가로만 경계
        [TestCase(1, 2, 0f, 0.5f)]
        public void GeometricCenterOffset_IsHalfOfWidthMinusOne(int w, int h, float ox, float oy)
        {
            // ⚠ `(W−1)/2` 다. 앵커가 **셀 인덱스**이고 셀 N 의 중심이 정수 N 이므로
            // `W/2` 가 아니다 — 그 착각이 반 칸 어긋남을 만든다.
            var off = FootprintMath.GeometricCenterOffset(new Vector2Int(w, h));
            Assert.AreEqual(ox, off.x, 1e-5f);
            Assert.AreEqual(oy, off.y, 1e-5f);
        }

        // 중심은 언제나 점유 rect **안**에 있다(경계 포함). 밖으로 나가면 사거리가 몸을 벗어난다.
        [Test]
        public void GeometricCenter_StaysInsideOccupiedRect()
        {
            var anchor = new Vector2Int(5, 7);
            foreach (var size in new[]
            {
                new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3),
                new Vector2Int(2, 2), new Vector2Int(2, 3), new Vector2Int(3, 2),
                new Vector2Int(3, 3), new Vector2Int(4, 3),
            })
            {
                var off = FootprintMath.GeometricCenterOffset(size);
                var rect = FootprintMath.Cells(anchor, size);
                float cx = anchor.x + off.x, cy = anchor.y + off.y;
                // 셀 N 은 [N−0.5, N+0.5) 를 덮으므로 rect 의 연속 범위는 [min−0.5, max−0.5].
                Assert.GreaterOrEqual(cx, rect.xMin - 0.5f, $"{size} x 하한");
                Assert.LessOrEqual(cx, rect.xMax - 1 + 0.5f, $"{size} x 상한");
                Assert.GreaterOrEqual(cy, rect.yMin - 0.5f, $"{size} y 하한");
                Assert.LessOrEqual(cy, rect.yMax - 1 + 0.5f, $"{size} y 상한");
            }
        }

        // 발밑은 **하단 행**이다. 중심으로 소팅하면 높이 2 이상에서 앞줄 유닛이 뒤로 들어간다.
        [TestCase(1, 1, 0f)]
        [TestCase(2, 3, 0.5f)]
        [TestCase(3, 3, 1f)]
        public void FootOffset_IsBottomRow_NotCenter(int w, int h, float ox)
        {
            var foot = FootprintMath.FootOffset(new Vector2Int(w, h));
            Assert.AreEqual(ox, foot.x, 1e-5f, "가로는 중심과 같다");
            Assert.AreEqual(0f, foot.y, 1e-5f, "세로는 **0** — 하단 행이지 중심이 아니다");
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
