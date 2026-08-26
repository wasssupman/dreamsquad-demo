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
    // ⚠ **대상이 자기뿐이라 진영 오사 경로가 없다.** 그래서 적 host 를 막을 이유가 없다.
    // (대상을 갖는 형제들도 «막지» 않는다 — 그쪽은 진영을 **caster 에서 도출**해서
    //  오사가 애초에 표현 불가능하다. 「막는다」와 「표현 불가」는 다르고, 이 레이어가
    //  택한 것은 후자다.)
    // ⚠ **출처가 트리거마다 다르다.** 그래서 공용 구현 하나에 얇은 파생 둘이다
    // (스탯 오라·seam 과 같은 형태). 출처는 하류가 읽는 **의미**이지 장식이 아니다 —
    // 「빈사에서 켜졌다」와 「드림캐쳐가 켰다」는 상태 연출과 오라 집계가 다르게 읽는다.
    public abstract class SelfStatBuffSkillBase : ISkill
    {
        public abstract int SkillId { get; }
        protected abstract SkillModifierOrigin ModifierOrigin { get; }

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
                Amount = a.Multiplier,
                // 「지속 <=0 = 영구」는 저작의 인코딩이다 — 어댑터가 무한으로 읽는다.
                Duration = a.Duration,
                StackId = a.StackId,
                HitThreshold = a.MagnitudeCap,
            });
        }
    }

    // 공격·처치로 쌓이는 버프 — 드림캐쳐 출처. 상태 연출이 「카드가 켰다」로 읽는다.
    public sealed class SelfStatBuffSkill : SelfStatBuffSkillBase
    {
        public const int Id = 18;
        public override int SkillId => Id;
        protected override SkillModifierOrigin ModifierOrigin => SkillModifierOrigin.Dreamcatcher;
    }

    // 체력 경계에서 켜지는 버프(빈사폭주) — **출처가 다르다.** 그 값은 「빈사에서 켜졌다」는
    // 뜻이고, 드림캐쳐로 바꾸면 없던 오라가 켜진다(`ModifierAuraClassifier` 가 그 출처만 센다).
    public sealed class ThresholdSelfBuffSkill : SelfStatBuffSkillBase
    {
        public const int Id = 22;
        public override int SkillId => Id;
        protected override SkillModifierOrigin ModifierOrigin => SkillModifierOrigin.HealthThreshold;
    }
}
