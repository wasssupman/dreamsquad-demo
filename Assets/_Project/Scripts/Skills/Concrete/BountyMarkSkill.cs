namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 4c — **살찌운 제물.** 악몽 하나에 표식을 찍는다:
    // 잡으면 각성을 더 주고, 대신 그때까지 덜 아프다.
    //
    // ⚠ **이 레이어에서 유일하게 적을 «이롭게» 하는 스킬이다.** 피해 감소가 그 대가이고,
    // 그것이 이 카드의 도박이다 — 오래 살려두는 만큼 위험하다. 그래서 두 효과가 한 쌍이고
    // 어느 하나만 걸리면 카드가 아니라 버그다.
    //
    // ⚠ **표식을 두 번 찍을 수 없다**는 판정은 여기 없다. 그건 부착 트랜잭션의 문제라
    // (이중 배율 방지) 부착 지점의 preflight 가 답하고, `Execute` 는 void 다.
    public sealed class BountyMarkSkill : ISkill
    {
        public const int Id = 27;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 배율이 1 이하면 현상금이 없다 — 감소만 걸어 주는 순수 이득 카드가 되면 안 된다.
            if (p.Magnitude <= 1f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ScaleKillReward,
                Target = target.Unit,
                Source = target.Unit,
                Amount = p.Magnitude,
            });

            // 피해 감소는 저작이 0 일 수 있다(순수 현상금 표식). 그때는 안 건다.
            if (p.HitThreshold <= 0f) return;
            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier,
                Target = target.Unit,
                Source = target.Unit,
                Selector = (int)SkillStatKind.DmgTakenMul,
                Op = SkillCombineOp.FromAuthoredMultiplier,
                Origin = SkillModifierOrigin.Dreamcatcher,
                Amount = p.HitThreshold,   // 감지자가 % → 배율로 이미 바꿔 실었다
                Duration = p.Duration,
                StackId = p.StackId,
            });
        }
    }
}
