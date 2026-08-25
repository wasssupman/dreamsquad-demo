using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 0 — 실드 부여. **한 payload 가 두 능력을 맡는다.**
    // `tileRange` 가 그 둘을 가르고, bake 가 조합을 거절한다.
    public class GrantShieldSkillTests
    {
        private static SkillParams P(float amount, int radius)
            => new SkillParams(amount, 0, radius, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0);

        // 꿈의 장막 — 경계마다 자기에게. host 제외 계약의 **예외**다.
        [Test]
        public void ZeroRadius_ShieldsSelfOnly()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(60f, 0), ctx);

            Assert.AreEqual(1, ctx.SimIntents.Count);
            Assert.AreEqual(100, ctx.SimIntents[0].Target.Value, "자기에게만");
            Assert.AreEqual(100, ctx.SimIntents[0].Source.Value, "같은 출처 = max 갱신");
            Assert.AreEqual(60f, ctx.SimIntents[0].Amount);
        }

        // 악몽의 가호 — 반경 내 같은 편, host 제외.
        // ⚠ host 제외가 계약인 이유: 병합 키가 source 라 두 능력이 같은 host 에서 나와
        // 자기 자신에게 겹치면 한 슬롯을 공유하고, 「경계에 생기는 벽」이 「상시 실드」로 붕괴한다.
        [Test]
        public void PositiveRadius_ShieldsAlliesExceptSelf()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);      // 같은 편
            ctx.Add(2, new float3(6.5f, 0, 6.5f), Faction.DefenderUnit);   // 반대편
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(60f, 3), ctx);

            var grants = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.GrantShield);
            Assert.AreEqual(1, grants.Count);
            Assert.AreEqual(1, grants[0].Target.Value);
        }

        // **대상 위치에 대상 수만큼** 쏜다 — host 에서 한 번만 쏘면 "보스가 반짝하고
        // 호위 실드는 소리 없이 생긴다"가 된다.
        [Test]
        public void Visual_FiresPerTarget_AtTargetPosition()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(2, new float3(4.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(60f, 3), ctx);

            var vfx = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.PlayVisual);
            Assert.AreEqual(2, vfx.Count, "대상 수만큼");
            Assert.AreEqual((int)SkillVisualKind.ShieldGranted, vfx[0].Selector);
            foreach (var v in vfx)
                Assert.AreNotEqual(5.5f, v.Position.x, "host 위치가 아니라 대상 위치여야 한다");
        }

        [Test]
        public void DegenerateAmount_DoesNothing()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(0f, 0), ctx);
            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(-1f, 3), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count);
        }
    }
}
