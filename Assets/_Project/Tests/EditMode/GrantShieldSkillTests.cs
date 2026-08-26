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

        // 셔틀 경로 — 인원 상한이 **있는** 저작. `Count > 0` 이 이 경로의 스위치다.
        private static SkillParams Shuttle(float amount, int radius, int count,
                                           SkillShieldFilter filter, bool includesSelf)
            => new SkillParams(amount, 0, radius, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0,
                               count: count, includesSelf: includesSelf, selector2: (int)filter);

        // 꿈의 장막 — 경계마다 자기에게. host 제외 계약의 **예외**다.
        [Test]
        public void ZeroRadius_ShieldsSelfOnly()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(60f, 0), ctx);

            var grants = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.GrantShield);
            Assert.AreEqual(1, grants.Count);
            Assert.AreEqual(100, grants[0].Target.Value, "자기에게만");
            Assert.AreEqual(100, grants[0].Source.Value, "같은 출처 = max 갱신");
            Assert.AreEqual(60f, grants[0].Amount);

            // 리뷰 H1 — self 분기도 연출을 낸다. 반경 분기만 내고 여기가 비어 있어서
            // 「실드는 생기는데 반짝임이 사라지는」 라이브 회귀였다.
            var vfx = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.PlayVisual);
            Assert.AreEqual(1, vfx.Count, "자기 실드도 반짝여야 한다");
            Assert.AreEqual((int)SkillVisualKind.ShieldGranted, vfx[0].Selector);
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

        // 리뷰 L6 — 만충이면 병합이 max 로 no-op 이라 헛 VFX 만 남는다(가디언 선례).
        [Test]
        public void AlreadyFullFromSameSource_IsSkipped()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.EnemyUnit,
                u => u.ShieldFromSource[100] = 60f);   // 이미 이 출처로 만충
            ctx.Add(2, new float3(4.5f, 0, 5.5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None, P(60f, 3), ctx);

            var grants = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.GrantShield);
            Assert.AreEqual(1, grants.Count, "만충인 대상은 건너뛴다");
            Assert.AreEqual(2, grants[0].Target.Value);
            Assert.AreEqual(1, ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.PlayVisual).Count,
                "건너뛴 대상에는 헛 VFX 도 없다");
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
    
        // ─────────────────────────────────────────────────────────────────
        // 재리뷰 MEDIUM-B — 셔틀 경로 그물.
        //
        // ⚠ **이 자리가 위험한 이유**: 실드 필터가 `ccKind` 에서 `shieldFilter` 로 이사했는데
        // 두 열거형에서 **값이 우연히 같다**(둘 다 2). 그래서 배선을 틀려도 값이 맞아 보이고,
        // 카드 경로 테스트 5건은 전부 `Count = 0` 이라 이 경로를 한 번도 안 밟는다.
        // 여기가 비어 있으면 "실드는 나가는데 엉뚱한 놈이 받는" 회귀가 조용히 산다.

        // (a) 실효 HP 낮은 순 2명 — 저작한 인원 상한과 정렬 기준이 살아 있나.
        [Test]
        public void Shuttle_MostHurt_PicksTwoLowestEffectiveHp()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 1.0f);
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 0.9f);
            ctx.Add(2, new float3(4.5f, 0, 5.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 0.2f);  // 최하
            ctx.Add(3, new float3(5.5f, 0, 6.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 0.5f);  // 차하
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None,
                Shuttle(40f, 3, count: 2, SkillShieldFilter.MostHurt, includesSelf: false), ctx);

            var got = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.GrantShield)
                                    .ConvertAll(i => i.Target.Value);
            got.Sort();
            Assert.AreEqual(2, got.Count, "인원 상한 2 를 지켜야 한다 — 상한이 증발하면 4명 전원이 받는다");
            CollectionAssert.AreEqual(new[] { 2, 3 }, got, "실효 HP 낮은 순 2명");
        }

        // (b) 자기 포함 — `IncludesSelf` 가 host 제외 계약을 **열어주는** 축인가.
        // 카드 경로는 이 축을 안 채워서(=false) 늘 host 를 뺀다. 셔틀만 켠다.
        [Test]
        public void Shuttle_IncludesSelf_CanShieldTheCaster()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5.5f, 0, 5.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 0.1f);  // 자기가 최하
            ctx.Add(1, new float3(6.5f, 0, 5.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 0.3f);
            ctx.Add(2, new float3(4.5f, 0, 5.5f), Faction.DefenderUnit, u => u.EffectiveHpRatio = 0.8f);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new GrantShieldSkill().Execute(caster, SkillTarget.None,
                Shuttle(40f, 3, count: 2, SkillShieldFilter.MostHurt, includesSelf: true), ctx);

            var got = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.GrantShield)
                                    .ConvertAll(i => i.Target.Value);
            got.Sort();
            CollectionAssert.AreEqual(new[] { 1, 100 }, got, "자기 포함 — 가장 다친 자기와 그 다음");
        }
}
}
