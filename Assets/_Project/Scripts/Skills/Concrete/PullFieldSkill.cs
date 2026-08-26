namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 7c — **토네이도.** 지정한 칸으로 반경 안 적을 계속 당긴다.
    //
    // ⚠ **장이지 스냅샷이 아니다.** 지속 중에 걸어 들어온 적도 당겨진다 — 그 규칙은
    // `MovementSystem` 이 매 프레임 살아 있는 장을 조회해서 소유한다(계약 5).
    // 스킬이 하는 일은 「저 칸에 이 세기로 이 시간 동안」까지다.
    //
    // ⚠ **재시전은 겹친다.** 독립된 장이 하나 더 생기고 적은 자기를 포함한 첫 장에 끌린다 —
    // 그 병합 규칙도 이 스킬 밖이다.
    public sealed class PullFieldSkill : ISkill
    {
        public const int Id = 31;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 당김 속도나 지속이 비면 「도는 척」만 한다 — 아예 안 놓는다.
            if (p.Magnitude <= 0f || p.Duration <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnFieldCarrier,
                Selector = (int)SkillFieldKind.Pull,
                Cell = target.CellA,
                TileRange = p.TileRange,
                Amount = p.Magnitude,     // 당김 속도(월드 단위/초)
                Duration = p.Duration,
            });
        }
    }

    // skill-layer-migration unit 7c — **포탈.** 입구에 닿은 적을 출구로 보낸다.
    //
    // ⚠ **액티브 중 유일하게 칸을 둘 받는다.** 그 축(`CellA`/`CellB`)은 토대가 미리
    // 깔아 둔 것이고, 안 깔았으면 이 가족이 못 들어왔다.
    //
    // ⚠ **입구 == 출구 거절은 여기 없다.** 그건 두 번 탭하는 입력의 규칙이라 호출자
    // (손패 컨트롤러 → 브리지 preflight)가 소유한다 — 스킬은 성사된 시전만 받는다.
    public sealed class PortalSkill : ISkill
    {
        public const int Id = 32;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            if (p.Duration <= 0f || !target.HasCellB) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnFieldCarrier,
                Selector = (int)SkillFieldKind.Portal,
                Cell = target.CellA,      // 입구
                Cell2 = target.CellB,     // 출구
                Duration = p.Duration,
            });
        }
    }
}
