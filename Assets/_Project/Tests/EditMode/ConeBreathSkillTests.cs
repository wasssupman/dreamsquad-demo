using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 8 — 화염 브레스. arm 에서 concrete 로 온 규칙을 고정한다.
    //
    // 레거시 arm 의 필터는 넷이었다(진영 · 통행 층 · 자기 제외 · 부채꼴). 앞의 셋은
    // 후보 질의로 접혔고 **부채꼴만 이 클래스가 직접 본다** — 사거리가 «반경» 이 아니라
    // «콘» 이라 질의에 못 맡긴다. 그래서 그물도 그 넷째에 집중한다.
    public class ConeBreathSkillTests
    {
        private const float HalfAngle50CosSq = 0.413175f;   // cos²(50°)

        private static SkillParams P(float damage, int tiles, float coneCosSq)
            => new SkillParams(damage, 0, tiles, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0,
                               coneCosSq: coneCosSq);

        private static SkillTarget Aim(float2 dir)
            => new SkillTarget(SkillEntityId.None, int2.zero, int2.zero, false, dir);

        // 정면은 맞고 등 뒤는 안 맞는다. ⚠ 이 대칭이 깨지는 것이 콘 판정의 대표 사고다 —
        // 제곱 비교라 부호 가드가 없으면 **등 뒤에 대칭 콘**이 생긴다.
        [Test]
        public void HitsFront_NotBehind()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(7f, 0, 5f), Faction.EnemyUnit);   // 정면(+X)
            ctx.Add(2, new float3(3f, 0, 5f), Faction.EnemyUnit);   // 등 뒤(−X)
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(new float2(1f, 0f)),
                P(30f, 5, HalfAngle50CosSq), ctx);

            var hits = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage)
                                     .ConvertAll(i => i.Target.Value);
            CollectionAssert.AreEqual(new[] { 1 }, hits, "등 뒤가 맞으면 부호 가드가 죽은 것이다");
        }

        // 반각 밖은 안 맞는다 — 사거리 안이어도.
        [Test]
        public void ExcludesTargetsOutsideTheHalfAngle()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(5f, 0, 7f), Faction.EnemyUnit);   // 정확히 옆(90°)
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(new float2(1f, 0f)),
                P(30f, 5, HalfAngle50CosSq), ctx);

            Assert.AreEqual(0, ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage).Count);
        }

        // ⚠ **축이 없으면 안 쏜다.** 지어내면 저작·감지 실수가 「엉뚱한 방향으로 뿜는」
        // 형태로 조용히 살아남는다 — 화면에선 브레스가 정상으로 보인다.
        [Test]
        public void NoDirection_FiresNothing()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(7f, 0, 5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(float2.zero),
                P(30f, 5, HalfAngle50CosSq), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count, "축이 없으면 임의 방향을 지어내지 않는다");
        }

        // 호출자가 곧 소유자 — 적이 쓰면 방어유닛을 태운다(코드 0줄).
        [Test]
        public void EnemyCaster_BurnsDefenders()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.EnemyUnit);
            ctx.Add(1, new float3(7f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(2, new float3(7f, 0, 5.1f), Faction.EnemyUnit);   // 같은 편은 안 맞는다
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new ConeBreathSkill().Execute(caster, Aim(new float2(1f, 0f)),
                P(30f, 5, HalfAngle50CosSq), ctx);

            var hits = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage)
                                     .ConvertAll(i => i.Target.Value);
            CollectionAssert.AreEqual(new[] { 1 }, hits);
        }

        // 피해 0 은 no-op — 레거시 `if (damage <= 0f) return;` 과 같은 자리.
        [Test]
        public void ZeroDamage_IsNoOp()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(7f, 0, 5f), Faction.EnemyUnit);
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(new float2(1f, 0f)),
                P(0f, 5, HalfAngle50CosSq), ctx);

            Assert.AreEqual(0, ctx.SimIntents.Count);
        }
    
        // ⚠ **통행 층은 후보 질의가 본다**(`MatchTraversalLayers`). arm 시절엔 이 줄이
        // 콘 판정 바로 옆에 있었는데, 이제 두 곳으로 갈렸다 — 층이 빠지면 지상 전용
        // 브레스가 하늘의 적을 태우고, 그 오류는 화면에서 정상으로 보인다.
        [Test]
        public void RespectsTraversalLayers()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit,
                    u => u.AttackTraversalLayers = 0x01);          // 지상만 때린다
            ctx.Add(1, new float3(7f, 0, 5f), Faction.EnemyUnit,
                    u => u.TraversalLayers = 0x02);                // 하늘을 다닌다
            ctx.Add(2, new float3(7f, 0, 5.05f), Faction.EnemyUnit,
                    u => u.TraversalLayers = 0x01);                // 지상
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(new float2(1f, 0f)),
                P(30f, 5, HalfAngle50CosSq), ctx);

            var hits = ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage)
                                     .ConvertAll(i => i.Target.Value);
            CollectionAssert.AreEqual(new[] { 2 }, hits, "못 때리는 층을 태웠다");
        }

        // 자기 자신은 콘 안(같은 자리)이어도 안 맞는다 — `ExcludeSelf` 가 그 자리다.
        // ⚠ 「같은 자리 = 콘 포함」이 판정의 규칙이라, 이 그물이 없으면 자기 자신이
        // 자기 브레스에 탄다.
        [Test]
        public void NeverBurnsItself()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.EnemyUnit);   // 적 진영 시전자
            ctx.Add(1, new float3(5f, 0, 5f), Faction.EnemyUnit);     // 같은 자리 같은 편
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.EnemyUnit);

            new ConeBreathSkill().Execute(caster, Aim(new float2(1f, 0f)),
                P(30f, 5, HalfAngle50CosSq), ctx);

            Assert.AreEqual(0, ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage).Count,
                "시전자도 같은 편도 자기 브레스에 타면 안 된다");
        }
}
}
