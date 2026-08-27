using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 1 — 발사 명세 스킬의 도메인 계약.
    //
    // 이 그물이 지키는 것은 하나다: **쏘지 않기로 한 판단이 의도로 새지 않는다.**
    // 어댑터가 「의도 하나 = 전진 + 인스턴스 추가」를 원자로 붙이기 때문에,
    // 「쏠 수 없다」가 의도를 방출하면 그 순간 카운터가 헛돈다 — 발사 위상이
    // 밀려서 다음 발동이 엉뚱한 총구를 쓴다. 그래서 아래 취소 케이스 넷이
    // 전부 「SimIntent 0개」를 단언한다.
    public class EmitPatternSkillTests
    {
        private const int PatternIdx = 0;

        private static SkillParams P(int patternIndex, int range)
            => new SkillParams(0f, 0f, range, 0, SkillParams.NoDataIndex, 0, 0f, 0f, 0f, 0, 0,
                               0f, patternIndex);

        private static TestSkillContext Ctx(PatternAimNeed need, out CasterRef caster)
        {
            var ctx = new TestSkillContext();
            var host = ctx.Add(1, new float3(0f, 0f, 0f), Faction.DefenderUnit);
            ctx.PatternAim[PatternIdx] = need;
            caster = CasterRef.OfUnit(host, Faction.DefenderUnit);
            return ctx;
        }

        [Test]
        public void MissingPattern_FiresNothing()
        {
            var ctx = Ctx(PatternAimNeed.Missing, out var caster);
            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count,
                "명세가 없으면 발사도 카운터 전진도 없다");
        }

        [Test]
        public void Preaimed_FiresWithoutTouchingAim()
        {
            var ctx = Ctx(PatternAimNeed.Preaimed, out var caster);
            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            var it = ctx.SimIntents[0];
            Assert.AreEqual(SimIntentKind.EmitPattern, it.Kind);
            Assert.AreEqual(PatternIdx, it.PatternIndex);
            // ⚠ **방향을 싣지 않는다.** 실으면 어댑터가 이미 조준된 템플릿을 덮어
            // 무타겟 방향 패턴이 host 현재 위치로 리셋된다.
            Assert.AreEqual(0f, math.lengthsq(it.DirectionXZ), 1e-6f,
                "조준이 이미 실린 명세는 건드리지 않는다");
        }

        [Test]
        public void NeedsAim_UsesFacing_EvenWithNoCandidates()
        {
            // 조준이 최근접보다 세다 — 조준 방향에 아무도 없어도 쏜다.
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Units[1].HasFacing = true;
            ctx.Units[1].Facing = new float2(1f, 0f);

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(1f, ctx.SimIntents[0].DirectionXZ.x, 1e-4f);
            Assert.AreEqual(3, ctx.SimIntents[0].TileRange, "사거리는 저작값 그대로 실린다");
        }

        [Test]
        public void NeedsAim_NoFacing_PicksNearestOpponent()
        {
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Add(2, new float3(0f, 0f, 5f), Faction.EnemyUnit);   // 멀다
            ctx.Add(3, new float3(2f, 0f, 0f), Faction.EnemyUnit);   // 가깝다

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 6), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(1f, ctx.SimIntents[0].DirectionXZ.x, 1e-4f, "최근접(+X)으로 겨눈다");
        }

        [Test]
        public void NeedsAim_NoFacing_NoCandidate_FiresNothing()
        {
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count,
                "조준도 후보도 없으면 사건을 없던 것으로 한다 — 방향 (0,0) 탄을 내보내지 않는다");
        }

        [Test]
        public void NeedsAim_CandidateOutOfRange_FiresNothing()
        {
            // ⚠ 자는 **유클리드**다. 셀 체비셰프로 골랐다면 대각선 (3,3) 은 「3칸 안」이라
            // 후보가 되고, 조준은 성립하는데 탄은 실거리 4.24 를 못 가 도중에 소멸한다 —
            // 발사 연출만 나가고 아무도 안 맞는 조용한 no-op 이 그 증상이다.
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Add(2, new float3(3f, 0f, 3f), Faction.EnemyUnit);

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count,
                "조준 후보를 보는 자와 탄이 닿는 자는 같은 자여야 한다");
        }

        [Test]
        public void NeedsAim_UnreachableLayer_IsNotAimedAt()
        {
            // 근접 가디언이 하늘의 적을 겨누면 탄이 통행 층 게이트에 막혀 아무도 못 맞힌다.
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Units[1].AttackTraversalLayers = 0x01;                 // 지상만 때린다
            ctx.Add(2, new float3(2f, 0f, 0f), Faction.EnemyUnit, u => u.TraversalLayers = 0x02);

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count, "못 때리는 층은 총구를 못 가져간다");
        }

        [Test]
        public void NeedsAim_DeadCandidate_IsNotAimedAt()
        {
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Add(2, new float3(2f, 0f, 0f), Faction.EnemyUnit, u => u.Dead = true);

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count, "시체는 총구를 못 가져간다");
        }

        [Test]
        public void NeedsAim_NoPosition_FiresNothing()
        {
            // 위치를 모르면 조준도 못 한다. 이 가드가 없으면 (0,0) 방향 탄이 조용히 나간다.
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Units[1].HasPosition = false;
            ctx.Units[1].HasFacing = true;
            ctx.Units[1].Facing = new float2(1f, 0f);

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count);
        }

        [Test]
        public void SameInstance_FiredTwice_DoesNotLeakThePreviousCandidates()
        {
            // ⚠ 레지스트리가 static 이라 **한 인스턴스가 판 내내 재사용된다.**
            // 두 번째 발사에서 후보가 줄었는데 첫 발사의 잔재가 남아 총구를 가져가면,
            // 「없어진 적을 계속 겨눈다」가 된다(리뷰 M3 가 지목한 재현 조건).
            var skill = new EmitPatternSkill();

            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Add(2, new float3(1f, 0f, 0f), Faction.EnemyUnit);   // 코앞 +X
            skill.Execute(caster, default, P(PatternIdx, 6), ctx);
            Assert.AreEqual(1f, ctx.SimIntents[0].DirectionXZ.x, 1e-4f, "1차: +X");

            // 2차 — 그 적이 사라지고 먼 +Z 하나만 남았다.
            var ctx2 = Ctx(PatternAimNeed.NeedsAim, out var caster2);
            ctx2.Add(3, new float3(0f, 0f, 4f), Faction.EnemyUnit);
            skill.Execute(caster2, default, P(PatternIdx, 6), ctx2);

            Assert.AreEqual(1, ctx2.SimIntents.Count);
            Assert.AreEqual(1f, ctx2.SimIntents[0].DirectionXZ.y, 1e-4f,
                "2차가 +Z 를 겨눠야 한다 — +X 면 1차의 후보가 샌 것이다");
        }

        [Test]
        public void DegenerateFacing_FallsBackToNearest()
        {
            // 조준 컴포넌트는 있는데 값이 0 인 경우 — 최근접으로 흐른다.
            var ctx = Ctx(PatternAimNeed.NeedsAim, out var caster);
            ctx.Units[1].HasFacing = true;
            ctx.Units[1].Facing = float2.zero;
            ctx.Add(2, new float3(0f, 0f, 2f), Faction.EnemyUnit);

            new EmitPatternSkill().Execute(caster, default, P(PatternIdx, 3), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(1f, ctx.SimIntents[0].DirectionXZ.y, 1e-4f);
        }
    }
}
