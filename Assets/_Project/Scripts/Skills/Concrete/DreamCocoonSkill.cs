namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 4b — **호접몽.** 부착 즉시 잠들고, 깨지 않고 완주하면
    // 영구 버프를 얻는다. 맞으면 파탄이고 보상은 없다.
    //
    // ⚠ **리스크가 이 카드의 내용이다.** 잠은 기존 wake-on-hit 그대로라 새 잠 변종이 없고,
    // 그래서 이 스킬이 하는 일은 「잠들게 한다 + 완주를 감시하게 한다」 둘뿐이다.
    // 깨우는 규칙도 완주 판정도 시스템이 소유한다(계약 5).
    //
    // ⚠ **의도가 하나인 이유가 원자성이다.** 잠만 붙고 감시가 안 붙으면 그냥 손해 보는
    // 카드가 되고, 감시만 붙으면 공짜 버프가 된다. 잠을 `ApplyCc` 로 따로 보내면 그건
    // 큐 경유라 **한 프레임 늦게** 도착해서 정확히 후자가 된다 — 그 사이에 맞아도 깨울
    // 잠이 없다. 그래서 「개시」를 쪼개지 않는다(`BeginUltimateLeap` 과 같은 판단).
    public sealed class DreamCocoonSkill : ISkill
    {
        public const int Id = 26;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 저작이 비면 「잠든 척」만 하고 공짜 버프가 된다 — 아예 안 건다.
            if (p.Duration <= 0f || p.Magnitude == 1f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.BeginDreamCocoon,
                Target = caster.Unit,     // **자기가 잔다**
                Source = caster.Unit,
                Duration = p.Duration,
                // 감지자가 저작(kind + %)을 스탯·배율로 이미 풀어 실었다 — 그 변환은
                // 저작 인코딩이라 도메인이 알 이유가 없다(`% → 배율` 과 같은 자리).
                Selector = p.StatSelector,
                Amount = p.Magnitude,
                StackId = p.StackId,
            });
        }
    }
}
