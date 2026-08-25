namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3b — 이번 공격의 대상 하나에게 스택을 얹는다(잿불물기·서리물기).
    //
    // ⚠ **대상은 이 스킬이 고르지 않는다** — 공격이 이미 정한 결과가 발화 시점 값으로
    // 실려 온다(`TargetCcSkill` 과 같은 계약).
    //
    // ⚠ **상한은 저작이 아니라 스택 종류가 갖는다.** 레거시 카드 경로는 `tileRange` 를
    // 상한으로 겸직시켰는데, 그건 「반경」이라는 이름이 뜻을 잃는 자리였다. 라이브
    // 저작 실측에서 그 겸직값(0/5)과 스택 SO 의 상한(전부 5)이 **같은 값**이라 겸직을
    // 여기서 끊어도 동작이 안 변한다. 광역판(`AreaStackSkill`)과도 규칙이 하나가 된다.
    public sealed class TargetStackSkill : ISkill
    {
        public const int Id = 17;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaStackParams(p);   // 축이 같다 — 반경만 안 읽는다
            if (!target.Unit.IsValid) return;
            if (a.Count <= 0) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ApplyStack,
                Target = target.Unit,
                // 출처는 시전자다 — 스택 파생 피해의 킬이 이 유닛에 귀속돼야 OnKill 이 이어진다.
                Source = caster.Unit,
                Selector = (int)a.Stack,
                Count = a.Count,
                Duration = a.PerStackDuration,
            });
        }
    }
}
