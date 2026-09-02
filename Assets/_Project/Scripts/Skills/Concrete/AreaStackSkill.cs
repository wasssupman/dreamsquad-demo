namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 2d — 반경 안 상대 전원에게 스택을 도포한다(난도질꾼).
    //
    // ⚠ **상한은 이 스킬의 것이 아니다.** 「출혈은 몇 겹까지 쌓이나」는 스택 종류의
    // 성질이지 시전자의 저작이 아니다 — 유닛마다 다른 상한을 적을 수 있게 두면
    // 같은 출혈이 누구에게 걸렸느냐로 다르게 쌓인다. 그래서 이 concrete 는 상한을
    // 아예 모르고 어댑터가 스택 종류에서 푼다.
    //
    // 저작 축은 셋뿐이다: 무슨 스택 · 몇 겹 · 겹당 지속.
    public sealed class AreaStackSkill : ISkill
    {
        public const int Id = 13;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaStackParams(p);
            // 0 겹은 발동을 조용히 소모한다 — 레거시 arm 과 같은 판정
            // (`magnitude <= 0` 이면 아무것도 안 하고 끝난다).
            if (a.Count <= 0 || a.Radius <= 0) return;
            if (!ctx.Has(caster.Unit, UnitPredicate.HasPosition)) return;

            var center = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(
                caster, center, a.Radius,
                CandidateFilter.ExcludeDead
                | CandidateFilter.ExcludeInUltimateLeap
                | CandidateFilter.MatchTraversalLayers,
                RangeMetric.AreaCircle, buf);

            for (int i = 0; i < n; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.ApplyStack,
                    Target = buf[i],
                    // 출처는 **시전자**다. 스택 파생 효과(출혈 피해 등)의 킬이 이 유닛에
                    // 귀속돼야 OnKill 사슬이 이어진다.
                    Source = caster.Unit,
                    Selector = (int)a.Stack,
                    Count = a.Count,
                    Duration = a.PerStackDuration,
                });
            }
        }
    }
}
