namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3d — **죽은 자리**에서 터진다(시체폭발).
    //
    // ⚠ **자기 자리 폭발과 자리가 다르다.** `SelfAreaBlastSkill` 은 시전자 발밑에서
    // 터지는데 이쪽은 **피해자가 쓰러진 곳**이다. 같은 「광역 폭발」이라도 그 한 축이
    // 게임에서 완전히 다른 그림이라(내가 맞은 자리 ↔ 내가 죽인 자리) 별도 concrete 다.
    //
    // ⚠ **그 자리는 지금 아니면 알 수 없다.** 드레인 시점엔 피해자가 파괴돼 있어
    // 재질의가 불가능하고, 그래서 감지자가 발화 시점 좌표를 실어 보낸다.
    //
    // ⚠ **owner 는 시전자다.** 폭발 킬이 죽인 쪽에 귀속돼야 연쇄(OnKill)가 이어진다 —
    // 여기서 owner 를 비우면 시체폭발 연쇄가 조용히 한 단계에서 멈춘다.
    public sealed class DeathSiteBlastSkill : ISkill
    {
        public const int Id = 20;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaDamageParams(p);
            if (a.Damage <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnProjectile,
                Source = caster.Unit,          // owner — 킬 귀속
                Target = SkillEntityId.None,   // 대상이 아니라 **자리**를 때린다
                Position = p.EventPosition,    // 죽은 자리(발화 시점 스냅샷)
                Amount = a.Damage,
                TileRange = a.Radius,
                DataIndex = a.VfxDataIndex,
                Duration = 0f,                 // flightTime 0 = 즉발
                VisualScale = a.VisualScale,
                TargetTraversalLayers = p.TargetTraversalLayers,
            });
        }
    }
}
