namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 7d — **메테오.** 지정한 칸에 예고 후 떨어진다.
    //
    // ⚠ **죽은 자리 폭발과 같은 형태인데 concrete 를 나눴다.** 그쪽은 「실려 온 자리」이고
    // 이쪽은 「찍은 칸」이라 조준의 출처가 다르고, **예고를 건다**는 판단이 여기만 있다.
    // 합치면 죽음 계열 폭발 넷이 전부 예고를 걸거나(사양 변경) 예고 축이 겸직이 된다.
    //
    // ⚠ **낙하 예고 시간이 곧 비행 시간이다.** 「언제 떨어지는지 보인다」와 「그때 떨어진다」가
    // 같은 값이어야 예고가 거짓말을 안 한다 — 그래서 한 필드에서 둘 다 나온다.
    public sealed class TileMeteorSkill : ISkill
    {
        public const int Id = 33;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 탄 저작이 없으면 떨어질 것이 없다 — 조용한 no-op 대신 아무것도 안 한다
            // (저작 검증은 호출자가 loud 하게 이미 했다).
            if (!p.HasData || p.Magnitude <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnProjectile,
                Source = caster.Unit,          // 무효 — 플레이어 시전이라 귀속할 유닛이 없다
                Target = SkillEntityId.None,   // 대상이 아니라 **칸**을 때린다
                Position = ctx.CellCenter(target.CellA),
                Amount = p.Magnitude,
                TileRange = p.TileRange,
                DataIndex = p.DataIndex,
                Duration = p.Duration,         // 낙하 예고 = 비행 시간
                VisualScale = p.VisualScale,
                Telegraph = true,              // ↑ 이 스킬의 판단
                TargetTraversalLayers = 0,     // 액티브는 층을 안 가린다(레거시 그대로)
            });
        }
    }
}
