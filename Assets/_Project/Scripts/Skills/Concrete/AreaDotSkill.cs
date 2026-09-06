namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 2e — 반경 안 상대 전원을 지속 시간 동안 지진다(버스터즈의 개점 조사).
    //
    // ⚠ **`scalar` 는 틱당 피해지 DPS 가 아니다**(`tickInterval > 0` 일 때). 이걸 DPS 로
    // 오해해 환산하면 피해가 배로 틀린다 — 실제로 spec 초안이 그렇게 적혀 있었다.
    // 총 피해 = 틱당 피해 × (지속 / 틱 간격).
    //
    // ⚠ **조사 중에는 기본 공격을 하지 않는다.** 이 스킬은 다른 배치 효과와 달리
    // **지속을 갖는 채널**이라 그동안 유닛이 여기 묶여 있는 것이 사양이다. 그건 연출이
    // 아니라 규칙이고, 그래서 `DelaySelfAttack` 어휘가 이 unit 에서 생겼다.
    public sealed class AreaDotSkill : ISkill
    {
        public const int Id = 15;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaDotParams(p);
            if (a.PerTickDamage <= 0f || a.Duration <= 0f || a.Radius <= 0) return;
            if (!ctx.Has(caster.Unit, UnitPredicate.HasPosition)) return;

            var center = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(
                caster, center, a.Radius,
                CandidateFilter.ExcludeDead
                | CandidateFilter.ExcludeInUltimateLeap
                | CandidateFilter.MatchTraversalLayers,
                RangeMetric.SelfArea, buf);

            // ⚠ **대상이 0이어도 묶인다.** 처음엔 「아무 일도 안 했는데 공격만 못 하는
            // 순수 손해」라고 보고 `n == 0` 에서 빠져나가게 했는데, 기존 그물이 그걸
            // 반박했다 — 레거시는 대상 수와 무관하게 쿨다운을 밀었다.
            //
            // 그쪽이 맞다: 조사는 **명중에 대한 보상이 아니라 동작에 대한 약속**이다.
            // 유닛은 2초간 눈에 보이게 훑고 있고, 그동안 평타를 쏘면 그림이 거짓말이 된다.
            // 빈 반경에 배치하는 건 플레이어의 선택이고, 적은 그 2초 안에 도착한다.
            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.DelaySelfAttack,
                Target = caster.Unit,
                Duration = a.Duration,
            });
            if (n == 0) return;

            for (int i = 0; i < n; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.ApplyDot,
                    Target = buf[i],
                    Source = caster.Unit,
                    Amount = a.PerTickDamage,
                    HitThreshold = a.TickInterval,
                    Duration = a.Duration,
                });

                // 대상별 빔 — 대상을 엔티티로 넘기므로 지속 동안 적이 걸어가도 따라간다.
                // `DataIndex < 0` 은 무연출 저작이다.
                if (p.HasData)
                {
                    ctx.Emit(new SimIntent
                    {
                        Kind = SimIntentKind.PlayVisual,
                        Selector = (int)SkillVisualKind.Beam,
                        Target = buf[i],
                        Source = caster.Unit,
                        DataIndex = p.DataIndex,
                        Duration = a.Duration,
                    });
                }
            }
        }
    }
}
