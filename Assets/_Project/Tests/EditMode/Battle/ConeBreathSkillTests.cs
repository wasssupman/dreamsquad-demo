using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.Skills.Concrete;

namespace Somnia.Battle.Tests.EditMode
{
    // skill-layer-migration unit 8 — 화염 브레스. arm 에서 concrete 로 온 규칙을 고정한다.
    //
    // 레거시 arm 의 필터는 넷이었다(진영 · 통행 층 · 자기 제외 · 부채꼴). 앞의 셋은
    // 후보 질의로 접혔다. unified-effect-layer unit 7 부터 **길이도 질의의 원**(사거리 + 시전자 몸 + 대상 몸)이고
    // 이 클래스는 방향 게이트(`SkillMath.SectorGate` · 대상 몸 걸침)만 곱한다 — 제약 13.
    public class ConeBreathSkillTests
    {
        // 반각 50° 의 (sin, cos) — bake 가 저작 각도에서 1회 만드는 값.
        private static readonly float Sin50 = math.sin(math.radians(50f));
        private static readonly float Cos50 = math.cos(math.radians(50f));

        private static SkillParams P(float damage, int tiles)
            => new SkillParams(damage, 0, tiles, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0,
                               coneSinHalf: Sin50, coneCosHalf: Cos50);

        private static int[] Hits(TestSkillContext ctx)
            => ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage)
                             .ConvertAll(i => i.Target.Value).ToArray();

        // unified-effect-layer unit 2 — 브레스는 **발사 자리**(①)에서 편다. 드레인이 채우는 원점을 페이크가 만든다.
        private static SkillTarget Aim(TestSkillContext ctx, CasterRef caster, float2 dir)
            => new SkillTarget(SkillEntityId.None, ctx.OriginOf(caster.Unit), int2.zero, false, dir);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)),
                P(30f, 5), ctx);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)),
                P(30f, 5), ctx);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, float2.zero),
                P(30f, 5), ctx);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)),
                P(30f, 5), ctx);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)),
                P(0f, 5), ctx);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)),
                P(30f, 5), ctx);

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

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)),
                P(30f, 5), ctx);

            Assert.AreEqual(0, ctx.SimIntents.FindAll(i => i.Kind == SimIntentKind.DealDamage).Count,
                "시전자도 같은 편도 자기 브레스에 타면 안 된다");
        }
        // ── unified-effect-layer unit 7 — 도달은 정본 자(제약 13) ──────────────────────────
        // 옛 판정(`SkillCone.IsInCone`)은 몸 없는 중심 거리로 길이를 다시 자르고 각도를 중심점으로만 봤다.
        // 아래 셋이 그 두 결함과, 넓어진 뒤에도 지켜야 하는 등 뒤 경계를 고정한다. 각 케이스는 몸 0 짝을 같이
        // 둬서 «몸이 판정을 바꿨다»를 보인다.

        // 몸 큰 적이 사거리 끝에 몸만 걸치면 맞는다 — 중심 3.4 > 사거리 3 이지만 3 + 몸 0.5 ≥ 3.4.
        [Test]
        public void BigBody_JustPastRange_ButBodyOverlapping_IsHit()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(8.4f, 0, 5f), Faction.EnemyUnit, u => u.BodyRadius = 0.5f);   // 몸이 걸친다
            ctx.Add(2, new float3(8.4f, 0, 5.01f), Faction.EnemyUnit);                           // 같은 자리 · 몸 0
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)), P(30f, 3), ctx);

            CollectionAssert.AreEqual(new[] { 1 }, Hits(ctx), "길이는 후보 원 하나 — 중심 거리로 다시 자르면 몸 큰 적이 빠진다");
        }

        // 부채꼴 가장자리 밖(60° · 반각 50°)에 중심이 있어도 몸이 걸치면 맞는다 — 가장자리 거리 2·sin10° ≈ 0.35 ≤ 몸 0.5.
        [Test]
        public void CenterOutsideConeEdge_ButBodyOverlapping_IsHit()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            float a = math.radians(60f);
            var at60 = new float3(5f + 2f * math.cos(a), 0, 5f + 2f * math.sin(a));
            ctx.Add(1, at60, Faction.EnemyUnit, u => u.BodyRadius = 0.5f);   // 몸이 가장자리에 걸친다
            ctx.Add(2, new float3(5f + 2f * math.cos(a), 0, 5f - 2f * math.sin(a)), Faction.EnemyUnit);   // 반대편 60° · 몸 0
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)), P(30f, 3), ctx);

            CollectionAssert.AreEqual(new[] { 1 }, Hits(ctx), "각도는 대상 몸 걸침 — 중심점만 보면 가장자리의 큰 적을 놓친다");
        }

        // 등 뒤는 몸 반경을 넘으면 안 맞는다 — 꼭짓점 거리로 잰다(가장자리 근사면 sinθ 배 관대해져 샌다).
        [Test]
        public void BehindApex_BeyondBodyRadius_IsMiss_WithinBodyRadius_IsHit()
        {
            var ctx = new TestSkillContext();
            ctx.Add(100, new float3(5f, 0, 5f), Faction.DefenderUnit);
            ctx.Add(1, new float3(4.4f, 0, 5f), Faction.EnemyUnit, u => u.BodyRadius = 0.5f);   // 0.6 뒤 > 몸 0.5
            ctx.Add(2, new float3(4.6f, 0, 5.001f), Faction.EnemyUnit, u => u.BodyRadius = 0.5f);   // 0.4 뒤 < 몸 0.5 — 꼭짓점에 걸친다
            var caster = CasterRef.OfUnit(new SkillEntityId(100), Faction.DefenderUnit);

            new ConeBreathSkill().Execute(caster, Aim(ctx, caster, new float2(1f, 0f)), P(30f, 3), ctx);

            CollectionAssert.AreEqual(new[] { 2 }, Hits(ctx), "등 뒤는 꼭짓점 거리 ≤ 몸 일 때만");
        }
}
}
