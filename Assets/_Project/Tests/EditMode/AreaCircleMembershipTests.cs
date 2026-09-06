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
            // ★ unit 23a — 시전자에 **몸을 준다.** 라이브에서 자기중심 광역의 원점 항은 시전자 몸이고,
            // 그 값은 «항상» ≥ 0.5 다(방어유닛 최소 폭1 = 0.5 · 적 소형 0.25 는 자기중심 광역 저작 0종).
            // 페이크가 0 을 쓰면 반경이 «좁아져» 「반경 1 = 여덟 이웃」 계약이 깨진다 —
            // 그게 `SkillMath` 의 옛 경고(십자 회귀)가 말하던 현상이고, **오늘 라이브에서는 불가능**하다.
            caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit, bodyRadius: 0.5f);
            return ctx;
        }

        [Test]
        public void RangeMetric_DefaultValue_IsNone_SoOmissionFailsClosed()
        {
            // ★ unit 23a — **기본값이 «유효 arm» 이면 안 된다.** 종전엔 `AreaCircle = 0` 이라
            // 인자를 빠뜨리면 조용히 광역 자를 탔다. 전환 뒤로는 자기중심이 9/11 이라, 0 이
            // 유효하면 **미래의 자기중심 스킬이 인자를 빠뜨렸을 때 이 spec 이 고친 그 버그가
            // 그대로 재생산**되고 — 하필 **폭 1 유닛에서는 답이 같아 안 보인다**.
            Assert.AreEqual("None", Enum.GetName(typeof(RangeMetric), (RangeMetric)0),
                "RangeMetric 의 기본값(0)은 fail-closed 여야 한다");
            Assert.IsFalse(SkillMath.TryOriginRadius(RangeMetric.None, 1.5f, out _),
                "None 은 매핑을 거절해야 한다 — 호출부가 후보 0 으로 접는다");
        }

        // ★★ **차등 단언 — 이 unit 의 그물.** 시전자 몸만 키우면 대상 집합이 넓어져야 한다.
        // ⚠ 호스트를 **폭 2 이상**으로 잡는 것이 요점이다. 폭 1(몸 0.5)은 옛 칸 반폭과 값이 같아
        // 전후 답이 같고, 그래서 1×1 픽스처만 있던 것이 unit 22 의 결함을 숨겼다.
        [Test]
        public void SelfArea_WidensWithCasterBody_NotWithACellConstant()
        {
            // 오프셋 (2.2, 0): 폭1 시전자(0.5)면 1 + 0.5 + 0.25 = 1.75 밖 → 제외.
            // 폭3 시전자(1.5)면 1 + 1.5 + 0.25 = 2.75 안 → 포함.
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(7.7f, 0, 5.5f), Faction.DefenderUnit);

            var thin = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit, bodyRadius: 0.5f);
            var wide = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit, bodyRadius: 1.5f);
            var buf = new SkillEntityId[8];

            int thinN = ctx.Opponents(thin, new float3(5.5f, 0, 5.5f), 1,
                                      CandidateFilter.ExcludeDead, RangeMetric.SelfArea, buf);
            int wideN = ctx.Opponents(wide, new float3(5.5f, 0, 5.5f), 1,
                                      CandidateFilter.ExcludeDead, RangeMetric.SelfArea, buf);

            Assert.AreEqual(0, thinN, "폭1 시전자에겐 2.2칸이 밖이다");
            Assert.AreEqual(1, wideN,
                "폭3 시전자에겐 안이어야 한다 — 여기가 0 이면 원점 항이 아직 칸 상수다(unit 23 의 결함)");
        }

        // 「자리에 떨어지는 것」은 시전자 몸에 **반응하지 않는다** — 형이 갈렸다는 증거.
        [Test]
        public void CellArea_DoesNotReactToCasterBody()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(7.7f, 0, 5.5f), Faction.DefenderUnit);
            var buf = new SkillEntityId[8];
            int a = ctx.Opponents(CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit, 0.5f),
                                  new float3(5.5f, 0, 5.5f), 1, CandidateFilter.ExcludeDead, RangeMetric.CellArea, buf);
            int b = ctx.Opponents(CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit, 1.5f),
                                  new float3(5.5f, 0, 5.5f), 1, CandidateFilter.ExcludeDead, RangeMetric.CellArea, buf);
            Assert.AreEqual(a, b, "자리형은 시전자 몸에 반응하면 안 된다(퇴근 운석이 이 형이다)");
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
            float half = n + SkillMath.CellShapePaddingTiles;
            int sq = 0, ci = 0;
            for (int dx = -n - 1; dx <= n + 1; dx++)
            for (int dz = -n - 1; dz <= n + 1; dz++)
            {
                bool inSquare = SkillMath.BodyOverlapsSquare(dx, dz, half, targetR);
                bool inCircle = SkillMath.ReachFromCell(dx, dz, n, targetR);
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
