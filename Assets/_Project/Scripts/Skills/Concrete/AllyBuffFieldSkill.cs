namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 7b — **아군 버프 장판**(파워 서지 · 래피드 파이어).
    //
    // 둘이 다른 것은 **스탯 하나**뿐이다 — 저작이 정하고 이 concrete 는 모른다.
    // 그래서 액티브 여섯 중 둘이 concrete 하나를 쓴다.
    //
    // ⚠ **장판이지 스냅샷이 아니다**(둔화 필드와 반대). 시전 후 걸어 들어온 아군도 걸리고,
    // 나간 아군은 풀린다 — 그 규칙은 `AllyBuffFieldSystem` 이 소유한다. 스킬이 하는 일은
    // 「저 칸에 이만한 장을 이 시간 동안 둔다」까지다(계약 5).
    //
    // ⚠ **빈 칸에도 놓인다.** 0기 거절은 폐기됐다(active-ally-zone unit 1) — 적 장판과
    // 규칙이 같아졌고, 「지금 아무도 없다」가 「놓을 수 없다」는 아니다.
    public sealed class AllyBuffFieldSkill : ISkill
    {
        public const int Id = 30;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 배율 1 이나 지속 0 은 「놓은 척」만 하는 장이다 — 아예 안 놓는다.
            if (p.Magnitude == 1f || p.Duration <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnFieldCarrier,
                Selector = (int)SkillFieldKind.AllyBuff,
                Selector2 = p.StatSelector,
                Cell = target.CellA,
                TileRange = p.TileRange,
                Amount = p.Magnitude,
                Duration = p.Duration,
            });
        }
    }
}
