namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 7a — **지정한 칸에 즉발 스탯**(둔화 장판).
    //
    // ⚠ **시전 주체가 없다.** 플레이어가 손패에서 칸을 찍어 쓴다 — 그래서 중심이
    // 「내 발밑」이 아니라 **찍은 칸**이고, `caster.Unit` 은 무효다. 진영은 `CasterRef` 가
    // 들고 온다(플레이어 = 방어유닛 편) — 그래서 「누구를」이 여전히 표현 가능하다.
    //
    // ⚠ **장판이 아니라 스냅샷이다.** 이름이 「둔화 «장판»」이지만 실제로는 그 순간
    // 반경 안에 있던 적에게 TTL 모디파이어를 한 번 거는 것이고, 나중에 걸어 들어온
    // 적은 안 걸린다. 진짜 장판(`AllyBuffField`·`TornadoField`)과 다른 형태다.
    public sealed class TileStatBurstSkill : ISkill
    {
        public const int Id = 29;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            if (p.Magnitude == 1f || p.Duration <= 0f || p.TileRange < 0) return;

            var center = ctx.CellCenter(target.CellA);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Opponents(caster, center, p.TileRange,
                CandidateFilter.ExcludeDead, RangeMetric.AreaCircle, buf);

            for (int i = 0; i < n; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.ApplyStatModifier,
                    Target = buf[i],
                    // ⚠ **출처가 대상 자신이다.** 시전 주체가 없어서 병합 키의 source 축을
                    // 채울 엔티티가 없다 — 레거시가 그렇게 했고(`EnqueueStatModifier` 의
                    // `source = target`), 바꾸면 같은 스킬을 두 번 써도 슬롯이 갈린다.
                    Source = buf[i],
                    Selector = p.StatSelector,
                    Op = SkillCombineOp.FromAuthoredMultiplier,
                    Origin = SkillModifierOrigin.Skill,
                    Amount = p.Magnitude,
                    Duration = p.Duration,
                    StackId = 0,
                });
            }
        }
    }
}
