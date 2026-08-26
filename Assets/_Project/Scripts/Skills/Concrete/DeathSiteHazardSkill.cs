namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3d′ — **죽은 자리에 장판을 깐다**(잿불).
    //
    // 시체폭발(`DeathSiteBlastSkill`)과 **자리가 같고 하는 일이 다르다** — 한쪽은 즉발
    // 폭발이고 이쪽은 남는 장판이다. 둘은 배타가 아니라 한 처치가 폭발과 불씨를
    // 동시에 낼 수 있다(같은 host 가 둘 다 가질 수 있다).
    //
    // ⚠ **모양·반경·지속·효과·틱·뷰가 전부 해저드 저작 소유다.** 이 스킬이 정하는 것은
    // 「깔리나 · 어디에 · 누구를 대상으로」까지고, 그래서 `DataIndex` 하나만 지나간다.
    //
    // ⚠ **통행 층은 지금 읽은 값이다.** 드레인 시점에 시전자가 이미 파괴됐을 수 있고
    // (동귀어진), 그때 0 으로 새면 무제한 통과가 되어 지상 전용 유닛의 불씨가 비행
    // 적을 태운다. 감지자가 발화 시점 사양을 실어 보내는 이유가 이것이다.
    public sealed class DeathSiteHazardSkill : ISkill
    {
        public const int Id = 21;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 저작 없는 해저드는 「깔린 척」만 하는 조용한 no-op 이다 — 아예 안 깐다.
            if (!p.HasHazard) return;
            // 사양을 모르면 **안 깐다**(fail-closed). 0 은 폴백이 아니라 무제한 통과다.
            if (p.TargetTraversalLayers == 0) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnZoneCarrier,
                Source = caster.Unit,
                Cell = ctx.CellOfPosition(p.EventPosition),   // 죽은 자리
                DataIndex = p.HazardDataIndex,
                TargetTraversalLayers = p.TargetTraversalLayers,
            });
        }
    }
}
