namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 0 — 궁극기 도약. 판을 떠났다가 강습한다.
    //
    // 일반 도약과 **다른 스킬**이다. 그건 「즉시 텔레포트 + 뷰 아치 + 피격 가능」이고
    // 이건 「판 밖 이탈 + 예고 + 무적」이다. 한 kind 에 플래그로 겸직시키면 성격이 다른
    // 둘이 한 슬롯을 공유하게 된다.
    //
    // ⚠ **착지 셀을 지금 고정하는 것이 계약이다.** 예고는 약속이라, 착지 직전에 재계산하면
    // 빨간 타일을 보고 유닛을 빼는 회피가 거짓이 된다.
    //
    // ⚠ 스킬은 **개시와 수치**까지다(계약 5). 카운트다운·착지·슬램은 `UltimateLeapSystem`
    // 이 굴린다. 다만 개시가 **두 컴포넌트의 원자 동시 부착**이라 — 잠금(비행)과
    // 무적(상태)은 레이어가 갈리지만 수명이 하나다 — 그것을 한 의도로 방출한다.
    public sealed class UltimateLeapSkill : ISkill
    {
        public const int Id = 6;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new LeapParams(p);

            if (!ctx.TryDensestOpponentCluster(caster, a.DensitySearchRadius, out var desired, out _)
                || !ctx.TryLandingCellNear(desired, a.MaxLandingRing, out var landing))
            {
                // ⚠ **조용히 넘기지 않는다.** 생존당 1회라 재시도가 없고, 1회성이라
                // 재현도 안 된다 — 조용하면 "궁극기가 왜 안 나왔는지"를 영영 알 수 없다.
                // 원인은 둘뿐이다: 상대 진영 0(밀집 셀 없음) 또는 링 안에 갈 수 있는 칸 없음.
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.Report,
                    Report = SkillReport.NoLandingSpot,
                    Source = caster.Unit,
                });
                return;
            }

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.BeginUltimateLeap,
                Target = caster.Unit,
                Cell = landing,
                Position = ctx.CellCenter(landing),
                Duration = a.TelegraphSeconds,
                Amount = a.SlamDamage,
                TileRange = a.SlamTileRange,
                DataIndex = p.DataIndex,
            });

            // 이탈 상승 신호. 채널이 없으면 연출만 없고 sim 은 그대로 돈다.
            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.PlayVisual,
                Selector = (int)SkillVisualKind.UltimateAscend,
                Source = caster.Unit,
                Position = ctx.Position(caster.Unit),
                DataIndex = SkillParams.NoDataIndex,
            });
        }
    }
}
