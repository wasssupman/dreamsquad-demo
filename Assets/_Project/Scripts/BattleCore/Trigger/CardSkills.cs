using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7b — **코어 쪽 concrete 둘.** 옛 전투에서 이 둘은 스킬이 아니라 브리지가 직접 하던 일이었다
    // (`ApplyPlacementSleep` · 드림스톤의 `EnqueueStatModifier`). 그래서 `Wassup.Skills` 에 대응 concrete 가 없고, 그 어셈블리는
    // **한 줄도 안 고친다**(git diff 0 계약) — 그래서 여기 산다. 규율은 같다: 무상태 · 상태를 안 바꾼다 · `Emit` 만.
    //
    // 둘 다 **사건의 주인에게** 건다(`caster.Unit`). `Any` 바인딩에서 디스패처는 사건의 주인(새로 배치된 유닛)을 시전자로
    // 넘기므로, 「배치된 그 유닛에게」가 곧 「시전자에게」다.
    //
    // ⚠ id 는 레지스트리 밖 구간이다(라우팅 표가 고르지 않는다 — 카드 bake 가 직접 든다). 1~34 는 `Wassup.Skills` 의 것.

    /// <summary>배치 오라의 수면 — 배치된 유닛이 `Duration` 초 동안 잔다(피격하면 깬다). 옛 `ApplyPlacementSleep`.</summary>
    public sealed class PlacementSleepSkill : ISkill
    {
        public const int Id = 101;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            if (!caster.Unit.IsValid || p.Duration <= 0f) return;
            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ApplyCc,
                Target = caster.Unit,
                Source = caster.Unit,
                Selector = (int)SkillCcKind.Sleep,
                Duration = p.Duration,
            });
        }
    }

    /// <summary>
    /// 드림스톤 — 배치된 유닛에게 판 수명의 스탯 배율(옛 `ApplyPendingDreamstones` → `ApplyActiveDcEffectsTo`).
    /// **출처가 드림스톤**이다 — 드림캐쳐로 접으면 「강화 오라」 연출이 모든 유닛에 켜진다(옛 기본 덱 사고의 원인).
    /// `Wassup.Skills` 의 출처 어휘에 드림스톤이 없어 코어 값(5)을 그대로 싣는다 — 두 어휘는 번호가 정렬돼 있고
    /// 쓰기 표면(`IntentApplier`)이 번호로 옮긴다.
    /// </summary>
    public sealed class DreamstoneStatSkill : ISkill
    {
        public const int Id = 102;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            if (!caster.Unit.IsValid || p.Magnitude == 1f) return;
            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier,
                Target = caster.Unit,
                Source = caster.Unit,
                Selector = p.StatSelector,
                Op = SkillCombineOp.FromAuthoredMultiplier,
                Origin = (SkillModifierOrigin)(byte)Effects.ModifierOrigin.Dreamstone,
                Amount = p.Magnitude,
                Duration = 0f,   // 판 끝까지
            });
        }
    }
}
