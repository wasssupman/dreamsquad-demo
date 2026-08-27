namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 2c — **판 밖 런타임**을 바꾸는 스킬 둘.
    //
    // 이 둘이 다른 concrete 와 다른 점은 하나다: **시뮬레이션 상태를 안 바꾼다.**
    // 코스트 잔량과 액티브 쿨다운은 판이 아니라 플레이어 쪽 자원이고, 계약이
    // 「즉시 반영」이라 `SimIntent` 큐를 타지 않는다(타면 코스트 획득이 한 프레임 늦다).
    //
    // ⚠ 그래도 **스킬이다.** 조건(트리거)이 만족하면 실행되고, 값은 저작에서 오고,
    // 퇴화 저작은 조용히 소모된다 — 다른 concrete 와 같은 규율을 그대로 따른다.
    // 「ECS 를 안 만지니 레이어 밖에 두자」는 유혹이 경계를 무너뜨리는 자리다.

    // 배치 시 코스트를 얻는다(정찰병).
    public sealed class GainCostSkill : ISkill
    {
        public const int Id = 11;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 0 이하는 조용히 소모한다 — 발동은 일어났고 아무 일도 안 한 것이 저작의 결과다.
            // (음수를 「코스트를 뺏는다」로 열지 않는다. 그건 별개의 사양이고, 여기서
            //  통과시키면 오타 하나가 조용히 플레이어를 벌준다.)
            if (p.Magnitude <= 0f) return;

            ctx.Emit(new MetaIntent
            {
                Kind = MetaIntentKind.GainCost,
                Amount = p.Magnitude,
            });
        }
    }

    // 배치 시 보유한 액티브 스킬 쿨다운을 줄인다(레인저).
    public sealed class ReduceSkillCooldownSkill : ISkill
    {
        public const int Id = 12;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            if (p.Magnitude <= 0f) return;

            ctx.Emit(new MetaIntent
            {
                Kind = MetaIntentKind.ReduceSkillCooldown,
                Amount = p.Magnitude,
            });
        }
    }
}
