namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 0 — 도약. 상대 진영이 가장 몰린 곳으로 뛴다.
    //
    // 이 스킬이 하는 판단은 둘이다: **어디가 제일 몰렸나**, 그리고 **거기 근처에서
    // 갈 수 있는 칸이 어디인가**. 둘 다 순수 계산이라 포트가 질의로 답한다.
    //
    // ⚠ **sim 은 이번 프레임에 텔레포트한다.** 뷰가 아치로 날리는 것은 연출이고,
    // 착지 퍼프 타이밍도 뷰가 소유한다 — 그래서 슬램 피해가 뷰 도착보다 먼저 터지지
    // 않는다. 스킬은 「어디로」까지고 「어떻게 보이는지」는 안 갖는다.
    //
    // ⚠ 목적지 해석에 실패하면 **그냥 skip 한다.** 임계는 이미 소모됐고 재발동은 없다 —
    // 「상대가 전멸했는데 도약만 계속 시도」를 막는 것이 그 계약이다.
    public sealed class BlinkToClusterSkill : ISkill
    {
        public const int Id = 5;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new LeapParams(p);

            if (!ctx.TryDensestOpponentCluster(caster, a.DensitySearchRadius, out var desired, out _))
                return;   // 상대 진영 전멸 — 밀집 셀이 없다
            if (!ctx.TryLandingCellNear(desired, a.MaxLandingRing, out var landing))
                return;   // 링 상한 안에 갈 수 있는 칸이 없다

            var from = ctx.Position(caster.Unit);
            var dest = ctx.CellCenter(landing);

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.Blink,
                Target = caster.Unit,
                Position = dest,
            });

            // 비행 연출 — 출발/도착과 슬램 파라미터를 같이 싣는다. 브리지가 아치를
            // 날리고 착지 시점에 슬램을 터뜨린다(그 타이밍을 이 채널이 소유한다).
            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.PlayVisual,
                Selector = (int)SkillVisualKind.LeapArc,
                Source = caster.Unit,
                Position = from,
                Cell = landing,
                DataIndex = p.DataIndex,          // < 0 = 무연출
                Amount = a.SlamDamage,
                TileRange = a.SlamTileRange,
            });
        }
    }
}
