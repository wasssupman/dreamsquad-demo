namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 4a — **마지막 불꽃.** 짧게 강해지고 그 시간이 끝나면 죽는다.
    //
    // 부착되는 순간 발동한다(트리거가 없다). 그래서 이 스킬은 다섯 감지자 중 어디에도
    // 실리지 않고 **부착 seam**을 탄다 — 브리지가 자기 콜스택에서 돌린다.
    //
    // ⚠ **두 사건이지 하나가 아니다.** 버프의 만료는 모디파이어 시계가, 죽음은
    // `LethalTimer` 가 각각 소유한다. 저작이 둘을 같은 초로 적을 뿐이라 의도도 둘이다 —
    // 합치면 「버프를 반만 걸고 죽는 것」 같은 변형을 표현할 수 없게 된다.
    //
    // ⚠ **가부는 이 스킬이 정하지 않는다.** 부착이 트랜잭션이라 「붙일 수 있나」는
    // 부착 지점의 preflight(이미 순수한 `DcApplicability`)가 답하고, `Execute` 는 void 다.
    public sealed class SelfBuffLethalSkill : ISkill
    {
        public const int Id = 25;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 저작이 비면 「강해진 척」만 하고 죽는다 — 아예 안 건다.
            if (p.Magnitude <= 1f || p.Duration <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier,
                Target = caster.Unit,
                Source = caster.Unit,
                Selector = (int)SkillStatKind.AttackSpeedMul,
                // 배율→버킷 변환은 저작 계층 규칙이라 어댑터가 소유한다.
                Op = SkillCombineOp.FromAuthoredMultiplier,
                Origin = SkillModifierOrigin.Dreamcatcher,
                Amount = p.Magnitude,     // 감지자가 % → 배율로 이미 바꿔 실었다
                Duration = p.Duration,
                StackId = 0,              // 레거시와 같은 슬롯(카드 자기 버프 공용)
            });

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.StartLethalTimer,
                Target = caster.Unit,
                Source = caster.Unit,
                Duration = p.Duration,
            });
        }
    }
}
