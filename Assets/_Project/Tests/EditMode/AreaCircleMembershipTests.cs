using System;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attach-range-preview unit 0a — 스킬 광역의 마지막 사각 잔존을 원으로.
    //
    // 지키는 것 둘: ① 광역 도형은 **원**이고 반경은 `N + 칸 반폭(0.5)` 이다 — 사각 모서리
    // (체비셰프 안 · 유클리드 밖)는 더 이상 걸리지 않는다. ② `RangeMetric` 의 **기본값이 원**
    // 이다 — `default(RangeMetric)` 이나 인자 누락이 조용히 은퇴한 사각 자로 판정하지 않는다.
    //
    // N=1 의 대각 인접(1.414 < 1.5)이 살아 있어야 하는 것도 같은 파일에 못박는다 — 반경을
    // `N` 으로 줄이면 자장가·둔화 장판이 십자 모양이 된다(SkillMath 헤더가 경고한 회귀).
    public class AreaCircleMembershipTests
    {
        private static SkillParams Sleep(int sleepCount, int radius, float duration)
            => new SkillParams(sleepCount, duration, radius, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0);

        private static TestSkillContext Ctx(out CasterRef caster)
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);
            return ctx;
        }

        [Test]
        public void RangeMetric_DefaultValue_IsAreaCircle()
        {
            // 값 0 = 원. 사각이 0 으로 남으면 인자 누락이 조용히 옛 자를 탄다.
            Assert.AreEqual("AreaCircle", Enum.GetName(typeof(RangeMetric), (RangeMetric)0),
                "RangeMetric 의 기본값(0)은 원 광역이어야 한다");
        }

        [Test]
        public void AreaSleep_N1_ExcludesSquareCornerOutsideCircle()
        {
            // 오프셋 (1.4, 1.4): 체비셰프 셀 거리 1(사각 안) · 유클리드 1.98 > 1.5(원 밖).
            var ctx = Ctx(out var caster);
            ctx.Add(1, new float3(6.9f, 0, 6.9f), Faction.DefenderUnit);

            new AreaSleepSkill().Execute(caster, SkillTarget.None, Sleep(3, 1, 1.5f), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count,
                "사각 모서리(원 밖)의 대상은 광역에 걸리지 않아야 한다");
        }

        // 리뷰 H(ecs) — 사각→원 손실표의 핀. 「골든 A/B 바이트 동일」은 카드 레이어가 N=1 뿐이라 N≥2 를
        // 증언하지 못한다(N≥2 저작 9건: 보스 수면 4·아처 둔화 오라 3·배치 스킬 6종 2). 그래서 손실이 **의도한
        // 크기**인지를 여기서 못박는다 — 정수 오프셋·소형 상대(0.25) 기준, 원은 사각의 부분집합이고 빠지는 칸은
        // 모서리만이다. 값이 움직이면 자가 바뀐 것이다.
        [TestCase(1, 9, 9)]
        [TestCase(2, 25, 21)]
        [TestCase(3, 49, 45)]
        [TestCase(4, 81, 69)]
        public void SquareToCircle_LosesOnlyCorners_ByTheDocumentedCount(int n, int squareCells, int circleCells)
        {
            const float targetR = SkillMath.StandardBodyRadiusTiles;
            float half = n + SkillMath.CellHalfWidthTiles;
            int sq = 0, ci = 0;
            for (int dx = -n - 1; dx <= n + 1; dx++)
            for (int dz = -n - 1; dz <= n + 1; dz++)
            {
                bool inSquare = SkillMath.BodyOverlapsSquare(dx, dz, half, targetR);
                bool inCircle = SkillMath.InBodyReach(dx, dz, n, SkillMath.CellHalfWidthTiles, targetR);
                if (inSquare) sq++;
                if (inCircle) ci++;
                Assert.IsFalse(inCircle && !inSquare, $"N={n} ({dx},{dz}) — 원이 사각 밖을 맞힌다(부분집합 위반)");
            }
            Assert.AreEqual(squareCells, sq, $"N={n} 종전 사각 칸 수");
            Assert.AreEqual(circleCells, ci, $"N={n} 원 칸 수 — 손실표와 다르면 자가 바뀐 것이다");
        }

        [Test]
        public void AreaSleep_N1_KeepsDiagonalNeighbour()
        {
            // 오프셋 (1, 1) = 1.414 ≤ 1 + 0.5 — 반경을 N 으로 줄이면 여기가 빠져 십자가 된다.
            var ctx = Ctx(out var caster);
            ctx.Add(1, new float3(6.5f, 0, 6.5f), Faction.DefenderUnit);

            new AreaSleepSkill().Execute(caster, SkillTarget.None, Sleep(3, 1, 1.5f), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count, "N=1 의 대각 인접은 원 안이다");
        }
    }
}
