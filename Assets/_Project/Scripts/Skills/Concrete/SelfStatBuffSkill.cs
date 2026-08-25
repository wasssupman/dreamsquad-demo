namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3b — 발동할 때마다 **자기에게** 스탯 버프(광란).
    //
    // ⚠ **누적된다.** 같은 병합 키로 계속 재발행하면서 상한까지 쌓이는 것이 이 스킬의
    // 정체다 — 상한이 0이면 덮어쓰기(=중첩 없음)이고, 그 갈림이 `MagnitudeCap` 하나에 있다.
    //
    // ⚠ **출처는 드림캐쳐다.** 경계 arm 의 출처(`HealthThreshold`)를 복사하면 안 된다 —
    // 그 값은 「빈사에서 켜졌다」는 뜻이라 상태 연출이 다르게 읽는다. 공격으로 쌓이는
    // 이 버프는 그 사건이 아니다.
    //
    // ⚠ **대상이 자기뿐이라 진영 오사 경로가 없다.** 그래서 적 host 를 막지 않는다
    // (대상을 갖는 형제 스킬들과 다른 점).
    public sealed class SelfStatBuffSkill : ISkill
    {
        public const int Id = 18;
        public int SkillId => Id;

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
                Origin = SkillModifierOrigin.Dreamcatcher,
                Amount = a.Multiplier,
                // 「지속 <=0 = 영구」는 저작의 인코딩이다 — 어댑터가 무한으로 읽는다.
                Duration = a.Duration,
                StackId = a.StackId,
                HitThreshold = a.MagnitudeCap,
            });
        }
    }
}
