namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3b′ — 이번 공격의 대상에게 탄 하나를 더 보낸다(비수·부메랑).
    //
    // ⚠ **이 스킬은 탄의 궤적을 모른다.** 유도인지 왕복인지 낙하인지는 저작이 정하고
    // 여기는 불투명 토큰으로 지나보낸다 — 「어떤 탄인가」는 저작의 사실이고, 이 스킬의
    // 판단은 **「쏘나 · 누구에게 · 어디서」** 까지다. 그 경계를 지키는 것이 이 레이어가
    // 「탄 종류마다 concrete 가 하나씩」으로 번지지 않는 이유다.
    //
    // ⚠ **대상은 이 스킬이 고르지 않는다** — 공격이 이미 정한 결과가 실려 온다.
    //
    // ⚠ **피해는 flat 이다**(계약 7). 공격자의 피해 배율을 안 태운다 — 태우면 강공·시너지가
    // 이 탄에도 얹혀 「N타마다 한 발」의 세기가 저작값과 무관해진다.
    public sealed class TargetProjectileSkill : ISkill
    {
        public const int Id = 19;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            if (!target.Unit.IsValid) return;
            if (!ctx.Has(caster.Unit, UnitPredicate.HasPosition)) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnProjectile,
                Target = target.Unit,
                Source = caster.Unit,          // owner — 위협 귀속이 시전자에게 간다
                Position = ctx.Position(caster.Unit),
                // 방향 바인딩 궤적만 쓴다. 발사 시점의 값이라 재계산하지 않는다.
                DirectionXZ = target.DirectionXZ,
                Amount = p.Magnitude,
                Speed = p.Speed,
                HitThreshold = p.HitThreshold,
                VisualScale = p.VisualScale,
                DataIndex = p.DataIndex,
                TileRange = p.TileRange,
                TargetTraversalLayers = p.TargetTraversalLayers,
                ProjectileMovement = p.ProjectileMovement,
                ProjectilePayload = p.ProjectilePayload,
            });
        }
    }
}
