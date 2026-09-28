namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3b — 발동할 때마다 **자기에게** 스탯 버프(광란).
    //
    // ⚠ **누적된다.** 같은 병합 키로 계속 재발행하면서 상한까지 쌓이는 것이 이 스킬의
    // 정체다 — 상한이 0이면 덮어쓰기(=중첩 없음)이고, 그 갈림이 `MagnitudeCap` 하나에 있다.
    //
    // ⚠ **연출 꼬리표는 강화 오라(`Dreamcatcher` — 옛 출처 이름)다.** 경계 arm 의 꼬리표(`HealthThreshold`)를 복사하면
    // 안 된다 — 그 값은 오라를 켜지 않는다. 꼬리표는 **이 효과가** 정한다(U15) — 누가 들었나에서 파생하지 않는다.
    //
    // ⚠ **대상이 자기뿐이라 진영 오사 경로가 없다.** 그래서 적 host 를 막을 이유가 없다.
    // (대상을 갖는 형제들도 «막지» 않는다 — 그쪽은 진영을 **caster 에서 도출**해서
    //  오사가 애초에 표현 불가능하다. 「막는다」와 「표현 불가」는 다르고, 이 레이어가
    //  택한 것은 후자다.)
    // ⚠ **꼬리표는 트리거와 무관하게 하나다(U15) — 칸 규칙만 트리거마다 다르다.** 그래서 공용
    // 구현 하나에 얇은 파생 둘이다(스탯 오라·seam 과 같은 형태). 꼬리표는 연출(강화 오라 켜짐)이,
    // 칸 규칙(`PerBindingSlot`)은 병합이 읽는다 — 둘은 한때 한 칸이었다(skill-data-table unit 2 에서
    // 갈랐다). 파생 둘이 «누가 들었나»에서 꼬리표를 갈랐던 적이 있었는데(빈사폭주가 `HealthThreshold`
    // 꼬리표를 썼다) 그건 U15 위반이었다 — 같은 효과는 트리거가 달라도 같은 연출이다.
    public abstract class SelfStatBuffSkillBase : ISkill
    {
        public abstract int SkillId { get; }
        protected abstract SkillModifierOrigin ModifierOrigin { get; }
        // 규칙 인스턴스 칸(붙일 때마다 새 칸)에 드나 — 병합 규칙이지 연출이 아니다.
        protected abstract bool PerBindingSlot { get; }

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new SelfBuffParams(p);
            if (a.Multiplier == 1f) return;   // 항등 배율은 발동을 조용히 소모한다

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier,
                Target = caster.Unit,
                Source = caster.Unit,
                Selector = (int)a.Stat,
                // 배율→버킷 변환은 저작 계층 규칙이라 어댑터가 소유한다.
                Op = SkillCombineOp.FromAuthoredMultiplier,
                Origin = ModifierOrigin,
                PerBindingSlot = PerBindingSlot,
                Amount = a.Multiplier,
                // 「지속 <=0 = 영구」는 저작의 인코딩이다 — 어댑터가 무한으로 읽는다.
                Duration = a.Duration,
                StackId = a.StackId,
                HitThreshold = a.MagnitudeCap,
            });
        }
    }

    // 공격·처치로 쌓이는 버프 — 강화 오라를 켠다(효과 기준 · 누가 들었든). 규칙 인스턴스 칸.
    public sealed class SelfStatBuffSkill : SelfStatBuffSkillBase
    {
        public const int Id = 18;
        public override int SkillId => Id;
        protected override SkillModifierOrigin ModifierOrigin => SkillModifierOrigin.Dreamcatcher;
        protected override bool PerBindingSlot => true;
    }

    // 체력 경계에서 켜지는 버프(빈사폭주) — **같은 효과라 꼬리표도 같다**(강화 오라, U15).
    // 칸만 다르다: 배치 칸(스택 id)이다 — 옛 동작 그대로(회수 방식 불변).
    public sealed class ThresholdSelfBuffSkill : SelfStatBuffSkillBase
    {
        public const int Id = 22;
        public override int SkillId => Id;
        protected override SkillModifierOrigin ModifierOrigin => SkillModifierOrigin.Dreamcatcher;
        protected override bool PerBindingSlot => false;
    }
}
