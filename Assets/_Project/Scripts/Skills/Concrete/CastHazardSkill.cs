namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 5a — **적의 발밑에 깐다.** 얼음·불·독 장판과 길막 벽이
    // 이 하나를 공유한다. 넷이 다른 것은 **저작한 해저드 에셋 하나**뿐이다 —
    // 모양·반경·지속·효과·틱·뷰가 전부 그 SO 소유이기 때문이다.
    //
    // ⚠ **자리는 감지자가 준다.** 「사거리 안 가장 가까운 적」 선정은 캐스터의 공격 사양
    // (사거리·대상 마스크·통행 층)과 얽혀 있고 그 값들은 캐스트 상태가 갖고 있다.
    // 스킬이 다시 고르면 그 사양을 복제하게 된다 — 실려 온 칸에 깔 뿐이다.
    //
    // ⚠ 종류(장판/길막)는 실려 온다. 두 종류가 **다른 등록부**에서 에셋을 찾으므로
    // 여기서 접으면 얼음 장판 자리에 벽이 선다.
    public sealed class CastHazardSkill : ISkill
    {
        public const int Id = 28;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 저작 없는 해저드는 「깐 척」만 하는 조용한 no-op 이다 — 아예 안 깐다.
            if (!p.HasHazard) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnZoneCarrier,
                Source = caster.Unit,
                Target = target.Unit,        // 계측용 — 드레인은 자리로만 깐다
                // 감지자가 대상 칸의 **중심 좌표**를 실어 온다. 칸↔좌표 왕복은 정확하고
                // (`CellToWorldCenter` ↔ `WorldToCell`), 이벤트가 좌표 축 하나만 갖는다.
                Cell = ctx.CellOfPosition(p.EventPosition),
                DataIndex = p.HazardDataIndex,
                Selector = p.Selector,       // 장판 / 길막
                TargetTraversalLayers = p.TargetTraversalLayers,
            });
        }
    }
}
