namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 2e — 반경 안 상대 전원을 잡아 세운다(말파이트의 착지 충격).
    //
    // ⚠ **이 스킬은 CC 가 있어야 성립한다.** 아래 피해는 그 위에 얹힌 부수 효과이므로,
    // 「지속 0 + 피해만」으로 저작하면 여기서 **조용히 끝난다** — 침묵이 의도다.
    // 피해만 주는 배치는 자기 자리 광역이 이미 하는 일이라, 가드를 쪼개 문을 열면
    // 같은 일을 하는 저작 경로가 둘이 된다(제약 8).
    //
    // ⚠ **띄움 길이는 잡는 길이와 다르다.** 심에서 넉업의 실체는 짧은 스턴이고
    // 「공중」은 뷰가 붙이는 해석이라, 잡는 시간을 3초로 늘리면서 체공까지 3초가 되면
    // 지진 충격이 아니라 무중력이 된다. `min` 인 이유가 그것이고, 더 중요하게는
    // **스턴보다 오래 떠 있으면 땅에 닿기 전에 적이 다시 움직인다.**
    public sealed class AreaCcSkill : ISkill
    {
        public const int Id = 14;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaCcParams(p);
            if (a.Duration <= 0f || a.Radius <= 0) return;
            if (!ctx.Has(caster.Unit, UnitPredicate.HasPosition)) return;

            var center = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(
                caster, center, a.Radius,
                CandidateFilter.ExcludeDead
                | CandidateFilter.ExcludeInUltimateLeap
                | CandidateFilter.MatchTraversalLayers,
                RangeMetric.Chebyshev, buf);
            if (n == 0) return;

            // 이 유닛이 «얼마나 높이 · 얼마나 오래» 띄우나는 스킬 저작이 아니라 유닛의
            // 성질이다(평타든 배치든 같다). 0 이면 안 띄운다 — 잡기만 한다.
            float hopHeight = ctx.Stat(caster.Unit, UnitStat.KnockupVisualHeight);
            float hopSec = ctx.Stat(caster.Unit, UnitStat.KnockupHopSeconds);
            hopSec = hopSec > 0f ? System.Math.Min(hopSec, a.Duration) : a.Duration;

            for (int i = 0; i < n; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.ApplyCc,
                    Target = buf[i],
                    Source = caster.Unit,
                    Selector = (int)a.Cc,
                    Duration = a.Duration,
                });

                // 「멈춘다」만이 아니라 「아프다」이기도 하다(사용자 결정 2026-08-19).
                // 0 이면 종전대로 CC 만 건다.
                // ⚠ **반경은 하나다** — 멈춘 적과 아픈 적의 집합이 갈리면 화면에서
                // 규칙을 읽을 수 없다. 그래서 같은 루프 안에 둔다.
                if (a.Damage > 0f)
                {
                    ctx.Emit(new SimIntent
                    {
                        Kind = SimIntentKind.DealDamage,
                        Target = buf[i],
                        Source = caster.Unit,
                        Amount = a.Damage,
                    });
                }

                if (hopHeight > 0f)
                {
                    ctx.Emit(new SimIntent
                    {
                        Kind = SimIntentKind.PlayVisual,
                        Selector = (int)SkillVisualKind.KnockupHop,
                        Target = buf[i],
                        Source = caster.Unit,
                        Duration = hopSec,
                        Amount = hopHeight,
                    });
                }
            }
        }
    }
}
