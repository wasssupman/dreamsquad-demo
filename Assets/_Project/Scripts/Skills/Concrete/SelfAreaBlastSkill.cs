namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 0 — 자기 자리 즉발 폭발. 경계 자폭·진동갑주가 쓴다.
    //
    // 「자기 위치에 즉발 TileAoe」라 조준도 후보 선별도 없다. 이 스킬이 하는 판단은
    // 하나뿐이다 — **누구를 때리는가.** 그게 caster 의 상대 진영이고, 그래서 이 한 줄이
    // 예전에 「기본값이 Enemy 라 그냥 두면 보스의 폭발이 자기 진영을 때린다」였다.
    //
    // ⚠ **owner = 자기 자신이 계약이다.** 폭발 킬이 이 유닛에 귀속돼야 OnKill 카드와
    // 연쇄된다. 시전자를 지우면 그 사슬이 조용히 끊긴다.
    public sealed class SelfAreaBlastSkill : ISkill
    {
        public const int Id = 4;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaDamageParams(p);
            if (a.Damage <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnProjectile,
                Source = caster.Unit,          // owner — 킬 귀속(위 계약)
                Target = SkillEntityId.None,   // 대상이 아니라 **자리**를 때린다
                Position = ctx.Position(caster.Unit),
                Amount = a.Damage,
                TileRange = a.Radius,
                DataIndex = a.VfxDataIndex,
                Duration = 0f,                 // flightTime 0 = 즉발
                VisualScale = a.VisualScale,   // 0 = 저작 없음 → 어댑터가 1 로 읽는다
                // ⚠ **시전자의 공격 층을 실는다.** 안 실으면 0 = 무제한이라 근접 유닛의
                // 폭발이 하늘의 적을 때린다(unit 2a 의 그물이 잡은 축).
                TargetTraversalLayers = p.TargetTraversalLayers,
            });
        }
    }
}
